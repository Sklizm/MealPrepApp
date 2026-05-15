---
tags: [database, table]
---

# IngredientCategories

Fisier: `Database/14_ingredient_categories.sql`

Tabel de lookup pentru gruparea [[Ingredients-ro]] in sidebar-ul Ingrediente al aplicatiei (Produse / Lactate / Carne / Conserve / Condimente / Cereale / Bauturi / Altele).

## Coloane
| Coloana               | Tip             | Note |
|-----------------------|-----------------|------|
| IngredientCategoryID  | INT IDENTITY    | PK |
| Name                  | NVARCHAR(50)    | UNIQUE, NOT NULL |

8 randuri populate in `seeds/ingredient_categories_seed.sql`.

## Inrudit: `Ingredients.IngredientCategoryID` (FK nullable)
Adaugat ca o coloana nullable pe [[Ingredients-ro]] astfel incat randurile existente sa supravietuiasca migrarii. Fisierul de seed apoi face UPDATE pe cele 44 de ingrediente livrate cu atribuiri rezonabile de categorie. Ingredientele noi adaugate de utilizator poarta NULL pana cand este aleasa o categorie.

Cascada: RESTRICT (consistent cu regula tabelelor de lookup — Categories/Units nu fac niciodata cascada).

## De ce exista asta
Sidebar-ul Ingrediente din designul aplicatiei are o intrare "Categorii". Fara acest tabel, acea intrare ar fi o grupare UI fara date reale. Acum este sustinuta de un FK propriu, sortabila / filtrabila / extensibila. Nu inlocuieste decizia originala "ingredientele sunt globale" — o augmenteaza.

## Proceduri
- `sp_GetIngredientCategories` — listeaza cele 8 (sau cate sunt) categorii.
- `sp_GetIngredients(@IngredientCategoryID = NULL)` — filtru optional; NULL = toate.

## Folosit de
- Sub-vizualizarea "Categorii" a tab-ului Ingrediente din aplicatie.

Vezi [[Schema Overview-ro]], [[Decisions Log-ro]]
