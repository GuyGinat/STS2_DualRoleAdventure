param(
    [Parameter(Mandatory = $true)]
    [string]$TaskDescription,

    [Parameter(Mandatory = $true)]
    [string]$LatestNote,

    [string]$Progress = "done",
    [string]$DevStatus = "done",
    [string]$Category = "enhancement",
    [string]$Source = "ai",
    [string]$Importance = "normal",
    [string]$DelayText = "on-time",
    [string]$CommitRef = "",
    [string]$TableId = "",
    [datetime]$StartDate = (Get-Date),
    [datetime]$ExpectedDate = (Get-Date),
    [datetime]$ActualDate = (Get-Date),
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$projectScript = Join-Path $PSScriptRoot "..\..\..\Scripts\Tools\SyncFeishuTask.ps1"
$projectScript = [System.IO.Path]::GetFullPath($projectScript)

if (-not (Test-Path $projectScript)) {
    throw "Project sync script not found: $projectScript"
}

$args = @(
    "-ExecutionPolicy", "Bypass",
    "-File", $projectScript,
    "-TaskDescription", $TaskDescription,
    "-LatestNote", $LatestNote,
    "-Progress", $Progress,
    "-DevStatus", $DevStatus,
    "-Category", $Category,
    "-Source", $Source,
    "-Importance", $Importance,
    "-DelayText", $DelayText,
    "-StartDate", $StartDate.ToString("o"),
    "-ExpectedDate", $ExpectedDate.ToString("o"),
    "-ActualDate", $ActualDate.ToString("o")
)

if (-not [string]::IsNullOrWhiteSpace($CommitRef)) {
    $args += @("-CommitRef", $CommitRef)
}

if (-not [string]::IsNullOrWhiteSpace($TableId)) {
    $args += @("-TableId", $TableId)
}

if ($DryRun) {
    $args += "-DryRun"
}

& powershell @args

exit $LASTEXITCODE
