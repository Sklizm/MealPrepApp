"""
Anexele raportului:
  A1 — Listingul complet al codului sursa (citit din fisierele reale).
  A2 — Configurarea si rularea asistentului Claude Code.
  A3 — Documentarea in Obsidian (vault bilingv, resume protocol).
  A4 — Fluxul de lucru cu Git (branch / commit / push / pull / rebranch).
"""

import os
from content import read_repo_file, REPO


# Fisiere incluse integral in listingul complet (Anexa A1).
DB_FILES = [
    "Database/run_all.sql",
    "Database/00_create_database.sql",
    "Database/01_users.sql",
    "Database/02_units.sql",
    "Database/03_categories.sql",
    "Database/04_ingredients.sql",
    "Database/05_recipes.sql",
    "Database/06_recipe_ingredients.sql",
    "Database/07_users_security.sql",
    "Database/08_audit_log.sql",
    "Database/09_app_role.sql",
    "Database/10_phase25_additions.sql",
    "Database/11_meal_plan.sql",
    "Database/12_favorites.sql",
    "Database/13_pantry.sql",
    "Database/14_ingredient_categories.sql",
    "Database/procs/01_users.sql",
    "Database/procs/02_recipes_write.sql",
    "Database/procs/03_recipes_read.sql",
    "Database/procs/04_ingredients.sql",
    "Database/procs/05_lookups.sql",
    "Database/procs/06_meal_plan.sql",
    "Database/procs/07_favorites.sql",
    "Database/procs/08_pantry.sql",
    "Database/procs/09_shopping_list.sql",
    "Database/procs/10_dashboard.sql",
    "Database/procs/11_reports.sql",
    "Database/seeds/units_seed.sql",
    "Database/seeds/categories_seed.sql",
    "Database/seeds/ingredient_categories_seed.sql",
    "Database/seeds/ingredients_seed.sql",
]

# Fisiere esentiale ale aplicatiei (selectie reprezentativa, incluse integral).
APP_FILES = [
    "App/MealPrepApp/App.xaml.cs",
    "App/MealPrepApp/Data/SqlConnectionFactory.cs",
    "App/MealPrepApp/Data/RepositoryBase.cs",
    "App/MealPrepApp/Data/AppDbException.cs",
    "App/MealPrepApp/Data/DbExceptionMapper.cs",
    "App/MealPrepApp/Data/Repositories/RecipeRepository.cs",
    "App/MealPrepApp/Data/Repositories/UserRepository.cs",
    "App/MealPrepApp/Data/Repositories/MealPlanRepository.cs",
    "App/MealPrepApp/Data/Repositories/ShoppingListRepository.cs",
    "App/MealPrepApp/Data/Repositories/ReportRepository.cs",
    "App/MealPrepApp/Services/SessionService.cs",
    "App/MealPrepApp/Services/NavigationService.cs",
    "App/MealPrepApp/Services/DialogService.cs",
    "App/MealPrepApp/Services/BcryptPasswordHasher.cs",
    "App/MealPrepApp/ViewModels/ViewModelBase.cs",
    "App/MealPrepApp/ViewModels/Auth/LoginViewModel.cs",
    "App/MealPrepApp/ViewModels/Retete/ReteteEditorViewModel.cs",
    "App/MealPrepApp/ViewModels/Ingrediente/ShoppingListViewModel.cs",
    "App/MealPrepApp/ViewModels/Planificare/PlanMealDialogViewModel.cs",
    "App/MealPrepApp/ViewModels/Rapoarte/RapoarteRootViewModel.cs",
    "App/MealPrepApp/ViewModels/Rapoarte/StatisticiLunareViewModel.cs",
]


def anexe(doc, h):
    h.heading(doc, "Anexe", level=1)

    # ---------------------- A1 — Listing complet ---------------------- #
    h.heading(doc, "Anexa A1 — Listingul complet al codului sursa", level=2)
    h.body(doc, "Aceasta anexa contine codul sursa integral. Pentru lizibilitate, fisierele sunt grupate "
                "pe categorii, fiecare cu numele si calea sa.")

    h.heading(doc, "A1.1 — Baza de date (scripturi T-SQL, proceduri, seed-uri)", level=3)
    for rel in DB_FILES:
        if os.path.exists(os.path.join(REPO, rel)):
            _file_listing(doc, h, rel)

    h.heading(doc, "A1.2 — Aplicatia WPF (fisiere esentiale)", level=3)
    h.body(doc, "Selectie reprezentativa din codul aplicatiei (infrastructura de date, servicii si "
                "ViewModel-uri cheie). Codul complet, inclusiv fisierele XAML ale tuturor ecranelor, este "
                "disponibil in depozitul Git al proiectului.")
    for rel in APP_FILES:
        if os.path.exists(os.path.join(REPO, rel)):
            _file_listing(doc, h, rel)

    # ---------------------- A2 — Claude Code ---------------------- #
    h.heading(doc, "Anexa A2 — Configurarea si rularea asistentului Claude Code", level=2)
    h.body(doc,
        "Pe parcursul proiectului a fost folosit Claude Code, asistentul de programare in linie de comanda "
        "al companiei Anthropic, care ruleaza in terminal, in directorul proiectului. Mai jos sunt pasii "
        "reproductibili pentru a ajunge la configuratia folosita.")
    h.heading(doc, "Instalare si pornire", level=3)
    h.numbered(doc, "Se instaleaza Node.js (LTS), apoi asistentul, prin managerul de pachete npm.")
    h.code_block(doc, "npm install -g @anthropic-ai/claude-code")
    h.numbered(doc, "Din directorul proiectului se porneste asistentul cu comanda:")
    h.code_block(doc, "cd ~/Practica\nclaude")
    h.numbered(doc, "La prima rulare se face autentificarea (cont Anthropic / cheie API), urmand instructiunile "
                    "afisate in terminal.")
    h.heading(doc, "Fisierul de instructiuni CLAUDE.md", level=3)
    h.body(doc,
        "In radacina proiectului exista un fisier CLAUDE.md care contine instructiuni permanente pentru "
        "asistent: structura depozitului, comenzile de rulare a bazei de date in Docker, conventiile de "
        "schema si „protocolul de reluare” (resume protocol) bazat pe vault-ul Obsidian. Acest fisier este "
        "citit automat la fiecare pornire si asigura coerenta intre sesiuni.")
    h.heading(doc, "Caracteristici folosite", level=3)
    h.bullet(doc, "Modul plan — asistentul propune un plan detaliat inainte de a modifica fisiere; planul "
                  "este aprobat explicit de utilizator.")
    h.bullet(doc, "Memorie persistenta — fapte despre proiect si preferinte de lucru salvate intre sesiuni.")
    h.bullet(doc, "Executarea comenzilor — rularea de scripturi (sqlcmd, git, build) direct din terminal, "
                  "cu confirmarea utilizatorului.")
    h.body(doc,
        "Acest raport (.docx) a fost, la randul sau, generat printr-un script Python (python-docx) realizat "
        "cu ajutorul asistentului — codul generatorului se afla in folderul Raport/ al proiectului.")

    # ---------------------- A3 — Obsidian ---------------------- #
    h.heading(doc, "Anexa A3 — Documentarea in Obsidian", level=2)
    h.body(doc,
        "Documentatia proiectului este tinuta intr-un vault Obsidian (folderul Vault/), tratat ca sursa "
        "unica de adevar intre sesiunile de lucru. Obsidian este o aplicatie de note interconectate, in "
        "fisiere Markdown locale.")
    h.heading(doc, "Structura vault-ului", level=3)
    h.bullet(doc, "00 - Index.md — pagina-hub a vault-ului.")
    h.bullet(doc, "Project Overview.md, Tech Stack.md — prezentarea proiectului si a tehnologiilor.")
    h.bullet(doc, "Decisions Log.md — jurnal de decizii arhitecturale, completat prin adaugare (append-only): "
                  "chiar si o decizie revizuita ramane, urmata de o noua intrare care o inlocuieste.")
    h.bullet(doc, "Database/ — cate o nota per tabel, sincronizata cu scripturile SQL.")
    h.bullet(doc, "Design/App Design Spec.md — specificatia de design a aplicatiei.")
    h.bullet(doc, "Sessions/ — jurnale de sesiune datate (YYYY-MM-DD - nume).")
    h.bullet(doc, "TODO.md — lista de sarcini, curenta si viitoare.")
    h.heading(doc, "Note bilingve (ro / en)", level=3)
    h.body(doc,
        "Fiecare nota are un corespondent in limba romana, intr-un fisier cu sufixul -ro.md (de exemplu "
        "Decisions Log.md si Decisions Log-ro.md). Cele doua variante se actualizeaza si se versioneaza "
        "impreuna, pentru a nu se desincroniza.")
    h.heading(doc, "Protocolul de reluare (resume protocol)", level=3)
    h.body(doc, "Pentru continuitate intre sesiuni se respecta o rutina fixa:")
    h.numbered(doc, "La inceputul sesiunii: se citesc, in ordine, Indexul, ultimul jurnal de sesiune, "
                    "lista TODO si jurnalul de decizii.")
    h.numbered(doc, "La finalul sesiunii (daca s-a lucrat semnificativ): se adauga un nou jurnal de sesiune, "
                    "se actualizeaza TODO, se completeaza jurnalul de decizii si notele per tabel afectate.")
    h.body(doc, "Regula de aur: daca vault-ul si codul SQL nu coincid, se considera corect codul SQL, iar "
                "vault-ul se actualizeaza.")

    # ---------------------- A4 — Git ---------------------- #
    h.heading(doc, "Anexa A4 — Fluxul de lucru cu Git", level=2)
    h.body(doc,
        "Codul este versionat cu Git, gazduit pe un depozit la distanta (GitHub). Fluxul de lucru folosit "
        "pune accent pe schimbari mici, frecvente si descriptive, lucrate pe ramuri de functionalitate.")
    h.heading(doc, "Principiile fluxului", level=3)
    h.bullet(doc, "Pentru o functionalitate noua se creeaza o ramura proprie, pornita din main.")
    h.bullet(doc, "Se face commit + push la fiecare schimbare mica si coerenta (nu un singur commit urias).")
    h.bullet(doc, "Imbinarea in main se face doar dupa verificarea functionalitatii (pe masina virtuala Windows).")
    h.bullet(doc, "Secretele nu se comit niciodata: appsettings.Local.json (parola contului de aplicatie), "
                  "precum si folderele bin/ si obj/ sunt excluse prin .gitignore.")
    h.heading(doc, "Comenzi uzuale", level=3)
    h.body(doc, "Crearea unei ramuri noi de functionalitate si publicarea ei:")
    h.code_block(doc,
        "git checkout main\n"
        "git pull\n"
        "git checkout -b nume-functionalitate\n"
        "# ... modificari in cod ...\n"
        "git add -A\n"
        "git commit -m \"Descriere scurta a schimbarii\"\n"
        "git push -u origin nume-functionalitate")
    h.body(doc, "Aducerea ultimelor schimbari de pe depozitul la distanta si schimbarea ramurii:")
    h.code_block(doc,
        "git pull                 # aduce si imbina schimbarile de pe ramura curenta\n"
        "git checkout alta-ramura # comuta pe alta ramura\n"
        "git checkout -b ramura-noua-din-main main   # re-ramificare din main")
    h.body(doc, "Imbinarea unei ramuri verificate in main:")
    h.code_block(doc,
        "git checkout main\n"
        "git merge nume-functionalitate\n"
        "git push")
    h.body(doc,
        "Colaborarea utilizator–asistent decurge astfel: asistentul propune un plan, il implementeaza pe o "
        "ramura dedicata si face commit + push la fiecare pas; utilizatorul revizuieste, testeaza pe masina "
        "virtuala si confirma inainte de imbinarea in main. Mesajele de commit sunt concise si descriu o "
        "singura schimbare logica, ceea ce face istoricul usor de citit si de inteles.")


def _file_listing(doc, h, relpath, max_lines=400):
    h.heading(doc, relpath, level=3)
    h.code_block(doc, read_repo_file(relpath), max_lines=max_lines)
