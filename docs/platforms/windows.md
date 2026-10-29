# Windows

The standard Windows download runs the same Avalonia application as macOS and Linux.
The original WPF client remains available from source for university grading.

## Install and update

1. Open the repository's [Releases](https://github.com/Sklizm/MealPrepApp/releases) and download `MealPrepApp-<version>-windows-x64-setup.exe` plus `SHA256SUMS`. If no published release is available, use the Windows artifact from a successful main-branch Compatibility run under Actions.
2. Compare the installer hash with its entry in `SHA256SUMS`:

   ```powershell
   Get-FileHash .\MealPrepApp-<version>-windows-x64-setup.exe -Algorithm SHA256
   ```

3. Quit MealPrepApp and run the installer. It installs for the current user without administrator rights, adds a Start Menu entry, and offers an optional desktop shortcut.
4. Launch **MealPrepApp** from Start. On a fresh installation, enter the database connection supplied by the administrator. See [first launch](../../README.md#connection-settings).

The .NET runtime is included. Installing the app does not install or migrate SQL Server.
The default app directory is `%LOCALAPPDATA%\Programs\MealPrepApp`.
Run the newer installer to update; user settings are kept outside the installation folder.
The initial installer is unsigned; Windows publisher/reputation prompts may appear.

## Uninstall and settings

Uninstall **MealPrepApp** through Windows **Settings → Apps → Installed apps**.
The app, shortcuts, and uninstall registration are removed. Connection settings,
preferences, and diagnostic logs remain in `%LOCALAPPDATA%\MealPrepApp`.
Delete that directory separately only if you want to remove local settings as well.
Your shared server database is unaffected.

## Build the installer

Requires Git, .NET 10 SDK, Python 3.11+, PowerShell 7, and
[Inno Setup 6.4.3](https://jrsoftware.org/files/is6-whatsnew.htm).
The build script checks the compiler version. In a Windows terminal:

```powershell
git clone --branch main https://github.com/Sklizm/MealPrepApp.git
cd MealPrepApp
pwsh -File scripts/build-windows.ps1
```

Output is `dist/release/MealPrepApp-<version>-windows-x64-setup.exe` with a matching
metadata sidecar. A custom compiler location can be supplied with `-IsccPath`.
The script publishes a self-contained Windows x64 payload, validates it for private
files, and compiles the installer. GitHub Actions runs the same script.

For development without installation:

```powershell
dotnet run --project App/MealPrepApp.Desktop
```

## WPF grading client

The WPF interface is retained with its original project path:

```powershell
dotnet build App/MealPrepApp/MealPrepApp.csproj -c Release
dotnet run --project App/MealPrepApp
```

It uses its own `App/MealPrepApp/appsettings.Local.template.json` and local configuration;
the Avalonia first-launch screen and installer do not configure WPF. WPF is compiled
in CI but does not have a separate installer. See the [academic report](../../Raport/README.md).

## Troubleshooting

- If the installer asks you to close the app, close every running instance and retry.
- If sign-in cannot reach SQL Server, select **Conexiune la baza de date** on the login screen. Check Tailscale, the server address and port, certificate settings, and database credentials.
- Diagnostics are stored under `%LOCALAPPDATA%\MealPrepApp\logs`; credentials are excluded from diagnostic messages.
- Installer testing belongs on a disposable Windows worker: `pwsh -File scripts/test-windows-install.ps1`. It creates and removes test user settings and refuses an existing installation/profile.
