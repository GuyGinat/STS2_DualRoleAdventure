param(
    [Parameter(Mandatory = $true)]
    [string]$TaskDescription,

    [Parameter(Mandatory = $true)]
    [string]$LatestNote,

    [string]$Progress = "已完成",
    [string]$DevStatus = "已完成",
    [string]$Category = "优化",
    [string]$Source = "AI整理",
    [string]$Importance = "重要不紧急",
    [string]$DelayText = "✅ 正常",
    [string]$CommitRef = "",
    [string]$TableId = "",
    [datetime]$StartDate = (Get-Date),
    [datetime]$ExpectedDate = (Get-Date),
    [datetime]$ActualDate = (Get-Date),
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Get-EnvOrThrow {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = [Environment]::GetEnvironmentVariable($Name, "User")
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "缺少环境变量: $Name"
    }

    return $value
}

function To-UnixMs {
    param(
        [Parameter(Mandatory = $true)]
        [datetime]$Date
    )

    return [DateTimeOffset]::new($Date).ToUnixTimeMilliseconds()
}

function Get-AccessContext {
    $appId = Get-EnvOrThrow -Name "FEISHU_APP_ID"
    $appSecret = Get-EnvOrThrow -Name "FEISHU_APP_SECRET"
    $wikiNodeToken = Get-EnvOrThrow -Name "FEISHU_WIKI_NODE_TOKEN"

    $tokenResp = Invoke-RestMethod `
        -Method Post `
        -Uri "https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal" `
        -ContentType "application/json; charset=utf-8" `
        -Body (@{ app_id = $appId; app_secret = $appSecret } | ConvertTo-Json)

    if ($tokenResp.code -ne 0) {
        throw "获取 tenant_access_token 失败: code=$($tokenResp.code), msg=$($tokenResp.msg)"
    }

    $tenantToken = $tokenResp.tenant_access_token

    $nodeResp = Invoke-RestMethod `
        -Method Get `
        -Uri ("https://open.feishu.cn/open-apis/wiki/v2/spaces/get_node?token={0}" -f $wikiNodeToken) `
        -Headers @{ Authorization = "Bearer $tenantToken" }

    if ($nodeResp.code -ne 0) {
        throw "获取 wiki 节点失败: code=$($nodeResp.code), msg=$($nodeResp.msg)"
    }

    if ($nodeResp.data.node.obj_type -ne "bitable") {
        throw "wiki 节点对象类型不是 bitable: $($nodeResp.data.node.obj_type)"
    }

    return [PSCustomObject]@{
        TenantToken = $tenantToken
        AppToken = $nodeResp.data.node.obj_token
    }
}

if ([string]::IsNullOrWhiteSpace($TableId)) {
    $TableId = Get-EnvOrThrow -Name "FEISHU_TABLE_ID"
}

if ([string]::IsNullOrWhiteSpace($CommitRef)) {
    $latestCommit = (git log -1 --pretty=format:"%h %s" 2>$null)
    if (-not [string]::IsNullOrWhiteSpace($latestCommit)) {
        $CommitRef = $latestCommit.Trim()
    }
}

$ctx = Get-AccessContext
$headers = @{ Authorization = "Bearer $($ctx.TenantToken)" }

$startMs = To-UnixMs -Date $StartDate
$expectedMs = To-UnixMs -Date $ExpectedDate
$actualMs = To-UnixMs -Date $ActualDate

$fields = [ordered]@{
    "任务描述" = $TaskDescription
    "进展" = $Progress
    "开始日期" = $startMs
    "预计完成日期" = $expectedMs
    "是否延期" = @(@{ text = $DelayText; type = "text" })
    "实际完成日期" = $actualMs
    "最新进展记录" = $LatestNote
    "重要紧急程度" = $Importance
    "数据来源" = $Source
    "开发状态" = $DevStatus
    "任务分类" = $Category
    "提交参考" = $CommitRef
}

$listResp = Invoke-RestMethod `
    -Method Get `
    -Uri ("https://open.feishu.cn/open-apis/bitable/v1/apps/{0}/tables/{1}/records?page_size=500" -f $ctx.AppToken, $TableId) `
    -Headers $headers

if ($listResp.code -ne 0) {
    throw "读取多维表格记录失败: code=$($listResp.code), msg=$($listResp.msg)"
}

$target = $listResp.data.items | Where-Object { $_.fields."任务描述" -eq $TaskDescription } | Select-Object -First 1
$payload = @{ fields = $fields } | ConvertTo-Json -Depth 12

if ($DryRun) {
    if ($null -eq $target) {
        Write-Output "DRYRUN: 将创建记录 -> $TaskDescription"
    }
    else {
        Write-Output "DRYRUN: 将更新记录 -> $TaskDescription / $($target.record_id)"
    }

    Write-Output "DRYRUN: 提交参考 -> $CommitRef"
    exit 0
}

if ($null -eq $target) {
    $createResp = Invoke-RestMethod `
        -Method Post `
        -Uri ("https://open.feishu.cn/open-apis/bitable/v1/apps/{0}/tables/{1}/records" -f $ctx.AppToken, $TableId) `
        -Headers $headers `
        -ContentType "application/json; charset=utf-8" `
        -Body $payload

    if ($createResp.code -ne 0) {
        throw "创建记录失败: code=$($createResp.code), msg=$($createResp.msg)"
    }

    Write-Output ("CREATED|{0}|{1}" -f $createResp.data.record.record_id, $TaskDescription)
}
else {
    $updateResp = Invoke-RestMethod `
        -Method Put `
        -Uri ("https://open.feishu.cn/open-apis/bitable/v1/apps/{0}/tables/{1}/records/{2}" -f $ctx.AppToken, $TableId, $target.record_id) `
        -Headers $headers `
        -ContentType "application/json; charset=utf-8" `
        -Body $payload

    if ($updateResp.code -ne 0) {
        throw "更新记录失败: code=$($updateResp.code), msg=$($updateResp.msg)"
    }

    Write-Output ("UPDATED|{0}|{1}" -f $target.record_id, $TaskDescription)
}

