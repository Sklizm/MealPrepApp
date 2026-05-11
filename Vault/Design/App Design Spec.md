---
tags: [design, spec, app]
---

# App Design Spec — Planificator de Retete si Mese

Written 2026-05-11, after the design review session with Codrin. This is the spec Margarita pulls from for the next Canva mockup revision. Source decisions live in [[Decisions Log]]; this doc captures the design-side conclusions only.

The DB side already supports everything described here — see [[Database/Schema Overview]] for the full proc list per area.

## Locked design decisions

| Topic | Decision |
|---|---|
| UI framework | WPF + MVVM (CommunityToolkit.Mvvm) + Dapper |
| Auth in UI | Full: Login, Register, Profile, Change-password screens |
| Nav model | Top tabs: Acasa / Retete / Ingrediente / Planificare / Rapoarte. User dropdown top-right replaces "Iesire ->" link in the mockup. |
| Recipe-card click | Full-screen detail view (replaces list area; tab strip stays). Detail has Edit / Sterge / Adauga la favorite / Adauga la plan / Inapoi buttons. |
| Recipe editor | Same shape as detail but every field is a form input; ingredients are an editable table with add/remove rows. |
| Calendar add | Click empty cell → "Plan meal" modal with date+slot pre-filled. No drag-drop in v1. |
| Categorii sidebar (Ingrediente) | Real DB grouping — backed by `IngredientCategories` table (8 categories). |
| Rapoarte | Three sub-tabs: Statistici lunare / Plan saptamanal pentru tiparire / Lista cumparaturi pentru tiparire |
| "Iesire" button | Logout (returns to login screen). Window X closes the app. |
| Plan shortcut | Recipe detail has "Adauga la plan" button — opens the Plan meal modal pre-filled with that recipe |
| Selection model | No selection-then-action. Each card/row has its own per-row actions (right-click or hover-revealed icons). Editing = click card → detail → Edit. |
| Async | Every proc call is awaited; buttons disable for the duration; small progress indicator in status bar. |
| Save semantics | Editor commits on Salveaza, not field-blur. Renunta with unsaved changes shows confirmation. |
| Optimistic concurrency | Editor carries `RowVersion` from load; on error 50004 show "Reteta a fost modificata in alta sesiune. Reincarca." dialog. |
| Language | Romanian only in v1. Strings live in `Resources.ro.resx` so EN translation is a future resx swap. |
| Palette | Olive / cream / dark brown across all screens (no theme switcher). |

Already settled earlier: full planner scope (planner + pantry + shopping list); Categories table = meal slots (the planner reuses the same Categories list; UI shows 4 of 6 as weekly columns); hard delete (no archive/soft delete); no `PricePerServing`, no `ImagePath`, no dish-type categories separate from meal slots; shopping list is computed, never stored; no cross-unit conversion in v1.

## Screen inventory

20 distinct screens / dialogs.   ✅ = exists in current mockup,  🆕 = new (to draw).

### Auth (4 screens) — all 🆕

1. **Login** — Username/Email field, password field, "Conectare" button, "Cont nou? Inregistreaza-te" link, lockout/error message slot. Backed by `sp_GetUserForLogin` + `sp_RecordLoginSuccess` / `sp_RecordLoginFailure`.
2. **Register** — Username, Email, Password, Confirm password, "Inregistreaza-te" button, "Ai cont? Conecteaza-te" link, inline validation. Backed by `sp_RegisterUser`.
3. **Profile** — Username (readonly), Email (editable), CreatedAt, LastLoginAt. Buttons: "Schimba parola", "Iesi din cont". Accessed via a user avatar/menu in the top-right of the shell. Backed by `sp_GetUserProfile` (Phase 4 add — does not expose `PasswordHash`).
4. **Change password modal** — Current password, New password, Confirm new password. Surfaces error 50001 (password reused) as inline message. Backed by `sp_ChangePassword`.

### Shell

5. **App shell** ✅ partial — top tab strip + status bar. Tabs: Acasa / Retete / Ingrediente / Planificare / Rapoarte. Top-right: user avatar dropdown (Profil / Iesi din cont). Status-bar text changes per tab (already drawn correctly in the mockup).

### Acasa (1 screen) ✅

6. **Acasa dashboard** ✅ — KPI tile row + "Retete Recente" grid. Tiles are clickable: clicking "5 Retete active" navigates to Retete; clicking "7 Mese planificate" navigates to Planificare. Empty state: greeting + "Adauga prima reteta" CTA when all counts are 0. Backed by `sp_GetDashboardCounts` + `sp_GetRecentRecipes`.

### Retete (3 screens)

7. **Retete list** ✅ — exists. *Fix*: drop "Archiveaza/Sterge" toolbar item, leave only "Sterge". *Fix*: remove the `aaa` text bottom-left. Sidebar entries Toate / Favorite / Recente are backed by `sp_GetRecipes`, `sp_GetFavoriteRecipes`, `sp_GetRecentRecipes`. Search + category filter use `sp_SearchRecipesByTitle` / `sp_GetRecipes(@CategoryID)`.
8. **Retete detail** 🆕 — full-screen view (replaces list area; tab strip + sidebar stay visible). Layout: header (title, category badge, prep+cook time, servings, author), description, instructions (rendered text), ingredient list (name + qty + unit + notes). Action buttons in the header: ⭐ "Adauga la favorite" (toggle, reflects current state), "Adauga la plan" (opens Plan meal modal pre-filled), "Editeaza", "Sterge", "Inapoi". Backed by `sp_GetRecipeFull` (returns `RowVersion`).
9. **Retete editor** 🆕 — same layout shape as detail but every field is a form input. Ingredient list is an editable table (Add row / Remove row, ingredient autocomplete via `sp_SearchIngredients`, unit dropdown via `sp_GetUnits`, quantity, notes). Category dropdown via `sp_GetCategories`. Buttons: "Salveaza", "Renunta". On save: `sp_CreateRecipe` (new) or `sp_UpdateRecipe` (edit; passes `@RowVersion` from load; on 50004 show conflict dialog with reload option). Soft 500-char limit on the Notes textbox.

### Ingrediente (5 screens / sub-views)

10. **Ingrediente list** ✅ — exists. *Fix*: drop "Archiveaza" from toolbar. Sidebar items: **Toate** (full list, `sp_GetIngredients`), **Categorii** (grouped — see Categorii rendering pattern below), **Frigider** (link to pantry view #11), **Lista de cumparaturi** (link to shopping list #13). The grid cells should show name + default unit badge — not be empty placeholders.
11. **Frigider (pantry)** 🆕 — replaces the list area when "Frigider" is selected in the sidebar. Compact table: ingredient name, quantity, unit, last updated. Row actions: edit quantity, remove. Toolbar: "+ Adauga in frigider" (opens pantry-add modal #12). Backed by `sp_GetPantry`, `sp_AddPantryItem`, `sp_UpdatePantryQuantity`, `sp_RemovePantryItem`.
12. **Pantry add/edit modal** 🆕 — Ingredient autocomplete + Unit dropdown + Quantity. Add flow uses `sp_AddPantryItem` (MERGE-upsert; same ingredient+unit accumulates). Edit flow uses `sp_UpdatePantryQuantity` (absolute set). Same UI; differs only by initial state and proc called.
13. **Lista de cumparaturi (shopping list)** 🆕 — date range selector (default: this week). Table: ingredient, needed qty, on-hand qty, **to-buy qty**, unit. Toolbar: "Export Excel", "Tipareste" (Print). Backed by `sp_GetShoppingList(@UserID, @StartDate, @EndDate)`. Empty state: "Nu sunt mese planificate in intervalul selectat".
14. **Ingredient add modal** 🆕 — Name + default unit dropdown + category dropdown (`sp_GetIngredientCategories`). Small modal triggered by "+ Adauga" on Ingrediente list. Backed by `sp_AddIngredient`. No separate edit modal in v1.

### Planificare (3 screens / sub-views)

15. **Planificare — Calendar (monthly)** ✅ — exists. 7×6 grid. **Cell content budget**: date number + up to 4 small colored chips (one per occupied meal slot), each chip showing item count (or just a dot if 1). Click a chip → day-detail popover/modal showing that day's full plan. Click an empty cell → Plan meal modal (#17) with date pre-filled and slot defaulted to Breakfast. Backed by `sp_GetMonthlyPlan`.
16. **Planificare — Saptamanal (weekly)** ✅ — exists. Rows = dates, columns = Breakfast / Lunch / Dinner / Snack. *Fix*: real recipe-title bullets instead of `bla bla`. Each cell may have multiple items (one bullet per entry). Empty cell → click → Plan meal modal pre-filled. Backed by `sp_GetWeeklyPlan`.
17. **Plan meal modal** 🆕 — opens from any empty calendar cell OR from "Adauga la plan" on the recipe detail screen. Fields: Date (date picker, pre-filled), Meal slot (dropdown from `sp_GetCategories`, pre-filled with the clicked column or recipe's default category), Recipe (autocomplete via `sp_SearchRecipesByTitle`, pre-filled if coming from recipe detail), Servings (number, defaults to recipe's Servings), Notes (optional text). Buttons: "Salveaza", "Renunta". On save: `sp_PlanMeal` (new) or `sp_UpdatePlannedMeal` (edit). Clicking an existing plan entry opens this same modal in edit mode with an added "Sterge" button (→ `sp_UnplanMeal`).

### Rapoarte (1 tab with 3 sub-tabs) — 🆕

18. **Rapoarte** — top-level tab with three sub-tabs:
    - **Statistici lunare** — month picker; total meals planned, per-slot counts, top 5 recipes (`sp_GetTopRecipes`), top 10 ingredients (`sp_GetTopIngredients`), distinct recipes/ingredients used (`sp_GetMonthlyStats`).
    - **Plan saptamanal pentru tiparire** — week picker; weekly plan in a printer-friendly layout (no toolbars, monochrome-friendly). "Tipareste" + "Export Excel" buttons. Reads `sp_GetWeeklyPlan`.
    - **Lista cumparaturi pentru tiparire** — same shape; reads `sp_GetShoppingList`.

### Cross-cutting components (4 patterns) — all 🆕

19. **Confirmation dialog** — used for every destructive action: "Sterge reteta?" / "Sterge intrarea din plan?" / "Sterge ingredientul din frigider?". Single parameterized component (title + body + danger-button label).
20. **Error dialog / toast** — generic display for `SqlException`. Maps the 4 custom error codes to friendly messages:
    - **50001** → "Aceasta parola a fost folosita recent. Alege o parola noua."
    - **50002** → "Nu ai permisiunea pentru aceasta actiune."
    - **50003** → "Elementul nu a fost gasit."
    - **50004** → "Reteta a fost modificata in alta sesiune. Reincarca si reincearca."
    - Any other `SqlException` → "Eroare neasteptata" with collapsible "Detalii" section.
21. **Empty state** — one per list/dashboard screen:
    - Acasa with 0 recipes: "Bun venit! Incepe prin a adauga prima reteta." + CTA button
    - Retete with 0 recipes: same CTA inline
    - Frigider empty: "Frigiderul este gol. Adauga produsele pe care le ai."
    - Lista cumparaturi empty: "Nu sunt mese planificate in intervalul selectat."
    - Planificare calendar empty: just renders the empty grid (date numbers visible, no chips)
22. **Loading state** — shimmer or spinner per list while procs are running. Async-first ViewModels means a click won't freeze the UI.

## Margarita's punch list — fixes on the existing mockup

- **p1**: 3 tiles — make sure they all look interactive (uniform border on focus, not just the green-filled one). Optional: add a 4th "Favorite (N)" tile.
- **p1**: "Retete Recente" cards are empty boxes — sketch the actual card content (title, category badge, time).
- **p2**: drop "Archiveaza" — toolbar becomes "+ Adauga / Sterge / Export Excel" (no archive in v1).
- **p2 + p3**: remove the `aaa` text bottom-left (Canva placeholder).
- **p3**: "Categorii" sidebar entry now refers to a real DB grouping — design how grouped lists render (collapsible headers? indented list? separate panel?).
- **p3**: "Frigider" sub-view isn't drawn — reserve a mockup page (see screen #11 above).
- **p3**: "Lista de cumparaturi" sub-view isn't drawn — reserve a mockup page (see screen #13).
- **p4**: monthly cells currently show "26 bla bla bla" 42 times. Sketch one fully-drawn cell with the chips-and-count representation; the rest can stay as placeholder.
- **p5**: weekly cells show "bla bla" — replace with sample recipe titles to confirm typography fits.
- **p5**: cells should differentiate "planned" (recipe-title bullet) from "empty" (subtle "+" hover).
- **p6**: discard. The page-6 dashboard is the alternate nav model we rejected.

## New mockup pages to add

Eleven new screens, one Canva page each:

1. Login
2. Register
3. Profile + user dropdown placement
4. Change password modal
5. Recipe detail
6. Recipe editor
7. Frigider list view
8. Pantry add/edit modal
9. Shopping list screen
10. Plan meal modal (covers both calendar entry + recipe-detail shortcut)
11. Rapoarte tab (showing the three sub-tabs as a strip)

Plus a few small artifacts that get reused across screens:
- Confirmation dialog template
- Error toast / dialog template
- Empty-state component

## What the DB now supports (for app-side context)

Quick reference of procs the app can already call. Full details in [[Database/Schema Overview]].

| Area | Procs |
|---|---|
| Users / auth | `sp_RegisterUser`, `sp_GetUserForLogin` (login flow only), `sp_GetUserProfile` (Profile screen), `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword` |
| Recipes (write) | `sp_CreateRecipe`, `sp_UpdateRecipe` (requires `@RowVersion`), `sp_DeleteRecipe` |
| Recipes (read) | `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients` |
| Ingredients | `sp_AddIngredient`, `sp_GetIngredients(@IngredientCategoryID = NULL)`, `sp_SearchIngredients`, `sp_GetIngredientUsage` |
| Lookups | `sp_GetUnits`, `sp_GetCategories`, `sp_GetIngredientCategories` |
| Meal plan | `sp_PlanMeal`, `sp_UpdatePlannedMeal`, `sp_UnplanMeal`, `sp_GetWeeklyPlan`, `sp_GetMonthlyPlan` |
| Favorites | `sp_ToggleFavorite`, `sp_GetFavoriteRecipes` |
| Pantry | `sp_AddPantryItem` (MERGE upsert), `sp_UpdatePantryQuantity`, `sp_RemovePantryItem`, `sp_GetPantry` |
| Shopping list | `sp_GetShoppingList(@UserID, @StartDate, @EndDate)` — computed |
| Dashboard | `sp_GetDashboardCounts`, `sp_GetRecentRecipes` |
| Reports | `sp_GetMonthlyStats`, `sp_GetTopRecipes`, `sp_GetTopIngredients` |

## What this spec does NOT cover

- Visual specifics (typography sizes, exact pixel paddings, icon set) — Margarita owns these.
- WPF code structure (ViewModels, Repositories, DI container) — out of scope for the design doc; that's Codrin + Margarita's implementation territory.
- A roadmap past v1.

## Next

When Margarita ships the revised mockup, mark items off this list and move on to WPF wiring.
