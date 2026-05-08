---
tags: [overview]
---

# Project Overview

A **meal preparation / recipe tracking** application built as a school practica.

## Split of Work
- **App side** — .NET application built in Visual Studio. *Not Codrin's responsibility for now*; this happens later or by someone else.
- **Database side** — Codrin's job. SQL Server running in Docker, accessed via DataGrip.

## Goal for v1 (Core Scope)
Ship a working, demoable database that supports:
- Users register and log in (the .NET app handles auth; the DB just stores hashes).
- Users create recipes, classify them by category, and list ingredients with quantities/units.
- Lookup tables (Units, Categories) are seeded with sensible defaults.

## Out of Scope for v1
- Meal plans / weekly schedules
- Shopping list generation
- Nutrition tracking (calories, macros)
- Recipe ratings or favorites
- Photos / image uploads

These can come later — see [[Decisions Log]] for why we cut them.

## Repo Layout
```
Practica/
├── Database/           # All SQL scripts (Codrin's work)
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
├── Vault/              # Obsidian knowledge graph (this folder)
└── (future) App/       # .NET app (later)
```

See also: [[Tech Stack]], [[Database/Schema Overview]]
