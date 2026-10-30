# Localization

The Avalonia app starts in English. Settings offers **English / Русский / Română**;
selection applies immediately and persists in `preferences.json`. Missing, malformed,
or unsupported language values select English while retaining other valid preferences.
Legacy WPF explicitly selects Romanian at startup and retains its existing screens.

## Resource and state conventions

`ILocalizationService` belongs to Core and has no UI dependency. Its embedded catalogs
are `App/MealPrepApp.Core/Localization/Catalogs/{en,ru,ro}.json`. Resource keys identify
messages; typed arguments remain available for formatting after a language change.
English, Romanian, and Russian have explicit singular/few/many count rules.

Use `L.Message(key, args)` for validation, errors, notifications, and application dialogs.
Use `L.Count(key, count)` for counted messages. Database errors retain their numeric
identity and map to localized messages without exposing raw connection errors.
Use `L.Text` for computed display properties and exports evaluated at the time of use.

Avalonia's `Loc` markup extension uses an explicit service source, independent of the
view's DataContext. `Display` translates known built-in identities at the display
boundary; `Format` combines a typed binding value with the active language. Presentation
properties marked `[LocalizedProperty]` refresh without issuing repository requests, replacing page instances, or
resetting collections. Cached pages and open dialogs retain their model state.
Subscriptions use weak targets and disposable presenters release them on disposal.

Navigation and selection keep stable identifiers. Existing Romanian identifiers and
procedure values are retained for compatibility and are never replaced by translated
labels. Category labels translate only recognized built-ins; custom names and all recipe
content, ingredients, instructions, notes, units, and stored values remain unchanged.

Date selectors use the shared localized calendar control, preserving the selected date
and incomplete input while refreshing calendar labels. Numeric conventions and date
order come from the system region. Switching updates day
and month names while preserving numeric separators and date patterns. Partially entered
numeric text is retained. Native operating-system file pickers follow the OS language.

## Verification and captures

Run the normal test suite and the localization-specific methods:

```bash
dotnet test Tests/MealPrepApp.Tests -c Release
dotnet run --project Tests/MealPrepApp.RegressionRunner -c Release -- --filter=Localization
dotnet run --project Tests/MealPrepApp.RegressionRunner -c Release -- --localization-screenshots
```

Use `--localization-section=reports` in place of `--localization-screenshots` to refresh
only one section while rebuilding its language manifests.

Refreshed public captures live under `screenshots/en/`, with neutral English synthetic
content. `screenshots/ru/` and `screenshots/ro/` are labeled localization-validation
evidence. Older screenshots elsewhere in `docs/` are historical comparisons and must
not be presented as captures of the English-default app.

Each language covers all seven sections in both themes at 860×600, 1280×800, and
1440×900, plus representative 150% and 200% bitmap exports. Settings, connection,
authentication, and application-dialog captures cover both themes. Per-language
manifests identify image names, byte sizes, and SHA-256 hashes.

Headless renders verify layout and Cyrillic font rendering. They do not establish native
DPI behavior, OS keyboard navigation, native menu handling, or installation acceptance.
See [validation](validation.md) for the exact checks run and remaining acceptance.
