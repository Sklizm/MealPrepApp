---
tags: [database, schema]
---

# Privire de ansamblu a schemei

Douasprezece tabele in `MealPrepDB` (sase de baza + doua pentru securitate/audit + trei pentru Faza 3 de planificare a meselor + unul pentru Faza 4 de categorizare a ingredientelor). Ordinea de build este ordinea de dependenta:

```
[[Users-ro]]                  [[Units-ro]]            [[Categories-ro]]
   │                          │                      │
   │                          ▼                      │
   │                     [[Ingredients-ro]]             │
   │                          │                      │
   ▼                          ▼                      ▼
[[Recipes-ro]] ─────────► [[RecipeIngredients-ro]] ◄───────┘
```

## Tabele
- [[Users-ro]] — conturi (Username, Email, PasswordHash) + stare de securitate (LastLoginAt, FailedLoginCount, LockedUntil)
- [[Units-ro]] — unitati de masura (g, kg, ml, cup, …) cu tip (weight/volume/count)
- [[Categories-ro]] — categorii de retete (Breakfast, Lunch, …)
- [[Ingredients-ro]] — lista globala de ingrediente cu unitate implicita optionala; populata cu ~44 de elemente comune
- [[Recipes-ro]] — detinute de un utilizator, optional categorizate; poarta un `RowVersion` pentru concurenta optimista
- [[RecipeIngredients-ro]] — jonctiune: reteta ↔ ingredient cu cantitate + unitate
- **PasswordHistory** — hash-uri recente de parole per utilizator (ultimele 5 retinute); cascada de la Users
- **AuditLog** — log append-only de actiuni care schimba starea; scris de fiecare procedura de mutatie
- [[MealPlanEntries-ro]] — retete programate la o data + slot de masa (Category) per utilizator
- [[RecipeFavorites-ro]] — tabel de jonctiune cu PK compozit pentru "utilizatorul a marcat aceasta reteta ca favorita"
- [[UserPantry-ro]] — stoc curent per utilizator, per ingredient+unitate (fara conversie intre unitati in v1)
- [[IngredientCategories-ro]] — lookup din Faza 4 care alimenteaza sidebar-ul Ingrediente; FK nullable din [[Ingredients-ro]]

## Suprafata API (Faza 2)
Aplicatia .NET nu face query direct pe tabele. Se conecteaza ca login SQL `mealprep_app` cu privilegii reduse si apeleaza stored procedures din `Database/procs/`:

| Zona | Proceduri |
|---|---|
| Utilizatori / auth | `sp_RegisterUser`, `sp_GetUserForLogin`, `sp_GetUserProfile`, `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword` |
| Retete (scriere) | `sp_CreateRecipe`, `sp_UpdateRecipe`, `sp_DeleteRecipe` |
| Retete (citire) | `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients` |
| Ingrediente | `sp_AddIngredient`, `sp_GetIngredients`, `sp_SearchIngredients`, `sp_GetIngredientUsage` |
| Lookup-uri | `sp_GetUnits`, `sp_GetCategories`, `sp_GetIngredientCategories` |
| Plan de masa | `sp_PlanMeal`, `sp_UpdatePlannedMeal`, `sp_UnplanMeal`, `sp_GetWeeklyPlan`, `sp_GetMonthlyPlan` |
| Favorite | `sp_ToggleFavorite`, `sp_GetFavoriteRecipes` |
| Camara | `sp_AddPantryItem`, `sp_UpdatePantryQuantity`, `sp_RemovePantryItem`, `sp_GetPantry` |
| Lista de cumparaturi | `sp_GetShoppingList` (calculata, face join intre mesele planificate minus camara) |
| Dashboard | `sp_GetDashboardCounts`, `sp_GetRecentRecipes` |
| Rapoarte | `sp_GetMonthlyStats`, `sp_GetTopRecipes`, `sp_GetTopIngredients` |
| Intern | `sp_WriteAudit` (apelata din procedurile de mutatie) |

Tipul TVP `dbo.IntList` este folosit de `sp_FindRecipesByIngredients` pentru a accepta o lista de ID-uri de ingrediente.

## Granita de securitate
- Login-ul SQL `mealprep_app` este principalul aplicatiei.
- Membru al `mealprep_app_role`:
  - `GRANT EXECUTE ON SCHEMA::dbo` (si `EXECUTE ON TYPE::dbo.IntList`)
  - `DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo`
- Mutatiile reusesc doar prin stored procedures via **SQL Server ownership chaining** (procedurile si tabelele sunt detinute de `dbo`).
- `sa` este rezervat doar pentru migrari; aplicatia nu il foloseste niciodata.

## Comportament cascada
- Stergerea unei [[Recipes-ro|Retete]] → randurile sale [[RecipeIngredients-ro]] sunt eliminate (`ON DELETE CASCADE`).
- Stergerea unui [[Users-ro|Utilizator]] → blocata daca detine retete (fara cascada prin design); dar randurile `PasswordHistory` CHIAR fac cascada (copilul nu are sens fara parinte).
- Stergerea unui [[Ingredients-ro|Ingredient]] → blocata daca vreo reteta il foloseste.
- Stergerea unei [[Units-ro|Unitati]] → blocata daca vreun ingredient de reteta o foloseste.

Vezi [[Decisions Log-ro]] pentru motivul pentru care cascadele sunt intentionat minime.

## Ordinea de build
Ruleaza `Database/run_all.sql` end-to-end (idempotent) — scriptul master `:r`-include totul in ordinea corecta. Ordine manuala daca este nevoie:

**Faza 1 (schema + seed-uri)**
1. `00_create_database.sql`
2. `01_users.sql` … `06_recipe_ingredients.sql`
3. `seeds/units_seed.sql`, `seeds/categories_seed.sql`

**Faza 2 (stare de securitate, audit, API, login)**
4. `07_users_security.sql` — augmenteaza `Users`, creeaza `PasswordHistory`
5. `08_audit_log.sql` — tabel `AuditLog` + TVP `IntList` + `sp_WriteAudit`
6. `10_phase25_additions.sql` — Faza 2.5: goluri de index FK + `RowVersion` pe Recipes (trebuie sa ruleze inainte de procedurile care refera `RowVersion`)

**Faza 3 (planificare mese, favorite, camara)**
7. `11_meal_plan.sql` — `MealPlanEntries`
8. `12_favorites.sql` — `RecipeFavorites`
9. `13_pantry.sql` — `UserPantry`

**Faza 4 (categorizare ingrediente + rapoarte)**
10. `14_ingredient_categories.sql` — tabel `IngredientCategories` + coloana FK nullable pe `Ingredients`
11. `seeds/ingredient_categories_seed.sql` — 8 categorii + backfill al Ingredients livrate

**API stored procedures (Faza 2 + 3 + 4)**
12. `procs/01_users.sql` … `procs/11_reports.sql` — API complet. `sp_UpdateRecipe` cere `@RowVersion`; `sp_FindRecipesByIngredients` foloseste GROUP BY + LEFT JOIN la TVP; `sp_AddPantryItem` este un upsert MERGE; `sp_GetShoppingList` este calculata (fara tabel); `sp_GetUserProfile` este citirea sigura a profilului (fara PasswordHash expus); `sp_GetIngredients` accepta `@IngredientCategoryID` optional.

**Login aplicatie + rol (rulat ultimul)**
13. `09_app_role.sql` — login + rol + grant-uri. Necesita `-v AppPassword="..."`.

**Coduri de eroare ridicate de proceduri**
- `50001` — parola reutilizata
- `50002` — neautorizat
- `50003` — negasit
- `50004` — rand invechit (conflict de concurenta optimista)
