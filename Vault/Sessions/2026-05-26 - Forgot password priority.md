---
tags: [session, todo, auth]
---

# 2026-05-26 — Forgot password priority

## Context
Codrin decided to dismiss the planned "add missing ingredient while making a recipe" feature and prioritize the forgot-password/change-password flow from the login window instead.

## TODO update
- Removed the missing-ingredient recipe-editor feature from active work.
- Moved "Add a function to change password in login window in case forgot" to `Now`.
- Left `.exe` conversion in `Soon`.

## Next implementation target
Build a login-window password reset/change flow.

Recommended design questions before implementation:
1. Decide whether this is a true forgot-password reset or a simpler change-password-from-login flow.
2. Since there is no email/SMS recovery infrastructure in the current practica app, prefer a local/demo-safe reset flow rather than pretending to send email.
3. Reuse existing password hashing and `sp_ChangePassword` rules where possible.
4. Check the current DB procs before adding new ones; `sp_ChangePassword` likely requires the old password, so a forgot-password flow may need a new controlled reset proc.
5. Keep the app's stored-proc-only security model intact.

## Implemented
- Added `dbo.sp_ResetForgottenPassword` to `Database/procs/01_users.sql`.
- Added repository support via `UserRepository.ResetForgottenPasswordAsync`.
- Added friendly SQL error mapping for code `50005`: `Nu am gasit un cont cu aceste date.`
- Added `ForgotPasswordViewModel` plus `ForgotPasswordDialog`.
- Wired `Ai uitat parola?` in the login view to open the reset dialog.
- Registered the forgot-password VM/dialog in DI.

## Verification
- Re-ran `Database/run_all.sql` against the local Docker `MealPrepDB` container; exit code 0.
- Verified `sys.procedures` contains `sp_ResetForgottenPassword`.
- Ran a rolled-back SQL smoke test with a temporary user; the proc updated the password hash, cleared `FailedLoginCount`, and cleared `LockedUntil`.
- Ran `.hermes/tests/test_drafts_static.py`; all static checks passed, including the forgot-password wiring check.
- XML-parsed changed auth XAML files:
  - `LoginView.xaml`
  - `LoginWindow.xaml`
  - `ForgotPasswordDialog.xaml`
  - `ChangePasswordDialog.xaml`
- Ran `git diff --check`; exit code 0.
- Attempted `dotnet build App/MealPrepApp/MealPrepApp.csproj --no-restore` on Fedora; it cannot run here because the installed Linux SDK is missing `Microsoft.NET.Sdk.WindowsDesktop` targets, so Windows runtime/build verification remains a Rita machine/VM step.

## Next
- Continue with `.exe` conversion from the TODO list.

## Windows verification
- Codrin verified the forgot-password reset flow on Rita's Windows machine/VM and confirmed everything works properly.
