; Build through scripts/build-windows.ps1; these values come from release.json.
#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef NumericVersion
  #error NumericVersion is required
#endif
#ifndef PublishDir
  #error PublishDir is required
#endif
#ifndef ReleaseDir
  #error ReleaseDir is required
#endif

[Setup]
AppId={{7B61A522-55E3-4C45-A224-AB93EA71B41C}
AppName=MealPrepApp
AppVersion={#AppVersion}
VersionInfoVersion={#NumericVersion}
AppPublisher=MealPrepApp
DefaultDirName={localappdata}\Programs\MealPrepApp
DefaultGroupName=MealPrepApp
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#ReleaseDir}
OutputBaseFilename=MealPrepApp-{#AppVersion}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\MealPrepApp.Desktop.exe
AppMutex=MealPrepApp.Desktop.Running
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MealPrepApp"; Filename: "{app}\MealPrepApp.Desktop.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\MealPrepApp"; Filename: "{app}\MealPrepApp.Desktop.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\MealPrepApp.Desktop.exe"; Description: "Launch MealPrepApp"; Flags: nowait postinstall skipifsilent

; User configuration lives outside {app}; never remove it during uninstall.
