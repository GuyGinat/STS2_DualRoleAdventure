param(
    [string]$Version,
    [switch]$PublishGitHub,
    [switch]$PushGit,
    [string]$ReleaseNotes
)

$ErrorActionPreference = "Stop"

function Get-NextVersion {
    param(
        [string]$CurrentVersion
    )

    if ($CurrentVersion -notmatch "^v(\d+)\.(\d+)$") {
        throw "Invalid current version: $CurrentVersion. Expected format: v1.11"
    }

    $major = [int]$Matches[1]
    $minor = [int]$Matches[2] + 1
    return ("v{0}.{1}" -f $major, $minor.ToString("00"))
}

function Assert-NoNewLineInField {
    param(
        [string]$FieldName,
        [string]$Value
    )

    if ([string]::IsNullOrEmpty($Value)) {
        return
    }

    if ($Value.Contains("`r") -or $Value.Contains("`n")) {
        throw "DualRoleAdventure.json field '$FieldName' must be single-line text. Do not use \\n."
    }
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$dllPath = Join-Path $projectRoot "DualRoleAdventure.dll"
$jsonPath = Join-Path $projectRoot "DualRoleAdventure.json"
$releaseRoot = Join-Path $projectRoot "release"

if (!(Test-Path -LiteralPath $dllPath)) {
    throw "Missing DLL: $dllPath"
}

if (!(Test-Path -LiteralPath $jsonPath)) {
    throw "Missing JSON: $jsonPath"
}

$rawJson = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8
$modJson = $rawJson | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($Version)) {
    $targetVersion = Get-NextVersion $modJson.version
}
else {
    if ($Version -notmatch "^v\d+\.\d+$") {
        throw "Invalid target version: $Version. Expected format: v1.12"
    }

    $targetVersion = $Version
}

Assert-NoNewLineInField "description" ([string]$modJson.description)
Assert-NoNewLineInField "detail" ([string]$modJson.detail)

$modJson.version = $targetVersion
$updatedJson = $modJson | ConvertTo-Json -Depth 20
Set-Content -LiteralPath $jsonPath -Value $updatedJson -Encoding UTF8

if (!(Test-Path -LiteralPath $releaseRoot)) {
    New-Item -ItemType Directory -Path $releaseRoot | Out-Null
}

$releaseName = "DualRoleAdventure-$targetVersion"
$releaseDir = Join-Path $releaseRoot $releaseName
$zipPath = Join-Path $releaseRoot "$releaseName.zip"

if (Test-Path -LiteralPath $releaseDir) {
    Remove-Item -LiteralPath $releaseDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $releaseDir | Out-Null
Copy-Item -LiteralPath $dllPath -Destination (Join-Path $releaseDir "DualRoleAdventure.dll") -Force
Copy-Item -LiteralPath $jsonPath -Destination (Join-Path $releaseDir "DualRoleAdventure.json") -Force

Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $zipPath -Force

Write-Host "Release folder created: $releaseDir"
Write-Host "Release zip created: $zipPath"
Write-Host "Version updated to: $targetVersion"

if ($PublishGitHub) {
    $gitVersion = git --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "git is required for PublishGitHub mode."
    }

    $ghVersion = gh --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "gh CLI is required for PublishGitHub mode."
    }

    git add "DualRoleAdventure.json" | Out-Null
    git commit -m "发布 $targetVersion" 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Skip commit (maybe no changes to commit)."
    }

    git tag --list $targetVersion | Out-Null
    $existingTag = git tag --list $targetVersion
    if ([string]::IsNullOrWhiteSpace($existingTag)) {
        git tag -a $targetVersion -m "Release $targetVersion"
    }

    if ($PushGit) {
        git push origin master --follow-tags
    }

    $releaseBody = $ReleaseNotes
    if ([string]::IsNullOrWhiteSpace($releaseBody)) {
        $releaseBody = "Automated release $targetVersion"
    }

    cmd /c "gh release view $targetVersion >nul 2>nul"
    if ($LASTEXITCODE -eq 0) {
        gh release upload $targetVersion $zipPath --clobber
        Write-Host "GitHub release asset updated for $targetVersion"
    }
    else {
        gh release create $targetVersion $zipPath --title $targetVersion --notes $releaseBody
        Write-Host "GitHub release created for $targetVersion"
    }
}
