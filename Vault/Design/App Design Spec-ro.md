---
tags: [design, spec, app]
---

# Specificatie de Design a Aplicatiei — Planificator de Retete si Mese

Scris pe 2026-05-11, dupa sesiunea de revizuire a designului cu Codrin. Acesta este documentul din care Margarita extrage informatii pentru urmatoarea revizie a machetei in Canva. Deciziile sursa sunt in [[Decisions Log]]; acest document captureaza doar concluziile de design.

Partea de baza de date suporta deja tot ce este descris aici — vezi [[Database/Schema Overview]] pentru lista completa de proceduri pe fiecare zona.

## Decizii de design stabilite

| Subiect | Decizie |
|---|---|
| Cadrul UI | WPF + MVVM (CommunityToolkit.Mvvm) + Dapper |
| Autentificare in UI | Completa: ecrane de Login, Register, Profil, Schimbare parola |
| Model de navigare | Taburi sus: Acasa / Retete / Ingrediente / Planificare / Rapoarte. Dropdown utilizator dreapta-sus inlocuieste linkul "Iesire ->" din macheta. |
| Click pe cardul de reteta | Vizualizare detaliata pe ecran complet (inlocuieste zona listei; tab-strip ramane). Detaliul are butoane Editeaza / Sterge / Adauga la favorite / Adauga la plan / Inapoi. |
| Editor reteta | Aceeasi structura ca detaliul, dar fiecare camp este un input de formular; ingredientele sunt un tabel editabil cu adaugare/eliminare randuri. |
| Adaugare in calendar | Click pe celula goala → modal "Plan meal" cu data + slot pre-completate. Fara drag-drop in v1. |
| Bara laterala Categorii (Ingrediente) | Grupare reala din baza de date — sustinuta de tabelul `IngredientCategories` (8 categorii). |
| Rapoarte | Trei sub-taburi: Statistici lunare / Plan saptamanal pentru tiparire / Lista cumparaturi pentru tiparire |
| Butonul "Iesire" | Deconectare (revine la ecranul de login). X-ul ferestrei inchide aplicatia. |
| Scurtatura de planificare | Detaliul retetei are butonul "Adauga la plan" — deschide modalul Plan meal pre-completat cu acea reteta |
| Model de selectie | Fara selectie-apoi-actiune. Fiecare card/rand are propriile actiuni per-rand (click-dreapta sau iconuri afisate la hover). Editarea = click pe card → detaliu → Editeaza. |
| Asincron | Fiecare apel de procedura este asteptat; butoanele se dezactiveaza pe durata operatiei; indicator mic de progres in bara de stare. |
| Semantica salvarii | Editorul comite la Salveaza, nu la pierderea focusului. Renunta cu modificari nesalvate afiseaza confirmare. |
| Concurenta optimista | Editorul retine `RowVersion` de la incarcare; la eroarea 50004 afiseaza dialogul "Reteta a fost modificata in alta sesiune. Reincarca." |
| Limba | Doar romana in v1. Stringurile traiesc in `Resources.ro.resx` astfel incat traducerea EN sa fie un simplu schimb de resx in viitor. |
| Paleta | Maslin / crem / maro inchis pe toate ecranele (fara comutator de teme). |

Deja stabilite anterior: scop complet pentru planificator (planificator + camara + lista de cumparaturi); tabelul Categories = sloturi de masa (planificatorul reutilizeaza aceeasi lista Categories; UI-ul afiseaza 4 din 6 ca si coloane saptamanale); stergere hard (fara arhivare/stergere soft); fara `PricePerServing`, fara `ImagePath`, fara categorii de tip de mancare separate de sloturile de masa; lista de cumparaturi este calculata, niciodata stocata; fara conversie intre unitati in v1.

## Inventar de ecrane

20 ecrane / dialoguri distincte.   ✅ = exista in macheta curenta,  🆕 = nou (de desenat).

### Autentificare (4 ecrane) — toate 🆕

1. **Login** — Camp Username/Email, camp parola, buton "Conectare", link "Cont nou? Inregistreaza-te", slot pentru mesaje de blocare/eroare. Sustinut de `sp_GetUserForLogin` + `sp_RecordLoginSuccess` / `sp_RecordLoginFailure`.
2. **Register** — Username, Email, Parola, Confirmare parola, buton "Inregistreaza-te", link "Ai cont? Conecteaza-te", validare inline. Sustinut de `sp_RegisterUser`.
3. **Profil** — Username (doar citire), Email (editabil), CreatedAt, LastLoginAt. Butoane: "Schimba parola", "Iesi din cont". Accesat printr-un avatar/meniu de utilizator in dreapta-sus a shell-ului. Sustinut de `sp_GetUserProfile` (adaugare in Faza 4 — nu expune `PasswordHash`).
4. **Modal Schimbare parola** — Parola curenta, Parola noua, Confirmare parola noua. Afiseaza eroarea 50001 (parola reutilizata) ca mesaj inline. Sustinut de `sp_ChangePassword`.

### Shell

5. **Shell-ul aplicatiei** ✅ partial — banda de taburi sus + bara de stare. Taburi: Acasa / Retete / Ingrediente / Planificare / Rapoarte. Dreapta-sus: dropdown de avatar utilizator (Profil / Iesi din cont). Textul barii de stare se schimba per tab (deja desenat corect in macheta).

### Acasa (1 ecran) ✅

6. **Dashboard Acasa** ✅ — Rand de placi KPI + grila "Retete Recente". Placile sunt clicabile: click pe "5 Retete active" navigheaza la Retete; click pe "7 Mese planificate" navigheaza la Planificare. Stare goala: salut + CTA "Adauga prima reteta" cand toate contoarele sunt 0. Sustinut de `sp_GetDashboardCounts` + `sp_GetRecentRecipes`.

### Retete (3 ecrane)

7. **Lista Retete** ✅ — exista. *De corectat*: elimina elementul "Archiveaza/Sterge" din bara de unelte, lasa doar "Sterge". *De corectat*: elimina textul `aaa` din stanga-jos. Intrarile din bara laterala Toate / Favorite / Recente sunt sustinute de `sp_GetRecipes`, `sp_GetFavoriteRecipes`, `sp_GetRecentRecipes`. Cautarea + filtrul de categorie folosesc `sp_SearchRecipesByTitle` / `sp_GetRecipes(@CategoryID)`.
8. **Detaliu Reteta** 🆕 — vizualizare pe ecran complet (inlocuieste zona listei; tab-strip-ul + bara laterala raman vizibile). Aspect: header (titlu, badge categorie, timp de pregatire + gatire, portii, autor), descriere, instructiuni (text randat), lista de ingrediente (nume + cantitate + unitate + observatii). Butoane de actiune in header: ⭐ "Adauga la favorite" (toggle, reflecta starea curenta), "Adauga la plan" (deschide modalul Plan meal pre-completat), "Editeaza", "Sterge", "Inapoi". Sustinut de `sp_GetRecipeFull` (intoarce `RowVersion`).
9. **Editor Reteta** 🆕 — aceeasi structura ca detaliul, dar fiecare camp este un input de formular. Lista de ingrediente este un tabel editabil (Adauga rand / Elimina rand, autocomplete pentru ingrediente prin `sp_SearchIngredients`, dropdown de unitati prin `sp_GetUnits`, cantitate, observatii). Dropdown de categorie prin `sp_GetCategories`. Butoane: "Salveaza", "Renunta". La salvare: `sp_CreateRecipe` (nou) sau `sp_UpdateRecipe` (editare; transmite `@RowVersion` de la incarcare; la 50004 afiseaza dialogul de conflict cu optiune de reincarcare). Limita soft de 500 caractere pe textbox-ul de Observatii.

### Ingrediente (5 ecrane / sub-vizualizari)

10. **Lista Ingrediente** ✅ — exista. *De corectat*: elimina "Archiveaza" din bara de unelte. Elementele din bara laterala: **Toate** (lista completa, `sp_GetIngredients`), **Categorii** (grupate — vezi tiparul de randare a Categoriilor mai jos), **Frigider** (link catre vizualizarea camara #11), **Lista de cumparaturi** (link catre lista de cumparaturi #13). Celulele grilei trebuie sa afiseze nume + badge de unitate implicita — sa nu fie placeholdere goale.
11. **Frigider (camara)** 🆕 — inlocuieste zona listei cand "Frigider" este selectat in bara laterala. Tabel compact: nume ingredient, cantitate, unitate, ultima actualizare. Actiuni pe rand: editeaza cantitatea, elimina. Bara de unelte: "+ Adauga in frigider" (deschide modalul de adaugare in camara #12). Sustinut de `sp_GetPantry`, `sp_AddPantryItem`, `sp_UpdatePantryQuantity`, `sp_RemovePantryItem`.
12. **Modal Adauga/Editeaza camara** 🆕 — Autocomplete ingredient + Dropdown unitate + Cantitate. Fluxul de adaugare foloseste `sp_AddPantryItem` (MERGE-upsert; acelasi ingredient+unitate se acumuleaza). Fluxul de editare foloseste `sp_UpdatePantryQuantity` (setare absoluta). Acelasi UI; difera doar starea initiala si procedura apelata.
13. **Lista de cumparaturi** 🆕 — selector de interval de date (implicit: saptamana aceasta). Tabel: ingredient, cantitate necesara, cantitate la indemana, **cantitate de cumparat**, unitate. Bara de unelte: "Export Excel", "Tipareste". Sustinut de `sp_GetShoppingList(@UserID, @StartDate, @EndDate)`. Stare goala: "Nu sunt mese planificate in intervalul selectat".
14. **Modal Adauga ingredient** 🆕 — Nume + dropdown unitate implicita + dropdown categorie (`sp_GetIngredientCategories`). Modal mic declansat de "+ Adauga" din lista Ingrediente. Sustinut de `sp_AddIngredient`. Fara modal separat de editare in v1.

### Planificare (3 ecrane / sub-vizualizari)

15. **Planificare — Calendar (lunar)** ✅ — exista. Grila 7×6. **Buget de continut pe celula**: numar de data + pana la 4 jetoane colorate mici (unul per slot de masa ocupat), fiecare jeton afisand contorul de elemente (sau doar un punct daca este 1). Click pe un jeton → popover/modal cu detaliile zilei, afisand planul complet al acelei zile. Click pe o celula goala → modal Plan meal (#17) cu data pre-completata si slot pre-setat la Mic dejun. Sustinut de `sp_GetMonthlyPlan`.
16. **Planificare — Saptamanal** ✅ — exista. Randuri = date, coloane = Mic dejun / Pranz / Cina / Gustare. *De corectat*: bullets reale cu titluri de retete in loc de `bla bla`. Fiecare celula poate avea mai multe elemente (un bullet per intrare). Celula goala → click → modal Plan meal pre-completat. Sustinut de `sp_GetWeeklyPlan`.
17. **Modal Plan meal** 🆕 — se deschide din orice celula goala a calendarului SAU din "Adauga la plan" pe ecranul de detaliu reteta. Campuri: Data (date picker, pre-completat), Slot de masa (dropdown din `sp_GetCategories`, pre-completat cu coloana clicata sau categoria implicita a retetei), Reteta (autocomplete prin `sp_SearchRecipesByTitle`, pre-completat daca venim din detaliul retetei), Portii (numar, implicit valoarea Servings a retetei), Observatii (text optional). Butoane: "Salveaza", "Renunta". La salvare: `sp_PlanMeal` (nou) sau `sp_UpdatePlannedMeal` (editare). Click pe o intrare de plan existenta deschide acelasi modal in mod editare cu un buton suplimentar "Sterge" (→ `sp_UnplanMeal`).

### Rapoarte (1 tab cu 3 sub-taburi) — 🆕

18. **Rapoarte** — tab de nivel superior cu trei sub-taburi:
    - **Statistici lunare** — selector de luna; total mese planificate, contoare pe fiecare slot, top 5 retete (`sp_GetTopRecipes`), top 10 ingrediente (`sp_GetTopIngredients`), retete/ingrediente distincte folosite (`sp_GetMonthlyStats`).
    - **Plan saptamanal pentru tiparire** — selector de saptamana; planul saptamanal intr-un layout prietenos pentru imprimanta (fara bare de unelte, prietenos cu monocrom). Butoane "Tipareste" + "Export Excel". Citeste `sp_GetWeeklyPlan`.
    - **Lista cumparaturi pentru tiparire** — aceeasi structura; citeste `sp_GetShoppingList`.

### Componente transversale (4 tipare) — toate 🆕

19. **Dialog de confirmare** — folosit pentru fiecare actiune distructiva: "Sterge reteta?" / "Sterge intrarea din plan?" / "Sterge ingredientul din frigider?". O singura componenta parametrizata (titlu + corp + eticheta buton pericol).
20. **Dialog / toast de eroare** — afisaj generic pentru `SqlException`. Mapeaza cele 4 coduri de eroare personalizate la mesaje prietenoase:
    - **50001** → "Aceasta parola a fost folosita recent. Alege o parola noua."
    - **50002** → "Nu ai permisiunea pentru aceasta actiune."
    - **50003** → "Elementul nu a fost gasit."
    - **50004** → "Reteta a fost modificata in alta sesiune. Reincarca si reincearca."
    - Orice alt `SqlException` → "Eroare neasteptata" cu sectiune pliabila "Detalii".
21. **Stare goala** — una per ecran de lista/dashboard:
    - Acasa cu 0 retete: "Bun venit! Incepe prin a adauga prima reteta." + buton CTA
    - Retete cu 0 retete: acelasi CTA inline
    - Frigider gol: "Frigiderul este gol. Adauga produsele pe care le ai."
    - Lista cumparaturi goala: "Nu sunt mese planificate in intervalul selectat."
    - Calendar Planificare gol: doar randeaza grila goala (numere de date vizibile, fara jetoane)
22. **Stare de incarcare** — shimmer sau spinner per lista in timp ce procedurile ruleaza. ViewModel-urile asincrone garanteaza ca un click nu va ingheta UI-ul.

## Lista de corectii a Margaritei — corectii pe macheta existenta

- **p1**: 3 placi — asigura-te ca toate par interactive (chenar uniform la focus, nu doar cea umpluta cu verde). Optional: adauga o a 4-a placa "Favorite (N)".
- **p1**: Cardurile "Retete Recente" sunt cutii goale — schiteaza continutul real al cardului (titlu, badge categorie, timp).
- **p2**: elimina "Archiveaza" — bara de unelte devine "+ Adauga / Sterge / Export Excel" (fara arhivare in v1).
- **p2 + p3**: elimina textul `aaa` din stanga-jos (placeholder Canva).
- **p3**: intrarea "Categorii" din bara laterala se refera acum la o grupare reala din baza de date — proiecteaza cum sunt randate listele grupate (headere pliabile? lista indentata? panou separat?).
- **p3**: sub-vizualizarea "Frigider" nu este desenata — rezerva o pagina de macheta (vezi ecranul #11 mai sus).
- **p3**: sub-vizualizarea "Lista de cumparaturi" nu este desenata — rezerva o pagina de macheta (vezi ecranul #13).
- **p4**: celulele lunare afiseaza in prezent "26 bla bla bla" de 42 de ori. Schiteaza o celula complet desenata cu reprezentarea jetoane-si-contor; restul pot ramane placeholder.
- **p5**: celulele saptamanale afiseaza "bla bla" — inlocuieste cu titluri de retete de exemplu pentru a confirma ca tipografia se potriveste.
- **p5**: celulele trebuie sa diferentieze "planificat" (bullet cu titlu de reteta) de "gol" (subtil "+" la hover).
- **p6**: arunca. Dashboard-ul de pe pagina 6 este modelul de navigare alternativ pe care l-am respins.

## Pagini noi de macheta de adaugat

Unsprezece ecrane noi, cate o pagina Canva fiecare:

1. Login
2. Register
3. Profil + plasarea dropdown-ului de utilizator
4. Modal Schimbare parola
5. Detaliu reteta
6. Editor reteta
7. Vizualizare lista Frigider
8. Modal Adauga/Editeaza camara
9. Ecran Lista de cumparaturi
10. Modal Plan meal (acopera atat intrarea din calendar cat si scurtatura din detaliul retetei)
11. Tab Rapoarte (afisand cele trei sub-taburi ca o banda)

Plus cateva artefacte mici care sunt reutilizate pe ecrane:
- Sablon dialog de confirmare
- Sablon toast / dialog de eroare
- Componenta stare goala

## Ce suporta baza de date acum (pentru context app)

Referinta rapida a procedurilor pe care aplicatia le poate apela deja. Detalii complete in [[Database/Schema Overview]].

| Zona | Proceduri |
|---|---|
| Utilizatori / autentificare | `sp_RegisterUser`, `sp_GetUserForLogin` (doar fluxul de login), `sp_GetUserProfile` (ecran Profil), `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword` |
| Retete (scriere) | `sp_CreateRecipe`, `sp_UpdateRecipe` (necesita `@RowVersion`), `sp_DeleteRecipe` |
| Retete (citire) | `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients` |
| Ingrediente | `sp_AddIngredient`, `sp_GetIngredients(@IngredientCategoryID = NULL)`, `sp_SearchIngredients`, `sp_GetIngredientUsage` |
| Lookup-uri | `sp_GetUnits`, `sp_GetCategories`, `sp_GetIngredientCategories` |
| Plan de masa | `sp_PlanMeal`, `sp_UpdatePlannedMeal`, `sp_UnplanMeal`, `sp_GetWeeklyPlan`, `sp_GetMonthlyPlan` |
| Favorite | `sp_ToggleFavorite`, `sp_GetFavoriteRecipes` |
| Camara | `sp_AddPantryItem` (MERGE upsert), `sp_UpdatePantryQuantity`, `sp_RemovePantryItem`, `sp_GetPantry` |
| Lista de cumparaturi | `sp_GetShoppingList(@UserID, @StartDate, @EndDate)` — calculata |
| Dashboard | `sp_GetDashboardCounts`, `sp_GetRecentRecipes` |
| Rapoarte | `sp_GetMonthlyStats`, `sp_GetTopRecipes`, `sp_GetTopIngredients` |

## Ce NU acopera aceasta specificatie

- Aspecte vizuale specifice (dimensiuni de tipografie, padding-uri exacte in pixeli, set de iconuri) — Margarita le detine.
- Structura codului WPF (ViewModels, Repositories, container DI) — in afara scopului documentului de design; acela este teritoriul de implementare al lui Codrin + Margarita.
- O foaie de parcurs dincolo de v1.

## Urmatorul pas

Cand Margarita livreaza macheta revizuita, bifeaza elementele din aceasta lista si treci la conectarea WPF.
