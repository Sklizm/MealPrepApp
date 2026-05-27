---
tags: [session, wpf, loading, shell]
---

# 2026-05-25 — Loading screen after login

## Context
Codrin asked for a simple, intuitive, smooth loading screen after logging in, with no visible lag or stop.

## Changes made
- Added a startup loading state to `ShellViewModel`:
  - `IsInitializing`
  - `LoadingMessage`
- Reworked `ShellViewModel.InitializeAsync()` so the shell shows a loading overlay before the Acasa dashboard starts loading.
- Added `await Task.Yield()` before dashboard navigation so WPF can paint the shell and overlay first instead of appearing frozen.
- Added a 650ms minimum display time so the loading screen does not flash too quickly on fast loads.
- Added a full-window modal loading overlay in `ShellWindow.xaml`:
  - dark translucent backdrop
  - centered cream card matching the app palette
  - animated olive spinner
  - title: `Se incarca aplicatia`
  - live loading message binding
- Added code-behind fade animation in `ShellWindow.xaml.cs`:
  - quick fade-in when initialization starts
  - smooth fade-out before collapsing the overlay
  - overlay captures input while visible, preventing the user from clicking half-loaded shell controls
- Extended `.hermes/tests/test_drafts_static.py` with static coverage for the loading overlay wiring.
- Updated `Vault/TODO.md`:
  - moved loading screen implementation to Done
  - added Windows/.NET 10 loading-screen verification to Now

## Verification
- Static checks pass:
  - `python .hermes/tests/test_drafts_static.py`
- XAML parses as XML for:
  - `App.xaml`
  - `ShellWindow.xaml`
  - `LoginWindow.xaml`
- `git diff --check` passed.

## Build note
Local Fedora still cannot build this WPF project because the installed Linux SDK does not include `Microsoft.NET.Sdk.WindowsDesktop` targets. Windows/.NET 10 VM verification is required.

## Windows smoke test
1. Launch the app.
2. Log in with a valid account.
3. Confirm the shell appears with the loading overlay instead of a blank/frozen window.
4. Confirm the spinner animates smoothly.
5. Confirm the overlay disappears smoothly after Acasa finishes loading.
6. Confirm no clicks are accepted behind the overlay while it is visible.
7. Log out and log back in once more to confirm the overlay works repeatedly.
