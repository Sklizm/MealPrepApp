---
tags: [session, todo, database, verification]
date: 2026-05-25
---

# 2026-05-25 — TODO cleanup and draft/photo DB verification

## Purpose
Codrin asked Hermes to follow the recommended work order and complete the first two items before moving on:

1. Clean stale TODO entries.
2. Verify the draft/photo database scripts build through `Database/run_all.sql`.

## TODO cleanup performed
Updated `Vault/TODO.md`:

- Removed stale active item for merging `fix-recipe-field-limits`; git history confirms merge `25276e3 Merge branch 'fix-recipe-field-limits'`.
- Removed stale active item for updating `origin`; `origin` now points to `https://github.com/Sklizm/MealPrepApp.git`.
- Moved meal planning / weekly schedule to Done because Phase G Planificare already shipped.
- Moved shopping list generation to Done because Phase F/H shopping list flows already exist.
- Removed duplicated Maybe Later photo reminder, keeping the concrete Soon item: `Ability to add photos to recipes`.
- Left active future work in Soon: Drafts, photos, loading screen, `.exe` packaging, add-ingredient-from-recipe-editor, and forgot/change-password flow.

## Database verification
Ran the established safe SQL Server build sequence:

```bash
docker exec -u 0 MealPrepDB rm -rf /tmp/Database
docker cp Database MealPrepDB:/tmp/Database
docker exec -w /tmp/Database MealPrepDB /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$(docker exec MealPrepDB printenv SA_PASSWORD)" \
  -C -b -i run_all.sql
```

Result: build exited with code 0.

Confirmed `Database/run_all.sql` includes the new draft/photo scripts in the right broad order:

- table scripts:
  - `15_recipe_drafts.sql`
  - `16_recipe_photos.sql`
- proc scripts:
  - `procs/12_recipe_drafts.sql`
  - `procs/13_recipe_photos.sql`

## Objects verified
Verified these tables exist in `MealPrepDB`:

- `RecipeDrafts`
- `RecipePhotos`

Verified these stored procedures exist:

- `sp_DeleteDraft`
- `sp_DeleteRecipePhoto`
- `sp_GetDraft`
- `sp_GetDrafts`
- `sp_GetRecipePhoto`
- `sp_SaveDraft`
- `sp_SetRecipePhoto`

Verified the app security model remains proc-only:

- `mealprep_app` is a member of `mealprep_app_role`.
- `mealprep_app_role` has `EXECUTE` grant.
- `mealprep_app_role` has direct `SELECT`, `INSERT`, `UPDATE`, and `DELETE` denied on schema `dbo`.

## Files changed

- `Vault/TODO.md`
- `Vault/Sessions/2026-05-25 - TODO cleanup and draft-photo DB verification.md`
- `Vault/00 - Index.md` updated to link this session note

No DB script changes were needed during this verification pass.

## Next recommended work

Proceed to implementation work:

1. Wire Drafts into the WPF recipe editor and recipe area.
2. Wire Photos into recipe detail/editor flow.
3. Then move to smaller polish items: add-ingredient-from-editor, loading screen, `.exe` packaging.
