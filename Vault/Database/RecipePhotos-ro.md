---
tags: [database, table, photos, ro]
---

# RecipePhotos (Romana)

Fisier: `Database/16_recipe_photos.sql`

## Scop
Stocheaza o singura poza optionala pentru fiecare reteta.

Bytes-ii imaginii traiesc in SQL Server, astfel incat poza calatoreste impreuna cu baza de date intre masini si ramane in API-ul stored-procedure-only al aplicatiei. Aplicatia WPF redimensioneaza si re-encodeaza fisierele selectate inainte de salvare.

## Coloane
| Coloana | Tip | Note |
|---|---|---|
| RecipeID | INT | PK si FK -> [[Recipes-ro]], cascade delete |
| ImageData | VARBINARY(MAX) | bytes JPEG trimisi de aplicatie |
| ContentType | NVARCHAR(100) | momentan salvat ca `image/jpeg` de aplicatie |
| UpdatedAt | DATETIME2(0) | default UTC; actualizat la inlocuire |

## Relatii
- `RecipeID` este si cheia primara, si cheia straina catre [[Recipes-ro]].
- Aceasta impune o singura poza per reteta.
- Stergerea retetei sterge prin cascada si poza.

## Stored procedures
- `sp_SetRecipePhoto` — insereaza sau inlocuieste poza retetei; doar proprietarul.
- `sp_GetRecipePhoto` — citeste bytes/content type; nu returneaza randuri daca poza lipseste.
- `sp_DeleteRecipePhoto` — elimina poza; doar proprietarul; nu da eroare daca poza nu exista.

## Tratare in aplicatie
- Imaginile selectate sunt decodate prin WPF imaging.
- Imaginile sunt redimensionate cu `DecodePixelWidth = 1200`.
- Imaginile sunt re-encodeate JPEG quality 85 inainte de `sp_SetRecipePhoto`.
- Cardurile de reteta incarca bytes-ii pozei si ii transforma prin `ByteArrayToImageSourceConverter`.

Vezi [[Schema Overview-ro]], [[Recipes-ro]], [[Decisions Log-ro]]
