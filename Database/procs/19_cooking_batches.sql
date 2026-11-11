USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetCookingBatches @UserID INT
AS BEGIN
 SET NOCOUNT ON;
 SELECT b.*,COALESCE(r.Title,JSON_VALUE(b.RecipeSnapshot,'$.Title')) AS RecipeTitle,
 COALESCE(b.ProducedPortions,b.PlannedPortions)-b.UsedPortions AS RemainingPortions,
 COALESCE((SELECT SUM(p.PortionQuantity-p.ConsumedPortions) FROM dbo.MealPlanEntries p WHERE p.CookingBatchID=b.CookingBatchID),0) AS ReservedPortions
 FROM dbo.CookingBatches b LEFT JOIN dbo.Recipes r ON r.RecipeID=b.RecipeID WHERE b.UserID=@UserID ORDER BY b.CookingBatchID DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PlanCookingBatch @UserID INT,@OperationID UNIQUEIDENTIFIER,@RecipeID INT,@PlannedPortions DECIMAL(18,4),@AllocationsJson NVARCHAR(MAX)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @PlannedPortions IS NULL OR @PlannedPortions<=0 OR @PlannedPortions>1000 OR ISJSON(@AllocationsJson)<>1 OR LEFT(LTRIM(@AllocationsJson),1)<>N'[' THROW 50000,N'Invalid batch plan',1;
 DECLARE @Alloc TABLE(PlannedDate DATE,CategoryID INT,Portions DECIMAL(18,4),Notes NVARCHAR(500));
 INSERT @Alloc SELECT PlannedDate,CategoryID,Portions,Notes FROM OPENJSON(@AllocationsJson) WITH(PlannedDate DATE,CategoryID INT,Portions DECIMAL(18,4),Notes NVARCHAR(500));
 IF EXISTS(SELECT 1 FROM @Alloc WHERE PlannedDate IS NULL OR CategoryID IS NULL OR Portions IS NULL OR Portions<=0) OR (SELECT SUM(Portions) FROM @Alloc)>@PlannedPortions THROW 50000,N'Invalid allocations',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'plan-batch' AS kind,@RecipeID AS recipe,@PlannedPortions AS portions,JSON_QUERY(@AllocationsJson) AS allocations FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@ID INT,@Version BIGINT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  INSERT dbo.CookingBatches(UserID,RecipeID,PlannedPortions) VALUES(@UserID,@RecipeID,@PlannedPortions); SET @ID=SCOPE_IDENTITY();
  INSERT dbo.MealPlanEntries(UserID,RecipeID,CategoryID,PlannedDate,Servings,Notes,CookingBatchID,PortionQuantity)
  SELECT @UserID,@RecipeID,CategoryID,PlannedDate,NULL,Notes,@ID,Portions FROM @Alloc;
  SELECT @Version=Version FROM dbo.CookingBatches WHERE CookingBatchID=@ID;
  EXEC dbo.sp_WriteAudit @UserID,N'BATCH_PLAN',N'CookingBatch',@ID;
  SET @Result=(SELECT @ID AS id,@Version AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AllocateCookingBatch @UserID INT,@OperationID UNIQUEIDENTIFIER,@CookingBatchID INT,@ExpectedVersion BIGINT,@PlannedDate DATE,@CategoryID INT,@Portions DECIMAL(18,4)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Portions IS NULL OR @Portions<=0 OR @PlannedDate IS NULL THROW 50000,N'Invalid allocation',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'allocate-batch' AS kind,@CookingBatchID AS batch,@ExpectedVersion AS version,@PlannedDate AS date,@CategoryID AS slot,@Portions AS portions FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Recipe INT,@ID INT,@Version BIGINT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Recipe=RecipeID FROM dbo.CookingBatches WITH(UPDLOCK,HOLDLOCK) WHERE CookingBatchID=@CookingBatchID AND UserID=@UserID AND Version=@ExpectedVersion;
  IF @Recipe IS NULL THROW 50004,N'Batch changed or unavailable',1;
  INSERT dbo.MealPlanEntries(UserID,RecipeID,CategoryID,PlannedDate,Servings,CookingBatchID,PortionQuantity) VALUES(@UserID,@Recipe,@CategoryID,@PlannedDate,NULL,@CookingBatchID,@Portions);
  SET @ID=SCOPE_IDENTITY(); SELECT @Version=Version FROM dbo.CookingBatches WHERE CookingBatchID=@CookingBatchID;
  EXEC dbo.sp_WriteAudit @UserID,N'BATCH_ALLOCATE',N'MealPlanEntry',@ID;
  SET @Result=(SELECT @ID AS id,@Version AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_FinishCookingBatch @UserID INT,@OperationID UNIQUEIDENTIFIER,@CookingBatchID INT,@ExpectedVersion BIGINT,@ProducedPortions DECIMAL(18,4),@ConsumedPortions DECIMAL(18,4),@PreparedOn DATE,@ExpiresOn DATE=NULL,@DeductionsJson NVARCHAR(MAX)=N'[]',@IncludeExpired BIT=0
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @ProducedPortions IS NULL OR @ProducedPortions<=0 OR @ProducedPortions>1000 OR @ConsumedPortions IS NULL OR @ConsumedPortions<0 OR @ConsumedPortions>@ProducedPortions OR @PreparedOn IS NULL OR @ExpiresOn<@PreparedOn OR ISJSON(@DeductionsJson)<>1 OR LEFT(LTRIM(@DeductionsJson),1)<>N'[' THROW 50000,N'Invalid cooking review',1;
 DECLARE @Deduct TABLE(BatchID INT,ExpectedVersion BIGINT,Quantity DECIMAL(18,4));
 INSERT @Deduct SELECT BatchID,ExpectedVersion,Quantity FROM OPENJSON(@DeductionsJson) WITH(BatchID INT,ExpectedVersion BIGINT,Quantity DECIMAL(18,4));
 IF EXISTS(SELECT 1 FROM @Deduct WHERE BatchID IS NULL OR ExpectedVersion IS NULL OR Quantity IS NULL OR Quantity<=0) OR EXISTS(SELECT BatchID FROM @Deduct GROUP BY BatchID HAVING COUNT(*)>1) THROW 50000,N'Invalid ingredient deductions',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'finish-batch' AS kind,@CookingBatchID AS batch,@ExpectedVersion AS version,@ProducedPortions AS produced,@ConsumedPortions AS consumed,@PreparedOn AS preparedOn,@ExpiresOn AS expiresOn,JSON_QUERY(@DeductionsJson) AS deductions,@IncludeExpired AS includeExpired FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Recipe INT,@Version BIGINT,@RecipeSnapshot NVARCHAR(MAX),@Nutrition NVARCHAR(MAX);
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Recipe=RecipeID FROM dbo.CookingBatches WITH(UPDLOCK,HOLDLOCK) WHERE CookingBatchID=@CookingBatchID AND UserID=@UserID AND Version=@ExpectedVersion AND CookedAt IS NULL;
  IF @Recipe IS NULL THROW 50004,N'Batch changed or already cooked',1;
  -- Lock ingredient versions before applying all deductions atomically.
  IF EXISTS(SELECT 1 FROM @Deduct d LEFT JOIN dbo.InventoryBatches b WITH(UPDLOCK,HOLDLOCK) ON b.BatchID=d.BatchID AND b.UserID=@UserID
   WHERE b.BatchID IS NULL OR b.Version<>d.ExpectedVersion OR b.RemainingQuantity<d.Quantity OR (@IncludeExpired=0 AND b.ExpiresOn<@PreparedOn)) THROW 50004,N'Stock changed or expired; review again',1;
  SET @RecipeSnapshot=(SELECT r.Title,r.Instructions,r.Description,r.Servings,JSON_QUERY((SELECT ri.IngredientID,i.Name AS IngredientName,ri.UnitID,u.Abbreviation AS UnitAbbreviation,ri.Quantity,ri.Notes FROM dbo.RecipeIngredients ri JOIN dbo.Ingredients i ON i.IngredientID=ri.IngredientID JOIN dbo.Units u ON u.UnitID=ri.UnitID WHERE ri.RecipeID=r.RecipeID FOR JSON PATH)) AS Ingredients FROM dbo.Recipes r WITH(HOLDLOCK) WHERE r.RecipeID=@Recipe FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  DECLARE @N TABLE(RecipeID INT,Servings INT,TotalCalories DECIMAL(10,2),TotalProteinGrams DECIMAL(10,2),TotalCarbsGrams DECIMAL(10,2),TotalFatGrams DECIMAL(10,2),CaloriesPerServing DECIMAL(10,2),ProteinGramsPerServing DECIMAL(10,2),CarbsGramsPerServing DECIMAL(10,2),FatGramsPerServing DECIMAL(10,2),MissingNutritionCount INT,UnconvertibleIngredientCount INT);
  INSERT @N EXEC dbo.sp_GetRecipeNutrition @Recipe;
  SET @Nutrition=(SELECT * FROM @N FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  UPDATE dbo.InventoryBatches SET RemainingQuantity=b.RemainingQuantity-d.Quantity,Version=b.Version+1,UpdatedAt=SYSUTCDATETIME() FROM dbo.InventoryBatches b JOIN @Deduct d ON d.BatchID=b.BatchID;
  INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter,CookingBatchID) SELECT b.BatchID,@UserID,-d.Quantity,N'Cooking',b.Version,@CookingBatchID FROM dbo.InventoryBatches b JOIN @Deduct d ON d.BatchID=b.BatchID;
  DECLARE @Ingredient INT,@Unit INT;
  DECLARE totals CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT b.IngredientID,b.UnitID FROM dbo.InventoryBatches b JOIN @Deduct d ON d.BatchID=b.BatchID;
  OPEN totals; FETCH NEXT FROM totals INTO @Ingredient,@Unit;
  WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.sp_RefreshPantryTotal @UserID,@Ingredient,@Unit; FETCH NEXT FROM totals INTO @Ingredient,@Unit; END;
  CLOSE totals; DEALLOCATE totals;
  -- Initial consumption uses allocated portions first, preserving future reservations.
  DECLARE @Left DECIMAL(18,4)=@ConsumedPortions,@Entry INT,@Allocation DECIMAL(18,4),@Take DECIMAL(18,4);
  DECLARE meals CURSOR LOCAL FAST_FORWARD FOR SELECT MealPlanEntryID,PortionQuantity-ConsumedPortions FROM dbo.MealPlanEntries WHERE CookingBatchID=@CookingBatchID ORDER BY PlannedDate,MealPlanEntryID;
  OPEN meals; FETCH NEXT FROM meals INTO @Entry,@Allocation;
  WHILE @@FETCH_STATUS=0 AND @Left>0
  BEGIN
   SET @Take=CASE WHEN @Allocation<@Left THEN @Allocation ELSE @Left END;
   UPDATE dbo.MealPlanEntries SET ConsumedPortions=ConsumedPortions+@Take WHERE MealPlanEntryID=@Entry;
   SET @Left=@Left-@Take; FETCH NEXT FROM meals INTO @Entry,@Allocation;
  END;
  CLOSE meals; DEALLOCATE meals;
  IF (SELECT COALESCE(SUM(PortionQuantity-ConsumedPortions),0) FROM dbo.MealPlanEntries WHERE CookingBatchID=@CookingBatchID)>@ProducedPortions-@ConsumedPortions THROW 50004,N'Production is below remaining meal allocations; review the plan',1;
  UPDATE dbo.CookingBatches SET ProducedPortions=@ProducedPortions,UsedPortions=@ConsumedPortions,RecipeSnapshot=@RecipeSnapshot,NutritionSnapshot=@Nutrition,PreparedOn=@PreparedOn,ExpiresOn=@ExpiresOn,CookedAt=SYSUTCDATETIME(),Version=Version+1 WHERE CookingBatchID=@CookingBatchID;
  IF @ConsumedPortions>0 INSERT dbo.PortionEvents(CookingBatchID,UserID,Kind,Portions) VALUES(@CookingBatchID,@UserID,N'Consumed',@ConsumedPortions);
  SELECT @Version=Version FROM dbo.CookingBatches WHERE CookingBatchID=@CookingBatchID;
  EXEC dbo.sp_WriteAudit @UserID,N'BATCH_COOKED',N'CookingBatch',@CookingBatchID;
  SET @Result=(SELECT @CookingBatchID AS id,@Version AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_RecordPortionEvent @UserID INT,@OperationID UNIQUEIDENTIFIER,@CookingBatchID INT,@ExpectedVersion BIGINT,@Portions DECIMAL(18,4),@Kind NVARCHAR(20),@MealPlanEntryID INT=NULL
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Portions IS NULL OR @Portions<=0 OR @Kind NOT IN(N'Consumed',N'Discarded') THROW 50000,N'Invalid portion event',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'portion-event' AS kind,@CookingBatchID AS batch,@ExpectedVersion AS version,@Portions AS portions,@Kind AS reason,@MealPlanEntryID AS meal FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Produced DECIMAL(18,4),@Used DECIMAL(18,4),@Reserved DECIMAL(18,4),@Version BIGINT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Produced=ProducedPortions,@Used=UsedPortions FROM dbo.CookingBatches WITH(UPDLOCK,HOLDLOCK) WHERE CookingBatchID=@CookingBatchID AND UserID=@UserID AND Version=@ExpectedVersion AND CookedAt IS NOT NULL;
  IF @Produced IS NULL THROW 50004,N'Prepared batch changed or unavailable',1;
  SELECT @Reserved=COALESCE(SUM(PortionQuantity-ConsumedPortions),0) FROM dbo.MealPlanEntries WHERE CookingBatchID=@CookingBatchID;
  IF @MealPlanEntryID IS NOT NULL
  BEGIN
   IF @Kind<>N'Consumed' OR NOT EXISTS(SELECT 1 FROM dbo.MealPlanEntries WHERE MealPlanEntryID=@MealPlanEntryID AND CookingBatchID=@CookingBatchID AND UserID=@UserID AND PortionQuantity-ConsumedPortions>=@Portions) THROW 50004,N'Meal allocation changed',1;
   UPDATE dbo.MealPlanEntries SET ConsumedPortions=ConsumedPortions+@Portions WHERE MealPlanEntryID=@MealPlanEntryID;
  END
  ELSE IF @Produced-@Used-@Reserved<@Portions THROW 50004,N'Portions are reserved for planned meals',1;
  IF @Produced-@Used<@Portions THROW 50004,N'Insufficient prepared portions',1;
  UPDATE dbo.CookingBatches SET UsedPortions=UsedPortions+@Portions,Version=Version+1 WHERE CookingBatchID=@CookingBatchID;
  INSERT dbo.PortionEvents(CookingBatchID,UserID,MealPlanEntryID,Kind,Portions) VALUES(@CookingBatchID,@UserID,@MealPlanEntryID,@Kind,@Portions);
  SELECT @Version=Version FROM dbo.CookingBatches WHERE CookingBatchID=@CookingBatchID;
  EXEC dbo.sp_WriteAudit @UserID,N'PORTION_EVENT',N'CookingBatch',@CookingBatchID;
  SET @Result=(SELECT @CookingBatchID AS id,@Version AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetWeeklyPlan @UserID INT,@StartDate DATE
AS BEGIN
 SET NOCOUNT ON;
 SELECT p.MealPlanEntryID,p.PlannedDate,p.CategoryID,c.Name AS CategoryName,p.RecipeID,r.Title AS RecipeTitle,p.Servings,p.Notes,p.CreatedAt,
 p.CookingBatchID,p.PortionQuantity,p.ConsumedPortions,p.Version,b.Version AS BatchVersion,b.PlannedPortions,b.ProducedPortions,b.CookedAt,b.ExpiresOn AS PreparedExpiresOn
 FROM dbo.MealPlanEntries p JOIN dbo.Recipes r ON r.RecipeID=p.RecipeID JOIN dbo.Categories c ON c.CategoryID=p.CategoryID LEFT JOIN dbo.CookingBatches b ON b.CookingBatchID=p.CookingBatchID
 WHERE p.UserID=@UserID AND p.PlannedDate>=@StartDate AND p.PlannedDate<DATEADD(DAY,7,@StartDate) ORDER BY p.PlannedDate,p.CategoryID,p.MealPlanEntryID;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetMonthlyPlan @UserID INT,@Year INT,@Month INT
AS BEGIN
 SET NOCOUNT ON;
 DECLARE @Start DATE=DATEFROMPARTS(@Year,@Month,1);
 SELECT p.MealPlanEntryID,p.PlannedDate,p.CategoryID,c.Name AS CategoryName,p.RecipeID,r.Title AS RecipeTitle,p.Servings,p.Notes,p.CreatedAt,
 p.CookingBatchID,p.PortionQuantity,p.ConsumedPortions,p.Version,b.Version AS BatchVersion,b.PlannedPortions,b.ProducedPortions,b.CookedAt,b.ExpiresOn AS PreparedExpiresOn
 FROM dbo.MealPlanEntries p JOIN dbo.Recipes r ON r.RecipeID=p.RecipeID JOIN dbo.Categories c ON c.CategoryID=p.CategoryID LEFT JOIN dbo.CookingBatches b ON b.CookingBatchID=p.CookingBatchID
 WHERE p.UserID=@UserID AND p.PlannedDate>=@Start AND p.PlannedDate<DATEADD(MONTH,1,@Start) ORDER BY p.PlannedDate,p.CategoryID,p.MealPlanEntryID;
END;
GO
