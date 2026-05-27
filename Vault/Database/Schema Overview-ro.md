---
tags: [database, schema, ro]
---

# Privire de ansamblu a schemei

Saisprezece tabele in `MealPrepDB` (sase de baza + doua securitate/audit + trei planificare/camara + un lookup pentru categorii de ingrediente + drafturi/poze + doua tabele de nutritie). Ordinea de build urmeaza dependentele:

```text
[[Users-ro]]                  [[Units-ro]]            [[Categories-ro]]
   │                          │                      │
   │                          ▼                      │
   │                     [[Ingredients-ro]]          │
   │                       │      │                  │
   ▼                       │      ▼                  ▼
[[Recipes-ro]] ─────────► [[RecipeIngredients-ro]] ◄──┘
   │     │                     │
   │     ▼                     ▼
   │  [[RecipePhotos-ro]] [[IngredientNutrition-ro]]
   ▼
[[RecipeDrafts-ro]]
```

## Tabele
- [[Users-ro]] — conturi (Username, Email, PasswordHash) + stare de securitate
- [[Units-ro]] — unitati de masura (g, kg, ml, cup, …) cu tip weight/volume/count
- [[Categories-ro]] — categorii de retete, refolosite si ca sloturi pentru planificare
- [[Ingredients-ro]] — lista globala de ingrediente, cu unitate implicita si categorie optionala; seed romanesc
- [[Recipes-ro]] — retete detinute de utilizator, cu `RowVersion` pentru concurenta optimista
- [[RecipeIngredients-ro]] — jonctiune reteta ↔ ingredient, cu cantitate si unitate
- **PasswordHistory** — parole recente per utilizator, ultimele 5 pastrate
- **AuditLog** — jurnal append-only pentru actiuni de modificare
- [[MealPlanEntries-ro]] — retete programate pe data + slot de masa
- [[RecipeFavorites-ro]] — retetele favorite ale utilizatorilor
- [[UserPantry-ro]] — stocul/frigiderul utilizatorului, per ingredient+unitate
- [[IngredientCategories-ro]] — lookup pentru gruparea ingredientelor
- [[RecipeDrafts-ro]] — drafturi de retete incomplete, cu JSON opac pentru ingrediente
- [[RecipePhotos-ro]] — o poza optionala stocata in DB per reteta
- [[UnitConversions-ro]] — conversii directe compatibile pentru nutritie
- [[IngredientNutrition-ro]] — valori nutritionale sursa per ingredient

## Suprafata API stored-procedure
Aplicatia .NET nu citeste tabele direct. Se conecteaza ca `mealprep_app` si apeleaza procedurile din `Database/procs/`:

| Zona | Proceduri |
|---|---|
| Utilizatori / auth | `sp_RegisterUser`, `sp_GetUserForLogin`, `sp_GetUserProfile`, `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword`, `sp_ResetForgottenPassword` |
| Retete | `sp_CreateRecipe`, `sp_UpdateRecipe`, `sp_DeleteRecipe`, `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients` |
| Ingrediente / lookup | `sp_AddIngredient`, `sp_GetIngredients`, `sp_SearchIngredients`, `sp_GetIngredientUsage`, `sp_GetUnits`, `sp_GetCategories`, `sp_GetIngredientCategories` |
| Planificare | `sp_PlanMeal`, `sp_UpdatePlannedMeal`, `sp_UnplanMeal`, `sp_GetWeeklyPlan`, `sp_GetMonthlyPlan` |
| Favorite / frigider / cumparaturi | `sp_ToggleFavorite`, `sp_GetFavoriteRecipes`, `sp_AddPantryItem`, `sp_UpdatePantryQuantity`, `sp_RemovePantryItem`, `sp_GetPantry`, `sp_GetShoppingList` |
| Dashboard / rapoarte | `sp_GetDashboardCounts`, `sp_GetRecentRecipes`, `sp_GetMonthlyStats`, `sp_GetTopRecipes`, `sp_GetTopIngredients` |
| Drafturi / poze | `sp_SaveDraft`, `sp_GetDrafts`, `sp_GetDraft`, `sp_DeleteDraft`, `sp_SetRecipePhoto`, `sp_GetRecipePhoto`, `sp_DeleteRecipePhoto` |
| Nutritie | `sp_GetIngredientNutrition`, `sp_SetIngredientNutrition`, `sp_DeleteIngredientNutrition`, `sp_GetRecipeNutrition` |
| Intern | `sp_WriteAudit` |

Tipul TVP `dbo.IntList` este folosit de `sp_FindRecipesByIngredients` pentru liste de ID-uri.

## Granita de securitate
- `mealprep_app` este principalul aplicatiei.
- `mealprep_app_role` are `GRANT EXECUTE` si are `DENY SELECT, INSERT, UPDATE, DELETE` pe schema `dbo`.
- Mutatiile functioneaza doar prin proceduri, via SQL Server ownership chaining.
- `sa` ramane doar pentru migrari; aplicatia nu il foloseste.

## Comportament cascada
- Stergerea unei [[Recipes-ro|retete]] sterge randurile copil unde copilul nu are sens fara reteta: ingrediente de reteta, poza, planificari/favorite dupa regulile FK.
- Stergerea unui [[Users-ro|utilizator]] este blocata daca detine retete, dar randurile precum `PasswordHistory`, drafturi, favorite si frigider pot cascada unde este proiectat asa.
- Stergerea unui [[Ingredients-ro|ingredient]] este blocata daca retete, frigider sau nutritie depind de el.
- Stergerea unei [[Units-ro|unitati]] este blocata daca exista dependente in ingrediente de reteta, frigider, conversii sau nutritie.

Vezi [[Decisions Log-ro]] pentru motivatia cascade/restrict.

## Ordinea de build
Ruleaza `Database/run_all.sql` end-to-end (idempotent). Ordinea manuala:

1. `00_create_database.sql`
2. `01_users.sql` … `06_recipe_ingredients.sql`
3. `seeds/units_seed.sql`, `seeds/categories_seed.sql`
4. `07_users_security.sql`, `08_audit_log.sql`, `10_phase25_additions.sql`
5. `11_meal_plan.sql`, `12_favorites.sql`, `13_pantry.sql`
6. `14_ingredient_categories.sql`, `seeds/ingredient_categories_seed.sql`, `seeds/ingredients_seed.sql`
7. `15_recipe_drafts.sql`, `16_recipe_photos.sql`
8. `17_unit_conversions.sql`, `18_ingredient_nutrition.sql`, `seeds/ingredient_nutrition_seed.sql`
9. `procs/01_users.sql` … `procs/14_nutrition.sql`
10. `09_app_role.sql` — login/rol/grant-uri la final. Rebuild-urile de obicei nu cer parola app daca login-ul exista deja; prima configurare urmeaza comentariile din script.

## Coduri de eroare
- `50001` — parola reutilizata
- `50002` — neautorizat
- `50003` — negasit
- `50004` — rand invechit / conflict de concurenta optimista
- `50005` — cont negasit in resetarea parolei
