---
tags: [session, todo, auth, ro]
---

# 2026-05-26 — Prioritate forgot password

## Context
Codrin a decis sa renunte la feature-ul de adaugare ingredient lipsa din editor si sa prioritizeze fluxul forgot-password/change-password din fereastra de login.

## TODO
- Feature-ul de ingredient lipsa a fost scos din active work.
- Resetarea parolei din login a fost mutata la Now.
- Conversia in `.exe` a ramas in Soon.

## Implementat
- `dbo.sp_ResetForgottenPassword` in `Database/procs/01_users.sql`.
- `UserRepository.ResetForgottenPasswordAsync`.
- Mapare eroare SQL 50005: `Nu am gasit un cont cu aceste date.`
- `ForgotPasswordViewModel` si `ForgotPasswordDialog`.
- Link `Ai uitat parola?` in login.
- Inregistrare VM/dialog in DI.

## Verificare
- `run_all.sql` a rulat in Docker cu exit 0.
- `sp_ResetForgottenPassword` exista in `sys.procedures`.
- Smoke test SQL cu user temporar, rollback: parola s-a schimbat, `FailedLoginCount` si `LockedUntil` s-au resetat.
- `.hermes/tests/test_drafts_static.py` a trecut.
- XAML parse OK pentru fisierele auth schimbate.
- `git diff --check` a trecut.
- Build local WPF ramane imposibil pe Fedora.

## Verificare Windows
Codrin a verificat flow-ul pe masina/VM-ul Ritei si a confirmat ca functioneaza corect.
