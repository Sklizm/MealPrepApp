---
tags: [session, wpf, exe, publish, verification]
---

# 2026-05-26 — Windows exe publishing

## Context
Codrin prioritized converting MealPrepApp into a Windows `.exe` deliverable.

## Implemented
- Added a Windows x64 publish profile:
  - `App/MealPrepApp/Properties/PublishProfiles/Windows-x64-Folder.pubxml`
  - `Release`
  - `net10.0-windows`
  - `win-x64`
  - self-contained
  - single-file executable
  - trimming disabled for WPF safety
- Added `App/publish-windows-exe.cmd` so Rita's Windows machine/VM can publish the app with one command.
- Added `App/MealPrepApp/appsettings.Local.template.json` as a safe non-secret template for the runtime connection string.
- Updated `MealPrepApp.csproj` so:
  - `appsettings.json` is still copied to output/publish;
  - real `appsettings.Local.json` still works for local development;
  - real `appsettings.Local.json` is never copied into publish output;
  - the safe template is included in publish output.
- Added static regression coverage for the publish profile, script, template, docs, and gitignore.
- Updated `README.md` with Windows publish and configuration instructions.

## Verification performed locally
- Ran `.hermes/tests/test_drafts_static.py`; all static checks passed, including the new exe-publishing check.
- Ran `git diff --check`; no whitespace errors.
- Attempted local `dotnet publish App/MealPrepApp/MealPrepApp.csproj /p:PublishProfile=Windows-x64-Folder --no-restore`; it failed as expected on Fedora because the local SDK is .NET 9 and Linux does not include WPF `Microsoft.NET.Sdk.WindowsDesktop` targets.

## Verification gap
- Actual `.exe` publish must be verified on a Windows machine/VM, because WPF WindowsDesktop publishing cannot be completed on this Fedora environment.

## How to publish on Windows
Run from the repo root:

```cmd
App\publish-windows-exe.cmd
```

Expected output:

```text
App\publish\MealPrepApp-win-x64\MealPrepApp.exe
```

Before running the exe, copy `appsettings.Local.template.json` to `appsettings.Local.json` in the publish folder and replace `__SET_APP_PASSWORD__` with the real `mealprep_app` password.

## Next
- Run the publish script on Rita's Windows machine/VM.
- Confirm `MealPrepApp.exe` launches and reaches the login screen with SQL Server reachable.
