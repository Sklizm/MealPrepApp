---
tags: [session, handoff, hermes, ro]
date: 2026-05-23
---

# 2026-05-23 — Preluare Hermes / context proiect absorbit

## Scop
Codrin a cerut ca Hermes sa preia de la Claude rolul de partener AI pentru proiectul de practica MealPrepApp. Nota pastreaza contextul absorbit din vault si din repo pentru un punct clar de reluare.

## Intelegere curenta
MealPrepApp este un proiect complet WPF + SQL Server, nu doar un exercitiu de baza de date. Repo-ul contine:
- `App/MealPrepApp/` — client desktop WPF in romana, cu MVVM, CommunityToolkit.Mvvm, Dapper, Microsoft.Data.SqlClient, DI, BCrypt si ClosedXML.
- `Database/` — schema SQL Server, seed-uri, API stored-procedure-only, login/rol pentru aplicatie si build idempotent prin `run_all.sql`.
- `Vault/` — sursa de adevar Obsidian pentru istoric, decizii, TODO-uri, note de schema si design.
- `Raport/` — raportul de practica generat cu Python.

## Istoric absorbit
- 2026-05-07: baza initiala cu 6 tabele, seed-uri, `run_all.sql`, note si verificari de constrangeri/cascade.
- 2026-05-07 Faza 2: securitate, `PasswordHistory`, `AuditLog`, TVP `IntList`, API prin proceduri si login low-privilege `mealprep_app`.
- 2026-05-11 Faza 2.5: seed de ingrediente, indexuri FK, `Recipes.RowVersion`, rescriere `sp_FindRecipesByIngredients`, `sp_GetIngredientUsage`.
- 2026-05-11 Faza 3: planificare mese, favorite, frigider/camara, lista de cumparaturi calculata.
- 2026-05-11 Faza 4: directie de design WPF, categorii de ingrediente, citire sigura de profil si proceduri de rapoarte.
- 2026-05-15: ingrediente in romana fara diacritice si default operational pentru `AppPassword`.
- 2026-05-18: fazele A-F ale aplicatiei confirmate pe PC-ul Ritei; Ingrediente/Frigider/Lista cumparaturi construite.
- 2026-05-21: restilizare UI, ferestre chrome-less, `MessageDialog`, stiluri globale si `App/` comis in git.
- 2026-05-22: crash-ul de salvare reteta a fost duplicat de ingredient; Planificare si Rapoarte livrate; repo-ul GitHub mutat la `Sklizm/MealPrepApp`.

## Stare gasita atunci
- Branch: `feature-drafts-and-photos`.
- Existau fisiere WIP pentru `RecipeDrafts` si `RecipePhotos`, cu proceduri si modele/repositories initiale.
- Design draft: drafturi incomplete in `RecipeDrafts`, ingrediente ca JSON opac, CRUD owner-only.
- Design poze: o poza optionala per reteta in `RecipePhotos`, `VARBINARY(MAX)`, set/delete owner-only, citire prin procedura.

## Conventii de pastrat
- SQL este autoritar daca notele difera de cod.
- Aplicatia foloseste doar stored procedures, niciodata acces direct la tabele.
- Conexiunea app este `mealprep_app`, nu `sa`.
- Scripturile raman idempotente; string-urile sunt `NVARCHAR`; timestamp-urile sunt UTC.
- Numele de constrangeri/indexuri raman explicite.
- `Recipes.RowVersion` este tokenul de concurenta optimista; eroarea 50004 ramane pentru rand invechit.
- UI-ul v1 este in romana; ingredientele seeduite sunt in romana fara diacritice.

## Urmatorul lucru recomandat atunci
Finalizarea drafturilor si pozelor in WPF, verificarea DB build-ului, verificarea pe Windows/VM si apoi impachetarea ca `.exe`.
