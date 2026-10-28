# Cooking-centred Desktop redesign

The Avalonia client redesign builds on the compatibility repairs and preserves the WPF
client and its procedure interfaces. The three stages below organize the implementation
for review. For build, transfer and connection commands, start with the [main README](../../README.md).
The agent has not changed the production database; rollout remains a separate operation.

## Stage 1 — visual system, shell, recipes

- System / Light / Dark, warm semantic brushes, registered Inter, accessible action colors,
  28/20/14/12 typography, 36 px controls, reusable card/navigation/form/status styles.
- Bundled outline vector geometries in `Themes/Icons.axaml`; no new UI dependency.
- Native window decorations, 1240×820 default, 860×600 minimum, icon rail below 1050 px.
- Seven direct destinations; profile/settings in the sidebar footer; native menus retained.
- Cached section models, scroll offsets tied to model lifetime, destination-aware Home
  Favorite/Drafts/search/fridge shortcuts, recent-recipe actions and today's meals.
- Recipe toolbar filters, combined search/category/sort and paging, responsive grid/list,
  image-free placeholders, explicit favorite/photo labels, owner-only editing controls,
  and an overflow menu with command/row binding regression coverage.
- Server-side sorting adds an optional procedure argument; old WPF callers remain valid.
- Core presentation preferences live in `preferences.json`, separate from connection settings.
- `ComponentGallery` provides populated, empty, loading, validation, disabled, error and
  notification components using the actual application resources.

Primary review files: Desktop `Themes/`, `Views/ShellWindow*`, `Views/AcasaView*`,
`Views/Retete/*`, `Controls/RecipeCardPanel.cs`, `Controls/ScrollMemory.cs`, Core
`NavigationService.cs`, `PresentationPreferences.cs`, `ShellViewModel.cs`, `AcasaViewModel.cs`.

## Stage 2 — inventory, editing, reports, settings

- Search/sort pantry, inline quantity/save controls, real category grouping in pantry
  and ingredient catalog; missing ingredient nutrition is labelled explicitly.
- Numeric recipe inputs, searchable ingredient entry, persistent save buttons, dirty
  snapshots covering fields/rows, and Save draft / Discard / Stay navigation/close guards.
- Photo management remains on the saved recipe's detail screen. The editor explains this;
  draft photos are not stored by the existing draft API.
- Nonmodal shell feedback, explicit loading/error/empty states on the primary flows,
  labelled vector buttons, search/save/back shortcuts, password visibility, scrollable auth.
- Theme and recipe-view preference settings plus connection test/edit/save; passwords stay
  masked, are omitted from diagnostics, and connection changes require restarting the app.
- 100 ms application color transitions; reduced-motion disables them and indeterminate
  progress animation. Native controls retain their platform behavior.
- Reports use readable full-width rankings and labelled category distributions.

Primary review files: Desktop `Views/Ingrediente/*`, `Views/Auth/*`, `Views/Rapoarte/*`,
`Views/SettingsWindow*`, `Services/DesktopDialogService.cs`, Core ingredient/editor models.

## Stage 3 — cooking, planning, persisted checklists

- Serving calculations use decimal arithmetic, do not mutate stored recipes, preserve
  units, and round only the display. Nutrition estimates retain incomplete-data flags.
- Cooking mode preserves instruction paragraphs, temporary ingredient checks, and a
  manually configured monotonic timer. Timer state remains in the shell across navigation;
  closing/signing out ends the session. Pantry stock is never consumed by ingredient checks.
- Weekly default, narrow agenda, supplementary categories, serving counts, drag-to-move,
  edit-dialog move, duplicate-to-date, and preview-before-copy week actions.
- Week copy locks source/target ranges in a transaction, rejects changed source previews,
  preserves target meals, skips recipe/date/category duplicates and audits created entries.
- Saved shopping snapshots keep old lists and purchase/import state per user. Purchase
  checks only change list state. Separate confirmed import adds the displayed quantity to
  pantry once per snapshot item, atomically with its import marker and audit event.
- Only supported mass conversions to g and l→ml normalize. No density or cup/weight
  conversion is guessed. Purchase quantities round up to the pantry's 0.01 precision.
- Category grouping, purchased/unpurchased filters, progress, local generation dates,
  Excel export and plain-text clipboard copying.

Primary review files: Core `ServingCalculator.cs`, `CookingSession.cs`,
`CookingViewModel.cs`, `ShoppingListViewModel.cs`, shopping/meal repositories,
Desktop cooking/weekly/shopping views, `Database/19_shopping_snapshots.sql`,
`Database/procs/15_shopping_snapshots.sql`, `16_week_copy.sql`.

## Screenshots and validation

[Validation evidence and outstanding checks](validation.md).
[Contrast report](contrast.txt). Rebuild packages with the [macOS and Linux commands](../../README.md#build-and-run).

To reproduce direct checks and all 72 shell screenshots from the repository root:

```bash
dotnet run --project Tests/MealPrepApp.RegressionRunner -c Release
dotnet run --project Tests/MealPrepApp.RegressionRunner -c Release -- --screenshots
python3 Tests/Scripts/check_redesign_contrast.py
```

- [Home, light](screenshots/home-Light-1280-100.png)
- [Recipe detail, dark](screenshots/recipe-Dark-1280-100.png)
- [Weekly planner, light](screenshots/planner-Light-1280-100.png)
- [Narrow Home](screenshots/home-Light-860-100.png)
- [Component gallery](screenshots/components-Light-1440-100.png)

`RedesignSamples` renders the actual shell/views with database-free Romanian fixtures in
both themes, at 860×600, 1280×800 and 1440×900, with 100/150/200% render DPI. These are
headless render checks, not native OS display-scaling smoke tests. Initial page-only design
samples are also retained with filenames lacking the DPI suffix.

## Release candidate

See the [draft release procedure](../releases/README.md) for versioned archives, checksums,
CI evidence and remaining native acceptance checks.

## Database rollout

The new client needs both the compatibility procedures and the cooking redesign migration.
Do not deploy the new shopping/planning features against an unmigrated database.
Old procedure names/signatures remain available for the WPF client.

First run the complete disposable suite from a shell that can access Docker:

```bash
bash scripts/test-database.sh
```

After it passes, close clients and use the existing verified-backup-first updater:

```bash
bash scripts/update-database.sh mealprep-db &&
bash scripts/update-database.sh mealprep-db 20261028_cooking_redesign.sql
```

Each invocation makes a COPY_ONLY CHECKSUM backup, verifies it, and securely copies it to
`backups/` before applying its migration. A failed backup/copy stops the update. The
redesign migration is additive, runs transactionally, and does not rerun seeds or change
login credentials. Codrin reports both migrations and the isolated SQL suite passed from normal SSH;
the agent has not independently inspected the deployed schema.

## Native acceptance flow

On both macOS and NixOS/Hyprland: find a recipe with combined filters; return from its
page without losing filters/scroll; scale it; start cooking and a timer; navigate away
and return; plan/move/duplicate meals; preview/copy a week including supplementary slots;
generate/reopen a saved list; check items; confirm an import; repeat the import and verify
stock is unchanged. Exercise keyboard focus, Cmd/Ctrl+F/S, Alt+Left, native menus/file
pickers, resizing, System/Light/Dark changes, and editor leave/close/sign-out decisions.
Verify the timer-complete banner and the documented stop-on-close behavior.

Existing and new database APIs trust the application's user ID as before; this change does
not introduce a new authentication boundary. Shared household, expiry-batch, nutrition-goal,
URL import, AI planning and web frontend work remain outside this redesign.
