---
tags: [session, app, planificare, rapoarte]
---

# 2026-05-22 — Recipe-save crash root cause; Planificare + Rapoarte shipped

Big session. The recipe-save crash was finally pinned down, and the last two app tabs
(Planificare, Rapoarte) are now **confirmed on Rita's VM and merged to `main`**.

## Recipe-save crash — root cause confirmed

The "eroare neasteptata de la baza de date" Rita hit on the 18-ingredient pizza was a **duplicate
ingredient** after all: **"Ulei" was entered twice**. That trips `UQ_RecipeIngredients_Recipe_Ingr`
→ SQL error **2627**. We had both eyeballed the list and missed it. Two defences (both merged):

- **Editor duplicate guard** (`ReteteEditorViewModel.Save`): blocks two rows with the same
  ingredient *before* the DB round-trip, with a clear message.
- **Native error mapping** (`DbExceptionMapper` + `AppDbException`): 2627/2601/547/515/2628/8152 now
  map to friendly Romanian; any *un*mapped code gets `(cod N)` appended so the next surprise is
  diagnosable instead of opaque.

Lesson recorded: keep the leading hypothesis *and* instrument for proof — don't drop a theory just
because a manual double-check "ruled it out".

## Phase G — Planificare (verified + merged)

Was already built last session on `phase-g-planificare` but never verified/merged. This session:
rebased it cleanly onto current `main` (the recipe-fix files didn't actually conflict — Phase G
never touched them), Rita verified on the VM, merged. Contents: Planificare root (Lunar/Saptamanal
toggle), monthly 6×7 calendar + weekly 7×4 grid, the Plan-meal dialog (add/edit/delete, recipe
autocomplete), and "Adauga la plan" on recipe detail. All on the existing `MealPlanEntries` procs.

## Phase H — Rapoarte (built this session + merged)

New `RapoarteRootViewModel` + view, 3-sub-tab toggle mirroring Planificare/Ingrediente:

- **Statistici lunare** — month picker → KPI tiles (total mese, rețete/ingrediente distincte),
  per-slot breakdown, top 5 rețete + top 10 ingrediente. `ReportRepository` / `sp_GetMonthlyStats`,
  `sp_GetTopRecipes`, `sp_GetTopIngredients`. Empty state for months with no plan.
- **Plan saptamanal pentru tiparire** — week picker (‹ ›) → print-friendly 7×4 grid; Tipareste
  (FlowDocument) + Export Excel. `sp_GetWeeklyPlan`.
- **Lista cumparaturi pentru tiparire** — date range → computed to-buy list; Tipareste + Export
  Excel. `sp_GetShoppingList`.

The whole report data layer (repos, models, procs) already existed — this was pure UI + wiring.
Reused the FlowDocument print + ClosedXML export patterns from the Ingrediente shopping list, and
the root-container pattern from Planificare/Ingrediente. **Rapoarte scope deliberately follows the
design spec's 3 sub-tabs**; the WinForms mockup's "calorii / pret mediu" cards were dropped (no data
behind them). See Decisions Log.

## Git

- `main` fast-forwarded through Phase G then Phase H (one linear history; `phase-h` was stacked on
  `phase-g`). Both branches pushed; `main` pushed.
- **GitHub repo moved**: `Sklizm/MealPrepDB` → `Sklizm/MealPrepApp`. Pushes still work via redirect;
  the `origin` URL should be updated when convenient.

## Still pending

- **`fix-recipe-field-limits`** branch (Title `MaxLength=150`, ingredient Notes `255` to match the DB
  columns and avoid truncation 8152) is pushed but **not merged** — verify on the VM, then merge.

## What's next

The "Soon" list: loading screen, .exe packaging, Drafts, add-ingredient-at-recipe-time,
forgot-password, recipe photos.
