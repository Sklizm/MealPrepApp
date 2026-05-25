---
tags: [session, wpf, loading, shell]
---

# 2026-05-25 — Standalone loading window before shell

## Context
Codrin asked to change the loading behavior from an overlay inside the app shell to a separate loading window that appears before the main app itself appears. The shell must stay hidden until the loading step has lasted a fixed 3–5 second window.

## Changes made
- Added `Views/StartupLoadingWindow.xaml` and `.xaml.cs`:
  - standalone chrome-less loading window
  - cream/olive styling consistent with the app
  - animated spinner
  - Romanian loading text
- Changed the login success flow in `Views/Auth/LoginWindow.xaml.cs`:
  - after login, show `StartupLoadingWindow`
  - hide the login window
  - create and initialize the shell while the loading window is visible
  - enforce a 3.5 second minimum display time
  - only call `shell.Show()` after both shell initialization and the minimum delay complete
- Changed `ShellWindow.xaml.cs`:
  - removed the old `Loaded` initialization path
  - added `InitializeBeforeShowAsync()` so the shell can load before it is displayed
- Removed the previous in-shell overlay from `ShellWindow.xaml` and removed the related loading state from `ShellViewModel`.
- Registered `StartupLoadingWindow` in DI.
- Updated `.hermes/tests/test_drafts_static.py` so static coverage now checks the standalone pre-shell loading window behavior instead of the old overlay behavior.

## Verification
- Static regression checks pass:
  - `python .hermes/tests/test_drafts_static.py`
- XAML parses as XML for:
  - `App.xaml`
  - `ShellWindow.xaml`
  - `LoginWindow.xaml`
  - `StartupLoadingWindow.xaml`
- `git diff --check` passed.

## Build note
Attempted local build:
- `dotnet build App/MealPrepApp/MealPrepApp.csproj --no-restore`
- result: failed with MSB4019 because `/usr/lib64/dotnet/sdk/9.0.117/Sdks/Microsoft.NET.Sdk.WindowsDesktop/targets/Microsoft.NET.Sdk.WindowsDesktop.targets` is missing.

This is the expected Fedora limitation: the local Linux SDK does not include the WindowsDesktop WPF targets. Windows/.NET 10 VM verification is still required.

## Windows smoke test
1. Launch the app.
2. Log in with a valid account.
3. Confirm the login window disappears and the standalone loading window appears.
4. Confirm the main app shell is not visible behind the loading window.
5. Confirm the loading window stays visible for about 3.5 seconds.
6. Confirm the shell appears only after the loading window closes.
7. Confirm Acasa is already loaded when the shell appears.
8. Log out and log back in once more to confirm the flow repeats correctly.
