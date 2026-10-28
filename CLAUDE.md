# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview & Architecture

MealPrepApp is a cross-platform .NET 10 desktop solution backed by a 24/7 SQL Server database.

- `App/MealPrepApp.Core/` — Pure .NET 10 library. Contains all Models, Dapper repositories, Services (`IFilePickerService`, `IDialogService`), and platform-neutral ViewModels. Zero UI or platform dependencies.
- `App/MealPrepApp.Desktop/` — Avalonia UI 11.3 desktop application. Targets macOS (Apple Silicon `osx-arm64`) and Linux (`linux-x64` / NixOS Wayland). Uses native window chrome, macOS top `NativeMenu`, and Skia rendering. Linux uses X11/XWayland.
- `App/MealPrepApp/` — Original Windows WPF desktop application targeting `net10.0-windows`. Preserved for university practica grading.
- `Database/` — SQL Server 2022 database scripts: schema, stored procedures, seeds, and the idempotent `run_all.sql`.
- `docker-compose.db.yml` — Docker Compose configuration for the 24/7 Home Server SQL Server container.
- `flake.nix` & `shell.nix` — Nix flake and environment for building/running on NixOS workstations.
- `scripts/` — Cross-compilation packaging scripts:
  - `build-mac.sh`: Produces self-contained `MealPrepApp.app` bundle for macOS.
  - `build-linux.sh`: Produces self-contained folder for Linux.
- `Vault/` — Obsidian project memory and decisions log.

## Building, validation and database updates

Use the current commands in [README.md](README.md) for .NET 10 builds, macOS transfer
archives, Linux packaging and per-user connection settings. Linux uses X11/XWayland.
Nix dependency metadata is an unvalidated bootstrap; see [nix/README.md](nix/README.md).

The database container is `mealprep-db`, publishing TCP 1433. Docker Compose only starts
SQL Server. For new setup, follow the README initialization instructions and supply the
application login password externally. For an existing database, run the disposable
suite, close clients, then apply the verified-backup-first migrations in order:

```bash
bash scripts/test-database.sh &&
bash scripts/update-database.sh mealprep-db &&
bash scripts/update-database.sh mealprep-db 20261028_cooking_redesign.sql
```

Do not substitute the seed-inclusive initialization script for the live upgrade process.
The updater preserves credentials and data and stops if backup verification/copy fails.

The [redesign guide](docs/redesign/README.md) describes the theme, navigation, cooking,
planning and saved shopping snapshot implementation. [Validation evidence](docs/redesign/validation.md)
distinguishes passing headless/direct checks from pending SQL, Windows and native tests.
Do not claim those pending checks passed based on a successful source build.

## Security & Architecture Constraints

- **Stored-Procedure-Only API:** Application login `mealprep_app` has `GRANT EXECUTE` on `dbo` schema and `TYPE::dbo.IntList`, but direct `SELECT`, `INSERT`, `UPDATE`, `DELETE` on tables is explicitly `DENIED`.
- **Decoupled ViewModels:** ViewModels in `MealPrepApp.Core` must remain 100% portable. Do not import `System.Windows.*` or `Avalonia.*` in Core.
- **Local Settings:** Connection strings are loaded from `appsettings.Local.json` (gitignored, excluded from packages), with per-user Desktop overrides. Presentation choices live separately in `preferences.json`. Never commit database passwords.
- **Auditing:** All mutating procs write audit rows via `sp_WriteAudit`.
- **Optimistic Concurrency:** Recipes carry `ROWVERSION` (`byte[]`) to detect and reject stale edits.
