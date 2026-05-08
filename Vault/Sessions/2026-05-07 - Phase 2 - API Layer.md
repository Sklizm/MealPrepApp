---
tags: [session, phase2]
date: 2026-05-07
---

# 2026-05-07 — Phase 2: App-facing API + Security Layer

Same calendar day as the kickoff, but a clearly separate scope of work — promoted to its own session note.

## Goals
- Give the .NET app a fixed contract to call into (stored procs, not raw SQL)
- Add user-security state the app's auth flow can rely on (lockout, last-login, password history)
- Add an audit trail for "who did what, when?"
- Bind it all behind a low-privilege SQL login so the app physically cannot run direct DML

## What got built
**New tables** (`Database/07_users_security.sql`, `08_audit_log.sql`):
- `dbo.PasswordHistory` (cascade from Users)
- `dbo.AuditLog`
- New columns on `dbo.Users`: `LastLoginAt`, `FailedLoginCount`, `LockedUntil`

**New TVP**: `dbo.IntList` for passing ID lists from the .NET app.

**18 stored procedures** under `Database/procs/`:
- Users: `sp_RegisterUser`, `sp_GetUserForLogin`, `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword`
- Recipes (write): `sp_CreateRecipe`, `sp_UpdateRecipe`, `sp_DeleteRecipe`
- Recipes (read): `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients`
- Ingredients: `sp_AddIngredient`, `sp_GetIngredients`, `sp_SearchIngredients`
- Lookups: `sp_GetUnits`, `sp_GetCategories`
- Internal helper: `sp_WriteAudit` (called from every mutating proc)

**Security layer** (`Database/09_app_role.sql`):
- SQL login `mealprep_app` (password set at run time via `sqlcmd -v AppPassword=…`, not in the file)
- Database user mapped to that login
- Role `mealprep_app_role` with `GRANT EXECUTE ON SCHEMA::dbo` and `DENY SELECT/INSERT/UPDATE/DELETE ON SCHEMA::dbo`
- Role can also `EXECUTE` the `IntList` TVP type
- Mutations succeed only via stored procs through SQL Server **ownership chaining** (proc and tables both owned by `dbo`).

## Verification (same session)
Each chunk built + tested in isolation before moving on. Final end-to-end re-run of `run_all.sql` was clean and idempotent.

Tests that passed:
- All 18 procs visible in `sys.procedures`
- `sp_RegisterUser` → row + audit
- `sp_RecordLoginFailure` × 5 → `LockedUntil` set + `ACCOUNT_LOCKED` audit
- `sp_RecordLoginSuccess` → reset + `LastLoginAt` set
- `sp_ChangePassword` rejects current and last-5 hashes; pruning verified across 6 changes
- `sp_CreateRecipe` round-trips JSON ingredient list correctly
- `sp_UpdateRecipe` / `sp_DeleteRecipe` reject wrong owner with `THROW 50002`
- `sp_GetRecipes` paging returns correct `TotalCount` from window function
- `sp_FindRecipesByIngredients` returns matched/total counts and sorts best-match first
- App login: can `EXEC dbo.sp_GetCategories` ✓, denied `SELECT * FROM dbo.Users` ✓, can mutate via `sp_RegisterUser` ✓ (ownership chaining), denied direct `SELECT FROM dbo.AuditLog` ✓

## Bugs caught and fixed
- **Pwd-history pruning was non-deterministic** when multiple changes happened in the same second (`ChangedAt` is `DATETIME2(0)` — 1-second resolution). Fixed by adding `PasswordHistoryID DESC` as a tiebreaker in both the recency check and the pruning ROW_NUMBER(). The IDENTITY column always grows monotonically.
- **Test-script bug** (not in code): tried to pass `CONCAT(...)` directly as a stored proc parameter; T-SQL only allows variables/constants there. Fixed by binding to `@var` first.

## Decisions made → see [[Decisions Log]]
- Stored-proc-only API
- Hard delete kept (no soft-delete)
- Lockout: 5 failures → 15-minute lock
- Password history depth: 5
- JSON over TVP for ingredient lists in writes (TVP only used for ID lists in reads)
- Dedicated app role with explicit `DENY` on direct DML
- Password handed in at run time via `sqlcmd -v`, not stored in the script file

## Next session
- Wire the .NET app to use the `mealprep_app` connection string
- Decide on EF Core vs Dapper (now genuinely possible to defer — proc API is layer-agnostic)
- Optional: add views for ad-hoc DataGrip exploration if useful
