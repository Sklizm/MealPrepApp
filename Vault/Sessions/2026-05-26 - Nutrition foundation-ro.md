---
tags: [session, nutrition, sql, wpf, verification, ro]
---

# 2026-05-26 — Fundatie nutritie

## Context
Codrin a reluat lucrul din TODO si a ales implementarea tracking-ului de nutritie.

## Implementat
- `dbo.UnitConversions` pentru conversii directe compatibile: g <-> kg si ml <-> l.
- `dbo.IngredientNutrition`, one-to-one pe `IngredientID`, cu cantitate/unitate baza si calorii/proteine/carbohidrati/grasimi.
- Proceduri: `sp_GetIngredientNutrition`, `sp_SetIngredientNutrition`, `sp_DeleteIngredientNutrition`, `sp_GetRecipeNutrition`.
- Scripturile de nutritie au fost incluse in `run_all.sql`.
- Modele app in `Models/Nutrition.cs` si `NutritionRepository` pentru pastrarea API-ului proc-only.
- `IngredientNutritionDialog` + ViewModel si actiunea `Nutritie` in lista de Ingrediente.
- Card `Nutritie estimata` in detaliu reteta, cu total/per portie si avertismente pentru lipsuri/conversii imposibile.
- Seed demo `seeds/ingredient_nutrition_seed.sql`, insert-only, pentru a pastra valorile corectate manual.

## Verificare locala
- `run_all.sql` a rulat in Docker cu exit 0.
- Smoke test SQL cu rollback: tabelele/procedurile exista, seed conversii, set/get nutritie ingredient si calcul nutritie reteta.
- `.hermes/tests/test_drafts_static.py` a trecut.
- XAML parse OK pentru dialog/lista/detaliu reteta.
- Seed-ul a inserat 44 randuri demo si pastreaza valorile modificate manual.
- Build local WPF ramane imposibil pe Fedora.

## Gap inchis
Codrin a verificat UI-ul de nutritie pe masina/VM-ul Ritei si a confirmat ca functioneaza. Observatie UX: introducerea manuala pentru toate valorile este obositoare, deci seed/import/preset ramane imbunatatire posibila.

## Next
Continuare cu conversia in `.exe`; optional mai tarziu CSV/import sau lookup extern.
