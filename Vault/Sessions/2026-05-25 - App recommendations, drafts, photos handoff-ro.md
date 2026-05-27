---
tags: [session, handoff, wpf, drafts, photos, todo, ro]
---

# 2026-05-25 — Recomandari app, Drafts, poze si handoff

## Context
Codrin a cerut readucerea recomandarilor pentru aplicatie si TODO, apoi inregistrarea lucrului in Obsidian, memorie si git daca este nevoie.

## Ordine recomandata discutata
1. Curatarea TODO-urilor vechi.
2. Verificarea scripturilor DB pentru draft/poze cu `run_all.sql`.
3. Legarea drafturilor in WPF.
4. Legarea pozelor in WPF.
5. Polish pentru imagini responsive si thumbnails.
6. Urmatoarele iteme: loading screen, `.exe`, adaugare ingredient din editor, forgot-password/change-password din login.

## Lucru finalizat
- `Vault/TODO.md` a fost curatat si actualizat.
- DB pentru `RecipeDrafts` si `RecipePhotos` a fost verificat prin `run_all.sql`.
- Drafts UI a fost implementat si Codrin l-a confirmat pe Windows/.NET 10.
- Photos UI a fost implementat: add/change/delete poza, downscale la `DecodePixelWidth=1200`, JPEG quality 85, thumbnails pe carduri.
- Wording-ul ramane intentionat `Drafts` / `Salveaza ca draft`.

## Verificare
- `.hermes/tests/test_drafts_static.py` a trecut.
- XAML parse OK pentru fisierele modificate.
- `git diff --check` a trecut inainte de commitul de responsive photos.

## Stare git de atunci
Branch: `feature-drafts-and-photos`; commituri impinse pentru drafturi, UI poze si poze responsive.

## Pas ramas atunci
Verificarea Photos UI pe Windows/.NET 10: adaugare, persistenta dupa redeschidere, schimbare, stergere si thumbnails in lista.
