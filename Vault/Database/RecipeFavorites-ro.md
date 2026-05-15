---
tags: [database, table]
---

# RecipeFavorites

Fisier: `Database/12_favorites.sql`

Multi-la-multi intre [[Users-ro]] si [[Recipes-ro]]. Un rand = "acest utilizator a marcat aceasta reteta ca favorita".

## Coloane
| Coloana     | Tip             | Note |
|-------------|-----------------|------|
| UserID      | INT             | FK → [[Users-ro]] **(ON DELETE CASCADE)** |
| RecipeID    | INT             | FK → [[Recipes-ro]] **(ON DELETE CASCADE)** |
| FavoritedAt | DATETIME2(0)    | UTC implicit |

**PK compozit `(UserID, RecipeID)`** — fara coloana IDENTITY separata. Un utilizator poate marca o reteta ca favorita cel mult o data.

## Indecsi
- `IX_RecipeFavorites_RecipeID` — index secundar pentru "ce utilizatori au marcat aceasta reteta ca favorita?" / performanta cascadei din reteta. `UserID` este deja coloana principala a PK-ului deci nu este nevoie de un index separat pe UserID.

## De ce ambele FK fac cascada
Un favorit este lipsit de sens fara oricare parte. Fara eroare de cale multi-cascada deoarece [[Recipes-ro]] → [[Users-ro]] este RESTRICT (fara ciclu).

## Proceduri
- `sp_ToggleFavorite` — insert daca absent, delete daca prezent. Returneaza `IsFavorite` 1/0 astfel incat aplicatia sa poata actualiza pictograma de inima direct.
- `sp_GetFavoriteRecipes(@UserID, @PageNumber, @PageSize)` — lista paginata cu aceeasi forma de output ca `sp_GetRecipes` (reutilizare VM).

Vezi [[Schema Overview-ro]]
