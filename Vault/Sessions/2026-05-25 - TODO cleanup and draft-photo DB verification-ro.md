---
tags: [session, todo, database, verification, ro]
date: 2026-05-25
---

# 2026-05-25 — Curatare TODO si verificare DB pentru draft/poze

## Scop
Codrin a cerut sa fie urmat ordinea recomandata: curatarea TODO-urilor vechi si verificarea scripturilor de baza de date pentru drafturi/poze prin `Database/run_all.sql`.

## TODO curatat
`Vault/TODO.md` a fost actualizat:
- itemul de merge pentru `fix-recipe-field-limits` era deja rezolvat in git;
- `origin` puncta deja la `https://github.com/Sklizm/MealPrepApp.git`;
- planificarea meselor si lista de cumparaturi au fost mutate la Done;
- reminderul duplicat despre poze a fost scos;
- au ramas active: Drafts, poze, loading screen, `.exe`, adaugare ingredient din editor si forgot/change-password.

## Verificare baza de date
S-a rulat secventa standard sigura: curatare `/tmp/Database` in container, `docker cp Database`, apoi `sqlcmd -C -b -i run_all.sql`.

Rezultat: build-ul a iesit cu cod 0.

## Obiecte verificate
- Tabele: `RecipeDrafts`, `RecipePhotos`.
- Proceduri: `sp_SaveDraft`, `sp_GetDrafts`, `sp_GetDraft`, `sp_DeleteDraft`, `sp_SetRecipePhoto`, `sp_GetRecipePhoto`, `sp_DeleteRecipePhoto`.
- Modelul de securitate ramane proc-only: `mealprep_app_role` are EXECUTE si are direct SELECT/INSERT/UPDATE/DELETE refuzate pe schema `dbo`.

## Urmatorul lucru recomandat
Implementarea UI pentru Drafts, apoi Photos, dupa care polish: ingredient din editor, loading screen si `.exe`.
