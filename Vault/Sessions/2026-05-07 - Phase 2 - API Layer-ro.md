---
tags: [session, phase2]
date: 2026-05-07
---

# 2026-05-07 — Faza 2: API orientat spre aplicatie + strat de securitate

Aceeasi zi calendaristica cu kickoff-ul, dar un scope clar separat de munca — promovat la propria sa nota de sesiune.

## Obiective
- Da aplicatiei .NET un contract fix de apelat (stored procedures, nu SQL raw)
- Adauga stare de securitate pentru utilizator pe care fluxul de auth al aplicatiei se poate baza (blocare, ultimul login, istoric parole)
- Adauga un audit trail pentru "cine a facut ce, cand?"
- Leaga totul in spatele unui login SQL cu privilegii reduse astfel incat aplicatia sa nu poata fizic rula DML direct

## Ce s-a construit
**Tabele noi** (`Database/07_users_security.sql`, `08_audit_log.sql`):
- `dbo.PasswordHistory` (cascada de la Users)
- `dbo.AuditLog`
- Coloane noi pe `dbo.Users`: `LastLoginAt`, `FailedLoginCount`, `LockedUntil`

**TVP nou**: `dbo.IntList` pentru transmiterea listelor de ID-uri din aplicatia .NET.

**18 stored procedures** sub `Database/procs/`:
- Users: `sp_RegisterUser`, `sp_GetUserForLogin`, `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword`
- Recipes (scriere): `sp_CreateRecipe`, `sp_UpdateRecipe`, `sp_DeleteRecipe`
- Recipes (citire): `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients`
- Ingredients: `sp_AddIngredient`, `sp_GetIngredients`, `sp_SearchIngredients`
- Lookup-uri: `sp_GetUnits`, `sp_GetCategories`
- Helper intern: `sp_WriteAudit` (apelata din fiecare procedura de mutatie)

**Strat de securitate** (`Database/09_app_role.sql`):
- Login SQL `mealprep_app` (parola setata la rulare via `sqlcmd -v AppPassword=…`, nu in fisier)
- Utilizator de baza de date mapat la acel login
- Rol `mealprep_app_role` cu `GRANT EXECUTE ON SCHEMA::dbo` si `DENY SELECT/INSERT/UPDATE/DELETE ON SCHEMA::dbo`
- Rolul poate de asemenea `EXECUTE` tipul TVP `IntList`
- Mutatiile reusesc doar prin stored procedures via **ownership chaining** SQL Server (procedura si tabelele sunt detinute de `dbo`).

## Verificare (aceeasi sesiune)
Fiecare bucata construita + testata in izolare inainte de a trece mai departe. Re-rularea finala end-to-end a `run_all.sql` a fost curata si idempotenta.

Testele care au trecut:
- Toate cele 18 proceduri vizibile in `sys.procedures`
- `sp_RegisterUser` → rand + audit
- `sp_RecordLoginFailure` × 5 → `LockedUntil` setat + audit `ACCOUNT_LOCKED`
- `sp_RecordLoginSuccess` → reset + `LastLoginAt` setat
- `sp_ChangePassword` respinge hash-urile curente si ultimele 5; curatarea verificata peste 6 schimbari
- `sp_CreateRecipe` circula JSON-ul de lista de ingrediente corect
- `sp_UpdateRecipe` / `sp_DeleteRecipe` resping proprietarul gresit cu `THROW 50002`
- Paginarea `sp_GetRecipes` returneaza `TotalCount` corect din functia window
- `sp_FindRecipesByIngredients` returneaza contoare matched/total si sorteaza cele mai bune potriviri primele
- Login-ul aplicatiei: poate `EXEC dbo.sp_GetCategories` ✓, respins `SELECT * FROM dbo.Users` ✓, poate face mutatii via `sp_RegisterUser` ✓ (ownership chaining), respins `SELECT FROM dbo.AuditLog` direct ✓

## Bug-uri prinse si corectate
- **Curatarea istoricului de parole era non-determinista** cand mai multe schimbari se intamplau in aceeasi secunda (`ChangedAt` este `DATETIME2(0)` — rezolutie de 1 secunda). Corectat prin adaugarea `PasswordHistoryID DESC` ca tiebreaker atat in verificarea de recenta cat si in ROW_NUMBER() de curatare. Coloana IDENTITY creste intotdeauna monoton.
- **Bug in scriptul de test** (nu in cod): am incercat sa trec `CONCAT(...)` direct ca parametru de stored procedure; T-SQL permite acolo doar variabile/constante. Corectat prin legare la `@var` mai intai.

## Decizii luate → vezi [[Decisions Log-ro]]
- API doar prin stored procedures
- Stergere hard pastrata (fara soft-delete)
- Blocare: 5 esecuri → 15 minute de blocare
- Adancimea istoricului de parole: 5
- JSON peste TVP pentru listele de ingrediente in scrieri (TVP folosit doar pentru liste de ID-uri in citiri)
- Rol dedicat al aplicatiei cu `DENY` explicit pe DML direct
- Parola predata la rulare via `sqlcmd -v`, nu stocata in fisierul de script

## Sesiunea urmatoare
- Conecteaza aplicatia .NET sa foloseasca connection string-ul `mealprep_app`
- Decide intre EF Core si Dapper (acum cu adevarat posibil de amanat — API-ul de proceduri este agnostic la strat)
- Optional: adauga view-uri pentru explorare ad-hoc in DataGrip daca este util
