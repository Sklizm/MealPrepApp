---
tags: [database, table]
---

# Ingredients

Fisier: `Database/04_ingredients.sql`

## Coloane
| Coloana         | Tip             | Note |
|-----------------|-----------------|------|
| IngredientID    | INT IDENTITY    | PK |
| Name            | NVARCHAR(100)   | UNIQUE, NOT NULL |
| DefaultUnitID   | INT             | FK → [[Units-ro]], nullable |

## De ce global (fara UserID)
Pentru v1, ingredientele sunt partajate intre toti utilizatorii. "Sare" este "Sare" — nu este nevoie ca fiecare utilizator
sa o aiba pe a lui. Daca avem vreodata nevoie de ingrediente private per utilizator, adauga o coloana `UserID` nullable
mai tarziu (NULL = global).

## IngredientCategoryID (adaugare Faza 4)
FK nullable la [[IngredientCategories-ro]]. Alimenteaza sidebar-ul "Categorii" din tab-ul Ingrediente al aplicatiei. NULL este permis (ingredientele adaugate de utilizator pot ramane necategorisite). Ingredientele livrate populate sunt toate atribuite unei categorii via UPDATE-ul din fisierul de seed.

## DefaultUnitID
Indiciu optional — cand acest ingredient este adaugat la o reteta, pre-completeaza aceasta unitate. Unitatea reala este
stocata per reteta in [[RecipeIngredients-ro]] astfel incat un utilizator poate sa o suprascrie.

## Indecsi
- `IX_Ingredients_DefaultUnitID` — index de coloana FK (adaugat in Faza 2.5).

## Seed
`Database/seeds/ingredients_seed.sql` livreaza 44 de elemente comune (camara / uleiuri / lactate / produse / proteine / ierburi). `MERGE` pe `Name`, cu `DefaultUnitID` rezolvat via `LEFT JOIN dbo.Units ON Abbreviation = ...`.

Numele sunt in romana fara diacritice (`Faina`, `Branza`, `Smantana`, `Piept de pui` etc.) pentru a se potrivi cu conventia folosita in [[IngredientCategories-ro]]. O versiune in engleza a aplicatiei ar trebui sa adauge un strat de localizare in loc de un seed paralel in engleza — vezi intrarea din 2026-05-15 in [[Decisions Log-ro]].

## Proceduri
- `sp_AddIngredient(@Name, @DefaultUnitID = NULL, @IngredientCategoryID = NULL)`, `sp_GetIngredients`, `sp_SearchIngredients` — suprafata de baza tip CRUD. Parametrul de categorie pe `sp_AddIngredient` a fost adaugat 2026-05-18 (follow-up Faza F); vezi [[Decisions Log-ro]].
- `sp_GetIngredientUsage` (Faza 2.5) — `RecipeCount` pentru un ingredient. Aplicatia o apeleaza inainte de o incercare de stergere; daca > 0, FK-ul ar bloca oricum stergerea, deci aplicatia afiseaza un mesaj util in schimb.

## Folosit de
- [[RecipeIngredients-ro]] (IngredientID, obligatoriu)

Vezi [[Schema Overview-ro]], [[Decisions Log-ro]]
