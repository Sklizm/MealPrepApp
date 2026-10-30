# Contributing to MealPrepApp

MealPrepApp uses .NET 10, Avalonia 11.3, and SQL Server 2022. The distributed application is `App/MealPrepApp.Desktop` on Windows x64, macOS Apple Silicon, and Linux x64.

## Getting started

Install Git, the .NET 10 SDK, and Python 3.11+. See the [platform guides](docs/platforms/) for native dependencies and packaging prerequisites. Windows installer builds also require PowerShell 7 and Inno Setup 6.4.3.

```bash
git clone --branch main https://github.com/Sklizm/MealPrepApp.git
cd MealPrepApp
dotnet run --project App/MealPrepApp.Desktop
```

Use the app's connection setup or copy the relevant `appsettings.Local.template.json` to a local override. Never commit deployment credentials. Database initialization and migrations are documented in the [database guide](docs/database.md).

## Project layout

| Directory | Purpose |
|---|---|
| `App/MealPrepApp.Core/` | UI-independent models, ViewModels, services, and Dapper repositories |
| `App/MealPrepApp.Desktop/` | Avalonia views, vector controls, themes, and desktop integration |
| `App/MealPrepApp/` | Legacy Windows WPF client sharing Core logic |
| `App/legacy-winforms/` | Historical Windows interface reference |
| `Database/` | SQL schema, stored procedures, seeds, and additive migrations |
| `Tests/` | Core/UI regressions, disposable SQL tests, and script checks |
| `scripts/` | Builds, packaging, installers, and database maintenance |
| `packaging/` | Platform installer definitions |
| `docs/` | Platform guides, release procedures, and technical references |
| `nix/` | NixOS development and package bootstrap |
| `Vault/` | Archived database and design references |

## Development rules

- Work on `main`, the maintained branch for all platforms. Pull before editing and preserve shared history.
- Keep Core free of Avalonia and WPF dependencies. Retain legacy client compatibility and its Windows CI build.
- Keep data access behind parameterized stored procedures. Database mutations must retain UTC timestamps, auditing, ownership checks, and concurrency handling.
- Keep connection settings separate from appearance preferences. Credentials, diagnostics containing secrets, generated packages, and backups must stay out of Git.
- Maintain complete English, Russian, and Romanian catalogs, stable presentation identifiers, keyboard access, theme selection, and reduced-motion support. Distinguish loading, empty, error, and retry states.

## Checks

Run checks appropriate to the change:

```bash
dotnet test Tests/MealPrepApp.Tests/MealPrepApp.Tests.csproj -c Release
python3 Tests/Scripts/test_database_update.py
python3 Tests/Scripts/test_release_package.py
python3 Tests/Scripts/test_windows_packaging.py
python3 Tests/Scripts/check_repository_paths.py
python3 Tests/Scripts/test_branch_consolidation.py
python3 Tests/Scripts/check_redesign_contrast.py
python3 Tests/Scripts/check_localization.py
bash scripts/test-database.sh
```

The SQL suite uses a disposable container. Never reset a deployed database to run tests.

If VSTest cannot open its local TCP listener, the direct runner invokes the same test methods in process, with an isolated Avalonia dispatcher for each UI test:

```bash
dotnet run --project Tests/MealPrepApp.RegressionRunner -c Release
```

This does not replace standard xUnit discovery in CI. To run one matching test, pass `--filter=<name>` after `--`. For builds that cannot create MSBuild worker sockets, use `-m:1 -p:UseSharedCompilation=false`.

Generate database-free screenshot fixtures with the runner's `--refinement-screenshots` or `--alignment-screenshots` option. See [UI evidence](docs/README.md) for the output locations. Use neutral sample names and synthetic data in screenshots and documentation.

Compatibility CI runs desktop tests on all three platforms, compiles WPF on Windows, checks packaging and Windows installation, and runs SQL and presentation checks. Native keyboard, hover, file picker, and display-scaling acceptance remains separate from headless rendering and cross-publishing.

## Releases

Builds run automatically; release publication is manual. Follow the [release procedure](docs/releases/README.md) and record native acceptance for the exact release commit. Do not replace published tags or assets.

## Branch maintenance

The optional cleanup helper requires a clean `main`, GitHub CLI authentication, matching local/remote commits, successful Compatibility CI for that exact commit, and fully merged branches:

```bash
bash scripts/consolidate-main.sh
```

It fetches remote heads, rejects unmerged work, sets the default branch, and deletes only merged branches using expected-commit leases. Leave branches intact if remote access or CI cannot be verified.

## Documentation

Write public documentation and developer prose in English for general users and contributors. Translation catalogs and preserved application data are intentionally multilingual. Keep personal identities, device inventories, private deployment details, academic framing, and conversational handoff logs out of public documentation. Preserve the distinction between recorded reports, independently verified results, and outstanding acceptance checks.

## Localization

See the [localization guide](docs/localization/README.md) for resource conventions,
regional formatting, compatibility boundaries, screenshot generation, and acceptance.
Add each resource key to all three embedded catalogs. Retain typed formatting arguments
until presentation; use `LocalizedMessage` for visible asynchronous results and dialogs.
Do not translate database values or compare behavior against translated labels.
