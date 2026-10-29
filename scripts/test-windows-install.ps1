# Run only on a disposable Windows CI worker. Does not connect to the live database.
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path $PSScriptRoot -Parent
$Release = Get-Content (Join-Path $RepoRoot 'release.json') -Raw | ConvertFrom-Json
$Installer = Join-Path $RepoRoot "dist/release/MealPrepApp-$($Release.version)-windows-x64-setup.exe"
$InstallDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/MealPrepApp'))
$UserDir = Join-Path $env:LOCALAPPDATA 'MealPrepApp'
$Shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'MealPrepApp/MealPrepApp.lnk'
$DesktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'MealPrepApp.lnk'
$UninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{7B61A522-55E3-4C45-A224-AB93EA71B41C}_is1'
$LogDir = Join-Path $RepoRoot 'dist/windows-install-logs'

function Invoke-Installer([string]$File, [string[]]$Arguments, [string]$LogName) {
    $LogFile = Join-Path $LogDir "$LogName.log"
    $Process = Start-Process -FilePath $File -ArgumentList ($Arguments + ('/LOG="' + $LogFile + '"')) -Wait -PassThru
    if ($Process.ExitCode -ne 0) { throw "Installer exited with $($Process.ExitCode); see $LogFile" }
}
function Assert-Exists([string]$Path) {
    if (-not (Test-Path $Path)) { throw "Missing expected installation item: $Path" }
}

if ((Test-Path $InstallDir) -or (Test-Path $UserDir)) {
    throw 'This smoke test requires a clean disposable user profile.'
}
New-Item $LogDir -ItemType Directory -Force | Out-Null
try {
    Invoke-Installer $Installer @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=desktopicon') 'install'
    Assert-Exists "$InstallDir/MealPrepApp.Desktop.exe"
    Assert-Exists $Shortcut
    Assert-Exists $DesktopShortcut
    Assert-Exists $UninstallKey
    $PayloadMetadata = Get-Content "$InstallDir/release-metadata.json" -Raw | ConvertFrom-Json
    if ($PayloadMetadata.version -ne $Release.version) { throw 'Installed version mismatch.' }
    $Shell = New-Object -ComObject WScript.Shell
    $ExpectedTarget = [IO.Path]::GetFullPath((Join-Path $InstallDir 'MealPrepApp.Desktop.exe'))
    if ($Shell.CreateShortcut($Shortcut).TargetPath -ne $ExpectedTarget) { throw 'Incorrect Start Menu shortcut target.' }

    # No credentials configured: the first-launch setup should stay open without crashing.
    $App = Start-Process "$InstallDir/MealPrepApp.Desktop.exe" -PassThru `
        -RedirectStandardOutput (Join-Path $LogDir 'application.stdout.log') `
        -RedirectStandardError (Join-Path $LogDir 'application.stderr.log')
    try {
        $Ready = $false
        for ($Attempt = 0; $Attempt -lt 60; $Attempt++) {
            Start-Sleep -Milliseconds 500
            $App.Refresh()
            if ($App.HasExited) { throw 'First launch exited unexpectedly.' }
            if ($App.MainWindowHandle -ne 0) { $Ready = $true; break }
        }
        if (-not $Ready) { throw 'First-launch window did not appear.' }
        if ($App.MainWindowTitle -notlike '*Conexiune*') { throw 'Fresh installation did not open connection setup.' }
    }
    finally {
        if (-not $App.HasExited) { Stop-Process -Id $App.Id -Force }
        $App.WaitForExit()
    }

    New-Item "$UserDir/logs" -ItemType Directory -Force | Out-Null
    $Fixtures = @{
        'preferences.json' = '{"Theme":1}'
        'appsettings.Local.json' = '{"ConnectionStrings":{"MealPrepDB":"Server=example.invalid;Database=MealPrepDB;User Id=fixture;Password=fixture"}}'
        'logs/smoke.log' = 'fixture'
    }
    foreach ($Entry in $Fixtures.GetEnumerator()) { [IO.File]::WriteAllText((Join-Path $UserDir $Entry.Key), $Entry.Value) }
    # A reinstall uses the stable AppId and exercises the same upgrade path.
    Invoke-Installer $Installer @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-') 'reinstall'
    Assert-Exists $UninstallKey
    Invoke-Installer "$InstallDir/unins000.exe" @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 'uninstall'
    if ((Test-Path "$InstallDir/MealPrepApp.Desktop.exe") -or (Test-Path $Shortcut) -or (Test-Path $DesktopShortcut) -or (Test-Path $UninstallKey)) {
        throw 'Uninstall left registered application files or shortcuts.'
    }
    foreach ($Entry in $Fixtures.GetEnumerator()) {
        if ([IO.File]::ReadAllText((Join-Path $UserDir $Entry.Key)) -ne $Entry.Value) { throw "User data changed: $($Entry.Key)" }
    }
}
finally {
    if (Test-Path "$UserDir/logs") {
        Copy-Item "$UserDir/logs" (Join-Path $LogDir 'application-diagnostics') -Recurse -Force -ErrorAction Continue
    }
    if (Test-Path "$InstallDir/unins000.exe") {
        Invoke-Installer "$InstallDir/unins000.exe" @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 'cleanup'
    }
    if (Test-Path $UserDir) { Remove-Item $UserDir -Recurse -Force }
}
Write-Host 'Windows install, startup, reinstall, uninstall and user-data checks passed.'
