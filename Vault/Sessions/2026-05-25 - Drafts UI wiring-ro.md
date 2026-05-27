---
tags: [session, app, drafts, wpf, ro]
date: 2026-05-25
---

# 2026-05-25 — Legare UI pentru Drafts

## Scop
Codrin a cerut implementarea functionalitatii Drafts dupa verificarea DB pentru drafturi/poze.

## Implementat
- `DraftRepository` inregistrat in DI.
- `ReteteEditorViewModel` primeste `DraftRepository`, stare `DraftId`/`IsDraft`, incarcare draft, deserializare `IngredientsJson`, `SaveDraftCommand` si stergere draft sursa dupa salvare ca reteta reala.
- `ReteteEditorView.xaml` primeste butonul de salvare draft.
- `ReteteListViewModel` primeste colectia `Drafts`, filtru de sidebar, incarcare prin `sp_GetDrafts`, open/delete draft.
- `ReteteListView.xaml` primeste carduri pentru drafturi si empty state.

## Verificare
- Script static: `.hermes/tests/test_drafts_static.py` — PASS pentru DI, lista, editor si controale XAML.
- XAML parse OK pentru `ReteteListView.xaml` si `ReteteEditorView.xaml`.
- Build local blocat de mediu: Fedora are SDK .NET 9 si proiectul cere `net10.0-windows` / WindowsDesktop targets.

## Follow-up important
Verificarea pe Windows/.NET 10 era necesara: salvare draft, listare in Retete > Drafts, redeschidere, salvare ca reteta reala si stergere draft.

Nota: ulterior Codrin a preferat wording-ul `Drafts` / `Salveaza ca draft`, nu `Ciorne` / `Salveaza ciorna`.
