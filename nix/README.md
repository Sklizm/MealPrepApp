# NixOS development

MealPrepApp's Nix environment targets `x86_64-linux` and provides the .NET 10 SDK plus the native libraries required by Avalonia and Skia. The app uses X11 or XWayland; native Wayland is not enabled.

## Run the app

From the repository root:

```bash
nix develop
dotnet run --project App/MealPrepApp.Desktop
```

An X11 display or XWayland session must be available. The classic shell is also supported:

```bash
nix-shell --run "dotnet run --project App/MealPrepApp.Desktop"
```

Configure the database through the app's [connection screen](../README.md#connection-settings). Keep deployment credentials outside Nix source and build outputs.

## Build a standalone folder

Inside the development shell:

```bash
bash scripts/build-linux.sh -m:1
./dist/linux/MealPrepApp.Desktop
bash scripts/install-linux.sh
```

The installer creates a per-user launcher pointing to the built folder. A generic self-contained .NET build still needs NixOS native-library integration; use the development shell or a validated Nix package.

## Bootstrap the Nix package

The checked-in dependency metadata is an empty bootstrap and no `flake.lock` is supplied. A reproducible Nix package is not yet validated. On a networked Nix host:

```bash
bash scripts/update-nix-deps.sh
git diff -- nix/deps.json
git status --short flake.lock nix/deps.json
nix build
./result/bin/MealPrepApp.Desktop
```

Review and commit the generated lock file and dependency metadata after a successful build. Regenerate them when package references change. The generator uses `buildDotnetModule.fetch-deps` and requires a working network restore.

## Checks and distribution

Run the [contributor checks](../CONTRIBUTING.md#checks), then test focus, keyboard navigation, file pickers, menus, drag-to-move, themes, resizing, and actual display scaling on NixOS. Headless captures establish rendering evidence rather than native acceptance.

[Release packages](../docs/releases/README.md) include a generic Linux x64 archive. They do not replace the Nix dependency bootstrap or native-library environment. The flake excludes local settings, `.env`, and build outputs; configure each client separately.
