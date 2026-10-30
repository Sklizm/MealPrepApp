# macOS (Apple Silicon)

## Install and update

1. Download `MealPrepApp-macos-arm64.tar.gz` from [Releases](https://github.com/Sklizm/MealPrepApp/releases), or the macOS artifact from a successful main-branch Compatibility run under Actions.
2. If using release downloads, compare `shasum -a 256 MealPrepApp-macos-arm64.tar.gz` with its entry in the release's `SHA256SUMS`.
3. Extract the archive. Quit any running copy, then drag `MealPrepApp.app` into **Applications** in Finder. Choose **Replace** when updating.
4. Open **Applications → MealPrepApp**. Keep it in the Dock if desired.
5. On first launch, enter the connection supplied by the database administrator. See [connection settings](../../README.md#connection-settings).

The .NET runtime is included. Packaging does not sign or notarize the app; a downloaded
bundle may require macOS approval under **System Settings → Privacy & Security**.
Native launch and approval must be verified on the Mac.

## Build and install from source

Requires Git, .NET 10 SDK, and Python 3.11+:

```bash
git clone --branch main https://github.com/Sklizm/MealPrepApp.git
cd MealPrepApp
bash scripts/build-mac.sh -m:1 &&
bash scripts/install-mac.sh &&
open /Applications/MealPrepApp.app
```

For an existing checkout, save any local edits and switch to `main`, then pull before
building. The build produces `dist/mac/MealPrepApp.app`. To make a transfer archive:

```bash
python3 scripts/package-release.py macos-arm64
```

Copy `dist/release/MealPrepApp-macos-arm64.tar.gz` to the Mac and follow the Finder steps.
Cross-publishing from another .NET host is supported; the installation script runs on macOS.

The installer accepts an extracted bundle and an optional destination:

```bash
bash scripts/install-mac.sh "$HOME/Downloads/MealPrepApp.app"
bash scripts/install-mac.sh "$HOME/Downloads/MealPrepApp.app" "$HOME/Applications"
```

Use the second command for a per-user installation if `/Applications` is not writable.

## Uninstall and troubleshooting

Quit the app and move `MealPrepApp.app` from Applications to Trash. Settings and logs
remain under `~/Library/Application Support/MealPrepApp/`; remove that directory
separately only if you want to reset local settings. The database remains on the server.

If sign-in fails, use **Database connection** on the login screen and check
network or VPN connectivity, server address/port, certificate settings, and credentials.
The app uses native window decorations, menus, and file pickers.
