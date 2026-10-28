# NixOS development and packaging

The flake targets `x86_64-linux` with the .NET 10 SDK and the native libraries used by Avalonia/Skia. The Linux client uses X11, or XWayland in a Hyprland session. Native Wayland is not enabled.

## Run from source

From the repository root:

```bash
nix develop
dotnet run --project App/MealPrepApp.Desktop
```

The classic shell is also available:

```bash
nix-shell --run "dotnet run --project App/MealPrepApp.Desktop"
```

An X11 display or XWayland session must be available. Connection settings and presentation preferences follow the [main README](../README.md#connection-settings); never add deployment credentials to the Nix source.

## Bootstrap the package

`nix/deps.json` is an empty bootstrap, and `flake.lock` has not been generated. Packaging is not yet validated or reproducible. On a networked Nix host:

```bash
bash scripts/update-nix-deps.sh
git diff -- nix/deps.json
git status --short flake.lock nix/deps.json
nix build
./result/bin/MealPrepApp.Desktop
```

Review and commit the generated `flake.lock` and populated `nix/deps.json` after a successful build. Regenerate dependency metadata when package references change. The generator uses `buildDotnetModule.fetch-deps`; it requires Nix, network access and a working .NET dependency restore.

The flake excludes `appsettings.Local.json`, `.env` and generated build folders from its source. Set connection credentials on each client separately.

## Linux folder build

Inside the development shell, a self-contained folder can also be generated:

```bash
bash scripts/build-linux.sh -m:1
./dist/linux/MealPrepApp.Desktop
bash scripts/install-linux.sh
```

The installer creates a per-user launcher pointing to that folder. NixOS still needs the development shell's native-library environment or the completed Nix package; self-contained .NET output alone does not provide NixOS library integration.

## Verification

Run the [shared regression commands](../README.md#verification). Native NixOS/Hyprland checks remain required for focus, keyboard shortcuts, file pickers, menus, drag-to-move, theme changes, resizing and actual 100/150/200% display scaling. The [headless screenshot matrix](../docs/redesign/README.md#screenshots-and-validation) is rendering evidence, not a native smoke test.

## Release downloads

The [release candidate workflow](../docs/releases/README.md) creates a generic Linux x64
archive. It does not complete the Nix dependency bootstrap or replace the required native
library environment described above.
