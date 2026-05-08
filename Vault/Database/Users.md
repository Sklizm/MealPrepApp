---
tags: [database, table]
---

# Users

File: `Database/01_users.sql`

## Columns
| Column         | Type            | Notes |
|----------------|-----------------|-------|
| UserID         | INT IDENTITY    | PK |
| Username       | NVARCHAR(50)    | UNIQUE, NOT NULL |
| Email          | NVARCHAR(255)   | UNIQUE, NOT NULL |
| PasswordHash   | NVARCHAR(255)   | NOT NULL — **never plaintext** |
| CreatedAt      | DATETIME2(0)    | UTC, default `SYSUTCDATETIME()` |

## Why these choices
- PasswordHash is a hash, not a password. The .NET app hashes (bcrypt/Argon2) before insert.
- Username and Email are both unique so login can accept either.
- UTC timestamps so timezone shifts don't corrupt history.

## Used by
- [[Recipes]] (FK → UserID)

See [[Schema Overview]], [[Decisions Log]]
