---
tags: [decisions, adr]
---

# Jurnal de Decizii

Decizii arhitecturale cu rationament. Adauga, nu rescrie — chiar si cand sunt inversate,
pastreaza intrarea originala si adauga o intrare ulterioara care o inlocuieste.

---

## 2026-05-07 — Doar scope-ul de baza pentru v1
**Decizie**: Livrare cu 6 tabele (Users, Units, Categories, Ingredients, Recipes, RecipeIngredients). Fara planuri de masa, liste de cumparaturi, nutritie sau fotografii in v1.
**De ce**: Practica are nevoie de un rezultat demonstrabil, nu de un produs complet. Sa livrezi ceva mic care functioneaza este mai bine decat sa livrezi ceva mare care nu functioneaza.
**Compromis**: Vom avea nevoie de o faza ulterioara pentru a adauga restul. Asta e in regula — schema este proiectata sa se extinda aditiv.

---

## 2026-05-07 — Ingredientele sunt globale (fara UserID)
**Decizie**: Tabelul [[Ingredients-ro]] nu are `UserID` — fiecare utilizator imparte aceeasi lista de ingrediente.
**De ce**: "Sare" nu trebuie re-creata pentru fiecare utilizator. Schema mai simpla, query-uri mai simple, iar autocomplete-ul aplicatiei .NET este mai bun cu o lista partajata.
**Reversibilitate**: Adauga o coloana `UserID` nullable mai tarziu (`NULL` = global, altfel = privat per utilizator). Datele existente raman valide.

---

## 2026-05-07 — O singura stergere in cascada
**Decizie**: `ON DELETE CASCADE` doar pe Recipes → [[RecipeIngredients-ro]]. Restul este RESTRICT.
**De ce**: Cascadele de stergere par convenabile pana cand distrug silentios date. Randurile din RecipeIngredients nu au sens fara reteta lor, asa ca cascadarea acolo este sigura. Stergerea unui utilizator cu retete sau a unui ingredient in uz ar trebui sa fie o operatie explicita — nu un efect secundar.

---

## 2026-05-07 — Timestamp-uri UTC via SYSUTCDATETIME()
**Decizie**: Toate coloanele `CreatedAt` / `UpdatedAt` au valoare implicita `SYSUTCDATETIME()`.
**De ce**: Timpul local al containerului Docker este oricare il are gazda, iar aplicatia .NET poate servi utilizatori in fusuri orare diferite. UTC este singura referinta stabila. Aplicatia converteste in ora locala pentru afisare.

---

## 2026-05-07 — Scripturi idempotente
**Decizie**: Fiecare CREATE invelit in `IF OBJECT_ID(...) IS NULL` (sau echivalent pentru indecsi). Seed-urile folosesc `MERGE`.
**De ce**: Re-rularea build-ului in timpul dezvoltarii trebuie sa fie sigura. Fara "drop and recreate" — asta distruge datele locale de test. Fara urmarirea manuala a "am rulat deja asta?".

---

## 2026-05-07 — Conventie de denumire a constrangerilor
**Decizie**: Prefixe `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_`; sufix specific coloanei.
**De ce**: Numele de constrangeri generate automat (`PK__Users__1788CC4C7DEB...`) sunt instabile intre rebuild-uri si ilizibile in mesajele de eroare. Numele explicite fac scripturile de migrare si log-urile de eroare mult mai usor de citit.

---

## 2026-05-07 — NVARCHAR peste tot
**Decizie**: Toate coloanele de string folosesc `NVARCHAR` (Unicode), nu `VARCHAR`.
**De ce**: Numele de retete, numele de ingrediente si instructiunile ar putea include caractere cu diacritice, emoji sau scripturi non-latine. Costul de stocare este neglijabil comparativ cu costul unei migrari viitoare.

---

## 2026-05-07 — API doar prin stored proceduri pentru aplicatie
**Decizie**: Aplicatia .NET NU va avea acces direct la tabele. Se conecteaza ca un login SQL cu privilegii reduse (`mealprep_app`) care are doar `GRANT EXECUTE ON SCHEMA::dbo`, cu `DENY SELECT/INSERT/UPDATE/DELETE ON SCHEMA::dbo` explicit. Mutatiile au succes prin ownership chaining (procedurile si tabelele impart proprietarul `dbo`).
**De ce**: SQL injection devine structural imposibil din partea aplicatiei — nu exista cale prin care un string controlat de atacator sa ajunga intr-un query ad-hoc. De asemenea forteaza un contract curat DB/aplicatie: procedurile sunt API-ul.
**Compromis**: Fiecare query nou are nevoie de o procedura noua. Pentru v1 este in regula — lista de proceduri este marginita. Daca aplicatia are nevoie de raportare ad-hoc mai tarziu, adauga un rol read-only cu `GRANT SELECT` pe view-uri specifice, nu pe tabele.

---

## 2026-05-07 — Stergere hard pastrata (fara soft-delete)
**Decizie**: Stergerile elimina fizic randurile. Fara flag `IsDeleted`.
**De ce**: Scope-ul practicii este mic; recuperarea nu este un obiectiv. Soft-delete adauga complexitate fiecarui query de citire (fiecare WHERE are nevoie de `AND IsDeleted = 0`).
**Reversibilitate**: Adaugarea de soft-delete mai tarziu este non-triviala — fiecare procedura si view ar trebui sa filtreze. Daca facem vreodata asta, este o decizie de Faza 3.

---

## 2026-05-07 — Politica de blocare: 5 esecuri → 15 minute
**Decizie**: `sp_RecordLoginFailure` incrementeaza `FailedLoginCount`; la al 5-lea esec seteaza `LockedUntil = now + 15 min` si scrie `ACCOUNT_LOCKED` in AuditLog. Login-ul reusit reseteaza ambele.
**De ce**: Ordinul de magnitudine standard din industrie. Suficient de lung pentru a descuraja brute force, suficient de scurt incat utilizatorii legitimi sa nu fie blocati catastrofal.
**Reversibilitate**: Ambele numere sunt constante locale in `sp_RecordLoginFailure`. Usor de ajustat.

---

## 2026-05-07 — Adancimea istoricului de parole: 5
**Decizie**: `sp_ChangePassword` respinge reutilizarea parolei curente SAU a ultimelor 5 intrari din `dbo.PasswordHistory`. Curatarea pastreaza istoricul la exact 5 randuri per utilizator.
**De ce**: Valoare implicita comuna de conformitate. Mai mult decat suficient pentru a preveni ciclarea evidenta, nu atat de multe incat utilizatorii sa se simta blocati.
**Reversibilitate**: `@HistoryDepth` este o constanta locala in procedura.

---

## 2026-05-07 — JSON pentru payload-uri de scriere, TVP pentru filtre de citire
**Decizie**: `sp_CreateRecipe` / `sp_UpdateRecipe` accepta ingredientele ca `@IngredientsJson NVARCHAR(MAX)` parsate via `OPENJSON`. `sp_FindRecipesByIngredients` accepta un TVP `dbo.IntList`.
**De ce**: Din C# / EF Core / Dapper, serializarea unei liste in JSON cu `System.Text.Json` este o linie; construirea unui `DataTable` pentru un TVP este mai mult cod. Dar pentru liste pure de ID-uri in caile de *citire*, TVP-ul este mai curat si SQL Server poate sa-l optimizeze ca pe un tabel real (posibil indexat).
**Compromis**: Doua stiluri de payload intr-un API. Documentate in comentariile la nivel de procedura.

---

## 2026-05-07 — Parola de login a aplicatiei furnizata la rulare, nu stocata in fisier
**Decizie**: `09_app_role.sql` foloseste substitutia de variabile sqlcmd (`$(AppPassword)`). Valoarea reala este transmisa via `sqlcmd -v AppPassword="..."`.
**De ce**: Fisierul este comis in git; parola nu ar trebui. Injectia la rulare pastreaza secretul in afara controlului de cod sursa fara a renunta la idempotenta.
**Cum se aplica**: Oricine ruleaza din nou build-ul are nevoie de parola (pastrata de Codrin). Rotatia este `ALTER LOGIN mealprep_app WITH PASSWORD = 'new'`.

---

## 2026-05-11 — IngredientCategories augmenteaza (nu inlocuieste) pool-ul global de Ingredients
**Decizie**: Adaugat `dbo.IngredientCategories` (8 randuri populate) si un FK nullable `Ingredients.IngredientCategoryID`. Decizia originala "Ingredientele sunt globale (fara UserID)" inca este valabila — fiecare utilizator imparte un pool de ingrediente. Categoria este o *grupare de afisare*, nu o granita de confidentialitate.
**De ce**: Sidebar-ul Ingrediente al aplicatiei are o intrare "Categorii"; fara o grupare reala ar fi o minciuna UI. Categorizarea este si o tema defensabila pentru practica ("cum ai lasa utilizatorii sa rasfoiasca 200 de ingrediente?").
**Cum se aplica**: Ingredientele noi pot fi livrate cu `IngredientCategoryID = NULL` (necategorisite → cad in "Altele" daca UI-ul grupeaza pe categorie si randeaza NULL ca Altele). Fisierul de seed completeaza valori rezonabile pentru cele 44 de ingrediente livrate. Adaugarea unei categorii noi este un rand in seed + o cale de cod noua in gruparea UI.

---

## 2026-05-11 — `sp_GetUserProfile` este citirea sigura a profilului; `sp_GetUserForLogin` ramane doar pentru login
**Decizie**: Procedura noua `sp_GetUserProfile(@UserID)` returneaza `UserID, Username, Email, CreatedAt, LastLoginAt` — fara `PasswordHash`, fara `FailedLoginCount`, fara `LockedUntil`. Ecranul Profil apeleaza asta; `sp_GetUserForLogin` ramane rezervata pentru fluxul de login.
**De ce**: `sp_GetUserForLogin` a fost initial proiectata sa returneze datele de care fluxul de login are nevoie pentru a verifica parola (inclusiv hash-ul). Refolosirea ei pentru ecranul Profil ar scurge hash-ul intr-un ecran care nu are de ce sa-l poarte. Doua proceduri cu scopuri unice si clare sunt mai ieftine decat auditarea fiecarui apelant.
**Cum se aplica**: Orice ecran nou de tip "arata-mi acest utilizator" ar trebui sa apeleze `sp_GetUserProfile`. Doar fluxul de login ar trebui sa citeasca vreodata `PasswordHash`.

---

## 2026-05-11 — Specificatia de design traieste in afara repo-ului
**Decizie**: Specificatia de design (Partea A a planului de Faza 4) este predata Margaritei pentru machetele Canva. Este referentiata din `Vault/Sessions/2026-05-11 - Phase 4 design and ingredient categories.md` si din fisierul de plan, dar machetele propriu-zise nu sunt comise in acest repo.
**De ce**: Acest repo este jumatatea DB. Designul aplicatiei isi detine propriul artefact (fisier Canva). Pastrarea lor separat evita ca repo-ul sa devina sursa de adevar pentru doua livrabile diferite mentinute de doua persoane diferite.
**Cum se aplica**: Cand Margarita revizuieste machetele, sursa de adevar este oricare fisier Canva pe care l-a salvat ultima data. Partea A din fisierul de plan este un snapshot al intentiei de design la momentul deciziei, nu o specificatie vie.

---

## 2026-05-11 — Slot-ul de masa este un FK la Categories, nu un enum separat
**Decizie**: `dbo.MealPlanEntries.CategoryID` este un FK obisnuit la `dbo.Categories`. DB-ul accepta oricare dintre cele 6 categorii (Breakfast/Lunch/Dinner/Snack/Dessert/Drink) ca slot de masa; UI-ul saptamanal alege doar sa randeze 4 coloane.
**De ce**: Adaugarea unei coloane separate `MealSlot NVARCHAR CHECK IN (...)` ar fi creat o taxonomie paralela care se suprapune cu Categories pe 4 nume dar exclude Dessert si Drink — confuz si nu cu adevarat util. Refolosirea Categories pastreaza schema simpla si permite unei intrari de plan "Dessert" sa coexiste natural.
**Cum se aplica**: UI-ul este responsabil pentru care categorii apar ca coloane in view-ul saptamanal. DB-ul nu face o astfel de judecata. Daca aplicatia decide mai tarziu sa afiseze o a 5-a coloana pentru Deserturi, nu este nevoie de schimbare de schema.

---

## 2026-05-11 — Lista de cumparaturi este calculata, nu stocata
**Decizie**: `sp_GetShoppingList(@UserID, @StartDate, @EndDate)` este o procedura pura de citire — fara tabel `ShoppingList` sau `ShoppingListItem`. Procedura face JOIN intre `MealPlanEntries → Recipes → RecipeIngredients` pentru intervalul de date, scaleaza dupa numarul de portii, face LEFT JOIN cu `UserPantry` si returneaza linii de ingrediente cu `NeededQty`, `OnHandQty`, `ToBuyQty`.
**De ce**: O lista de cumparaturi stocata devine invechita in momentul in care o intrare de plan sau un stoc de camara se schimba. Calculul la cerere inseamna ca lista este intotdeauna actuala si nu exista logica de sincronizare de mentinut. Pentru o aplicatie desktop offline single-user, costul de performanta este irelevant.
**Compromis**: Adaugarile manuale ad-hoc ("cumpara hartie igienica chiar daca nicio reteta nu o cere") nu sunt posibile. Daca sunt vreodata necesare, adauga un tabel `ManualShoppingItems` si fa UNION cu el.
**Cum se aplica**: Nu adauga caching sau materializare pentru asta fara un motiv masurat.

---

## 2026-05-11 — Scalarea portiilor in lista de cumparaturi
**Decizie**: Cererea de ingredient este calculata ca `ri.Quantity * ISNULL(mpe.Servings, 1) / NULLIF(r.Servings, 0)`. Planificarea unei retete de 4 portii pentru 6 portii scaleaza fiecare ingredient cu 1.5×.
**De ce**: Numarul de portii la nivel de reteta este lipsit de sens daca planificatorul nu poate sa-l suprascrie. Altfel, lista de cumparaturi reflecta intotdeauna o singura dimensiune de batch canonica, ceea ce nu se potriveste gatitului real.
**Cazuri marginale**: `NULLIF(r.Servings, 0)` protejeaza o reteta cu `Servings = 0` sau `NULL` — randul este eliminat din rezultat via filtrul `> 0` de la sfarsit. Tratarea cu `ISNULL` pe partea de planificare acopera cazul comun de "foloseste valoarea implicita a retetei".

---

## 2026-05-11 — Camara este exacta pe unitate (fara conversie in v1)
**Decizie**: `UserPantry` are `UQ (UserID, IngredientID, UnitID)` iar lista de cumparaturi face join pe tuplul exact. "500 g faina" si "2 cesti faina" sunt urmarite ca doua randuri separate si nu se combina.
**De ce**: Conversia intre unitati (greutate ↔ volum) are nevoie de tabele de densitate si matematica specifica ingredientului. Mult in afara scopului v1, si 95% din utilizarea practica a camarei este oricum consistenta pe unitate.
**Reversibilitate**: Un strat de conversie v2 ar putea reduce UQ la `(UserID, IngredientID)` dupa introducerea unitatilor canonice per ingredient + o functie de conversie. Schema nu ne blocheaza.

---

## 2026-05-11 — Adaugarea in camara este un upsert (MERGE), nu un insert
**Decizie**: `sp_AddPantryItem` face MERGE pe `(UserID, IngredientID, UnitID)`. Daca randul exista, `Quantity += @Quantity, UpdatedAt = SYSUTCDATETIME()`; altfel insert.
**De ce**: Se potriveste cu modelul mental al utilizatorului — "am cumparat inca 500 g de faina" este o singura actiune, nu "gaseste randul meu de faina, citeste cantitatea, adauga 500, scrie inapoi". De asemenea evita race-uri in interiorul unui singur utilizator (un MERGE este atomic; citire-modificare-scriere nu).
**Cum se aplica**: Foloseste `sp_UpdatePantryQuantity` (setare absoluta) pentru "utilizatorul a editat numarul in UI"; foloseste `sp_AddPantryItem` pentru "utilizatorul a adaugat mai mult stoc".

---

## 2026-05-11 — Reguli de cascada pentru tabelele noi
**Decizie**:
- `MealPlanEntries.RecipeID` → `ON DELETE CASCADE` (o intrare planificata fara reteta nu are sens).
- `MealPlanEntries.UserID` → RESTRICT (consistent cu Recipes — stergerea unui utilizator cu intrari de plan este o operatie explicita).
- `MealPlanEntries.CategoryID` → RESTRICT (Categories sunt populate, niciodata sterse in practica).
- `RecipeFavorites.UserID` si `.RecipeID` → ambele CASCADE. Sigur deoarece `Recipes.UserID` este RESTRICT, deci nu apare eroare de cale multi-cascada.
- `UserPantry.UserID` → CASCADE; `UserPantry.IngredientID` si `.UnitID` → RESTRICT.
**De ce**: Acelasi principiu ca in Faza 1 — cascada doar cand copilul nu are sens fara parinte.

---

## 2026-05-11 — Parametrii EXEC trebuie sa fie variabile, nu expresii
**Decizie**: Cand detaliile de audit pentru o procedura sunt calculate via CASE, atribuie unei variabile locale mai intai:
```sql
DECLARE @Details NVARCHAR(500) = CASE WHEN @x = 1 THEN N'on' ELSE N'off' END;
EXEC dbo.sp_WriteAudit ..., @Details = @Details;
```
**De ce**: T-SQL respinge `EXEC ... @param = CASE WHEN ... END` cu Msg 156. Parametrii `EXEC` accepta constante, variabile, NULL, DEFAULT — nu expresii arbitrare.
**Cum se aplica**: Daca te trezesti scriind `@param = (somefunc(x))` sau `@param = ISNULL(...)` intr-un EXEC, ridica-l intr-un pattern `DECLARE @v = expr; EXEC ... @param = @v;`.

---

## 2026-05-11 — Forma seed-ului de ingrediente (~40 elemente comune, MERGE pe Name, unitate dupa abreviere)
**Decizie**: `seeds/ingredients_seed.sql` populeaza ~40 de ingrediente comune. Idempotent via `MERGE` pe `Name`. `DefaultUnitID` este rezolvat via `LEFT JOIN dbo.Units ON Abbreviation = ...` in loc de ID-uri de unitate hardcoded.
**De ce**: Lista trebuie sa arate bine la demo (dropdown-urile goale ucid prima impresie a aplicatiei), sa fie re-rulabila fara duplicate sau churn si sa supravietuiasca oricarei renumarari viitoare a Units. Cautarea dupa abreviere inseamna ca fisierul de seed se citeste ca date reale, nu ca un puzzle de chei straine. `LEFT JOIN` (nu `JOIN`) inseamna ca o unitate lipsa/redenumita lasa `DefaultUnitID` NULL in loc sa elimine ingredientul.
**Cum se aplica**: Adaugarea de mai multe ingrediente = adauga la blocul `VALUES`. Redenumirea unei abrevieri de unitate in `units_seed.sql` necesita actualizarea coloanei corespunzatoare in seed-ul de ingrediente.

---

## 2026-05-11 — Concurenta optimista pe Recipes via ROWVERSION
**Decizie**: `dbo.Recipes` primeste o coloana `RowVersion ROWVERSION NOT NULL`. `sp_GetRecipeFull` o returneaza; `sp_UpdateRecipe` o cere ca `@RowVersion BINARY(8)` si `THROW 50004` daca nu se potriveste cu randul curent.
**De ce**: Chiar daca v1 este in esenta single-user, aplicatia .NET altfel nu ar avea aparare impotriva unei salvari invechite dintr-un al doilea tab care suprascrie modificarile primului tab. `ROWVERSION` este mentinut automat de SQL Server (fara implicarea aplicatiei sau a trigger-elor), deci costul este un parametru in plus la update si o coloana in plus la read. Material bun pentru practica — scoate la suprafata o preocupare reala de productie cu complexitate minima.
**Cum se aplica**: Orice procedura noua de mutatie pe Recipes ar trebui sa accepte si sa verifice `@RowVersion`. Eroarea 50004 este acum rezervata pentru conflicte de rand invechit prin tot API-ul.

---

## 2026-05-11 — Rescrierea sp_FindRecipesByIngredients: GROUP BY + LEFT JOIN la TVP
**Decizie**: Cele doua subquery-uri `CROSS APPLY` sunt inlocuite cu un singur `GROUP BY r.RecipeID` peste `JOIN dbo.RecipeIngredients ri` + `LEFT JOIN @IngredientIDs m ON m.ID = ri.IngredientID`. `MatchedIngredients = SUM(CASE WHEN m.ID IS NOT NULL THEN 1 ELSE 0 END)`, `TotalIngredients = COUNT(*)`.
**De ce**: Originalul recalcula doua agregate per rand de reteta via CROSS APPLY — un singur pass cu GROUP BY este mai rapid si mai clar. Forma LEFT JOIN este singura cale de a face asta in T-SQL: SQL Server respinge subquery-urile in interiorul functiilor de agregare (Msg 130, "Cannot perform an aggregate function on an expression containing an aggregate or a subquery"), deci `SUM(CASE WHEN x IN (SELECT ...))` nu este legal.
**Cum se aplica**: Orice pattern de "numara potriviri impotriva unui TVP" ar trebui sa faca LEFT JOIN pe TVP, nu subquery pe el in interiorul unui agregat. Forma de output a procedurii este neschimbata — apelantii nu trebuie sa stie.

---

## 2026-05-11 — Coloanele FK trebuie sa fie indexate explicit
**Decizie**: Adaugate `IX_Ingredients_DefaultUnitID` si `IX_RecipeIngredients_UnitID`. Celelalte coloane FK erau deja acoperite (fie prin indecsi `IX_*`, fie ca si coloane principale ale indecsilor `UQ_*` / compoziti).
**De ce**: SQL Server NU creeaza automat un index pentru o coloana FK (doar pentru PK-ul referit). Fara index, verificarea RESTRICT pe `DELETE FROM dbo.Units WHERE UnitID = X` face scan pe tabelul care refera. Ieftin de adaugat si un raspuns defensabil la "de ce acesti indecsi?" daca este intrebat.
**Cum se aplica**: Orice adaugare viitoare de FK are nevoie de un `IX_<Table>_<Column>` corespunzator daca coloana nu este deja coloana principala a altui index.

---

## 2026-05-11 — Pas de rebuild: curata directorul tinta in container inainte de docker cp
**Decizie**: Secventa completa de rebuild este acum: `docker exec -u 0 MealPrepDB rm -rf /tmp/Database` → `docker cp Database MealPrepDB:/tmp/Database` → `sqlcmd ... -i run_all.sql`.
**De ce**: `docker cp Database MealPrepDB:/tmp/Database` copiaza IN `/tmp/Database/` existent (producand `/tmp/Database/Database/...`) cand directorul tinta exista deja. Simptom: build-ul "reuseste" pe copia exterioara invechita si nu apar obiecte noi. `-u 0` este necesar deoarece fisierele existente sunt detinute de uid 1000 in container.
**Cum se aplica**: Curata intotdeauna inainte de a copia. Daca un raport de build viitor arata suspect de curat (fara randuri afectate pentru seed-uri noi etc.), verifica intai layout-ul `/tmp/Database/`.

---

## 2026-05-07 — Curatarea istoricului de parole are nevoie de un tiebreak determinist
**Decizie**: Atat verificarea "este asta in ultimele 5 hash-uri?" cat si curatarea `ROW_NUMBER()` ordoneaza dupa `ChangedAt DESC, PasswordHistoryID DESC`. Nu doar `ChangedAt DESC`.
**De ce**: `ChangedAt` este `DATETIME2(0)` (secunde intregi). Mai multe schimbari de parola in aceeasi secunda au timestamp-uri identice, deci ordonarea doar dupa timestamp este non-determinista si curatarea poate sterge randul gresit. `PasswordHistoryID` este `INT IDENTITY` deci creste intotdeauna monoton — tiebreak perfect.
**Cum se aplica**: Orice `ORDER BY <timestamp>` peste randuri care pot fi create in succesiune rapida are nevoie de un tiebreak IDENTITY. Util de retinut pentru orice query viitor "recent N".

---

## 2026-05-15 — Numele de ingrediente traiesc in romana in seed, nu in engleza
**Decizie**: `seeds/ingredients_seed.sql` livreaza numele ingredientelor in romana, scrise fara diacritice (`Faina`, `Branza`, `Smantana`) pentru a se potrivi cu conventia deja folosita in [[IngredientCategories-ro]]. Fara seed paralel in engleza.
**De ce**: Aplicatia se livreaza in romana; un demo DB in romana inseamna un seed in romana. Pastrarea unei copii in engleza ca seed frate ar permite celor doua sa diverga, iar `MERGE` are cheie pe `Name`, deci orice schimbare de limba la nivel de seed este nebanala (randurile existente nu se redenumesc — ar sta pur si simplu alaturi de cele noi).
**Reversibilitate / cum se aplica**: O versiune in engleza a aplicatiei ar trebui sa adauge un strat de localizare (fisiere de resurse in aplicatie, sau un tabel `Translations` cu cheie pe `IngredientID`) in loc sa bifurce seed-ul. Seed-ul este copia canonica in romana.

---

## 2026-05-15 — `AppPassword` are default gol in `09_app_role.sql` (inlocuieste decizia din 2026-05-07 privind variabila la runtime, pentru rebuild-uri)
**Decizie**: `09_app_role.sql` declara acum `:setvar AppPassword ""` aproape de inceput astfel incat preprocesorul sqlcmd sa nu erorea pe variabila nedefinita. Linia `CREATE LOGIN ... WITH PASSWORD = N'$(AppPassword)'` se declanseaza doar cand login-ul lipseste, deci la rebuild default-ul gol este nefolosit.
**De ce**: Decizia din 2026-05-07 ("Parola login-ului aplicatiei este furnizata la runtime, nu stocata in fisier") este in continuare corecta in spirit, dar consecinta operationala — fiecare rebuild are nevoie de `-v AppPassword=...` desi login-ul exista deja iar valoarea este aruncata — era frictiune fara castig. Login-ul persista la nivel de server peste `DROP DATABASE MealPrepDB`, deci calea de *create* este in esenta doar pentru prima rulare.
**Capcana**: Un `:setvar` intr-un script suprascrie `-v` din linia de comanda, deci calea documentata pentru prima rulare (`sqlcmd ... -v AppPassword="..."`) cere acum fie editarea liniei `:setvar` direct, fie stergerea ei. Comentariul de header din `09_app_role.sql` explica ambele cai.
**Cum se aplica**: Rebuild → ruleaza `run_all.sql`, fara flag. Prima setare pe un server proaspat → editeaza `:setvar AppPassword ""` la parola aleasa (sau sterge linia si foloseste `-v`).

---

## 2026-05-18 — `sp_AddIngredient` nu poate seta categoria; dialogul de adaugare ingredient renunta la selectorul de categorie
**Decizie**: Dialogul de adaugare ingredient (`IngredientAddDialog`) prezinta doar Nume + Unitate implicita. Ingredientele noi ajung in grupul "Fara categorie" pana cand o procedura viitoare accepta un parametru `IngredientCategoryID`.
**De ce**: `Ingredients` are o coloana `IngredientCategoryID` (adaugata in `14_ingredient_categories.sql`), iar `sp_GetIngredients` o intoarce, dar `sp_AddIngredient` nu o accepta — deci "dropdown-ul de categorie" din specificatie nu poate persista nimic. Afisarea unui selector nefunctional ar fi UX mai prost decat scoaterea lui.
**Aplicare**: Cand polish-ul are nevoie de categorii editabile, extinde `sp_AddIngredient` sa primeasca `@IngredientCategoryID INT = NULL` (si adauga o `sp_UpdateIngredientCategory` paralela daca backfill-ul randurilor existente e necesar). Apoi reintrodu selectorul in `IngredientAddDialogViewModel`.

---

## 2026-05-18 — Pattern pentru deschidere dialog: constructor fara parametri + `IDialogService.ShowDialog<TWindow>(vm)`
**Decizie**: Dialogurile modale adaugate in Faza F (`IngredientAddDialog`, `PantryItemDialog`) au constructori fara parametri si leaga VM-ul prin `DataContext`. `IDialogService.ShowDialog<TWindow>(viewModel)` este singurul punct de intrare — instantieaza fereastra, seteaza `DataContext = vm`, seteaza `Owner` la fereastra activa si apeleaza `ShowDialog()`. Dialogurile se inchid singure prin emiterea unui eveniment `SaveSucceeded` din VM, gestionat in code-behind pentru a seta `DialogResult = true`.
**De ce**: `ChangePasswordDialog` existent este construit cu DI (constructorul ii primeste VM-ul) pentru ca shell-ul detine fluxul de deschidere. Dialogurile din Faza F sunt deschise din VM-uri de lista (`IngredienteListViewModel`, `FrigiderViewModel`), nu din shell, asa ca trecerea prin `IDialogService` pastreaza acele VM-uri testabile si evita raspandirea apelurilor `App.Services.GetRequiredService`. VM-ul ramane rezolvat prin DI de catre apelant (deci dependentele se injecteaza); fereastra in sine nu trebuie sa stie de DI.
**Aplicare**: Dialogurile noi din Fazele G/H sa urmeze acelasi pattern, exceptie controalele brute (ex. `PasswordBox`) ale caror valori nu pot fi legate direct — acelea pastreaza forma VM-in-ctor.

---

## 2026-05-18 — Izolare dezactivata pentru sesiunile de background in acest repo (`.claude/settings.json`)
**Decizie**: `.claude/settings.json` seteaza `{"worktree": {"bgIsolation": "none"}}`. Sesiunile Claude Code de background pot acum edita direct `App/MealPrepApp/` in loc sa fie rutate printr-un worktree.
**De ce**: Garda implicita `bgIsolation: "worktree"` a harness-ului creeaza un worktree git izolat inainte de a permite editari. Dar `App/` este in `.gitignore` — worktree-urile contin doar fisiere urmarite, deci worktree-ul ar porni fara niciun cod existent al aplicatiei. Orice editare bg ar ajunge intr-un shell gol fara context.
**Aplicare**: Daca/cand `App/` devine urmarit in git (ex. cand placeholder-ul WinForms este in sfarsit eliminat si sursa WPF este commit-uita), revoca setarea astfel incat sesiunile bg sa-si recapete garda de izolare. Pana atunci ramane oprita.

---

## 2026-05-18 (mai tarziu) — `sp_AddIngredient` extinsa cu `@IngredientCategoryID`; dialogul recapata selectorul de categorie
**Decizie**: `sp_AddIngredient` primeste acum un al treilea parametru optional `@IngredientCategoryID INT = NULL` si il insereaza in `dbo.Ingredients`. `IngredientRepository.AddIngredientAsync` il transmite mai departe; `IngredientAddDialog` reintroduce dropdown-ul de categorie.
**De ce**: Decizia anterioara din aceeasi zi de a scoate selectorul era un workaround pentru o limitare v1 a procedurii, nu o decizie de design. Extinderea procedurii este o schimbare single-column, non-breaking (parametrul nou este optional, default NULL) — strict mai bine decat sa expediem un dialog care ignora silentios categoria.
**Reversibilitate**: Inlocuieste intrarea anterioara din 2026-05-18. Schimbarea de procedura este idempotenta (`CREATE OR ALTER`) si deja aplicata pe containerul rulant; smoke test-ul a confirmat ca un insert cu `@IngredientCategoryID = 8 (Altele)` ajunge corect.
**Aplicare**: Daca se adauga vreodata `sp_UpdateIngredient` (pentru editare ingrediente existente), potriveste aceasta semnatura: `(@IngredientID, @Name, @DefaultUnitID, @IngredientCategoryID)`.

---

## 2026-05-21 — Ferestre fara chrome nativ via `System.Windows.Shell.WindowChrome`
**Decizie**: Toate cele cinci ferestre (`LoginWindow`, `ShellWindow`, `ChangePasswordDialog`, `IngredientAddDialog`, `PantryItemDialog`) seteaza `WindowStyle="None"` si folosesc un bloc `<shell:WindowChrome>` (`CaptionHeight="44"`) ca sa suprime bara de titlu nativa Windows, pastrand drag/snap/resize de la OS. Banda de header inchisa de 44px existenta devine zona de drag a OS-ului; butoanele de caption din ea sunt marcate `shell:WindowChrome.IsHitTestVisibleInChrome="True"` ca sa primeasca click-uri. Dialogurile au doar × inchidere; `LoginWindow` are ─ minimizare + ×; `ShellWindow` are ─ ▢/❐ maximizare-restaurare + ×.
**De ce**: Header-ul inchis personalizat se randa *sub* bara de titlu nativa Windows — un dublu-header vizibil. `WindowChrome` este inclus in `PresentationFramework` (fara dependinta in plus) si este modul standard WPF de a detine intreaga suprafata a ferestrei fara a pierde managementul de ferestre al OS-ului.
**Capcana**: Orice element clicabil din zona de caption este altfel inghitit de zona de drag — fiecare buton de caption are nevoie de `IsHitTestVisibleInChrome="True"`. `ResizeBorderThickness` trebuie sa fie nenul (6) pe ferestrele redimensionabile altfel marginile nu redimensioneaza la drag; dialogurile il pastreaza la 0.
**Aplicare**: Ferestrele noi urmeaza acelasi bloc. Stilurile de buton de caption sunt in `Themes/Styles.xaml` (`WindowChromeButton` 46×44 cu hover `#33FFFFFF`, `WindowCloseButton` cu hover `DangerBrush`).

---

## 2026-05-21 — `MessageDialog` inlocuieste `MessageBox`; `DialogService` deleaga la el
**Decizie**: Un singur `Views/Shared/MessageDialog.xaml` (+ `.cs`) stilizat inlocuieste fiecare `MessageBox.Show`. Are aceeasi structura header-inchis / continut / footer-Cream2 ca celelalte dialoguri si un enum `MessageDialogKind`: **Info** (un OK), **Confirm** (Da/Nu, intoarce true la Da), **Error** (header rosu `DangerBrush` + glif ⚠, un OK). `DialogService.Confirm/ShowError/ShowInfo` sunt acum delegatoare pe o linie; interfata `IDialogService` este neschimbata deci niciun apelant nu s-a schimbat.
**De ce**: `MessageBox` brut este chrome nativ Windows nestilizabil — strica paleta crem/oliv in momentul in care apare orice confirmare/eroare. Centralizarea pe un singur dialog inseamna ca stilul de eroare (rosu + ⚠) este consecvent si modificarile viitoare se fac intr-un singur loc.
**Capcana**: Titlul/mesajul sunt atribuite prin controale denumite (`HeaderText.Text`, `MessageBody.Text`) in fabrica statica `Show()` **dupa** `InitializeComponent`, NU prin binding-uri la proprietati CLR ale ferestrei — acele proprietati CLR se evalueaza dupa ce ruleaza initializatorul de obiect, deci binding-urile s-ar randa goale.
**Aplicare**: Nu mai apela niciodata `MessageBox.Show` — ruteaza prin `IDialogService`. Variantele noi extind enum-ul + switch-ul `Configure()`.

---

## 2026-05-21 — Chrome-ul nativ stilizat prin stiluri implicite globale (fara cheie)
**Decizie**: `DatePicker`/`Calendar`/`CalendarItem`/`CalendarDayButton`/`CalendarButton`, `Menu`/`MenuItem`, `ToolTip` si `ScrollBar` (+ partile thumb/repeat-button) sunt restilizate in `Themes/Styles.xaml` ca stiluri **implicite** (TargetType fara `x:Key`) astfel incat fiecare instanta din toata aplicatia mosteneste paleta automat — fara editari la callsite. Ambele campuri de data din `ShoppingListView`, meniul de utilizator din `ShellWindow`, fiecare tooltip si fiecare scrollbar de `ScrollViewer`/`DataGrid` le preiau gratuit.
**De ce**: Aceste controale lasau sa transpara chrome-ul implicit albastru/gri Windows. Stilurile implicite le stilizeaza peste tot dintr-o data si pastreaza ecranele noi consecvente fara cablare per-control.
**Capcana**: `CalendarButton` (selectorul de luna/an) **nu** are `IsSelected` — aceea e doar pe `CalendarDayButton`; foloseste `HasSelectedDays` pentru evidentierea lui (altfel eroare de compilare XAML). Template-ul implicit `CalendarItem` trebuie suprascris complet altfel chrome-ul nativ Aero tot incadreaza grila de zile. `SaveFileDialog` si `PrintDialog` sunt native OS si **nu** pot fi restilizate — limitare documentata, in afara scopului.
**Aplicare**: Pastreaza controalele noi stilizate fara cheie unde se vrea un aspect global; foloseste un stil cu cheie doar pentru variante punctuale.

---

## 2026-05-21 — Izolarea sesiunilor de background reactivata acum ca `App/` este urmarit (revoca opt-out-ul din 2026-05-18)
**Decizie**: `App/` este acum commit-uit in git (115 fisiere; `appsettings.Local.json`, `bin`/`obj` si `App/*.zip` raman ignorate). Cu aplicatia urmarita, motivul din 2026-05-18 pentru `bgIsolation: "none"` nu mai e valabil — un worktree contine acum sursa aplicatiei — deci setarea este revocata la valoarea implicita a harness-ului.
**De ce**: Acea intrare spunea explicit sa se revoce "daca/cand `App/` devine urmarit in git." Tocmai s-a intamplat, deci sesiunile bg isi recapata garda de izolare.
**Aplicare**: `.claude/` este in `.gitignore`, deci asta e o schimbare doar pe masina locala; oglindeste-o pe orice alta masina care avea opt-out-ul.
