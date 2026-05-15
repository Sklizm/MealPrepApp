---
tags: [session]
---

# 2026-05-11 — Faza 3: Planificare mese, favorite, camara, lista de cumparaturi

## Trigger

Codrin a impartasit `~/Downloads/MealPrepApp.pdf` — designul aplicatiei facut cu prietena lui (Railean Margarita). A scos la suprafata mai multe caracteristici pe care DB-ul v1 nu le suporta, cea mai importanta fiind functionalitatea omonima: **planificarea meselor**. Am ales scope-ul **Complet**: planificator + camara + lista de cumparaturi calculata, plus un tabel de Favorite. Stergere hard pastrata. Tabelul Categories neatins. In afara scopului: `IsArchived`, `PricePerServing`, `ImagePath`, categorii de tip de fel de mancare, adaugari manuale in lista de cumparaturi, conversie de unitati.

## Ce s-a adaugat

### Tabele (3)
- `dbo.MealPlanEntries` — `(MealPlanEntryID, UserID, RecipeID, CategoryID, PlannedDate DATE, Servings NULL, Notes NULL, CreatedAt)`. `RecipeID` FK face cascada la stergerea retetei; `UserID` si `CategoryID` RESTRICT. Indexat pe `(UserID, PlannedDate)` pentru citirile saptamana/luna, plus indecsi de coloana FK pe RecipeID si CategoryID.
- `dbo.RecipeFavorites` — PK compozit `(UserID, RecipeID)`. Ambele FK fac cascada (fara problema multi-cale deoarece Recipes→Users este RESTRICT).
- `dbo.UserPantry` — `(UserPantryID, UserID, IngredientID, UnitID, Quantity, AddedAt, UpdatedAt)` cu `UQ (UserID, IngredientID, UnitID)`. `sp_AddPantryItem` face MERGE pe UQ astfel incat adaugarea aceluiasi ingredient+unitate din nou ridica cantitatea in loc sa duplice randurile.

### Proceduri (14 noi)
- Plan de masa: `sp_PlanMeal`, `sp_UpdatePlannedMeal`, `sp_UnplanMeal`, `sp_GetWeeklyPlan`, `sp_GetMonthlyPlan`
- Favorite: `sp_ToggleFavorite` (returneaza IsFavorite 0/1), `sp_GetFavoriteRecipes` (paginat, aceeasi forma ca `sp_GetRecipes`)
- Camara: `sp_AddPantryItem` (upsert via MERGE), `sp_UpdatePantryQuantity` (setare absoluta), `sp_RemovePantryItem`, `sp_GetPantry`
- Lista de cumparaturi: `sp_GetShoppingList(@UserID, @StartDate, @EndDate)` — calculata, face join intre MealPlanEntries → Recipes → RecipeIngredients cu scalare de portii, LEFT JOIN UserPantry, returneaza randuri unde `NeededQty - OnHandQty > 0`.
- Dashboard: `sp_GetDashboardCounts` (contoare placi Acasa 4), `sp_GetRecentRecipes` (grila Retete Recente).

Total proceduri: 32 (era 19 dupa Faza 2.5).

### Decizii integrate

- **Slot de masa = FK Category**: `MealPlanEntries.CategoryID` refera `Categories`. DB-ul permite oricare dintre cele 6 categorii ca slot de masa; UI-ul saptamanal randeaza doar 4 coloane (Breakfast/Lunch/Dinner/Snack). Decuplarea DB de prezentarea UI permite unei intrari "Desert" sa existe fara gimnastica de schema.
- **Scalarea portiilor in lista de cumparaturi**: `ri.Quantity * ISNULL(mpe.Servings, 1) / NULLIF(r.Servings, 0)`. Daca o reteta de 4 portii este planificata pentru 6, cererea de ingrediente scaleaza cu 1.5×. `NULLIF` protejeaza `Servings = 0`; `ISNULL` protejeaza `Servings = NULL`. Matematica este verificata end-to-end (planificat 200g faina @ 6/4 = 300g; camara avea 50g; ToBuy = 250g — potriveste asteptarea).
- **Camara este exacta pe unitate**: "500 g faina" si "2 cesti faina" sunt randuri separate. Fara conversie in v1.
- **Lista de cumparaturi este calculata, nu stocata**. Fara tabel; procedura pura de citire. Elementele manuale ad-hoc sunt v2.
- **Dashboard-ul "MealsPlannedFromTodayCount"** foloseste `PlannedDate >= CAST(SYSUTCDATETIME() AS DATE)` astfel incat placa reflecta mesele *urmatoare*, ceea ce este ceea ce conteaza pentru utilizator pe ecranul de pornire.

## Capcane de retinut

1. **`CASE` nu este o valoare de parametru legala pentru `EXEC`.** Prima versiune a `sp_ToggleFavorite` facea `EXEC dbo.sp_WriteAudit @Details = CASE WHEN ... END;` si SQL Server a respins-o (Msg 156). Corectare: atribuie CASE unei variabile mai intai, transmite variabila. Aceeasi regula ca si capcana "fara subquery-uri in interiorul agregatelor" — T-SQL este mai strict cu expresiile in pozitia de argument decat majoritatea limbajelor.
2. **`INSERT…EXEC` nu se intelege bine cu rollback de tranzactie exterioara** cand procedura interioara are propriul BEGIN/COMMIT/CATCH-cu-ROLLBACK. CATCH-ul din procedura interioara se declanseaza la incalcarea FK si incearca sa faca ROLLBACK, ceea ce ridica Msg 3915 in scope-ul INSERT-EXEC. Solutie: nu inveli testele de proceduri intr-o tranzactie exterioara; curata explicit cu DELETE-uri dupa.
3. **Valorile IDENTITY continua sa creasca peste rebuild-uri** deoarece schema foloseste `IF OBJECT_ID IS NULL` (tabelul nu este sters, doar creat daca lipseste). Dupa cateva rebuild-uri, ID-urile `Ingredients` populate incep la >2000. Testele ar trebui sa caute ID-uri dupa nume, nu sa le hardcodeze.

## Verificare (toate verzi)

- 3 tabele noi prezente.
- 14 proceduri noi prezente.
- Cascada verificata: planifica o reteta → sterge-o → intrarea de plan dispare.
- Favorite: toggle returneaza 1 apoi 0 peste doua apeluri.
- MERGE camara: 100 + 50 = 150 intr-un singur rand.
- Lista de cumparaturi end-to-end: scalarea 6/4 corecta; scaderea camara corecta; doar randuri `ToBuy > 0` afisate.
- Plan saptamanal: 3 intrari intr-o saptamana returnate ordonate dupa data + categorie.
- Dashboard: set de rezultate cu 4 coloane asa cum a fost proiectat.
- Login-ul aplicatiei (`mealprep_app`) poate EXEC fiecare procedura noua; `SELECT FROM dbo.MealPlanEntries` direct este corect respins.

## Ce NU este in aceasta faza

Conform alegerilor utilizatorului:
- Fara `Recipes.IsArchived` — butonul UI ramane simplul "Sterge".
- Fara `Recipes.PricePerServing` — coloana `Pret/portie` din dashboard-ul de pagina 6 al designului este eliminata din v1.
- Fara `Recipes.ImagePath` — cardurile de retete raman doar text.
- Fara categorii de tip de fel de mancare — tabelul Categories pastreaza seed-ul curent de 6 randuri (Breakfast/Lunch/Dinner/Snack/Dessert/Drink).
- Fara adaugari manuale in lista de cumparaturi — lista este complet calculata.
- Fara conversie de unitati in agregarea cumparaturilor.
- Fara urmarirea "vazute recent" — `sp_GetRecentRecipes` foloseste `ISNULL(UpdatedAt, CreatedAt)`.

## Feedback de design predat inapoi lui Codrin + prietena

- Doua modele de navigare concureaza (top tabs vs tree nav). Alege unul — top tabs (paginile 1-5) este mai curat.
- Ecranele de detaliu/editor nu sunt desenate inca: editor de reteta, vizualizare reteta, editor de ingrediente, modal "adauga masa la plan", dialoguri de confirmare.
- Bugetul de celula al calendarului (grila 7×6 × pana la 4 sloturi de masa/zi) are nevoie de o reprezentare compacta + click-to-expand.
- `Rapoarte` este in navigare dar nu are design.
- Artefact Canva `aaa` pe paginile 2/3 — curatare.
- Starile goale / de incarcare / de eroare nu sunt desenate.

## Urmatorul

Faza 4 — implementarea reala a aplicatiei .NET (detinuta de Codrin + prietena, WPF + MVVM). DB-ul este acum feature-complete pentru v1.
