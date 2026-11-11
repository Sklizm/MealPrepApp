USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER FUNCTION dbo.fn_BatchShoppingDemand(@UserID INT,@StartDate DATE,@EndDate DATE)
RETURNS TABLE AS RETURN(
 SELECT ri.IngredientID,m.CanonicalUnitID AS UnitID,
 SUM(ri.Quantity*m.Multiplier*b.PlannedPortions/COALESCE(NULLIF(r.Servings,0),1)) AS NeededQty
 FROM dbo.CookingBatches b JOIN dbo.Recipes r ON r.RecipeID=b.RecipeID
 JOIN dbo.RecipeIngredients ri ON ri.RecipeID=r.RecipeID JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=ri.UnitID
 WHERE b.UserID=@UserID AND b.CookedAt IS NULL AND EXISTS(SELECT 1 FROM dbo.MealPlanEntries p WHERE p.CookingBatchID=b.CookingBatchID AND p.PlannedDate BETWEEN @StartDate AND @EndDate)
 GROUP BY ri.IngredientID,m.CanonicalUnitID
);
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetShoppingList @UserID INT,@StartDate DATE,@EndDate DATE,@StockDate DATE=NULL
AS BEGIN
 SET NOCOUNT ON;
 SET @StockDate=COALESCE(@StockDate,CONVERT(DATE,SYSUTCDATETIME()));
 ;WITH stock AS(
 SELECT b.IngredientID,m.CanonicalUnitID AS UnitID,SUM(b.RemainingQuantity*m.Multiplier) AS Qty
 FROM dbo.InventoryBatches b JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=b.UnitID
 WHERE b.UserID=@UserID AND (b.ExpiresOn IS NULL OR b.ExpiresOn>=@StockDate) GROUP BY b.IngredientID,m.CanonicalUnitID)
 SELECT d.IngredientID,i.Name AS IngredientName,COALESCE(c.Name,N'Fără categorie') AS CategoryName,d.UnitID,u.Name AS UnitName,u.Abbreviation AS UnitAbbreviation,
 d.NeededQty,COALESCE(s.Qty,0) AS OnHandQty,CEILING((d.NeededQty-COALESCE(s.Qty,0))*10000)/10000 AS ToBuyQty
 FROM dbo.fn_BatchShoppingDemand(@UserID,@StartDate,@EndDate) d JOIN dbo.Ingredients i ON i.IngredientID=d.IngredientID
 JOIN dbo.Units u ON u.UnitID=d.UnitID LEFT JOIN dbo.IngredientCategories c ON c.IngredientCategoryID=i.IngredientCategoryID
 LEFT JOIN stock s ON s.IngredientID=d.IngredientID AND s.UnitID=d.UnitID WHERE d.NeededQty>COALESCE(s.Qty,0)
 ORDER BY i.Name,u.Abbreviation;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_CreateShoppingSnapshot @UserID INT,@StartDate DATE,@EndDate DATE,@StockDate DATE=NULL
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @StartDate IS NULL OR @EndDate IS NULL OR @EndDate<@StartDate THROW 50000,N'Invalid date range',1;
 BEGIN TRY BEGIN TRAN;
 DECLARE @ID INT;
 INSERT dbo.ShoppingSnapshots(UserID,StartDate,EndDate) VALUES(@UserID,@StartDate,@EndDate); SET @ID=SCOPE_IDENTITY();
 SET @StockDate=COALESCE(@StockDate,CONVERT(DATE,SYSUTCDATETIME()));
 ;WITH stock AS(
 SELECT b.IngredientID,m.CanonicalUnitID AS UnitID,SUM(b.RemainingQuantity*m.Multiplier) AS Qty
 FROM dbo.InventoryBatches b JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=b.UnitID
 WHERE b.UserID=@UserID AND (b.ExpiresOn IS NULL OR b.ExpiresOn>=@StockDate) GROUP BY b.IngredientID,m.CanonicalUnitID)
 INSERT dbo.ShoppingSnapshotItems(SnapshotID,IngredientID,UnitID,IngredientName,CategoryName,UnitAbbreviation,NeededQty,OnHandQty,ToBuyQty)
 SELECT @ID,d.IngredientID,d.UnitID,i.Name,COALESCE(c.Name,N'Fără categorie'),u.Abbreviation,d.NeededQty,COALESCE(s.Qty,0),CEILING((d.NeededQty-COALESCE(s.Qty,0))*10000)/10000
 FROM dbo.fn_BatchShoppingDemand(@UserID,@StartDate,@EndDate) d JOIN dbo.Ingredients i ON i.IngredientID=d.IngredientID JOIN dbo.Units u ON u.UnitID=d.UnitID
 LEFT JOIN dbo.IngredientCategories c ON c.IngredientCategoryID=i.IngredientCategoryID LEFT JOIN stock s ON s.IngredientID=d.IngredientID AND s.UnitID=d.UnitID
 WHERE d.NeededQty>COALESCE(s.Qty,0);
 EXEC dbo.sp_WriteAudit @UserID,N'SHOPPING_CREATE',N'ShoppingSnapshot',@ID;
 COMMIT; SELECT @ID;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetActivityReport @UserID INT,@StartDate DATE,@EndDate DATE,@UtcStart DATETIME2(7),@UtcEnd DATETIME2(7)
AS BEGIN
 SET NOCOUNT ON;
 SELECT
 (SELECT COUNT(*) FROM dbo.MealPlanEntries WHERE UserID=@UserID AND PlannedDate BETWEEN @StartDate AND @EndDate) AS PlannedMeals,
 (SELECT COALESCE(SUM(PortionQuantity),0) FROM dbo.MealPlanEntries WHERE UserID=@UserID AND PlannedDate BETWEEN @StartDate AND @EndDate) AS PlannedPortions,
 (SELECT COALESCE(SUM(ProducedPortions),0) FROM dbo.CookingBatches WHERE UserID=@UserID AND CookedAt>=@UtcStart AND CookedAt<@UtcEnd) AS CookedPortions,
 (SELECT COALESCE(SUM(Portions),0) FROM dbo.PortionEvents WHERE UserID=@UserID AND Kind=N'Consumed' AND CreatedAt>=@UtcStart AND CreatedAt<@UtcEnd) AS ConsumedPortions,
 (SELECT COALESCE(SUM(Portions),0) FROM dbo.PortionEvents WHERE UserID=@UserID AND Kind=N'Discarded' AND CreatedAt>=@UtcStart AND CreatedAt<@UtcEnd) AS DiscardedPortions;
 SELECT b.IngredientID,i.Name AS IngredientName,m.CanonicalUnitID AS UnitID,u.Abbreviation AS UnitAbbreviation,SUM(-e.QuantityDelta*m.Multiplier) AS Quantity
 FROM dbo.StockEvents e JOIN dbo.InventoryBatches b ON b.BatchID=e.BatchID JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=b.UnitID
 JOIN dbo.Units u ON u.UnitID=m.CanonicalUnitID JOIN dbo.Ingredients i ON i.IngredientID=b.IngredientID
 WHERE e.UserID=@UserID AND e.Kind=N'Discard' AND e.CreatedAt>=@UtcStart AND e.CreatedAt<@UtcEnd
 GROUP BY b.IngredientID,i.Name,m.CanonicalUnitID,u.Abbreviation;
 SELECT p.RecipeID,r.Title AS RecipeTitle,COUNT(*) AS PlannedMeals FROM dbo.MealPlanEntries p JOIN dbo.Recipes r ON r.RecipeID=p.RecipeID
 WHERE p.UserID=@UserID AND p.PlannedDate BETWEEN @StartDate AND @EndDate GROUP BY p.RecipeID,r.Title HAVING COUNT(*)>1 ORDER BY PlannedMeals DESC,r.Title;
END;
GO
