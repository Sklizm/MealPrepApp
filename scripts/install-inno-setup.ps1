# Install the exact compiler on a disposable Windows CI worker, independently of Chocolatey.
[CmdletBinding()]
param([string]$Destination = (Join-Path ([IO.Path]::GetTempPath()) 'mealprep-inno-6.4.3'))

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Run this script with PowerShell 7 on Windows.' }
$RepoRoot = Split-Path $PSScriptRoot -Parent
$Tool = Get-Content (Join-Path $RepoRoot 'packaging/windows/inno-setup.json') -Raw | ConvertFrom-Json
$Download = Join-Path ([IO.Path]::GetTempPath()) ("mealprep-inno-" + [Guid]::NewGuid().ToString('N') + '.exe')
$Destination = [IO.Path]::GetFullPath($Destination)
New-Item $Destination -ItemType Directory -Force | Out-Null
try {
    Invoke-WebRequest -Uri $Tool.downloadUrl -OutFile $Download -MaximumRetryCount 3 -RetryIntervalSec 2
    $ActualHash = (Get-FileHash $Download -Algorithm SHA256).Hash
    if ($ActualHash -ne $Tool.sha256) { throw 'Inno Setup download checksum does not match the pinned official asset.' }
    $LogFile = Join-Path $Destination 'compiler-install.log'
    $Arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/NOICONS',
        ('/DIR="' + $Destination + '"'), ('/LOG="' + $LogFile + '"'))
    $Process = Start-Process -FilePath $Download -ArgumentList $Arguments -Wait -PassThru
    if ($Process.ExitCode -notin @(0, 3010)) { throw "Inno Setup installation failed ($($Process.ExitCode)); see $LogFile" }
    $Compiler = Join-Path $Destination 'ISCC.exe'
    if (-not (Test-Path $Compiler)) { throw "Compiler missing after installation: $Compiler" }
    $Version = [regex]::Match((Get-Item $Compiler).VersionInfo.ProductVersion, '^\d+\.\d+\.\d+').Value
    if ($Version -ne $Tool.version) { throw "Expected Inno Setup $($Tool.version); found $Version" }
    if ($env:GITHUB_ENV) {
        "MEALPREP_ISCC=$Compiler" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
    }
    Write-Host "Verified Inno Setup $Version at $Compiler"
}
finally {
    if (Test-Path $Download) { Remove-Item $Download -Force }
}
