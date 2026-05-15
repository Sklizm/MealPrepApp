---
tags: [todo]
---

# TODO

Lista de verificare in desfasurare. Bifeaza pe masura ce sunt facute. Cele mai recente in partea de sus a fiecarei sectiuni.

## Acum
- [ ] Margarita: revizuieste macheta Canva conform specificatiei de design din Faza 4 (11 ecrane noi, 10 elemente de curatare, 4 componente transversale — vezi `Vault/Sessions/2026-05-11 - Faza 4 design si categorii de ingrediente-ro.md`)
- [ ] Reimprospateaza DataGrip si confirma ca cele 12 tabele + 38 proceduri + tipul `IntList` apar
- [ ] Conecteaza aplicatia .NET ca `mealprep_app` (nu `sa`) — connection string-ul merge in config-ul `App/` (WPF + MVVM + Dapper)

## Curand
- [ ] Optional: adauga view-uri de comoditate pentru explorare ad-hoc in DataGrip (doar citire, grant-uri de rol separate daca sunt expuse aplicatiei)
- [ ] Optional: adauga un rol de administrator pentru migrari distinct de `sa`
- [ ] Optional: `sp_GetAuditForUser` daca aplicatia vrea un feed "activitatea ta"

## Poate Mai Tarziu (in afara scopului v1, vezi [[Decisions Log-ro]])
- [ ] Tabele de planuri de masa / programare saptamanala
- [ ] Generarea listei de cumparaturi
- [ ] Urmarirea nutritiei (calorii, macronutrienti per ingredient)
- [ ] Rating-uri / favorite pentru retete
- [ ] Fotografii / atasamente de imagini
- [ ] Ingrediente private per utilizator (adauga UserID nullable la [[Ingredients-ro]])

## Facute
- [x] Seed-ul de ingrediente tradus in romana (fara diacritice, urmand conventia din IngredientCategories); JOIN-ul de backfill al categoriilor actualizat; `AppPassword` are default gol in `09_app_role.sql` astfel incat rebuild-urile nu mai au nevoie de `-v AppPassword=...`. Rebuild curat verificat — vezi [[Sessions/2026-05-15 - Ingredients Romanian + AppPassword default-ro]]
- [x] Faza 4 — Finalizare design + adaugari DB: blocate deciziile pentru auth/navigare/click/calendar/categorii/rapoarte/iesire/scurtatura-plan; adaugat tabel `IngredientCategories` + FK + seed cu 8 categorii + backfill al celor 44 de ingrediente; adaugate `sp_GetUserProfile` (citire sigura), `sp_GetIngredientCategories`, `sp_GetMonthlyStats`, `sp_GetTopRecipes`, `sp_GetTopIngredients`; extins `sp_GetIngredients` cu filtru opțional pe categorie
- [x] Faza 3 — Planificare mese + camara + lista de cumparaturi: `MealPlanEntries`, `RecipeFavorites`, `UserPantry`, 14 proceduri noi (plan/unplan/saptamanal/lunar, toggle favorite, upsert pantry via MERGE, lista de cumparaturi calculata cu scalare portii, contoare dashboard + retete recente)
- [x] Faza 2.5 — Slefuire DB: ingrediente populate (~44), goluri de index FK inchise, `RowVersion` pe Recipes pentru concurenta optimista (THROW 50004), `sp_FindRecipesByIngredients` rescrisa ca single GROUP BY + LEFT JOIN la TVP, nou `sp_GetIngredientUsage`
- [x] Faza 2 — Strat API aplicatie + securitate: 18 stored procedures, login `mealprep_app` cu privilegii reduse, `DENY` pe DML direct, audit log, istoric parole, blocare cont (5/15min)
- [x] `run_all.sql` end-to-end ruleaza curat si idempotent
- [x] Verificat ca login-ul aplicatiei poate EXEC proceduri dar nu poate SELECT/INSERT/UPDATE/DELETE direct pe tabele (ownership chaining acopera mutatiile)
- [x] Rulat `run_all.sql` impotriva containerului Docker prin `sqlcmd` — toate cele 6 tabele create, 12 unitati + 6 categorii populate
- [x] Verificat ca constrangerile CHECK se declanseaza (Quantity > 0, PrepTime/CookTime >= 0, Servings > 0, UnitType in setul permis)
- [x] Verificat ca constrangerile UNIQUE se declanseaza (Users.Username, RecipeIngredients(RecipeID, IngredientID))
- [x] Verificat `ON DELETE CASCADE` pe Recipes → RecipeIngredients functioneaza
- [x] Verificat RESTRICT pe stergere Ingredient (blocheaza daca este in uz)
- [x] Structura folderelor de proiect
- [x] `00_create_database.sql` — `MealPrepDB`
- [x] `01_users.sql` — [[Users-ro]]
- [x] `02_units.sql` — [[Units-ro]]
- [x] `03_categories.sql` — [[Categories-ro]]
- [x] `04_ingredients.sql` — [[Ingredients-ro]]
- [x] `05_recipes.sql` — [[Recipes-ro]]
- [x] `06_recipe_ingredients.sql` — [[RecipeIngredients-ro]]
- [x] `seeds/units_seed.sql`, `seeds/categories_seed.sql`
- [x] Script master `run_all.sql`
- [x] Vault Obsidian schelet
