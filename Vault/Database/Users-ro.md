---
tags: [database, table]
---

# Users

Fisier: `Database/01_users.sql`

## Coloane
| Coloana        | Tip             | Note |
|----------------|-----------------|------|
| UserID         | INT IDENTITY    | PK |
| Username       | NVARCHAR(50)    | UNIQUE, NOT NULL |
| Email          | NVARCHAR(255)   | UNIQUE, NOT NULL |
| PasswordHash   | NVARCHAR(255)   | NOT NULL — **niciodata text in clar** |
| CreatedAt      | DATETIME2(0)    | UTC, implicit `SYSUTCDATETIME()` |

## De ce aceste alegeri
- PasswordHash este un hash, nu o parola. Aplicatia .NET face hash (bcrypt/Argon2) inainte de insert.
- Username si Email sunt amandoua unice astfel incat login-ul sa poata accepta oricare.
- Timestamp-uri UTC pentru ca schimbarile de fus orar sa nu corupa istoricul.

## Folosit de
- [[Recipes-ro]] (FK → UserID)

Vezi [[Schema Overview-ro]], [[Decisions Log-ro]]
