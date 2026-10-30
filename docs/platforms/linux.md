# Linux x64 and NixOS

## Install and update

Download `MealPrepApp-linux-x64.tar.gz` from [Releases](https://github.com/Sklizm/MealPrepApp/releases),
or the Linux artifact from a successful main-branch Compatibility run under Actions.
Compare `sha256sum MealPrepApp-linux-x64.tar.gz` with the release's `SHA256SUMS`, if available.

Extract the archive to a stable directory, then launch:

```bash
tar -xzf MealPrepApp-linux-x64.tar.gz
./MealPrepApp-linux-x64/MealPrepApp.Desktop
```

The .NET runtime is included; native graphics libraries are still required.
On first launch, supply the database connection from the administrator. See
[connection settings](../../README.md#connection-settings).

Quit the app before updating. Extract the new package to a new directory and switch
the launcher to it after verifying launch. User settings are independent of the app directory.

## Build and add a launcher

Requires Git, .NET 10 SDK, Python 3.11+, and Avalonia's native Linux dependencies:

```bash
git clone --branch main https://github.com/Sklizm/MealPrepApp.git
cd MealPrepApp
bash scripts/build-linux.sh -m:1
./dist/linux/MealPrepApp.Desktop
bash scripts/install-linux.sh
```

For an extracted download, point the repository's launcher installer at that directory:

```bash
bash scripts/install-linux.sh /absolute/path/to/MealPrepApp-linux-x64
```

The installer writes `${XDG_DATA_HOME:-$HOME/.local/share}/applications/mealprep.desktop`.
Keep the installed folder at that location or rerun the installer after moving it.
To create a transfer archive, run `python3 scripts/package-release.py linux-x64`.

## NixOS and Hyprland

Use the [Nix guide](../../nix/README.md) for the dev shell, native dependencies, and
package bootstrap. The generic archive does not supply NixOS library integration.
The app uses X11/XWayland, including under Hyprland; this version has no native Wayland backend.
Nix dependency metadata still needs native validation as documented in that guide.

## Uninstall and troubleshooting

Quit the app, remove its extracted folder and the generated `mealprep.desktop` launcher.
Settings and logs remain in `${XDG_DATA_HOME:-$HOME/.local/share}/MealPrepApp` under
the standard .NET `LocalApplicationData` mapping. Remove them separately to reset the app.
The shared database is unaffected.

If the app fails before showing a window, verify native libraries and the X11/XWayland
environment. If it opens but sign-in fails, use **Conexiune la baza de date** at login
and check network or VPN connectivity, server address/port, certificate settings, and credentials.
