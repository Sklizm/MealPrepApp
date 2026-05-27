---
tags: [database, table, nutrition, ro]
---

# IngredientNutrition (Romana)

Fisier: `Database/18_ingredient_nutrition.sql`

## Scop
Stocheaza valorile nutritionale la nivel de ingredient, astfel incat nutritia unei retete sa fie calculata la cerere.

Nutritia nu este stocata ca total calculat pe `Recipes`, deoarece acel total s-ar invechi cand se schimba ingredientele, cantitatile sau valorile nutritionale.

## Coloane
| Coloana | Tip | Note |
|---|---|---|
| IngredientID | INT | PK si FK -> [[Ingredients-ro]]; un rand de nutritie per ingredient |
| BasisQuantity | DECIMAL(10,2) | cantitatea la care se refera valorile, de obicei 100 |
| BasisUnitID | INT | FK -> [[Units-ro]]; unitatea-baza, de exemplu g, ml sau pc |
| Calories | DECIMAL(10,2) | kcal pentru cantitatea-baza |
| ProteinGrams | DECIMAL(10,2) | proteine pentru cantitatea-baza |
| CarbsGrams | DECIMAL(10,2) | carbohidrati pentru cantitatea-baza |
| FatGrams | DECIMAL(10,2) | grasimi pentru cantitatea-baza |
| UpdatedAt | DATETIME2(0) | default UTC; actualizat la modificari |

## Stored procedures
- `sp_GetIngredientNutrition` — citeste nutritia pentru un ingredient.
- `sp_SetIngredientNutrition` — insereaza sau actualizeaza nutritia pentru un ingredient.
- `sp_DeleteIngredientNutrition` — sterge nutritia pentru un ingredient.
- `sp_GetRecipeNutrition` — calculeaza totalul si valorile per portie pentru calorii, proteine, carbohidrati si grasimi.

## Date seed
`seeds/ingredient_nutrition_seed.sql` insereaza valori nutritionale demo pentru ingredientele comune seeduite. Scriptul insereaza doar randurile lipsa, deci valorile corectate manual in aplicatie sunt pastrate la rebuild.

## Note de design
- Nutritia retetelor este calculata prin stored procedures, nu prin citiri directe din tabele.
- `UnitConversions` ofera doar conversii directe compatibile; randurile lipsa sau incompatibile sunt numarate si afisate ca incomplete.
- Rapoartele viitoare zilnice/saptamanale de nutritie ar trebui sa porneasca de la acest model cu sursa la nivel de ingredient.

Vezi [[UnitConversions-ro]], [[Ingredients-ro]], [[Recipes-ro]], [[Schema Overview-ro]], [[Decisions Log-ro]]
