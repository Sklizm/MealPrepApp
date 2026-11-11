#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_CONTAINER="mealprep-test-$(date +%s)-$$"
TEST_PASSWORD="Test-$(openssl rand -hex 16)!"
cleanup() { docker rm -f "$TEST_CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT
# No host port or persistent volume: this never targets the deployed database.
docker run -d --name "$TEST_CONTAINER" -e ACCEPT_EULA=Y -e MSSQL_PID=Developer \
    -e MSSQL_SA_PASSWORD="$TEST_PASSWORD" \
    -v "$REPO_ROOT/Database:/schema:ro" -v "$REPO_ROOT/Tests/Database:/tests:ro" \
    mcr.microsoft.com/mssql/server:2022-latest >/dev/null
sql() {
    docker exec -e SQLCMDPASSWORD="$TEST_PASSWORD" -w /schema "$TEST_CONTAINER" \
        /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b "$@"
}
ready=false
for attempt in {1..60}; do
    if sql -Q 'SELECT 1' >/dev/null 2>&1; then ready=true; break; fi
    sleep 2
done
if [ "$ready" != true ]; then echo "Disposable SQL Server failed to start"; exit 1; fi
sql -v AppPassword="$TEST_PASSWORD" -i run_all.sql
# Re-run migrations to validate preservation/idempotency.
sql -v AppPassword="$TEST_PASSWORD" -i run_all.sql
sql -i /tests/regression.sql
# Simulate the relevant predecessor state with real data, then test the live migration twice.
sql -Q "USE MealPrepDB;
INSERT dbo.RecipePhotos (RecipeID, ImageData, ContentType)
SELECT RecipeID, 0x01020304, N'image/jpeg' FROM dbo.Recipes WHERE Title=N'Updated';
DROP PROCEDURE dbo.sp_BrowseRecipes;
ALTER TABLE dbo.RecipePhotos DROP COLUMN ThumbnailData;"
sql -i migrations/20261028_compatibility.sql
sql -i migrations/20261028_compatibility.sql
sql -i /tests/upgrade-regression.sql

sql -i /tests/redesign-regression.sql
sql -i migrations/20261028_cooking_redesign.sql
sql -i migrations/20261028_cooking_redesign.sql
sql -i /tests/redesign-upgrade-regression.sql

sql -i /tests/inventory-regression.sql
sql -i migrations/20261111_inventory.sql
sql -i migrations/20261111_inventory.sql
sql -Q "USE MealPrepDB; IF EXISTS(SELECT 1 FROM dbo.UserPantry p FULL JOIN (SELECT UserID,IngredientID,UnitID,SUM(RemainingQuantity) AS Qty FROM dbo.InventoryBatches GROUP BY UserID,IngredientID,UnitID HAVING SUM(RemainingQuantity)>0) b ON b.UserID=p.UserID AND b.IngredientID=p.IngredientID AND b.UnitID=p.UnitID WHERE COALESCE(p.Quantity,0)<>COALESCE(b.Qty,0)) THROW 51000,N'Batch totals mismatch after migration rerun',1;"
# Reconstruct the predecessor inventory schema in this disposable container, retaining real totals.
sql -Q "USE MealPrepDB;
SELECT UserID,IngredientID,UnitID,Quantity,AddedAt,UpdatedAt INTO dbo.InventoryUpgradeExpected FROM dbo.UserPantry;
ALTER TABLE dbo.ShoppingSnapshotItems DROP CONSTRAINT FK_ShoppingSnapshotItems_InventoryBatch;
UPDATE dbo.ShoppingSnapshotItems SET ImportedBatchID=NULL;
DROP TRIGGER dbo.tr_UserPantry_Batches;
DROP TABLE dbo.StockEvents; DROP TABLE dbo.StockThresholds; DROP TABLE dbo.ClientOperations; DROP TABLE dbo.InventoryBatches;"
sql -i migrations/20261111_inventory.sql
sql -i migrations/20261111_inventory.sql
sql -Q "USE MealPrepDB;
IF EXISTS(SELECT 1 FROM dbo.InventoryUpgradeExpected p LEFT JOIN dbo.InventoryBatches b ON b.UserID=p.UserID AND b.IngredientID=p.IngredientID AND b.UnitID=p.UnitID WHERE b.BatchID IS NULL OR b.RemainingQuantity<>p.Quantity OR b.SourceAddedAt<>p.AddedAt OR b.UpdatedAt<>COALESCE(p.UpdatedAt,p.AddedAt) OR b.PurchasedOn IS NOT NULL OR b.ExpiresOn IS NOT NULL) THROW 51000,N'Predecessor stock migration changed quantities or dates',1;
DROP TABLE dbo.InventoryUpgradeExpected;"
sql -i migrations/20261111_shopping.sql
sql -i migrations/20261111_shopping.sql
sql -i /tests/shopping-changes-regression.sql

sql -i migrations/20261111_batch_meals.sql
sql -i migrations/20261111_batch_meals.sql
sql -i /tests/cooking-batches-regression.sql
sql -i migrations/20261111_workflows.sql
sql -i migrations/20261111_workflows.sql
sql -i /tests/workflows-regression.sql

sql -i migrations/20261111_companion.sql
sql -i migrations/20261111_companion.sql
