---
tags: [session, documentation, readme, vault, romanian]
---

# 2026-05-27 — README and Romanian vault counterparts

## Context
Codrin asked for the final documentation stretch: make the root git README more detailed about the database, app and features, and update the Obsidian vault so notes have Romanian counterparts where they were missing.

## README updates
- Expanded the app section with runtime flow, DI/navigation details and key implementation details.
- Expanded the database section with design rules: idempotent scripts, least-privilege app role, audit logging, FK indexes, conservative cascade/restrict rules, computed shopping lists and computed nutrition totals.
- Kept the README aligned with the current source shape: 16 tables, 50 stored procedures including internal `sp_WriteAudit`, Windows `.exe` publish instructions and the stored-procedure-only app boundary.

## Vault updates
- Added Romanian counterparts for the missing database notes:
  - `RecipeDrafts-ro.md`
  - `RecipePhotos-ro.md`
- Added new English and Romanian table notes for nutrition/conversions:
  - `UnitConversions.md` / `UnitConversions-ro.md`
  - `IngredientNutrition.md` / `IngredientNutrition-ro.md`
- Refreshed `Schema Overview.md` and `Schema Overview-ro.md` so they describe 16 tables, the nutrition procs, current build order and current error codes.
- Added Romanian counterparts for missing session notes from 2026-05-23 through 2026-05-27.
- Updated `00 - Index.md` and `00 - Index-ro.md` to link the new DB notes, all session counterparts and this handoff note.
- Synced `TODO-ro.md` with the current active/done state and added this documentation work to `TODO.md` / `TODO-ro.md` as Done.

## Verification
- Checked the vault inventory programmatically: every English markdown note in `Vault/` now has a matching `-ro.md` counterpart.
- Ran `git diff --check`; it passed.
- Reviewed current git status so the changed/new documentation files are visible for commit staging.

## Remaining non-doc verification
- The existing active TODO remains Windows `.exe` publish/runtime verification on Rita's Windows machine/VM.
