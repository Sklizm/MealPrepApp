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
