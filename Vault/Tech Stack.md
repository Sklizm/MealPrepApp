---
tags: [tech, stack]
---

# Tech Stack

## Database Side (current focus)
- **SQL Server** running in a **Docker container** (already set up by Codrin)
- **DataGrip** as the GUI client (already connected)
- T-SQL dialect; idempotent scripts so re-runs are safe

## App Side (future)
- **.NET** (Visual Studio)
- Will connect to the same SQL Server instance
- Auth, business logic, and UI all live here — not Codrin's part

## Conventions
- All `CREATE TABLE` wrapped in `IF OBJECT_ID(...) IS NULL`
- All seeds use `MERGE` so they're rerunnable
- Strings: `NVARCHAR` (Unicode)
- Timestamps: `DATETIME2(0)` defaulting to `SYSUTCDATETIME()` (UTC, not local)
- PK columns named `<TableName>ID`, `INT IDENTITY(1,1)`
- Constraints prefixed: `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_`

See [[Decisions Log]] for the reasoning behind these.
