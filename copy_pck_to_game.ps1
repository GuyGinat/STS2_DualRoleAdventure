$ErrorActionPreference = "Stop"

$modName = "DualRoleAdventure"
$targetDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\$modName"
$sourceDllPath = Join-Path $PSScriptRoot "$modName.dll"
$sourceJsonPath = Join-Path $PSScriptRoot "$modName.json"

if (-not (Test-Path -LiteralPath $sourceDllPath)) {
    throw "Source dll not found: $sourceDllPath. Build project first to generate dll in project root."
}

if (-not (Test-Path -LiteralPath $sourceJsonPath)) {
    throw "Source json not found: $sourceJsonPath. Ensure release config json exists in project root."
}

if (-not (Test-Path -LiteralPath $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

Copy-Item -LiteralPath $sourceDllPath -Destination (Join-Path $targetDir "$modName.dll") -Force
Copy-Item -LiteralPath $sourceJsonPath -Destination (Join-Path $targetDir "$modName.json") -Force

Write-Host "Copied dll: $sourceDllPath -> $(Join-Path $targetDir "$modName.dll")"
Write-Host "Copied json: $sourceJsonPath -> $(Join-Path $targetDir "$modName.json")"
