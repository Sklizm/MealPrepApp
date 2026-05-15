---
tags: [session, kickoff]
date: 2026-05-07
---

# 2026-05-07 — Sesiunea de Kickoff

## Obiectivele sesiunii
- Definirea structurii proiectului
- Construirea inceputului bazei de date (scope Core)
- Configurarea acestui vault Obsidian pentru continuitate intre sesiuni
- Memorarea contextului proiectului astfel incat sesiunile viitoare Claude sa poata relua

## Ce s-a facut
- Creat `Database/` cu SQL idempotent pentru toate cele 6 tabele de baza:
  - [[Users-ro]], [[Units-ro]], [[Categories-ro]], [[Ingredients-ro]], [[Recipes-ro]], [[RecipeIngredients-ro]]
- Creat scripturi de seed pentru [[Units-ro]] si [[Categories-ro]]
- Creat scriptul master `Database/run_all.sql` (foloseste include-urile `:r` ale DataGrip)
- Creat acest vault Obsidian: index, prezentare generala, note per tabel, [[TODO-ro]], [[Decisions Log-ro]]
- Salvat memorie persistenta astfel incat sesiunile viitoare Claude sa poata relua fara a cere din nou contextul

## Decizii luate (logate in [[Decisions Log-ro]])
- Doar scope-ul Core — fara planuri de masa / liste de cumparaturi / nutritie pentru v1
- Ingredientele sunt globale (fara UserID) pentru v1
- O singura cascada: Recipes → RecipeIngredients
- Timestamp-uri UTC peste tot
- Scripturi idempotente (re-rulabile in siguranta)

## Verificare (aceeasi sesiune)
Rulat `run_all.sql` in interiorul containerului Docker `MealPrepDB` cu `sqlcmd`. Rezultate:
- Toate cele 6 tabele exista; 6 PK-uri, 6 FK-uri, 6 UNIQUE-uri, 5 CHECK-uri, 2 DEFAULT-uri, plus cei 3 indecsi non-PK asteptati
- Seed-uri inserate: 12 [[Units-ro]], 6 [[Categories-ro]]
- Smoke test (insert user + ingrediente + reteta + recipe_ingredients) circula corect
- Toate constrangerile CHECK resping date proaste (Quantity ≤ 0, timpi negativi, Servings ≤ 0, UnitType in afara {weight, volume, count})
- UNIQUE pe `(RecipeID, IngredientID)` blocheaza randurile duplicate de ingredient
- Cascada pe Recipes → RecipeIngredients functioneaza
- Stergerea unui Ingredient inca in uz este corect blocata (RESTRICT)
- Date de test curatate; toate tabelele de utilizator inapoi la 0 randuri

## Sesiunea urmatoare
Vezi [[TODO-ro]]. Pasii imediati:
1. Deschide DataGrip si confirma vizual schema in arbore (verificare de sanatate din partea GUI)
2. Decide daca sa populezi `Ingredients` (in prezent gol) sau lasi asta pentru aplicatia .NET
3. Decide daca sa incepi view-uri/stored procedures sau astepti partea .NET

## Note / intrebari deschise
- Confirma cu supervisor-ul practicii: cere specificatia tabele dincolo de scope-ul Core inainte de revizuire?
- Va folosi aplicatia .NET Entity Framework sau ADO raw? Afecteaza daca ar trebui sa adaugam coloane `RowVersion` acum pentru concurenta.
