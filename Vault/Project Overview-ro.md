---
tags: [overview]
---

# Privire de ansamblu a proiectului

O aplicatie de **pregatire a meselor / urmarire a retetelor** construita ca proiect de practica.

## Impartirea muncii
- **Partea aplicatiei** — aplicatie .NET construita in Visual Studio. *Nu este responsabilitatea lui Codrin pentru moment*; se va face mai tarziu sau de altcineva.
- **Partea bazei de date** — sarcina lui Codrin. SQL Server ruland in Docker, accesat prin DataGrip.

## Scop pentru v1 (continut de baza)
Livrare a unei baze de date functionale si demonstrabile care suporta:
- Utilizatorii se inregistreaza si se conecteaza (aplicatia .NET gestioneaza autentificarea; DB-ul doar stocheaza hash-urile).
- Utilizatorii creeaza retete, le clasifica pe categorii si listeaza ingrediente cu cantitati/unitati.
- Tabelele de lookup (Units, Categories) sunt pre-populate cu valori implicite rezonabile.

## In afara scopului pentru v1
- Planuri de masa / programari saptamanale
- Generarea listei de cumparaturi
- Urmarirea nutritiei (calorii, macronutrienti)
- Rating-uri de retete sau favorite
- Fotografii / incarcari de imagini

Acestea pot veni mai tarziu — vezi [[Decisions Log-ro]] pentru motivele pentru care le-am taiat.

## Structura repo-ului
```
Practica/
├── Database/           # Toate scripturile SQL (munca lui Codrin)
│   ├── 00_create_database.sql
│   ├── 01_users.sql
│   ├── 02_units.sql
│   ├── 03_categories.sql
│   ├── 04_ingredients.sql
│   ├── 05_recipes.sql
│   ├── 06_recipe_ingredients.sql
│   ├── run_all.sql
│   └── seeds/
│       ├── units_seed.sql
│       └── categories_seed.sql
├── Vault/              # Graf de cunostinte Obsidian (acest folder)
└── (viitor) App/       # Aplicatia .NET (mai tarziu)
```

Vezi si: [[Tech Stack-ro]], [[Database/Schema Overview-ro]]
