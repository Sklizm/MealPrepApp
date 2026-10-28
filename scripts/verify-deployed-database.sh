#!/usr/bin/env bash
# Read-only schema inspection; never applies migrations or reads personal data.
set -euo pipefail
DB_CONTAINER="${1:-mealprep-db}"
docker exec -i "$DB_CONTAINER" sh -c '
  export SQLCMDPASSWORD="${MSSQL_SA_PASSWORD:-${SA_PASSWORD:-}}"
  : "${SQLCMDPASSWORD:?Administrator password unavailable}"
  exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d MealPrepDB
' <<'SQL'
SET NOCOUNT ON;
DECLARE @Required TABLE (Name sysname);
INSERT @Required VALUES (N'sp_BrowseRecipes'), (N'sp_CreateShoppingSnapshot'),
(N'sp_GetShoppingSnapshots'), (N'sp_GetShoppingSnapshotItems'),
(N'sp_SetShoppingPurchased'), (N'sp_ImportShoppingItem'),
(N'sp_PreviewWeekCopy'), (N'sp_CopyWeek');
IF EXISTS (SELECT 1 FROM @Required WHERE OBJECT_ID(N'dbo.' + Name, N'P') IS NULL)
    THROW 51000, N'Required release procedures are missing. Inspect migration logs.', 1;
IF COL_LENGTH(N'dbo.RecipePhotos', N'ThumbnailData') IS NULL
    THROW 51000, N'Compatibility photo migration is missing.', 1;
SELECT Name AS VerifiedProcedure FROM @Required ORDER BY Name;
PRINT N'Required procedure names and thumbnail column exist; this is not a functional test.';
SQL
