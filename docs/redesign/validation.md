# Validation — 28 October 2026

The source changes are implemented. Full release acceptance remains pending: isolated SQL execution, standard xUnit/VSTest, WPF compilation and native
macOS/Hyprland smoke tests remain outstanding for the environmental reasons below.

Codrin reports that the rebuilt interface looks good. This is user feedback; the full
native acceptance checklist and expanded SQL suite remain unverified.

## Passed on ideapad-server

- Release compilation of Core, Desktop, tests and direct runner: zero warnings/errors.
  `dotnet build Tests/MealPrepApp.RegressionRunner -c Release --no-restore -m:1 -v quiet`
- **21/21 direct regression methods**. [Exact output](regression-output.txt).
  The direct runner executes the same Fact/AvaloniaFact methods without VSTest's TCP
  transport; it does not replace standard test discovery or CI execution.
- Navigation commands/selection for all seven shell destinations; real populated recipe,
  draft, ingredient, grouped ingredient, pantry, monthly/weekly calendar, shopping and
  agenda templates. Overflow menus are opened and their command and exact row parameter
  are asserted. Report rankings and labelled category distributions render populated data.
- Decimal serving factors, invalid base/target handling, unmodified saved quantities and
  retained incomplete-nutrition flags; cooking check progress, preserved instruction
  paragraphs, monotonic timer completion and session lifetime across page models.
- Preference persistence and malformed-file recovery; retained section instances and
  rejected dirty departures; editor field/ingredient dirty snapshots and failed draft save;
  explicit ownership/favorite labels; Home destination parameters; loading/error suppression
  of recipe empty states; live reduced-motion transition updates.
- Existing BCrypt, photo-processing, timeout mapping, modal-close, DataGrid selection and
  navigation regressions continue to pass.
- **28 semantic foreground/background pairs** meet 4.5:1 in the contrast check, including
  action hover/pressed and all six meal category colors. [Measured ratios](contrast.txt).
  Reproduce with `python3 Tests/Scripts/check_redesign_contrast.py`.
- **72 actual Avalonia shell renders**: Home, recipe detail, planner, component gallery ×
  two themes × three window sizes × three render DPIs. PNG dimensions checked against
  the requested matrix. [Manifest](screenshot-matrix.json). Animation is disabled during
  capture for deterministic settled states; real transition preference behavior is tested.
  Representative light/dark and narrow layouts were inspected visually.
- Three existing backup-first updater orchestration tests pass:
  `python3 Tests/Scripts/test_database_update.py`. These mock Docker and do not execute SQL.
- All 34 AXAML files parse; SQL runner/updater shell syntax and `git diff --check` pass.
- macOS arm64 and Linux x64 self-contained packaging succeeded using cached dependencies:
  `bash scripts/build-mac.sh -p:NuGetAudit=false -p:RestoreSources=/tmp/mealprep-empty-feed -m:1 -v quiet`
  and the corresponding `build-linux.sh`. No dependency versions were changed by this
  redesign. Offline builds do not constitute a new vulnerability audit.

## Environment-blocked verification

| Check | Observed result | Required follow-up |
|---|---|---|
| Standard `dotnet test … -c Release --no-build --no-restore` | VSTest aborts with `SocketException (13): Permission denied` while creating its local TCP listener | Run the normal xUnit command in CI or a normal shell |
| `bash scripts/test-database.sh` | Docker API socket permission denied | Run the complete disposable SQL suite with Docker access |
| WPF cross-build / offline restore with `EnableWindowsTargeting=true` | `Microsoft.WindowsDesktop.App.Ref` is absent from the local package cache (`NU1101`); initial no-restore build reported missing assets | Run the WPF Release build on Windows/CI |
| macOS native UI | Execution host is Linux/headless | Run the documented native acceptance flow on the Mac |
| NixOS/Hyprland UI | No local graphical session; remote native session not available | Run the same flow under Hyprland/XWayland, including actual OS DPI scaling |

The offline WPF restore was followed by a normal cached restore of the Desktop regression
project; the successful final build/tests use the repaired Core/Desktop assets.

## SQL coverage supplied, not executed here

`Tests/Database/redesign-regression.sql` covers mass and volume normalization, incompatible
units, saved snapshot preservation on regeneration, purchase state independent of stock,
once-only imports and audit counts, owner rejection, copied-week duplicate skipping and
preserved targets, stale preview rejection, per-entry auditing, and an injected audit-write
failure proving transactional rollback. The upgrade script checks preserved purchases,
imports and planned meals after the new additive migration runs twice.

The complete runner also retains the prior schema, authorization, stale-save, failed-write,
password and compatibility-upgrade regressions. None of the new SQL assertions have been
reported as passing: the Docker failure prevents executing them on this host. Concurrent
multi-client timing and native drag-and-drop still need integration testing.

## Artifacts and rollout status

- macOS bundle: `dist/mac/MealPrepApp.app`
- macOS transfer archive: `dist/MealPrepApp-macos-arm64.tar.gz`
- Linux executable folder: `dist/linux/`
- Screenshots and matrix: `docs/redesign/screenshots/`, `screenshot-matrix.json`
- New migration: `Database/migrations/20261028_cooking_redesign.sql`

The macOS executable is Mach-O arm64; the Linux executable is ELF x86-64. Package scans
exclude `appsettings.Local.json`. The database has **not** been migrated by the agent.
Use the [backup-first rollout commands](README.md#database-rollout) only after the
isolated SQL suite passes. Native smoke testing is required before declaring the complete
find → scale → cook/plan → checklist → explicit pantry import flow accepted.
