USE MealPrepDB;
GO
-- Only explicitly supported mass/volume conversions normalize to g/ml. No density guesses.
CREATE OR ALTER FUNCTION dbo.fn_ShoppingUnitMap()
RETURNS TABLE AS RETURN (
 SELECT u.UnitID, COALESCE(c.ToUnitID,u.UnitID) AS CanonicalUnitID,
 COALESCE(c.Multiplier,CONVERT(DECIMAL(18,8),1)) AS Multiplier
 FROM dbo.Units u
 OUTER APPLY (SELECT TOP(1) uc.ToUnitID,uc.Multiplier FROM dbo.UnitConversions uc
 JOIN dbo.Units target ON target.UnitID=uc.ToUnitID
 WHERE uc.FromUnitID=u.UnitID AND
 ((target.Abbreviation=N'g' AND u.Abbreviation IN(N'kg',N'mg',N'oz',N'lb')) OR (target.Abbreviation=N'ml' AND u.Abbreviation=N'l'))
 ORDER BY uc.UnitConversionID) c
);
GO
CREATE OR ALTER PROCEDURE dbo.sp_CreateShoppingSnapshot @UserID INT,@StartDate DATE,@EndDate DATE
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @StartDate IS NULL OR @EndDate IS NULL OR @EndDate<@StartDate THROW 50000,N'Invalid date range',1;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @SnapshotID INT;
 INSERT dbo.ShoppingSnapshots(UserID,StartDate,EndDate) VALUES(@UserID,@StartDate,@EndDate);
 SET @SnapshotID=SCOPE_IDENTITY();
 ;WITH demand AS (
 SELECT ri.IngredientID,m.CanonicalUnitID AS UnitID,
 SUM(CONVERT(DECIMAL(18,6),ri.Quantity)*m.Multiplier*COALESCE(p.Servings,r.Servings,1)/COALESCE(NULLIF(r.Servings,0),1)) AS Qty
 FROM dbo.MealPlanEntries p JOIN dbo.Recipes r ON r.RecipeID=p.RecipeID
 JOIN dbo.RecipeIngredients ri ON ri.RecipeID=p.RecipeID JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=ri.UnitID
 WHERE p.UserID=@UserID AND p.PlannedDate BETWEEN @StartDate AND @EndDate GROUP BY ri.IngredientID,m.CanonicalUnitID
 ), stock AS (
 SELECT p.IngredientID,m.CanonicalUnitID AS UnitID,SUM(p.Quantity*m.Multiplier) AS Qty
 FROM dbo.UserPantry p JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=p.UnitID
 WHERE p.UserID=@UserID GROUP BY p.IngredientID,m.CanonicalUnitID
 )
 INSERT dbo.ShoppingSnapshotItems(SnapshotID,IngredientID,UnitID,IngredientName,CategoryName,UnitAbbreviation,NeededQty,OnHandQty,ToBuyQty)
 SELECT @SnapshotID,d.IngredientID,d.UnitID,i.Name,COALESCE(ic.Name,N'Fără categorie'),u.Abbreviation,d.Qty,COALESCE(s.Qty,0),CEILING((d.Qty-COALESCE(s.Qty,0))*100)/100
 FROM demand d JOIN dbo.Ingredients i ON i.IngredientID=d.IngredientID
 LEFT JOIN dbo.IngredientCategories ic ON ic.IngredientCategoryID=i.IngredientCategoryID
 JOIN dbo.Units u ON u.UnitID=d.UnitID LEFT JOIN stock s ON s.IngredientID=d.IngredientID AND s.UnitID=d.UnitID
 WHERE d.Qty>COALESCE(s.Qty,0);
 EXEC dbo.sp_WriteAudit @UserID,N'SHOPPING_CREATE',N'ShoppingSnapshot',@SnapshotID;
 COMMIT;
 SELECT @SnapshotID;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetShoppingSnapshots @UserID INT
AS BEGIN SET NOCOUNT ON; SELECT SnapshotID,StartDate,EndDate,CreatedAt FROM dbo.ShoppingSnapshots WHERE UserID=@UserID ORDER BY SnapshotID DESC; END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetShoppingSnapshotItems @UserID INT,@SnapshotID INT
AS BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshots WHERE SnapshotID=@SnapshotID AND UserID=@UserID) THROW 50002,N'Not authorized',1;
 SELECT i.*, CONVERT(BIT,CASE WHEN ImportedAt IS NULL THEN 0 ELSE 1 END) AS IsImported FROM dbo.ShoppingSnapshotItems i WHERE SnapshotID=@SnapshotID ORDER BY CategoryName,IngredientName,ItemID;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_SetShoppingPurchased @UserID INT,@ItemID INT,@IsPurchased BIT
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRAN;
 UPDATE i SET IsPurchased=@IsPurchased,UpdatedAt=SYSUTCDATETIME() FROM dbo.ShoppingSnapshotItems i
 JOIN dbo.ShoppingSnapshots s ON s.SnapshotID=i.SnapshotID WHERE i.ItemID=@ItemID AND s.UserID=@UserID;
 IF @@ROWCOUNT=0 THROW 50002,N'Not authorized',1;
 EXEC dbo.sp_WriteAudit @UserID,N'SHOPPING_CHECK',N'ShoppingItem',@ItemID;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- Idempotent import. Lock the snapshot item before updating pantry and its import marker.
CREATE OR ALTER PROCEDURE dbo.sp_ImportShoppingItem @UserID INT,@ItemID INT
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRAN;
 DECLARE @IngredientID INT,@UnitID INT,@Quantity DECIMAL(10,2),@Imported DATETIME2(0),@Purchased BIT;
 SELECT @IngredientID=i.IngredientID,@UnitID=i.UnitID,@Quantity=i.ToBuyQty,@Imported=i.ImportedAt,@Purchased=i.IsPurchased
 FROM dbo.ShoppingSnapshotItems i WITH(UPDLOCK,HOLDLOCK) JOIN dbo.ShoppingSnapshots s ON s.SnapshotID=i.SnapshotID
 WHERE i.ItemID=@ItemID AND s.UserID=@UserID;
 IF @IngredientID IS NULL THROW 50002,N'Not authorized',1;
 IF @Purchased=0 THROW 50000,N'Item must be purchased before import',1;
 IF @Imported IS NULL
 BEGIN
 EXEC dbo.sp_AddPantryItem @UserID,@IngredientID,@UnitID,@Quantity;
 UPDATE dbo.ShoppingSnapshotItems SET ImportedAt=SYSUTCDATETIME(),UpdatedAt=SYSUTCDATETIME() WHERE ItemID=@ItemID;
 EXEC dbo.sp_WriteAudit @UserID,N'SHOPPING_IMPORT',N'ShoppingItem',@ItemID;
 END;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
