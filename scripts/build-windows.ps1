[CmdletBinding()]
param([string]$IsccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Run this script with PowerShell 7 on Windows.' }
$RepoRoot = Split-Path $PSScriptRoot -Parent
$Payload = Join-Path $RepoRoot 'dist/windows'
$ReleaseDir = Join-Path $RepoRoot 'dist/release'
$Release = Get-Content (Join-Path $RepoRoot 'release.json') -Raw | ConvertFrom-Json
if (-not (Test-Path $IsccPath)) { throw 'Install Inno Setup 6.4.3 or supply -IsccPath.' }
$CompilerVersion = (Get-Item $IsccPath).VersionInfo.ProductVersion
if ($CompilerVersion -notmatch '^6\.4\.3(?:\.|$)') { throw "Expected Inno Setup 6.4.3; found $CompilerVersion" }

Push-Location $RepoRoot
try {
    if (Test-Path $Payload) { Remove-Item $Payload -Recurse -Force }
    dotnet publish App/MealPrepApp.Desktop/MealPrepApp.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false "-p:Version=$($Release.version)" -o $Payload -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    python scripts/package-release.py windows-payload
    if ($LASTEXITCODE -ne 0) { throw 'Windows payload validation failed.' }
    $NumericVersion = ([xml](Get-Content App/MealPrepApp.Desktop/Version.props -Raw)).Project.PropertyGroup.FileVersion
    & $IsccPath "/DAppVersion=$($Release.version)" "/DNumericVersion=$NumericVersion" "/DPublishDir=$Payload" "/DReleaseDir=$ReleaseDir" packaging/windows/MealPrepApp.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    python scripts/package-release.py windows-x64
    if ($LASTEXITCODE -ne 0) { throw 'Installer metadata validation failed.' }
}
finally { Pop-Location }
