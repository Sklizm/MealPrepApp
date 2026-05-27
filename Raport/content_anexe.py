"""
Anexele raportului:
  A1 — Listingul complet al codului sursa (citit din fisierele reale).
  A2 — Configurarea si folosirea asistentilor Claude Code, Codex si Hermes Agent.
  A3 — Documentarea in Obsidian (vault bilingv, resume protocol).
  A4 — Fluxul de lucru cu Git (branch / commit / push / pull / rebranch).
  A5 — Checklist de conformitate cu cerintele raportului.
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
    "Database/15_recipe_drafts.sql",
    "Database/16_recipe_photos.sql",
    "Database/17_unit_conversions.sql",
    "Database/18_ingredient_nutrition.sql",
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
    "Database/procs/12_recipe_drafts.sql",
    "Database/procs/13_recipe_photos.sql",
    "Database/procs/14_nutrition.sql",
    "Database/seeds/units_seed.sql",
    "Database/seeds/categories_seed.sql",
    "Database/seeds/ingredient_categories_seed.sql",
    "Database/seeds/ingredients_seed.sql",
    "Database/seeds/ingredient_nutrition_seed.sql",
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
    "App/MealPrepApp/Data/Repositories/DraftRepository.cs",
    "App/MealPrepApp/Data/Repositories/NutritionRepository.cs",
    "App/MealPrepApp/Services/SessionService.cs",
    "App/MealPrepApp/Services/NavigationService.cs",
    "App/MealPrepApp/Services/DialogService.cs",
    "App/MealPrepApp/Services/BcryptPasswordHasher.cs",
    "App/MealPrepApp/ViewModels/ViewModelBase.cs",
    "App/MealPrepApp/ViewModels/Auth/LoginViewModel.cs",
    "App/MealPrepApp/ViewModels/Auth/ForgotPasswordViewModel.cs",
    "App/MealPrepApp/ViewModels/Retete/ReteteEditorViewModel.cs",
    "App/MealPrepApp/ViewModels/Retete/ReteteDetailViewModel.cs",
    "App/MealPrepApp/ViewModels/Ingrediente/ShoppingListViewModel.cs",
    "App/MealPrepApp/ViewModels/Ingrediente/IngredientNutritionDialogViewModel.cs",
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

    # ---------------------- A2 — Claude Code, Codex si Hermes ---------------------- #
    h.heading(doc, "Anexa A2 — Claude Code, Codex si Hermes Agent", level=2)
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
    h.heading(doc, "Trecerea la Codex si Hermes Agent", level=3)
    h.body(doc,
        "Dupa etapa lucrata cu Claude Code, fluxul a fost mutat catre un ecosistem mai flexibil: Codex CLI "
        "pentru sarcini autonome de programare si Hermes Agent ca asistent local principal. Aceasta schimbare "
        "nu modifica proiectul in sine, ci modul de lucru: in locul unui singur asistent, se poate folosi un "
        "agent configurabil, cu memorie persistenta, tool-uri locale, cautare in sesiuni, skill-uri si acces la "
        "modele diferite, inclusiv GPT-5.5 atunci cand este disponibil prin providerul configurat.")
    h.body(doc,
        "Codex este agentul de programare al OpenAI pentru terminal. El poate citi repository-ul Git, poate "
        "modifica fisiere, rula teste si propune reparatii/refactorizari. Este util pentru sarcini izolate, "
        "precum implementarea unei functionalitati, verificarea unui bug sau revizuirea unei diferente Git. "
        "Fiind un agent de cod, trebuie rulat intr-un repository Git si trebuie folosit cu atentie la optiunile "
        "de auto-aprobare.")
    h.heading(doc, "Instalarea si folosirea Codex CLI", level=3)
    h.numbered(doc, "Se instaleaza Node.js LTS, apoi Codex CLI prin npm.")
    h.code_block(doc, "npm install -g @openai/codex")
    h.numbered(doc, "Se configureaza autentificarea OpenAI: fie prin cheia OPENAI_API_KEY, fie prin fluxul de login/OAuth al Codex CLI.")
    h.code_block(doc,
        "# varianta cu cheie API, daca este folosita\n"
        "export OPENAI_API_KEY='<cheia_ta_openai>'\n"
        "# apoi, din repository\n"
        "cd ~/Practica\n"
        "codex")
    h.numbered(doc, "Pentru o sarcina unica se poate folosi modul exec, care porneste agentul, executa promptul si iese.")
    h.code_block(doc,
        "cd ~/Practica\n"
        "codex exec 'Analizeaza modificarile curente si propune teste de regresie'")
    h.numbered(doc, "Pentru sarcini de implementare se poate folosi auto-aprobarea controlata in workspace; modul fara protectii trebuie evitat daca nu se intelege riscul.")
    h.code_block(doc,
        "codex exec --full-auto 'Adauga o validare lipsa si ruleaza testele relevante'\n"
        "# --yolo exista, dar este riscant: elimina sandbox-ul si aprobarile")

    h.heading(doc, "Instalarea si folosirea Hermes Agent", level=3)
    h.body(doc,
        "Hermes Agent este asistentul local folosit in etapa finala a proiectului. Spre deosebire de un "
        "agent strict de cod, Hermes este un cadru general: poate lucra cu fisiere, terminal, browser, cron, "
        "memorie persistenta, skill-uri si mai multi provideri de modele. In acest proiect a fost folosit ca "
        "„agent nou angajat” pentru a prelua contextul din Vault, a actualiza raportul si a verifica cerintele.")
    h.numbered(doc, "Instalarea se face cu scriptul oficial, apoi se ruleaza wizard-ul de configurare.")
    h.code_block(doc,
        "curl -fsSL https://raw.githubusercontent.com/NousResearch/hermes-agent/main/scripts/install.sh | bash\n"
        "hermes setup\n"
        "hermes doctor")
    h.numbered(doc, "Pornirea interactiva din proiect se face din folderul repository-ului.")
    h.code_block(doc,
        "cd ~/Practica\n"
        "hermes")
    h.numbered(doc, "Pentru o singura intrebare sau sarcina se poate folosi modul non-interactiv.")
    h.code_block(doc,
        "hermes chat -q 'Citeste Vault/TODO.md si spune-mi urmatorul pas'")
    h.numbered(doc, "Modelul se schimba prin selectorul interactiv; daca GPT-5.5 este disponibil in cont/provider, se alege din lista.")
    h.code_block(doc,
        "hermes model\n"
        "# sau explicit, in functie de providerul configurat:\n"
        "hermes chat --provider openrouter --model openai/gpt-5.5 -q 'Verifica raportul'")
    h.numbered(doc, "Pentru providerul OpenAI Codex gestionat de Hermes se poate adauga autentificarea prin comanda de auth.")
    h.code_block(doc,
        "hermes auth add openai-codex\n"
        "hermes model")
    h.body(doc,
        "In practica, colaborarea devine urmatoarea: Obsidian pastreaza memoria proiectului, Git pastreaza "
        "istoricul verificabil al codului, Codex poate fi folosit pentru sarcini autonome de programare, iar "
        "Hermes coordoneaza lucrul zilnic, citeste contextul, executa verificari si mentine raportul actualizat.")

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
    h.heading(doc, "Conceptul de vault si legaturi intre note", level=3)
    h.body(doc,
        "Un vault Obsidian este un folder obisnuit de fisiere Markdown, dar organizat ca o retea de cunostinte. "
        "Fiecare nota poate trimite la alta prin legaturi de forma [[Nume nota]], iar Obsidian construieste "
        "automat o harta a relatiilor. Pentru proiect, aceasta abordare a fost mai potrivita decat un document "
        "unic, deoarece schema bazei de date, deciziile, sesiunile si TODO-urile evolueaza independent, dar "
        "trebuie sa ramana conectate.")
    h.body(doc,
        "Decisions Log functioneaza ca un ADR (Architecture Decision Record): nu se sterg deciziile vechi, ci "
        "se adauga intrari noi care explica de ce o decizie a fost schimbata. Astfel, raportul nu arata doar "
        "rezultatul final, ci si rationamentul: de ce ingredientele sunt globale, de ce accesul la baza de date "
        "se face numai prin proceduri, de ce lista de cumparaturi este calculata si nu stocata etc.")
    h.body(doc,
        "Notele de sesiune au rolul de jurnal tehnic. Cand contextul unei conversatii se pierde sau se trece "
        "la un alt agent, acesta poate relua lucrul citind Indexul, ultima sesiune, TODO-ul si jurnalul de "
        "decizii. In practica, vault-ul devine memoria externa a proiectului, independenta de un anumit model AI.")
    h.heading(doc, "Reguli practice pentru intretinerea vault-ului", level=3)
    h.bullet(doc, "Notele trebuie sa fie scurte, clare si legate intre ele; informatia temporara ramane in Sessions, iar deciziile stabile intra in Decisions Log.")
    h.bullet(doc, "Dupa modificari de schema, notele din Vault/Database/ trebuie sincronizate cu scripturile SQL.")
    h.bullet(doc, "TODO.md se foloseste pentru prioritati curente, nu pentru istoric complet; ce este finalizat se muta la Done.")
    h.bullet(doc, "Raportul poate prelua idei din vault, dar daca exista contradictii intre documentatie si cod, codul verificat este sursa finala.")

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
    h.heading(doc, "Concepte Git folosite in proiect", level=3)
    h.body(doc,
        "Git este sistemul de control al versiunilor folosit pentru a pastra istoricul complet al proiectului. "
        "Un commit reprezinta o fotografie logica a modificarilor, o ramura (branch) permite lucrul izolat la "
        "o functionalitate, iar merge-ul aduce inapoi rezultatul verificat in ramura principala. Remote-ul "
        "GitHub ofera backup si posibilitatea de a lucra de pe mai multe masini.")
    h.bullet(doc, "Working tree — fisierele asa cum exista pe disc, inainte de a fi pregatite pentru commit.")
    h.bullet(doc, "Staging area — zona intermediara populata cu git add, unde se aleg exact modificarile care intra in commit.")
    h.bullet(doc, "Commit — punct verificabil in istoric; trebuie sa descrie o singura schimbare coerenta.")
    h.bullet(doc, "Branch — linie paralela de lucru pentru o functie sau reparatie, fara a destabiliza main.")
    h.bullet(doc, "Merge — integrarea unei ramuri verificate in main.")
    h.bullet(doc, "Remote — copia de pe GitHub, folosita pentru push/pull si siguranta datelor.")
    h.heading(doc, "Comenzi de verificare si siguranta", level=3)
    h.body(doc, "Inainte de commit se verifica starea repository-ului si diferentele exacte:")
    h.code_block(doc,
        "git status\n"
        "git diff\n"
        "git diff --check")
    h.body(doc, "Pentru a vedea istoricul intr-o forma compacta:")
    h.code_block(doc, "git log --oneline --graph --decorate --all")
    h.body(doc, "Pentru a evita includerea secretelor sau fisierelor generate, se foloseste .gitignore:")
    h.code_block(doc,
        "# exemple relevante pentru proiect\n"
        "App/MealPrepApp/appsettings.Local.json\n"
        "bin/\n"
        "obj/\n"
        "*.user")
    h.body(doc,
        "Legatura dintre Git si Obsidian este importanta: nu se versioneaza doar codul, ci si rationamentul "
        "din vault. Astfel, repository-ul contine atat produsul program, cat si explicatia deciziilor, ceea ce "
        "ajuta la redactarea raportului si la reluarea lucrului dupa pauze lungi.")

    # ---------------------- A5 — Checklist cerinte raport ---------------------- #
    h.heading(doc, "Anexa A5 — Verificarea cerintelor raportului", level=2)
    h.body(doc,
        "Tabelul urmator leaga explicit cerintele indrumarului de locul in care sunt acoperite in raport. "
        "Este inclus pentru control intern si pentru a demonstra ca structura ceruta a fost respectata.")
    h.simple_table(
        doc,
        ["Cerinta", "Unde este acoperita"],
        [
            ["Foaie de titlu", "Prima pagina a documentului; campurile marcate cu [...] se completeaza dupa modelul oficial al institutiei."],
            ["Cuprins", "Sectiunea Cuprins, camp TOC nativ Word actualizabil cu F9."],
            ["Introducere", "Sectiunea Introducere, cu tema, scopul si obiectivele O1–O5."],
            ["Continutul activitatilor si sarcinilor", "Sectiunea Continutul activitatilor si sarcinilor de lucru."],
            ["Descrierea modului de elaborare", "Subsectiunea Descrierea modului de elaborare a produsului program."],
            ["Listingul produsului program", "Subsectiunea Listingul produsului program si Anexa A1."],
            ["Date de intrare", "Tabelul 1 — coloana Date de intrare."],
            ["Date de iesire", "Tabelul 1 — coloana Date de iesire (rezultat)."],
            ["Functionalitatea produsului program", "Subsectiunea Functionalitatea produsului program si figurile rezervate capturilor."],
            ["Observatii generale. Concluzie", "Sectiunea Observatii generale. Concluzie, cu rezultate, dificultati, directii viitoare si comentariu personal."],
            ["Bibliografie/Webografie", "Sectiunea Bibliografie (Webografie), lista numerotata de surse."],
            ["Anexe", "Sectiunea Anexe A1–A5."],
            ["Setari pagina si fonturi", "Aplicate automat de docx_helpers.py: A4, margini 30/20/20/10 mm, TNR 12/14, Courier New 10, numerotare jos-centru."],
        ],
        caption="Tabelul 2 — Checklist de conformitate cu cerintele raportului."
    )


def _file_listing(doc, h, relpath, max_lines=400):
    h.heading(doc, relpath, level=3)
    h.code_block(doc, read_repo_file(relpath), max_lines=max_lines)
