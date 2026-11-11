USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_UpdateIngredient @UserID INT,@IngredientID INT,@Name NVARCHAR(150),@DefaultUnitID INT=NULL,@IngredientCategoryID INT=NULL,@ExpectedVersion BINARY(8)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@Name)),N'') IS NULL THROW 50000,N'Name required',1;
 BEGIN TRY BEGIN TRAN;
 UPDATE dbo.Ingredients SET Name=LTRIM(RTRIM(@Name)),DefaultUnitID=@DefaultUnitID,IngredientCategoryID=@IngredientCategoryID WHERE IngredientID=@IngredientID AND RowVersion=@ExpectedVersion;
 IF @@ROWCOUNT=0 THROW 50004,N'Ingredient changed; reload before editing',1;
 EXEC dbo.sp_WriteAudit @UserID,N'INGREDIENT_EDIT',N'Ingredient',@IngredientID;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetIngredients @IngredientCategoryID INT=NULL
AS BEGIN
 SET NOCOUNT ON;
 SELECT i.IngredientID,i.Name,i.DefaultUnitID,u.Name AS DefaultUnitName,u.Abbreviation AS DefaultUnitAbbreviation,
 i.IngredientCategoryID,c.Name AS IngredientCategoryName,i.RowVersion,
 CONVERT(BIT,CASE WHEN n.IngredientID IS NULL THEN 0 ELSE 1 END) AS HasNutrition
 FROM dbo.Ingredients i LEFT JOIN dbo.Units u ON u.UnitID=i.DefaultUnitID LEFT JOIN dbo.IngredientCategories c ON c.IngredientCategoryID=i.IngredientCategoryID
 LEFT JOIN dbo.IngredientNutrition n ON n.IngredientID=i.IngredientID WHERE @IngredientCategoryID IS NULL OR i.IngredientCategoryID=@IngredientCategoryID ORDER BY i.Name;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_SaveRecipeBundle @UserID INT,@OperationID UNIQUEIDENTIFIER,@RecipeID INT=NULL,@CategoryID INT=NULL,
 @Title NVARCHAR(150),@Description NVARCHAR(MAX)=NULL,@Instructions NVARCHAR(MAX),@PrepTimeMinutes INT=NULL,@CookTimeMinutes INT=NULL,@Servings INT=NULL,
 @IngredientsJson NVARCHAR(MAX),@ExpectedVersion BINARY(8)=NULL,@PhotoMode NVARCHAR(10)=N'Keep',@ImageData VARBINARY(MAX)=NULL,@ThumbnailData VARBINARY(MAX)=NULL
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @PhotoMode NOT IN(N'Keep',N'Set',N'Delete') OR (@PhotoMode=N'Set' AND (@ImageData IS NULL OR DATALENGTH(@ImageData)=0 OR DATALENGTH(@ImageData)>10485760)) THROW 50000,N'Invalid photo',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'recipe-bundle' AS kind,@RecipeID AS id,@CategoryID AS category,@Title AS title,@Description AS description,@Instructions AS instructions,@PrepTimeMinutes AS prep,@CookTimeMinutes AS cook,@Servings AS servings,@IngredientsJson AS ingredients,CONVERT(VARCHAR(16),@ExpectedVersion,2) AS version,@PhotoMode AS photoMode,CONVERT(VARCHAR(64),HASHBYTES('SHA2_256',@ImageData),2) AS imageHash,CONVERT(VARCHAR(64),HASHBYTES('SHA2_256',@ThumbnailData),2) AS thumbnailHash FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@ID INT=@RecipeID;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  IF @ID IS NULL
  BEGIN
   DECLARE @New TABLE(ID INT);
   INSERT @New EXEC dbo.sp_CreateRecipe @UserID,@CategoryID,@Title,@Description,@Instructions,@PrepTimeMinutes,@CookTimeMinutes,@Servings,@IngredientsJson;
   SELECT @ID=ID FROM @New;
  END
  ELSE EXEC dbo.sp_UpdateRecipe @ID,@UserID,@CategoryID,@Title,@Description,@Instructions,@PrepTimeMinutes,@CookTimeMinutes,@Servings,@IngredientsJson,@ExpectedVersion;
  IF @PhotoMode=N'Set' EXEC dbo.sp_SetRecipePhoto @ID,@UserID,@ImageData,N'image/jpeg',@ThumbnailData;
  IF @PhotoMode=N'Delete' EXEC dbo.sp_DeleteRecipePhoto @ID,@UserID;
  EXEC dbo.sp_WriteAudit @UserID,N'RECIPE_BUNDLE',N'Recipe',@ID;
  SET @Result=(SELECT @ID AS id,RowVersion AS version FROM dbo.Recipes WHERE RecipeID=@ID FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MovePlannedMeal @UserID INT,@OperationID UNIQUEIDENTIFIER,@MealPlanEntryID INT,@ExpectedVersion BIGINT,@PlannedDate DATE,@CategoryID INT
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'move-meal' AS kind,@MealPlanEntryID AS id,@ExpectedVersion AS version,@PlannedDate AS date,@CategoryID AS slot FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Old NVARCHAR(MAX),@Batch INT,@Version BIGINT,@BatchVersion BIGINT,@Move INT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Old=(SELECT PlannedDate,CategoryID FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Batch=CookingBatchID FROM dbo.MealPlanEntries WITH(UPDLOCK,HOLDLOCK) WHERE MealPlanEntryID=@MealPlanEntryID AND UserID=@UserID AND Version=@ExpectedVersion;
  IF @Old IS NULL THROW 50004,N'Meal changed; review again',1;
  UPDATE dbo.MealPlanEntries SET PlannedDate=@PlannedDate,CategoryID=@CategoryID WHERE MealPlanEntryID=@MealPlanEntryID;
  SELECT @Version=Version FROM dbo.MealPlanEntries WHERE MealPlanEntryID=@MealPlanEntryID;
  SELECT @BatchVersion=Version FROM dbo.CookingBatches WHERE CookingBatchID=@Batch;
  INSERT dbo.PlannerMoves(UserID,MealPlanEntryID,CookingBatchID,OldState,AfterVersion,BatchVersion) VALUES(@UserID,@MealPlanEntryID,@Batch,@Old,@Version,@BatchVersion); SET @Move=SCOPE_IDENTITY();
  EXEC dbo.sp_WriteAudit @UserID,N'MEAL_MOVE',N'MealPlanEntry',@MealPlanEntryID;
  SET @Result=(SELECT @MealPlanEntryID AS id,@Version AS version,@Move AS eventId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_UndoPlannerMove @UserID INT,@OperationID UNIQUEIDENTIFIER,@MoveID INT
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'undo-meal' AS kind,@MoveID AS id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Entry INT,@Old NVARCHAR(MAX),@Version BIGINT,@After BIGINT,@Batch INT,@BatchVersion BIGINT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Entry=MealPlanEntryID,@Old=OldState,@After=AfterVersion,@Batch=CookingBatchID,@BatchVersion=BatchVersion FROM dbo.PlannerMoves WITH(UPDLOCK,HOLDLOCK) WHERE MoveID=@MoveID AND UserID=@UserID AND UndoneAt IS NULL;
  IF @Entry IS NULL THROW 50004,N'Move unavailable for undo',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.MealPlanEntries WITH(UPDLOCK,HOLDLOCK) WHERE MealPlanEntryID=@Entry AND UserID=@UserID AND Version=@After) OR (@Batch IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.CookingBatches WITH(UPDLOCK,HOLDLOCK) WHERE CookingBatchID=@Batch AND Version=@BatchVersion)) THROW 50004,N'Subsequent changes prevent undo',1;
  UPDATE dbo.MealPlanEntries SET PlannedDate=CONVERT(DATE,JSON_VALUE(@Old,'$.PlannedDate')),CategoryID=CONVERT(INT,JSON_VALUE(@Old,'$.CategoryID')) WHERE MealPlanEntryID=@Entry;
  UPDATE dbo.PlannerMoves SET UndoneAt=SYSUTCDATETIME() WHERE MoveID=@MoveID;
  SELECT @Version=Version FROM dbo.MealPlanEntries WHERE MealPlanEntryID=@Entry;
  EXEC dbo.sp_WriteAudit @UserID,N'MEAL_MOVE_UNDO',N'MealPlanEntry',@Entry;
  SET @Result=(SELECT @Entry AS id,@Version AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetWeekTemplates @UserID INT AS BEGIN SET NOCOUNT ON; SELECT TemplateID,Name,Version,CreatedAt FROM dbo.WeekTemplates WHERE UserID=@UserID ORDER BY Name,TemplateID; END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_SaveWeekTemplate @UserID INT,@OperationID UNIQUEIDENTIFIER,@Name NVARCHAR(100),@WeekStart DATE
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@Name)),N'') IS NULL OR @WeekStart IS NULL THROW 50000,N'Template name and week required',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'save-template' AS kind,@Name AS name,@WeekStart AS week FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Entries NVARCHAR(MAX),@ID INT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SET @Entries=(SELECT b.RecipeID,b.PlannedPortions,JSON_QUERY((SELECT DATEDIFF(DAY,@WeekStart,p.PlannedDate) AS DayOffset,p.CategoryID,p.PortionQuantity AS Portions,p.Notes FROM dbo.MealPlanEntries p WHERE p.CookingBatchID=b.CookingBatchID AND p.PlannedDate>=@WeekStart AND p.PlannedDate<DATEADD(DAY,7,@WeekStart) ORDER BY p.PlannedDate,p.MealPlanEntryID FOR JSON PATH)) AS Allocations FROM dbo.CookingBatches b WHERE b.UserID=@UserID AND EXISTS(SELECT 1 FROM dbo.MealPlanEntries p WHERE p.CookingBatchID=b.CookingBatchID AND p.PlannedDate>=@WeekStart AND p.PlannedDate<DATEADD(DAY,7,@WeekStart)) ORDER BY b.CookingBatchID FOR JSON PATH);
  IF @Entries=N'[]' THROW 50000,N'No meals in this week',1;
  INSERT dbo.WeekTemplates(UserID,Name,EntriesJson) VALUES(@UserID,@Name,@Entries); SET @ID=SCOPE_IDENTITY();
  EXEC dbo.sp_WriteAudit @UserID,N'TEMPLATE_SAVE',N'WeekTemplate',@ID;
  SET @Result=(SELECT @ID AS id,CONVERT(BIGINT,1) AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ApplyWeekTemplate @UserID INT,@OperationID UNIQUEIDENTIFIER,@TemplateID INT,@ExpectedVersion BIGINT,@WeekStart DATE
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'apply-template' AS kind,@TemplateID AS id,@ExpectedVersion AS version,@WeekStart AS week FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Entries NVARCHAR(MAX),@Recipe INT,@Portions DECIMAL(18,4),@Alloc NVARCHAR(MAX),@Batch INT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Entries=EntriesJson FROM dbo.WeekTemplates WHERE TemplateID=@TemplateID AND UserID=@UserID AND Version=@ExpectedVersion;
  IF @Entries IS NULL OR @WeekStart IS NULL THROW 50004,N'Template unavailable',1;
  DECLARE entries CURSOR LOCAL FAST_FORWARD FOR SELECT RecipeID,PlannedPortions,Allocations FROM OPENJSON(@Entries) WITH(RecipeID INT,PlannedPortions DECIMAL(18,4),Allocations NVARCHAR(MAX) AS JSON);
  OPEN entries; FETCH NEXT FROM entries INTO @Recipe,@Portions,@Alloc;
  WHILE @@FETCH_STATUS=0
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM dbo.Recipes WHERE RecipeID=@Recipe) THROW 50004,N'Template recipe unavailable',1;
   INSERT dbo.CookingBatches(UserID,RecipeID,PlannedPortions) VALUES(@UserID,@Recipe,@Portions); SET @Batch=SCOPE_IDENTITY();
   INSERT dbo.MealPlanEntries(UserID,RecipeID,CategoryID,PlannedDate,Notes,CookingBatchID,PortionQuantity)
   SELECT @UserID,@Recipe,CategoryID,DATEADD(DAY,DayOffset,@WeekStart),Notes,@Batch,Portions FROM OPENJSON(@Alloc) WITH(DayOffset INT,CategoryID INT,Portions DECIMAL(18,4),Notes NVARCHAR(500));
   FETCH NEXT FROM entries INTO @Recipe,@Portions,@Alloc;
  END;
  CLOSE entries; DEALLOCATE entries;
  EXEC dbo.sp_WriteAudit @UserID,N'TEMPLATE_APPLY',N'WeekTemplate',@TemplateID;
  SET @Result=(SELECT @TemplateID AS id,@ExpectedVersion AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PlanSeparateMeals @UserID INT,@OperationID UNIQUEIDENTIFIER,@RecipeID INT,@AllocationsJson NVARCHAR(MAX)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @RecipeID IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.Recipes WHERE RecipeID=@RecipeID) OR ISJSON(@AllocationsJson)<>1 OR LEFT(LTRIM(@AllocationsJson),1)<>N'[' THROW 50000,N'Invalid meals',1;
 DECLARE @Rows TABLE(PlannedDate DATE,CategoryID INT,Portions DECIMAL(18,4),Notes NVARCHAR(500));
 INSERT @Rows SELECT PlannedDate,CategoryID,Portions,Notes FROM OPENJSON(@AllocationsJson) WITH(PlannedDate DATE,CategoryID INT,Portions DECIMAL(18,4),Notes NVARCHAR(500));
 IF NOT EXISTS(SELECT 1 FROM @Rows) OR EXISTS(SELECT 1 FROM @Rows WHERE PlannedDate IS NULL OR CategoryID IS NULL OR Portions IS NULL OR Portions<=0 OR Portions>1000) THROW 50000,N'Invalid allocations',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'separate-meals' AS kind,@RecipeID AS recipe,JSON_QUERY(@AllocationsJson) AS allocations FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@ID INT=0,@Batch INT,@Date DATE,@Slot INT,@Qty DECIMAL(18,4),@Notes NVARCHAR(500);
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  DECLARE entries CURSOR LOCAL FAST_FORWARD FOR SELECT PlannedDate,CategoryID,Portions,Notes FROM @Rows;
  OPEN entries; FETCH NEXT FROM entries INTO @Date,@Slot,@Qty,@Notes;
  WHILE @@FETCH_STATUS=0
  BEGIN
   INSERT dbo.CookingBatches(UserID,RecipeID,PlannedPortions,ResizeWithMeal) VALUES(@UserID,@RecipeID,@Qty,1); SET @Batch=SCOPE_IDENTITY();
   INSERT dbo.MealPlanEntries(UserID,RecipeID,CategoryID,PlannedDate,Notes,CookingBatchID,PortionQuantity) VALUES(@UserID,@RecipeID,@Slot,@Date,@Notes,@Batch,@Qty);
   SET @ID=SCOPE_IDENTITY(); EXEC dbo.sp_WriteAudit @UserID,N'MEAL_PLAN_ADD',N'MealPlanEntry',@ID;
   FETCH NEXT FROM entries INTO @Date,@Slot,@Qty,@Notes;
  END;
  CLOSE entries; DEALLOCATE entries;
  SET @Result=(SELECT @ID AS id,CONVERT(BIGINT,1) AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- Preview includes source batch versions; copied meals always use fresh planned batches.
CREATE OR ALTER PROCEDURE dbo.sp_GetWeekCopyToken @UserID INT,@Source DATE,@Token VARBINARY(32) OUTPUT
AS BEGIN
 SET NOCOUNT ON;
 SET @Token=HASHBYTES('SHA2_256',(SELECT p.MealPlanEntryID,p.RecipeID,p.CategoryID,p.PlannedDate,p.Servings,p.Notes,p.PortionQuantity,p.Version,b.Version AS BatchVersion
 FROM dbo.MealPlanEntries p JOIN dbo.CookingBatches b ON b.CookingBatchID=p.CookingBatchID WHERE p.UserID=@UserID AND p.PlannedDate>=@Source AND p.PlannedDate<DATEADD(DAY,7,@Source) ORDER BY p.MealPlanEntryID FOR JSON PATH));
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PreviewWeekCopy @UserID INT,@Source DATE,@Target DATE
AS BEGIN
 SET NOCOUNT ON;
 DECLARE @Token VARBINARY(32); EXEC dbo.sp_GetWeekCopyToken @UserID,@Source,@Token OUTPUT;
 SELECT c.*,r.Title AS RecipeTitle,cat.Name AS CategoryName,@Token AS PreviewToken,
 CONVERT(BIT,CASE WHEN EXISTS(SELECT 1 FROM dbo.MealPlanEntries t WHERE t.UserID=@UserID AND t.RecipeID=c.RecipeID AND t.PlannedDate=c.PlannedDate AND t.CategoryID=c.CategoryID) THEN 1 ELSE 0 END) AS IsDuplicate
 FROM dbo.fn_WeekCopy(@UserID,@Source,@Target)c JOIN dbo.Recipes r ON r.RecipeID=c.RecipeID JOIN dbo.Categories cat ON cat.CategoryID=c.CategoryID ORDER BY c.PlannedDate,c.CategoryID,c.RecipeID;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_CopyWeek @UserID INT,@Source DATE,@Target DATE,@PreviewToken VARBINARY(32)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Source IS NULL OR @Target IS NULL OR @PreviewToken IS NULL THROW 50000,N'Preview required',1;
 BEGIN TRY BEGIN TRAN;
 DECLARE @Resource NVARCHAR(255)=CONCAT(N'mealprep-user-',@UserID),@Lock INT,@Locked INT,@Actual VARBINARY(32);
 EXEC @Lock=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 50004,N'Planner busy',1;
 SELECT @Locked=COUNT(*) FROM dbo.MealPlanEntries WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@UserID AND ((PlannedDate>=@Source AND PlannedDate<DATEADD(DAY,7,@Source)) OR (PlannedDate>=@Target AND PlannedDate<DATEADD(DAY,7,@Target)));
 EXEC dbo.sp_GetWeekCopyToken @UserID,@Source,@Actual OUTPUT;
 IF @Actual<>@PreviewToken THROW 50004,N'Plan changed; preview again',1;
 DECLARE @Rows TABLE(OldBatch INT,RecipeID INT,CategoryID INT,PlannedDate DATE,Servings INT,Notes NVARCHAR(500),Portions DECIMAL(18,4));
 ;WITH ranked AS(SELECT p.*,ROW_NUMBER() OVER(PARTITION BY RecipeID,CategoryID,PlannedDate ORDER BY MealPlanEntryID) AS rn FROM dbo.MealPlanEntries p WHERE p.UserID=@UserID AND p.PlannedDate>=@Source AND p.PlannedDate<DATEADD(DAY,7,@Source))
 INSERT @Rows SELECT p.CookingBatchID,p.RecipeID,p.CategoryID,DATEADD(DAY,DATEDIFF(DAY,@Source,p.PlannedDate),@Target),p.Servings,p.Notes,p.PortionQuantity FROM ranked p
 WHERE rn=1 AND NOT EXISTS(SELECT 1 FROM dbo.MealPlanEntries t WHERE t.UserID=@UserID AND t.RecipeID=p.RecipeID AND t.CategoryID=p.CategoryID AND t.PlannedDate=DATEADD(DAY,DATEDIFF(DAY,@Source,p.PlannedDate),@Target));
 DECLARE @Old INT,@Recipe INT,@Qty DECIMAL(18,4),@Batch INT,@Count INT=0;
 DECLARE @Created TABLE(ID INT);
 DECLARE batches CURSOR LOCAL FAST_FORWARD FOR SELECT r.OldBatch,r.RecipeID,CASE WHEN MAX(b.PlannedPortions)>SUM(r.Portions) THEN MAX(b.PlannedPortions) ELSE SUM(r.Portions) END FROM @Rows r JOIN dbo.CookingBatches b ON b.CookingBatchID=r.OldBatch GROUP BY r.OldBatch,r.RecipeID;
 OPEN batches; FETCH NEXT FROM batches INTO @Old,@Recipe,@Qty;
 WHILE @@FETCH_STATUS=0
 BEGIN
  INSERT dbo.CookingBatches(UserID,RecipeID,PlannedPortions) VALUES(@UserID,@Recipe,@Qty); SET @Batch=SCOPE_IDENTITY();
  INSERT dbo.MealPlanEntries(UserID,RecipeID,CategoryID,PlannedDate,Servings,Notes,CookingBatchID,PortionQuantity)
  OUTPUT inserted.MealPlanEntryID INTO @Created
  SELECT @UserID,RecipeID,CategoryID,PlannedDate,Servings,Notes,@Batch,Portions FROM @Rows WHERE OldBatch=@Old;
  FETCH NEXT FROM batches INTO @Old,@Recipe,@Qty;
 END;
 CLOSE batches; DEALLOCATE batches;
 INSERT dbo.AuditLog(UserID,Action,EntityType,EntityID) SELECT @UserID,N'MEAL_COPY',N'MealPlanEntry',ID FROM @Created;
 SELECT @Count=COUNT(*) FROM @Created;
 COMMIT; SELECT @Count;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetClientOperationResult @UserID INT,@OperationID UNIQUEIDENTIFIER
AS BEGIN SET NOCOUNT ON; SELECT ResultJson FROM dbo.ClientOperations WHERE UserID=@UserID AND OperationID=@OperationID; END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetRecentRecipes @UserID INT,@TopN INT=12
AS BEGIN
 SET NOCOUNT ON; IF @TopN IS NULL OR @TopN<1 SET @TopN=12; IF @TopN>200 SET @TopN=200;
 SELECT TOP(@TopN) r.RecipeID,r.Title,r.CategoryID,c.Name AS CategoryName,r.PrepTimeMinutes,r.CookTimeMinutes,r.Servings,r.CreatedAt,r.UpdatedAt,
 COALESCE(r.UpdatedAt,r.CreatedAt) AS LastTouchedAt,COALESCE(p.ThumbnailData,p.ImageData) AS PhotoData
 FROM dbo.Recipes r LEFT JOIN dbo.Categories c ON c.CategoryID=r.CategoryID LEFT JOIN dbo.RecipePhotos p ON p.RecipeID=r.RecipeID
 WHERE r.UserID=@UserID ORDER BY COALESCE(r.UpdatedAt,r.CreatedAt) DESC,r.RecipeID DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetInventorySummary @UserID INT,@Today DATE
AS BEGIN
 SET NOCOUNT ON;
 ;WITH stock AS(SELECT b.IngredientID,m.CanonicalUnitID,SUM(b.RemainingQuantity*m.Multiplier) AS Qty FROM dbo.InventoryBatches b JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=b.UnitID WHERE b.UserID=@UserID AND (b.ExpiresOn IS NULL OR b.ExpiresOn>=@Today) GROUP BY b.IngredientID,m.CanonicalUnitID)
 SELECT (SELECT COUNT(*) FROM dbo.fn_BatchShoppingDemand(@UserID,@Today,DATEADD(DAY,6,@Today)) d LEFT JOIN stock s ON s.IngredientID=d.IngredientID AND s.CanonicalUnitID=d.UnitID WHERE d.NeededQty>COALESCE(s.Qty,0)) AS Shortages,
 (SELECT COUNT(*) FROM dbo.InventoryBatches WHERE UserID=@UserID AND RemainingQuantity>0 AND ExpiresOn BETWEEN @Today AND DATEADD(DAY,3,@Today)) AS ExpiringBatches,
 (SELECT COUNT(*) FROM dbo.StockThresholds t JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=t.UnitID LEFT JOIN stock s ON s.IngredientID=t.IngredientID AND s.CanonicalUnitID=m.CanonicalUnitID WHERE t.UserID=@UserID AND t.Quantity>0 AND COALESCE(s.Qty,0)<t.Quantity*m.Multiplier) AS LowStockIngredients;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AllocatePreparedMeals @UserID INT,@OperationID UNIQUEIDENTIFIER,@CookingBatchID INT,@ExpectedVersion BIGINT,@AllocationsJson NVARCHAR(MAX)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF ISJSON(@AllocationsJson)<>1 OR LEFT(LTRIM(@AllocationsJson),1)<>N'[' THROW 50000,N'Invalid allocations',1;
 DECLARE @Rows TABLE(PlannedDate DATE,CategoryID INT,Portions DECIMAL(18,4),Notes NVARCHAR(500));
 INSERT @Rows SELECT PlannedDate,CategoryID,Portions,Notes FROM OPENJSON(@AllocationsJson) WITH(PlannedDate DATE,CategoryID INT,Portions DECIMAL(18,4),Notes NVARCHAR(500));
 IF NOT EXISTS(SELECT 1 FROM @Rows) OR EXISTS(SELECT 1 FROM @Rows WHERE PlannedDate IS NULL OR CategoryID IS NULL OR Portions IS NULL OR Portions<=0) THROW 50000,N'Invalid allocations',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'allocate-prepared' AS kind,@CookingBatchID AS batch,@ExpectedVersion AS version,JSON_QUERY(@AllocationsJson) AS allocations FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Recipe INT,@Version BIGINT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Recipe=RecipeID FROM dbo.CookingBatches WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@UserID AND CookingBatchID=@CookingBatchID AND Version=@ExpectedVersion AND CookedAt IS NOT NULL;
  IF @Recipe IS NULL THROW 50004,N'Prepared batch changed',1;
  INSERT dbo.MealPlanEntries(UserID,RecipeID,CategoryID,PlannedDate,Notes,CookingBatchID,PortionQuantity) SELECT @UserID,@Recipe,CategoryID,PlannedDate,Notes,@CookingBatchID,Portions FROM @Rows;
  SELECT @Version=Version FROM dbo.CookingBatches WHERE CookingBatchID=@CookingBatchID;
  EXEC dbo.sp_WriteAudit @UserID,N'PREPARED_ALLOCATE',N'CookingBatch',@CookingBatchID;
  SET @Result=(SELECT @CookingBatchID AS id,@Version AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ClearInventoryRows @UserID INT,@OperationID UNIQUEIDENTIFIER,@IngredientID INT,@UnitID INT,@ExpectedBatchesJson NVARCHAR(MAX)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF ISJSON(@ExpectedBatchesJson)<>1 THROW 50000,N'Batch review required',1;
 DECLARE @Expected TABLE(BatchID INT PRIMARY KEY,Version BIGINT);
 INSERT @Expected SELECT BatchID,Version FROM OPENJSON(@ExpectedBatchesJson) WITH(BatchID INT,Version BIGINT);
 DECLARE @Events TABLE(ID INT);
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'clear-stock' AS kind,@IngredientID AS ingredient,@UnitID AS unit,JSON_QUERY(@ExpectedBatchesJson) AS batches FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@EventJson NVARCHAR(MAX);
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM @Expected) OR EXISTS(SELECT 1 FROM @Expected e LEFT JOIN dbo.InventoryBatches b WITH(UPDLOCK,HOLDLOCK) ON b.BatchID=e.BatchID AND b.UserID=@UserID AND b.IngredientID=@IngredientID AND b.UnitID=@UnitID WHERE b.BatchID IS NULL OR b.Version<>e.Version OR b.RemainingQuantity<=0)
   OR EXISTS(SELECT 1 FROM dbo.InventoryBatches b WHERE b.UserID=@UserID AND b.IngredientID=@IngredientID AND b.UnitID=@UnitID AND b.RemainingQuantity>0 AND NOT EXISTS(SELECT 1 FROM @Expected e WHERE e.BatchID=b.BatchID)) THROW 50004,N'Stock changed; review again',1;
  INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter) OUTPUT inserted.EventID INTO @Events SELECT b.BatchID,@UserID,-b.RemainingQuantity,N'Correction',b.Version+1 FROM dbo.InventoryBatches b JOIN @Expected e ON e.BatchID=b.BatchID;
  UPDATE dbo.InventoryBatches SET RemainingQuantity=0,Version=Version+1,UpdatedAt=SYSUTCDATETIME() WHERE BatchID IN(SELECT BatchID FROM @Expected);
  EXEC dbo.sp_RefreshPantryTotal @UserID,@IngredientID,@UnitID;
  EXEC dbo.sp_WriteAudit @UserID,N'STOCK_CLEAR',N'Ingredient',@IngredientID;
  SELECT @EventJson=CONCAT(N'[',STRING_AGG(CONVERT(NVARCHAR(MAX),ID),N','),N']') FROM @Events;
  SET @Result=(SELECT @IngredientID AS id,CONVERT(BIGINT,0) AS version,JSON_QUERY(@EventJson) AS eventIds FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_UndoStockGroup @UserID INT,@OperationID UNIQUEIDENTIFIER,@EventIdsJson NVARCHAR(MAX)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF ISJSON(@EventIdsJson)<>1 THROW 50000,N'Events required',1;
 DECLARE @Events TABLE(ID INT PRIMARY KEY); INSERT @Events SELECT CONVERT(INT,value) FROM OPENJSON(@EventIdsJson);
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'undo-stock-group' AS kind,JSON_QUERY(@EventIdsJson) AS events FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@I INT,@U INT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM @Events) OR EXISTS(SELECT 1 FROM @Events x LEFT JOIN dbo.StockEvents e ON e.EventID=x.ID LEFT JOIN dbo.InventoryBatches b WITH(UPDLOCK,HOLDLOCK) ON b.BatchID=e.BatchID WHERE e.UserID<>@UserID OR e.EventID IS NULL OR e.Kind<>N'Correction' OR b.Version<>e.VersionAfter OR b.RemainingQuantity-e.QuantityDelta<0 OR EXISTS(SELECT 1 FROM dbo.StockEvents u WHERE u.UndoOfEventID=x.ID)) THROW 50004,N'Subsequent use prevents undo',1;
  UPDATE b SET RemainingQuantity=b.RemainingQuantity-e.QuantityDelta,Version=b.Version+1,UpdatedAt=SYSUTCDATETIME() FROM dbo.InventoryBatches b JOIN dbo.StockEvents e ON e.BatchID=b.BatchID JOIN @Events x ON x.ID=e.EventID;
  INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter,UndoOfEventID) SELECT b.BatchID,@UserID,-e.QuantityDelta,N'Undo',b.Version,e.EventID FROM dbo.InventoryBatches b JOIN dbo.StockEvents e ON e.BatchID=b.BatchID JOIN @Events x ON x.ID=e.EventID;
  DECLARE totals CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT b.IngredientID,b.UnitID FROM dbo.InventoryBatches b JOIN dbo.StockEvents e ON e.BatchID=b.BatchID JOIN @Events x ON x.ID=e.EventID;
  OPEN totals; FETCH NEXT FROM totals INTO @I,@U; WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.sp_RefreshPantryTotal @UserID,@I,@U;FETCH NEXT FROM totals INTO @I,@U;END;CLOSE totals;DEALLOCATE totals;
  EXEC dbo.sp_WriteAudit @UserID,N'STOCK_GROUP_UNDO',N'Ingredient',@I;
  SET @Result=N'{"id":0,"version":0}'; EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT;SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
