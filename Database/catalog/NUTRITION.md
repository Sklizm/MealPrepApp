# Ingredient nutrition references

`ingredient_nutrition.csv` reviews all 233 catalog names: 225 complete calorie,
protein, carbohydrate and fat profiles, and eight explicitly unresolved entries.
Available fibre, saturated fat, sugar, sodium, salt, cholesterol, vitamins and minerals
are also retained, with units in column names. Blank means unknown, not zero.
The existing app/database supports four macro fields; additional nutrients remain
in this reference CSV and are not presented as tracked nutrients in the app.

## Sources and interpretation

USDA composition data comes through the public [rstudio EDAWR extract](https://raw.githubusercontent.com/rstudio/EDAWR/master/data-raw/nutrition.csv).
Each match preserves its exact food description. The [extract script](https://github.com/rstudio/EDAWR/blob/master/data-raw/nutrition.R)
calculates calories using food-specific Atwater factors. Missing factors produce
missing energy; this catalog explicitly identifies general 4/4/9 estimates used to
fill those gaps. `energy_method` distinguishes those estimates from source-specific
calculations and retailer label values. These are historical references, not claimed
to be the latest USDA analyses.

Product references link specific labels published by Linella, METRO, BONUS and other
retailers, plus a manufacturer reference for marinated pork neck. A representative
product does not establish exact values for every brand of a generic ingredient.
Availability evidence stays in the separate Moldova catalog; a foreign nutrition
reference does not add a food to the availability list.

Values are per 100 g edible portion unless explicitly marked ml. Dry cereals and
legumes use dry references; raw/cooked and fresh/preserved states are distinguished.
Bones, shells, peel and ice glaze are excluded where appropriate. The starter chicken
breast reference is cooked roasted meat, matching its existing 165 kcal/100 g default;
do not apply it to raw meat weight.

Several variants use representative references: grape colours, plain rye flakes and
dry rice cultivars, for example. Row notes identify these approximations. USDA
carbohydrate-by-difference includes fibre; European labels often report available
carbohydrate separately. Source conventions are retained. Vitamin A/D source values
use IU and are not converted to micrograms. Salt derived from sodium uses sodium mg
× 0.0025 (g). Unknown sugar values remain blank.

Juice and coconut milk profiles are per 100 g and require weighed recipe quantities.
No universal g/ml density conversion is assumed; the app continues to flag incompatible
recipe units. Unsweetened water has zero macros; unverified foods are not zero-filled.

## Remaining gaps

| Ingredient | Reason |
|---|---|
| Fructul dragonului | Complete matching composition record still required. |
| Oase de porc | Bones are inedible stock ingredients; use a measured broth recipe. |
| Mici | Located local listings omit carbohydrate; complete product label needed. |
| Tolstolob | Matching wet-weight silver-carp composition record needed. |
| Rosii marinate | Recipe/brine-dependent; complete matching label needed. |
| Ardei in sos de tomate | Recipe-dependent; complete matching label needed. |
| Leustean uscat | Located energy conflicts with macros; fresh/dried ambiguity. |
| Piper verde macinat | Located manufacturer page lacks a clear energy/serving basis. |

These incomplete/conflicting sources are not imported. The silver-carp paper found
during review reports dry-basis composition, which must not be copied into a 100 g
raw edible-fish row. No zero placeholder is used for these gaps.

## Import and preservation

```bash
python3 scripts/generate-ingredient-nutrition.py --check
python3 Tests/Scripts/test_ingredient_nutrition.py
python3 Tests/Scripts/test_database_update.py
bash scripts/test-database.sh &&
bash scripts/update-database.sh mealprep-db 20261111_ingredient_nutrition.sql
```

The combined migration inserts missing ingredients and nutrition using the verified-
backup-first updater. Existing IDs, units, categories, nutrition and nutrition timestamps
are preserved, including the 44 starter defaults and custom edits. The CSV does not
imply that existing/custom rows have been replaced by its references. Each inserted
nutrition row receives a UTC audit event with its source. Repeat imports create no
extra rows or audits. Extra reference nutrients are not attached to existing macro
rows, whose food, product or basis may differ.

Edit the CSV and run `python3 scripts/generate-ingredient-nutrition.py` to regenerate
SQL. Validation checks inventory coverage, finite/nonnegative values, macro mass,
saturated fat versus total fat, energy methods, provenance and unknown handling.
Tests check dry/raw/canned states and unit bases. Disposable SQL regressions check
repeat imports, custom-value preservation, insertion auditing and unknown handling;
executing these requires Docker access.
