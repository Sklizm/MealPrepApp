---
tags: [todo, ro]
---

# TODO (Romana)

Lista de verificare in desfasurare. Cele mai recente iteme sunt sus.

## Acum
- [ ] **Verificare publish/runtime Windows exe** — ruleaza `App\publish-windows-exe.cmd` pe masina/VM-ul Windows al Ritei; confirma ca `MealPrepApp.exe` este creat si porneste cu un `appsettings.Local.json` valid

## Curand
- [ ] ~~Optional: adauga view-uri de comoditate pentru explorare ad-hoc in DataGrip (read-only, grant-uri separate daca ar fi expuse aplicatiei)~~
- [ ] ~~Optional: adauga un rol de administrator pentru migrari, separat de `sa`~~
- [ ] ~~Optional: `sp_GetAuditForUser` daca aplicatia va avea un feed "activitatea ta"~~

## Poate mai tarziu (in afara scopului v1, vezi [[Decisions Log-ro]])
- [ ] Ingrediente private per utilizator (adauga `UserID` nullable la [[Ingredients-ro]])

## Facute
- [x] **README si contraparti romanesti in Vault actualizate** — `README.md` documenteaza mai detaliat DB/app/features; au fost adaugate contraparti `-ro` lipsa pentru notele de baza de date si sesiuni; schema overview si indexurile au fost actualizate pentru drafturi, poze, conversii unitati si nutritie
- [x] **Raportul de practica regenerat si verificat pe cerinte** — `Raport/Raport_practica.docx` si `.pdf` au fost regenerate; raportul reflecta functionalitatile finale (forgot password, drafturi, poze, loading, nutritie, Windows exe publishing) si include Anexa A5
- [x] **Calea de publish Windows exe implementata** — profil win-x64 self-contained single-file, `App\publish-windows-exe.cmd`, template sigur `appsettings.Local.template.json` si instructiuni README; verificarea reala WPF ramane pe Windows
- [x] **Seed comun de nutritie adaugat** — `seeds/ingredient_nutrition_seed.sql` insereaza valori demo pentru ingredientele comune si pastreaza valorile editate manual
- [x] **Nutritia verificata pe masina/VM-ul Ritei** — editarea nutritiei ingredientelor si afisarea nutritiei pe reteta functioneaza
- [x] **Fundatia de nutritie implementata** — `UnitConversions`, `IngredientNutrition`, proceduri de nutritie, dialog de editare si card `Nutritie estimata`
- [x] **Resetarea parolei verificata pe masina/VM-ul Ritei** — flow-ul din login functioneaza end-to-end
- [x] **Resetarea parolei implementata** — `Ai uitat parola?`, `ForgotPasswordDialog`, `sp_ResetForgottenPassword`
- [x] **Itemul de ingredient lipsa din editor a fost abandonat** — Codrin a prioritizat forgot-password/change-password
- [x] **Loading standalone si Photos UI verificate pe masina Ritei**
- [x] **Fereastra standalone de loading inainte de shell implementata**
- [x] **Loading screen dupa login implementat** — ulterior inlocuit de fereastra standalone pre-shell
- [x] **Photos UI implementat** — adauga/schimba/sterge poza, persistenta prin proceduri si thumbnails in lista
- [x] **Drafts UI verificat de Codrin** — flow open/save/delete functioneaza; wording-ul ramane `Drafts` / `Salveaza ca draft`
- [x] **Drafts UI implementat** — repository, lista Drafts, open/delete si salvare draft din editor
- [x] **Scripturile DB pentru drafturi/poze verificate prin `run_all.sql`**
- [x] **Origin URL actualizat** — `https://github.com/Sklizm/MealPrepApp.git`
- [x] **Branch-ul `fix-recipe-field-limits` merge-uit**
- [x] **Planificare si Rapoarte livrate** — Planificare lunara/saptamanala, rapoarte, print/export
- [x] **Crash-ul la salvare reteta rezolvat** — duplicat ingredient + mapare erori SQL
- [x] **Restilizare UI confirmata pe PC-ul Ritei + VM Windows 11**
- [x] **`App/` commit-uit in git** — proiectul nu mai este doar DB
- [x] Faza F (Ingrediente / Frigider / Lista cumparaturi) confirmata pe PC-ul Ritei
- [x] Fazele A-E WPF confirmate pe PC-ul Ritei
- [x] Seed ingrediente tradus in romana fara diacritice; `AppPassword` default gol pentru rebuild-uri
- [x] Faza 4 — design + `IngredientCategories` + proceduri profil/rapoarte
- [x] Faza 3 — meal planning, favorite, pantry, shopping list
- [x] Faza 2.5 — seed ingrediente, indexuri FK, `RowVersion`, optimizare find-by-ingredients
- [x] Faza 2 — API + securitate, `mealprep_app`, audit, password history, lockout
- [x] Faza 1 — schema de baza, seed-uri, `run_all.sql`, vault Obsidian
