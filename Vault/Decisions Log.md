---
tags: [decisions, adr]
---

# Decisions Log

Architectural decisions with reasoning. Append, don't rewrite — even when reversed,
keep the original entry and add a follow-up entry that supersedes it.

---

## 2026-05-07 — Core scope only for v1
**Decision**: Ship with 6 tables (Users, Units, Categories, Ingredients, Recipes, RecipeIngredients). No meal plans, shopping lists, nutrition, or photos in v1.
**Why**: Practica needs a demoable result, not a complete product. Shipping a small thing that works beats shipping a big thing that doesn't.
**Trade-off**: We'll need a follow-up phase to add the rest. That's fine — the schema is designed to extend additively.

---

## 2026-05-07 — Ingredients are global (no UserID)
**Decision**: [[Ingredients]] table has no `UserID` — every user shares the same ingredient list.
**Why**: "Salt" doesn't need to be re-created for each user. Simpler schema, simpler queries, and the .NET app's autocomplete is better with a shared list.
**Reversibility**: Add a nullable `UserID` column later (`NULL` = global, otherwise = user-private). Existing data stays valid.

---

## 2026-05-07 — Only one cascade delete
**Decision**: `ON DELETE CASCADE` only on Recipes → [[RecipeIngredients]]. Everything else is RESTRICT.
**Why**: Cascading deletes feel convenient until they silently destroy data. RecipeIngredients rows have no meaning without their recipe, so cascading there is safe. Deleting a user with recipes, or an ingredient that's in use, should be an explicit operation — not a side effect.

---

## 2026-05-07 — UTC timestamps via SYSUTCDATETIME()
**Decision**: All `CreatedAt` / `UpdatedAt` columns default to `SYSUTCDATETIME()`.
**Why**: The Docker container's local time is whatever the host happens to be, and the .NET app may serve users in different timezones. UTC is the only stable reference. The app converts to local time for display.

---

## 2026-05-07 — Idempotent scripts
**Decision**: Every CREATE wrapped in `IF OBJECT_ID(...) IS NULL` (or equivalent for indexes). Seeds use `MERGE`.
**Why**: Re-running the build during development must be safe. No "drop and recreate" — that destroys local test data. No manual "did I already run this?" tracking.

---

## 2026-05-07 — Constraint naming convention
**Decision**: `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_` prefixes; column-specific suffix.
**Why**: Auto-generated constraint names (`PK__Users__1788CC4C7DEB...`) are unstable across rebuilds and unreadable in error messages. Explicit names make migration scripts and error logs much easier to read.

---

## 2026-05-07 — NVARCHAR everywhere
**Decision**: All string columns use `NVARCHAR` (Unicode), not `VARCHAR`.
**Why**: Recipe names, ingredient names, and instructions might include accented characters, emoji, or non-Latin scripts. Storage cost is negligible compared to the cost of a future migration.

---

## 2026-05-07 — Stored-proc-only API for the app
**Decision**: The .NET app will NOT have direct table access. It connects as a low-privilege SQL login (`mealprep_app`) that only has `GRANT EXECUTE ON SCHEMA::dbo`, with explicit `DENY SELECT/INSERT/UPDATE/DELETE ON SCHEMA::dbo`. Mutations succeed via ownership chaining (procs and tables share the `dbo` owner).
**Why**: SQL injection becomes structurally impossible from the app side — there is no path for an attacker-controlled string to land in an ad-hoc query. Also forces a clean DB/app contract: the procs are the API.
**Trade-off**: Every new query needs a new proc. For v1 this is fine — list of procs is bounded. If the app needs ad-hoc reporting later, add a read-only role with `GRANT SELECT` on specific views, not on tables.

---

## 2026-05-07 — Hard delete kept (no soft-delete)
**Decision**: Deletes physically remove rows. No `IsDeleted` flag.
**Why**: Practica scope is small; recovery isn't a goal. Soft-delete adds complexity to every read query (every WHERE needs `AND IsDeleted = 0`).
**Reversibility**: Adding soft-delete later is non-trivial — every proc and view would need to filter. If we ever do, it's a Phase 3 decision.

---

## 2026-05-07 — Lockout policy: 5 failures → 15 minutes
**Decision**: `sp_RecordLoginFailure` increments `FailedLoginCount`; on the 5th failure it sets `LockedUntil = now + 15 min` and writes `ACCOUNT_LOCKED` to AuditLog. Successful login resets both.
**Why**: Industry-standard order of magnitude. Long enough to deter brute force, short enough that legitimate users aren't catastrophically locked out.
**Reversibility**: Both numbers are local constants in `sp_RecordLoginFailure`. Easy to tune.

---

## 2026-05-07 — Password history depth: 5
**Decision**: `sp_ChangePassword` rejects reuse of the current password OR the last 5 entries in `dbo.PasswordHistory`. Pruning keeps history at exactly 5 rows per user.
**Why**: Common compliance default. More than enough to prevent obvious cycling, not so many that users feel boxed in.
**Reversibility**: `@HistoryDepth` is a local constant in the proc.

---

## 2026-05-07 — JSON for write payloads, TVP for read filters
**Decision**: `sp_CreateRecipe` / `sp_UpdateRecipe` accept ingredients as `@IngredientsJson NVARCHAR(MAX)` parsed via `OPENJSON`. `sp_FindRecipesByIngredients` accepts a `dbo.IntList` TVP.
**Why**: From C# / EF Core / Dapper, serializing a list to JSON with `System.Text.Json` is a one-liner; building a `DataTable` for a TVP is more code. But for pure ID lists in *read* paths, the TVP is cleaner and SQL Server can optimize it as a real (possibly-indexed) table.
**Trade-off**: Two payload styles in one API. Documented in proc-level comments.

---

## 2026-05-07 — App login password supplied at run time, not stored in the file
**Decision**: `09_app_role.sql` uses sqlcmd variable substitution (`$(AppPassword)`). The actual value is passed via `sqlcmd -v AppPassword="..."`.
**Why**: The file is committed to git; the password should not be. Run-time injection keeps the secret out of source control without giving up idempotency.
**How to apply**: Anyone re-running the build needs the password (kept by Codrin). Rotation is `ALTER LOGIN mealprep_app WITH PASSWORD = 'new'`.

---

## 2026-05-07 — Password history pruning needs a deterministic tiebreak
**Decision**: Both the "is this in the last 5 hashes?" check and the pruning `ROW_NUMBER()` order by `ChangedAt DESC, PasswordHistoryID DESC`. Not just `ChangedAt DESC`.
**Why**: `ChangedAt` is `DATETIME2(0)` (whole seconds). Multiple password changes in the same second have identical timestamps, so ordering by timestamp alone is non-deterministic and pruning can delete the wrong row. `PasswordHistoryID` is `INT IDENTITY` so it always grows monotonically — perfect tiebreak.
**How to apply**: Any `ORDER BY <timestamp>` over rows that can be created in rapid succession needs an IDENTITY tiebreak. Worth remembering for any future "recent N" query.
