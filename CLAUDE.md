# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview & Architecture

MealPrepApp is a cross-platform .NET 10 desktop solution backed by a 24/7 SQL Server database.

- `App/MealPrepApp.Core/` — Pure .NET 10 library. Contains all Models, Dapper repositories, Services (`IFilePickerService`, `IDialogService`), and 37 decoupled ViewModels. Zero UI or platform dependencies.
- `App/MealPrepApp.Desktop/` — Avalonia UI 11.2 desktop application. Targets macOS (Apple Silicon `osx-arm64`) and Linux (`linux-x64` / NixOS Wayland). Uses native window chrome, macOS top `NativeMenu`, and Skia rendering.
- `App/MealPrepApp/` — Original Windows WPF desktop application targeting `net10.0-windows`. Preserved for university practica grading.
- `Database/` — SQL Server 2022 database scripts: schema, stored procedures, seeds, and the idempotent `run_all.sql`.
- `docker-compose.db.yml` — Docker Compose configuration for the 24/7 Home Server SQL Server container.
- `flake.nix` & `shell.nix` — Nix flake and environment for building/running on NixOS workstations.
- `scripts/` — Cross-compilation packaging scripts:
  - `build-mac.sh`: Produces self-contained `MealPrepApp.app` bundle for macOS.
  - `build-linux.sh`: Produces standalone single-file binary for Linux.
- `Vault/` — Obsidian project memory and decisions log.

## Running the Database

The database runs in a Docker container named `mealprep-db` (port 1433).

Start container:
```bash
docker compose -f docker-compose.db.yml up -d
```

Execute full schema migration (idempotent):
```bash
docker exec -w /docker-entrypoint-initdb.d mealprep-db \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'MealPrepSecure2026!' -C -b -i run_all.sql
```

Query with low-privilege application login:
```bash
docker exec mealprep-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U mealprep_app -P 'MealPrepSecure2026!' -C -d MealPrepDB -Q "EXEC sp_GetUnits;"
```

## Security & Architecture Constraints

- **Stored-Procedure-Only API:** Application login `mealprep_app` has `GRANT EXECUTE` on `dbo` schema and `TYPE::dbo.IntList`, but direct `SELECT`, `INSERT`, `UPDATE`, `DELETE` on tables is explicitly `DENIED`.
- **Decoupled ViewModels:** ViewModels in `MealPrepApp.Core` must remain 100% portable. Do not import `System.Windows.*` or `Avalonia.*` in Core.
- **Local Settings:** Connection strings are loaded from `appsettings.Local.json` (gitignored). Never commit database passwords.
- **Auditing:** All mutating procs write audit rows via `sp_WriteAudit`.
- **Optimistic Concurrency:** Recipes carry `ROWVERSION` (`byte[]`) to detect and reject stale edits.
