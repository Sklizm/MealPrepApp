# MealPrepApp working agreements

- Read [README.md](README.md) and [CONTRIBUTING.md](CONTRIBUTING.md) before changes. `main` is the single maintained branch for all platforms; preserve shared Git history.
- Distribute `App/MealPrepApp.Desktop` (Avalonia) on Windows x64, macOS Apple Silicon, and Linux x64. Preserve `App/MealPrepApp` (WPF) and its build for legacy Windows compatibility. Do not move or rename project folders without updating all references.
- Keep `App/MealPrepApp.Core` free of Avalonia and WPF dependencies. Use stored procedures for C# data access, UTC timestamps, and mutation auditing.
- Follow [platform guides](docs/platforms/) and [release instructions](docs/releases/README.md). Run checks appropriate to the change; distinguish headless/build evidence from native acceptance.
- Never commit or package deployment credentials. Store Avalonia connection settings in the per-user `appsettings.Local.json`; diagnostics must not include messages containing credentials. Preserve user settings across app upgrades and uninstall.
- Run SQL tests in the disposable test container. Live database updates require the existing verified-backup-first scripts; client installers must not provision or migrate the server database.
- Keep technical schema/design notes and older validation evidence as historical references. Current operational instructions belong in the README, contributor guide, and platform guides.

- Public descriptions, README files, docs, examples and screenshots must address general users. Exclude personal identities, academic framing, private deployments and conversational handoffs. Use neutral synthetic accounts for fixtures.

- Public documentation and developer prose are English. Avalonia defaults to English and supports Russian and Romanian live switching. Keep catalog keys and placeholders complete; preserve user data and legacy WPF Romanian behavior.
