---
tags: [database, table, drafts, ro]
---

# RecipeDrafts (Romana)

Fisier: `Database/15_recipe_drafts.sql`

## Scop
Stocheaza retete partial completate, salvate din editor, astfel incat utilizatorul sa le poata continua mai tarziu.

Drafturile sunt intentionat mai permisive decat [[Recipes-ro]]: majoritatea coloanelor de continut sunt nullable si nu exista constrangeri de completitudine ca la o reteta finala, deoarece un draft poate fi completat doar pe jumatate.

## Coloane
| Coloana | Tip | Note |
|---|---|---|
| DraftID | INT IDENTITY | PK |
| UserID | INT | FK -> [[Users-ro]], NOT NULL, cascade delete |
| CategoryID | INT | FK -> [[Categories-ro]], nullable |
| Title | NVARCHAR(150) | nullable |
| Description | NVARCHAR(MAX) | nullable |
| Instructions | NVARCHAR(MAX) | nullable |
| PrepTimeMinutes | INT | nullable |
| CookTimeMinutes | INT | nullable |
| Servings | INT | nullable |
| IngredientsJson | NVARCHAR(MAX) | nullable; blob JSON opac controlat de aplicatie |
| UpdatedAt | DATETIME2(0) | default UTC; actualizat la salvare |

## Indexuri
- `IX_RecipeDrafts_UserID` — listeaza rapid drafturile unui utilizator.

## Stored procedures
- `sp_SaveDraft` — insereaza/actualizeaza un draft; returneaza `DraftID`; doar proprietarul poate actualiza.
- `sp_GetDrafts` — listeaza drafturile utilizatorului, cele mai noi primele.
- `sp_GetDraft` — incarca complet un draft; doar proprietarul.
- `sp_DeleteDraft` — sterge un draft; doar proprietarul.

## De ce aceasta forma
- `IngredientsJson` este pastrat ca blob opac in loc de randuri normalizate deoarece ingredientele unui draft pot fi incomplete sau invalide in timpul editarii.
- Randurile draft fac cascada cand utilizatorul este sters, deoarece nu au sens fara acel utilizator.
- Drafturile nu fac cascada de la retete, pentru ca inca nu sunt retete finale.

Vezi [[Schema Overview-ro]], [[Decisions Log-ro]]
