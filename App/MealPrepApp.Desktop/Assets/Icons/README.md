# Application icon

The original plate-and-leaf mark uses the app's cream, sage, and terracotta palette.
The transparent margin and rounded tile keep the silhouette readable in the Dock,
taskbar, launchers, and small window decorations. It contains no text or external images.

![Application icon](app-icon-512.png)

`app-icon.svg` is the editable source. Generated assets are checked in so normal app
builds need no image tooling:

| Asset | Use |
|---|---|
| `app-icon.png` | 1024px embedded Avalonia window/dialog icon |
| `app-icon-512.png` | Linux launcher and Nix package |
| `app-icon.ico` | Windows executable and setup; 16, 24, 32, 48, 64, 128, 256px |
| `app-icon.icns` | macOS bundle; standard and Retina representations through 1024px |

Regenerate from the repository root with the .NET 10 SDK:

```bash
dotnet run --project tools/MealPrepApp.IconGenerator -c Release
python3 Tests/Scripts/test_app_icons.py
```

The build-only generator uses the same SkiaSharp version as the app and reads the SVG's
paths and basic shapes. It rejects unsupported shapes rather than silently dropping them.
Keep the SVG's 1024×1024 viewBox and review the output at both Dock and taskbar sizes.

The Mac build copies the ICNS into `Contents/Resources/MealPrepApp.icns` and declares
`CFBundleIconFile` in the bundle's Info.plist. Quit the old app before installing the new
bundle. The installer updates the bundle timestamp so Finder can notice icon changes.
Linux installation adds the PNG to the user's hicolor theme and sets `Icon=mealprep`.

Format, bundle-copy, launcher-install, embedded-image, and window-style regressions run
without a native desktop. Actual Dock/Finder, Windows Explorer/taskbar, and Linux launcher
appearance still require native acceptance.
