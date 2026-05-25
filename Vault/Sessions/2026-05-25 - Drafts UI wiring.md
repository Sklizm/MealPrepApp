---
tags: [session, app, drafts, wpf]
date: 2026-05-25
---

# 2026-05-25 — Drafts UI wiring

## Purpose
Codrin asked Hermes to implement Drafts after the draft/photo DB scripts were verified.

## Implemented

### Dependency injection
Registered `DraftRepository` in `App/MealPrepApp/App.xaml.cs`.

### Recipe editor
Updated `App/MealPrepApp/ViewModels/Retete/ReteteEditorViewModel.cs`:

- Injects `DraftRepository`.
- Adds `DraftId` and `IsDraft` state.
- Loads a draft into the editor via `PopulateFromDraftAsync`.
- Deserializes draft ingredient rows from `IngredientsJson` into editable ingredient rows.
- Adds `SaveDraftCommand`.
- Saves incomplete editor state as a draft without normal recipe validation.
- Deletes the source draft after a draft is successfully saved as a real recipe.

Updated `App/MealPrepApp/Views/Retete/ReteteEditorView.xaml`:

- Added `Salveaza ciorna` button wired to `SaveDraftCommand`.

### Recipe list
Updated `App/MealPrepApp/ViewModels/Retete/ReteteListViewModel.cs`:

- Injects `DraftRepository`.
- Adds `Drafts` collection.
- Adds `Ciorne` sidebar filter.
- Adds draft list loading via `sp_GetDrafts`.
- Adds `OpenDraftCommand` to open a draft in the editor.
- Adds `DeleteDraftCommand` with confirmation.

Updated `App/MealPrepApp/Views/Retete/ReteteListView.xaml`:

- Added `Ciorne` sidebar entry.
- Added draft card grid with open/delete behavior.
- Added empty state for no drafts.

## Validation performed

Created a small static regression script at:

- `.hermes/tests/test_drafts_static.py`

Ran:

```bash
python .hermes/tests/test_drafts_static.py
```

Result:

- PASS `test_draft_repository_registered_in_di`
- PASS `test_recipe_list_exposes_draft_filter_and_commands`
- PASS `test_recipe_editor_can_save_and_load_drafts`
- PASS `test_draft_controls_are_visible_in_xaml`

Also parsed the changed XAML files as XML:

- `App/MealPrepApp/Views/Retete/ReteteListView.xaml`
- `App/MealPrepApp/Views/Retete/ReteteEditorView.xaml`

Result: both parsed successfully.

Attempted local app build:

```bash
dotnet build App/MealPrepApp/MealPrepApp.csproj -v:minimal
```

Result: blocked by local environment, not by a source error:

- local Fedora has .NET SDK `9.0.117`
- project targets `net10.0-windows`
- build fails with `NETSDK1045` because this SDK cannot target .NET 10

## Important follow-up

Drafts are wired, but still need verification on the Windows/.NET 10 environment/VM:

1. Build the app with a .NET 10 SDK.
2. Open Retete > create recipe > Save Draft.
3. Confirm Retete > Ciorne shows the draft.
4. Open the draft and verify fields/ingredients restore.
5. Save the draft as a real recipe and confirm the draft disappears.
6. Confirm delete draft works.

## Files changed

- `App/MealPrepApp/App.xaml.cs`
- `App/MealPrepApp/ViewModels/Retete/ReteteEditorViewModel.cs`
- `App/MealPrepApp/ViewModels/Retete/ReteteListViewModel.cs`
- `App/MealPrepApp/Views/Retete/ReteteEditorView.xaml`
- `App/MealPrepApp/Views/Retete/ReteteListView.xaml`
- `.hermes/tests/test_drafts_static.py`
- `Vault/TODO.md`
- `Vault/Sessions/2026-05-25 - Drafts UI wiring.md`
- `Vault/00 - Index.md`

## Next recommended work

Before starting Photos, verify this Drafts wiring on Windows/.NET 10 if possible. If Codrin wants to continue without VM verification, next feature is Photos.
