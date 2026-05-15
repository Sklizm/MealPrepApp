---
tags: [tech, stack]
---

# Stiva tehnologica

## Partea bazei de date (focus curent)
- **SQL Server** ruland intr-un **container Docker** (deja configurat de Codrin)
- **DataGrip** ca si client GUI (deja conectat)
- Dialect T-SQL; scripturi idempotente astfel incat re-rularile sunt sigure

## Partea aplicatiei (viitor)
- **.NET** (Visual Studio)
- Se va conecta la aceeasi instanta SQL Server
- Autentificare, logica de business si UI traiesc aici — nu este partea lui Codrin

## Conventii
- Toate `CREATE TABLE` invelite in `IF OBJECT_ID(...) IS NULL`
- Toate seed-urile folosesc `MERGE` pentru a fi re-rulabile
- String-uri: `NVARCHAR` (Unicode)
- Timestamp-uri: `DATETIME2(0)` cu valoare implicita `SYSUTCDATETIME()` (UTC, nu local)
- Coloanele PK numite `<TableName>ID`, `INT IDENTITY(1,1)`
- Constrangeri prefixate: `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_`

Vezi [[Decisions Log-ro]] pentru rationamentul din spatele acestora.
