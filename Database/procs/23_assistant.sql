USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetApplicationIdentity
AS BEGIN SET NOCOUNT ON;SELECT DatabaseID FROM dbo.ApplicationIdentity WHERE Singleton=1;END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AssistantPreferences @UserID INT,@Json NVARCHAR(MAX)=NULL,@Notice NVARCHAR(30)=NULL,@Accept BIT=NULL
AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @Json IS NOT NULL AND ISJSON(@Json)<>1 THROW 50000,N'Invalid preferences',1;
 BEGIN TRY BEGIN TRAN;
 IF NOT EXISTS(SELECT 1 FROM dbo.AssistantPreferences WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@UserID)
 BEGIN
  INSERT dbo.AssistantPreferences(UserID,PreferencesJson) VALUES(@UserID,N'{}');
  EXEC dbo.sp_WriteAudit @UserID,N'ASSISTANT_PREFERENCES_INIT',N'User',@UserID;
 END;
 IF @Json IS NOT NULL OR @Accept IS NOT NULL
 BEGIN
  UPDATE dbo.AssistantPreferences SET PreferencesJson=COALESCE(@Json,PreferencesJson),
   NoticeVersion=CASE WHEN @Accept=1 THEN @Notice WHEN @Accept=0 THEN NULL ELSE NoticeVersion END,
   AcceptedAt=CASE WHEN @Accept=1 THEN SYSUTCDATETIME() WHEN @Accept=0 THEN NULL ELSE AcceptedAt END,
   Version=Version+1,UpdatedAt=SYSUTCDATETIME() WHERE UserID=@UserID;
  EXEC dbo.sp_WriteAudit @UserID,N'ASSISTANT_PREFERENCES',N'User',@UserID;
 END;
 COMMIT;SELECT PreferencesJson,NoticeVersion,AcceptedAt,Version FROM dbo.AssistantPreferences WHERE UserID=@UserID;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AssistantJob @Action NVARCHAR(20),@UserID INT=0,@JobID UNIQUEIDENTIFIER=NULL,@SessionID UNIQUEIDENTIFIER=NULL,
 @Kind NVARCHAR(20)=NULL,@Work NVARCHAR(MAX)=NULL,@Result NVARCHAR(MAX)=NULL,@Error NVARCHAR(80)=NULL,@InputTokens INT=0,@OutputTokens INT=0
AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRAN;
 IF @Action=N'create'
 BEGIN
  IF @JobID IS NULL OR @JobID='00000000-0000-0000-0000-000000000000' OR ISJSON(@Work)<>1 OR
   NOT EXISTS(SELECT 1 FROM dbo.DeviceSessions s JOIN dbo.Users u ON u.UserID=s.UserID WHERE s.SessionID=@SessionID AND s.UserID=@UserID AND s.RevokedAt IS NULL AND s.ExpiresAt>SYSUTCDATETIME() AND s.CredentialHash=HASHBYTES('SHA2_256',u.PasswordHash)) OR
   NOT EXISTS(SELECT 1 FROM dbo.AssistantPreferences WHERE UserID=@UserID AND NoticeVersion=N'gemini-free-1' AND AcceptedAt IS NOT NULL)
   THROW 50002,N'Assistant consent and active session required',1;
  IF EXISTS(SELECT 1 FROM dbo.AssistantJobs WITH(UPDLOCK,HOLDLOCK) WHERE JobID=@JobID AND (UserID<>@UserID OR RequestHash<>HASHBYTES('SHA2_256',JSON_QUERY(@Work,'$.request')))) THROW 50004,N'Operation reused',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.AssistantJobs WHERE JobID=@JobID)
  BEGIN
   IF (SELECT COUNT(*) FROM dbo.AssistantJobs WHERE UserID=@UserID AND State IN(N'Queued',N'Running'))>=2 THROW 50004,N'Wait for pending requests',1;
   INSERT dbo.AssistantJobs(JobID,UserID,SessionID,Kind,WorkJson,RequestHash) VALUES(@JobID,@UserID,@SessionID,@Kind,@Work,HASHBYTES('SHA2_256',JSON_QUERY(@Work,'$.request')));
   EXEC dbo.sp_WriteAudit @UserID,N'ASSISTANT_REQUEST',N'AssistantJob',NULL;
  END;
 END;
 IF @Action=N'claim'
 BEGIN
  -- A single hosted worker serializes provider requests. Recheck the originating session.
  SELECT TOP(1) @JobID=j.JobID,@UserID=j.UserID FROM dbo.AssistantJobs j WITH(UPDLOCK,READPAST)
   JOIN dbo.DeviceSessions s ON s.SessionID=j.SessionID JOIN dbo.Users u ON u.UserID=j.UserID JOIN dbo.AssistantPreferences pref ON pref.UserID=j.UserID AND pref.AcceptedAt IS NOT NULL AND pref.NoticeVersion=N'gemini-free-1'
   WHERE j.State=N'Queued' AND j.ExpiresAt>SYSUTCDATETIME() AND s.RevokedAt IS NULL AND s.ExpiresAt>SYSUTCDATETIME() AND s.CredentialHash=HASHBYTES('SHA2_256',u.PasswordHash)
   ORDER BY j.CreatedAt;
  UPDATE dbo.AssistantJobs SET State=N'Running',Version=Version+1 WHERE JobID=@JobID AND State=N'Queued';
  IF @@ROWCOUNT>0 EXEC dbo.sp_WriteAudit @UserID,N'ASSISTANT_START',N'AssistantJob',NULL;
 END;
 IF @Action IN(N'complete',N'fail')
 BEGIN
  IF @Action=N'complete' AND ISJSON(@Result)<>1 THROW 50000,N'Invalid proposal',1;
  IF @Action=N'complete' AND NOT EXISTS(SELECT 1 FROM dbo.AssistantJobs j JOIN dbo.DeviceSessions s ON s.SessionID=j.SessionID JOIN dbo.Users u ON u.UserID=j.UserID
    JOIN dbo.AssistantPreferences pref ON pref.UserID=j.UserID WHERE j.JobID=@JobID AND j.UserID=@UserID AND s.RevokedAt IS NULL AND s.ExpiresAt>SYSUTCDATETIME()
    AND s.CredentialHash=HASHBYTES('SHA2_256',u.PasswordHash) AND pref.NoticeVersion=N'gemini-free-1' AND pref.AcceptedAt IS NOT NULL)
   BEGIN SET @Action=N'fail';SET @Error=N'SessionExpired';SET @Result=NULL;END;
  UPDATE dbo.AssistantJobs SET State=CASE WHEN @Action=N'complete' THEN N'Completed' ELSE N'Failed' END,
   ResultJson=@Result,ErrorCode=@Error,InputTokens=@InputTokens,OutputTokens=@OutputTokens,WorkJson=CASE WHEN @Action=N'complete' THEN WorkJson ELSE NULL END,Version=Version+1
   WHERE JobID=@JobID AND UserID=@UserID AND State=N'Running';
  IF @@ROWCOUNT>0 EXEC dbo.sp_WriteAudit @UserID,N'ASSISTANT_RESULT',N'AssistantJob',NULL;
 END;
 IF @Action=N'cancel'
 BEGIN
  UPDATE dbo.AssistantJobs SET State=N'Cancelled',WorkJson=NULL,ResultJson=NULL,Version=Version+1
   WHERE JobID=@JobID AND UserID=@UserID AND State IN(N'Queued',N'Running',N'Completed');
  IF @@ROWCOUNT>0 EXEC dbo.sp_WriteAudit @UserID,N'ASSISTANT_CANCEL',N'AssistantJob',NULL;
 END;
 IF @Action=N'recover'
 BEGIN
  UPDATE dbo.AssistantJobs SET State=N'Failed',ErrorCode=N'Interrupted',WorkJson=NULL,Version=Version+1 WHERE State=N'Running';
  IF @@ROWCOUNT>0 EXEC dbo.sp_WriteAudit NULL,N'ASSISTANT_RECOVER',N'AssistantJob',NULL;
 END;
 IF @Action=N'cleanup'
 BEGIN
  UPDATE dbo.AssistantJobs SET State=N'Expired',WorkJson=NULL,ResultJson=NULL,Version=Version+1 WHERE ExpiresAt<=SYSUTCDATETIME() AND State<>N'Expired';
  IF @@ROWCOUNT>0 EXEC dbo.sp_WriteAudit NULL,N'ASSISTANT_EXPIRE',N'AssistantJob',NULL;
  UPDATE j SET State=N'Cancelled',WorkJson=NULL,Version=j.Version+1 FROM dbo.AssistantJobs j JOIN dbo.DeviceSessions s ON s.SessionID=j.SessionID JOIN dbo.Users u ON u.UserID=j.UserID JOIN dbo.AssistantPreferences pref ON pref.UserID=j.UserID AND pref.AcceptedAt IS NOT NULL AND pref.NoticeVersion=N'gemini-free-1'
   WHERE j.State IN(N'Queued',N'Running') AND (s.RevokedAt IS NOT NULL OR s.ExpiresAt<=SYSUTCDATETIME() OR s.CredentialHash<>HASHBYTES('SHA2_256',u.PasswordHash) OR NOT EXISTS(SELECT 1 FROM dbo.AssistantPreferences pref WHERE pref.UserID=j.UserID AND pref.NoticeVersion=N'gemini-free-1' AND pref.AcceptedAt IS NOT NULL));
  IF @@ROWCOUNT>0 EXEC dbo.sp_WriteAudit NULL,N'ASSISTANT_SESSION_CANCEL',N'AssistantJob',NULL;
 END;
 COMMIT;
 IF @Action=N'list' SELECT TOP(30) JobID,UserID,SessionID,Kind,State,Version,CAST(NULL AS NVARCHAR(MAX)) AS WorkJson,ResultJson,ErrorCode,CreatedAt,ExpiresAt,InputTokens,OutputTokens FROM dbo.AssistantJobs WHERE UserID=@UserID AND ExpiresAt>SYSUTCDATETIME() ORDER BY CreatedAt DESC;
 ELSE IF @Action=N'usage' SELECT COUNT(*) AS Requests,COALESCE(SUM(CONVERT(BIGINT,InputTokens)),0) AS InputTokens,COALESCE(SUM(CONVERT(BIGINT,OutputTokens)),0) AS OutputTokens FROM dbo.AssistantJobs WHERE UserID=@UserID AND CreatedAt>=DATEADD(DAY,-30,SYSUTCDATETIME());
 ELSE SELECT JobID,UserID,SessionID,Kind,State,Version,CASE WHEN @Action IN(N'claim',N'get') THEN WorkJson ELSE NULL END AS WorkJson,ResultJson,ErrorCode,CreatedAt,ExpiresAt,InputTokens,OutputTokens FROM dbo.AssistantJobs WHERE JobID=@JobID AND UserID=@UserID;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AssistantUpload @UserID INT,@UploadID UNIQUEIDENTIFIER,@Hash BINARY(32)=NULL
AS BEGIN
 SET NOCOUNT ON;
 IF @Hash IS NOT NULL
 BEGIN
  INSERT dbo.AssistantUploads(UploadID,UserID,ContentHash) VALUES(@UploadID,@UserID,@Hash);
  EXEC dbo.sp_WriteAudit @UserID,N'ASSISTANT_UPLOAD',N'User',@UserID;
 END;
 SELECT UploadID,ContentHash,ExpiresAt FROM dbo.AssistantUploads WHERE UploadID=@UploadID AND UserID=@UserID AND ExpiresAt>SYSUTCDATETIME();
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ApplyReviewedPlan @UserID INT,@OperationID UNIQUEIDENTIFIER,@Meals NVARCHAR(MAX),@Replace NVARCHAR(MAX),@Recipes NVARCHAR(MAX),@Entries NVARCHAR(MAX),@Batches NVARCHAR(MAX)
AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF ISJSON(@Meals)<>1 OR ISJSON(@Replace)<>1 OR ISJSON(@Recipes)<>1 OR ISJSON(@Entries)<>1 OR ISJSON(@Batches)<>1 THROW 50000,N'Invalid plan',1;
 DECLARE @Rows TABLE(PlannedDate DATE,CategoryID INT,RecipeID INT,Portions DECIMAL(18,4),BatchKey NVARCHAR(80),PreparedBatchID INT,Reason NVARCHAR(500));
 INSERT @Rows SELECT * FROM OPENJSON(@Meals) WITH(PlannedDate DATE '$.date',CategoryID INT '$.slotId',RecipeID INT '$.recipeId',Portions DECIMAL(18,4) '$.portions',BatchKey NVARCHAR(80) '$.batchKey',PreparedBatchID INT '$.preparedBatchId',Reason NVARCHAR(500) '$.reason');
 IF NOT EXISTS(SELECT 1 FROM @Rows) OR (SELECT COUNT(*) FROM @Rows)>84 OR EXISTS(SELECT 1 FROM @Rows WHERE PlannedDate IS NULL OR PlannedDate<CONVERT(DATE,GETDATE()) OR CategoryID IS NULL OR RecipeID IS NULL OR Portions IS NULL OR Portions<=0 OR Portions>1000 OR NULLIF(BatchKey,N'') IS NULL) OR
  EXISTS(SELECT PlannedDate,CategoryID FROM @Rows GROUP BY PlannedDate,CategoryID HAVING COUNT(*)>1) OR
  EXISTS(SELECT BatchKey FROM @Rows GROUP BY BatchKey HAVING COUNT(DISTINCT RecipeID)>1 OR COUNT(DISTINCT COALESCE(PreparedBatchID,0))>1 OR SUM(Portions)>1000) THROW 50000,N'Invalid plan rows',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'apply-reviewed-plan' AS kind,JSON_QUERY(@Meals) AS meals,JSON_QUERY(@Replace) AS replaceEntries,JSON_QUERY(@Recipes) AS recipes,JSON_QUERY(@Entries) AS entries,JSON_QUERY(@Batches) AS batches FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX);
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  -- Lock the complete affected date range, including absent slots, against concurrent plan writes.
  DECLARE @Guard BIGINT;SELECT @Guard=COUNT_BIG(*) FROM dbo.MealPlanEntries WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@UserID AND PlannedDate BETWEEN (SELECT MIN(PlannedDate) FROM @Rows) AND (SELECT MAX(PlannedDate) FROM @Rows);
  IF EXISTS(SELECT 1 FROM @Rows r LEFT JOIN dbo.Recipes p WITH(UPDLOCK,HOLDLOCK) ON p.RecipeID=r.RecipeID LEFT JOIN OPENJSON(@Recipes) v ON TRY_CONVERT(INT,v.[key])=r.RecipeID
   WHERE p.RecipeID IS NULL OR v.value IS NULL OR TRY_CONVERT(BINARY(8),v.value,1) IS NULL OR p.RowVersion<>TRY_CONVERT(BINARY(8),v.value,1)) THROW 50004,N'Recipe changed',1;
  IF EXISTS(SELECT 1 FROM OPENJSON(@Replace) d LEFT JOIN dbo.MealPlanEntries p WITH(UPDLOCK,HOLDLOCK) ON p.MealPlanEntryID=TRY_CONVERT(INT,d.value) AND p.UserID=@UserID
   LEFT JOIN OPENJSON(@Entries) v ON TRY_CONVERT(INT,v.[key])=p.MealPlanEntryID LEFT JOIN dbo.CookingBatches b WITH(UPDLOCK,HOLDLOCK) ON b.CookingBatchID=p.CookingBatchID
   WHERE p.MealPlanEntryID IS NULL OR v.value IS NULL OR p.Version<>TRY_CONVERT(BIGINT,v.value) OR p.ConsumedPortions>0 OR b.CookedAt IS NOT NULL OR p.PlannedDate<CONVERT(DATE,GETDATE())) THROW 50004,N'Meal changed or already cooked',1;
  DELETE p FROM dbo.MealPlanEntries p JOIN OPENJSON(@Replace) d ON p.MealPlanEntryID=TRY_CONVERT(INT,d.value) WHERE p.UserID=@UserID;
  IF EXISTS(SELECT 1 FROM @Rows r JOIN dbo.MealPlanEntries p ON p.UserID=@UserID AND p.PlannedDate=r.PlannedDate AND p.CategoryID=r.CategoryID) THROW 50004,N'Meal slot occupied',1;
  DECLARE @Key NVARCHAR(80),@Recipe INT,@Prepared INT,@Batch INT,@Portions DECIMAL(18,4),@Produced DECIMAL(18,4),@Used DECIMAL(18,4),@Expiry DATE;
  DECLARE groups_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT BatchKey,MIN(RecipeID),MIN(PreparedBatchID),SUM(Portions) FROM @Rows GROUP BY BatchKey;
  OPEN groups_cursor;FETCH NEXT FROM groups_cursor INTO @Key,@Recipe,@Prepared,@Portions;
  WHILE @@FETCH_STATUS=0
  BEGIN
   IF @Prepared IS NULL
   BEGIN
    INSERT dbo.CookingBatches(UserID,RecipeID,PlannedPortions) VALUES(@UserID,@Recipe,@Portions);SET @Batch=SCOPE_IDENTITY();
   END
   ELSE
   BEGIN
    SET @Batch=NULL;
    SELECT @Batch=b.CookingBatchID,@Produced=b.ProducedPortions,@Used=b.UsedPortions,@Expiry=b.ExpiresOn FROM dbo.CookingBatches b WITH(UPDLOCK,HOLDLOCK)
     JOIN OPENJSON(@Batches) v ON TRY_CONVERT(INT,v.[key])=b.CookingBatchID AND TRY_CONVERT(BIGINT,v.value)=b.Version
     WHERE b.CookingBatchID=@Prepared AND b.UserID=@UserID AND b.RecipeID=@Recipe AND b.CookedAt IS NOT NULL;
    IF @Batch IS NULL OR @Portions>@Produced-@Used-(SELECT COALESCE(SUM(PortionQuantity-ConsumedPortions),0) FROM dbo.MealPlanEntries WHERE CookingBatchID=@Batch) OR
     EXISTS(SELECT 1 FROM @Rows WHERE BatchKey=@Key AND PlannedDate>@Expiry) THROW 50004,N'Prepared portions changed',1;
   END;
   INSERT dbo.MealPlanEntries(UserID,RecipeID,CategoryID,PlannedDate,Servings,Notes,CookingBatchID,PortionQuantity)
    SELECT @UserID,RecipeID,CategoryID,PlannedDate,NULL,Reason,@Batch,Portions FROM @Rows WHERE BatchKey=@Key;
   FETCH NEXT FROM groups_cursor INTO @Key,@Recipe,@Prepared,@Portions;
  END;
  CLOSE groups_cursor;DEALLOCATE groups_cursor;
  EXEC dbo.sp_WriteAudit @UserID,N'PLAN_REVIEW_APPLY',N'User',@UserID;
  SET @Result=(SELECT (SELECT COUNT(*) FROM @Rows) AS id,CONVERT(BIGINT,1) AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT;SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AcceptReceipt @UserID INT,@OperationID UNIQUEIDENTIFIER,@JobID UNIQUEIDENTIFIER,@ExpectedVersion BIGINT,@Indexes NVARCHAR(MAX),@Purchases NVARCHAR(MAX),@SnapshotID INT=NULL,@ConfirmDuplicate BIT=0
AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF ISJSON(@Indexes)<>1 OR ISJSON(@Purchases)<>1 THROW 50000,N'Invalid receipt',1;
 DECLARE @Rows TABLE(RowIndex INT PRIMARY KEY,Name NVARCHAR(150),IngredientID INT,Quantity DECIMAL(18,4),UnitID INT,PurchasedOn DATE,ExpiresOn DATE);
 INSERT @Rows SELECT CONVERT(INT,j.[key]),p.Name,p.IngredientID,p.Quantity,p.UnitID,p.PurchasedOn,p.ExpiresOn FROM OPENJSON(@Purchases) j CROSS APPLY OPENJSON(j.value) WITH(Name NVARCHAR(150) '$.name',IngredientID INT '$.ingredientId',Quantity DECIMAL(18,4) '$.quantity',UnitID INT '$.unitId',PurchasedOn DATE '$.purchasedOn',ExpiresOn DATE '$.expiresOn') p JOIN OPENJSON(@Indexes) i ON TRY_CONVERT(INT,i.value)=CONVERT(INT,j.[key]);
 IF NOT EXISTS(SELECT 1 FROM @Rows) OR (SELECT COUNT(*) FROM @Rows)<>(SELECT COUNT(*) FROM OPENJSON(@Indexes)) OR EXISTS(SELECT 1 FROM @Rows WHERE NULLIF(Name,N'') IS NULL OR IngredientID IS NULL OR UnitID IS NULL OR Quantity IS NULL OR Quantity<=0 OR PurchasedOn IS NULL OR ExpiresOn<PurchasedOn) THROW 50000,N'Review receipt mappings and dates',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'accept-receipt' AS kind,@JobID AS job,@ExpectedVersion AS version,JSON_QUERY(@Indexes) AS indexes,JSON_QUERY(@Purchases) AS purchases,@SnapshotID AS snapshot,@ConfirmDuplicate AS confirmDuplicate FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Hash BINARY(32);
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.AssistantJobs WITH(UPDLOCK,HOLDLOCK) WHERE JobID=@JobID AND UserID=@UserID AND Kind=N'receipt' AND State=N'Completed' AND Version=@ExpectedVersion AND ExpiresAt>SYSUTCDATETIME()) THROW 50004,N'Receipt changed',1;
  IF EXISTS(SELECT 1 FROM @Rows r JOIN dbo.AssistantAcceptedRows a ON a.JobID=@JobID AND a.RowIndex=r.RowIndex) THROW 50004,N'Purchase already accepted',1;
  IF EXISTS(SELECT 1 FROM @Rows r WHERE r.RowIndex>=(SELECT COUNT(*) FROM OPENJSON((SELECT ResultJson FROM dbo.AssistantJobs WHERE JobID=@JobID),'$.purchases'))) THROW 50000,N'Invalid receipt row',1;
  -- Warn when a different accepted receipt has the same normalized reviewed lines.
  SET @Hash=HASHBYTES('SHA2_256',@Purchases);
  IF @ConfirmDuplicate=0 AND EXISTS(SELECT 1 FROM dbo.AssistantAcceptedRows a JOIN dbo.AssistantJobs j ON j.JobID=a.JobID WHERE j.UserID=@UserID AND a.ReceiptHash=@Hash AND a.JobID<>@JobID) THROW 50004,N'Possible duplicate receipt',1;
  IF @SnapshotID IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshots WHERE SnapshotID=@SnapshotID AND UserID=@UserID) THROW 50002,N'Not authorized',1;
  IF @SnapshotID IS NULL
  BEGIN
   INSERT dbo.ShoppingSnapshots(UserID,StartDate,EndDate) SELECT @UserID,MIN(PurchasedOn),MAX(PurchasedOn) FROM @Rows;SET @SnapshotID=SCOPE_IDENTITY();
  END;
  DECLARE @Index INT,@Name NVARCHAR(150),@Ingredient INT,@Quantity DECIMAL(18,4),@Unit INT,@Purchased DATE,@Expires DATE,@ID INT;
  DECLARE receipt_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT * FROM @Rows ORDER BY RowIndex;
  OPEN receipt_cursor;FETCH NEXT FROM receipt_cursor INTO @Index,@Name,@Ingredient,@Quantity,@Unit,@Purchased,@Expires;
  WHILE @@FETCH_STATUS=0
  BEGIN
   INSERT dbo.ShoppingSnapshotItems(SnapshotID,IngredientID,UnitID,IngredientName,CategoryName,UnitAbbreviation,NeededQty,OnHandQty,ToBuyQty,IsPurchased,PurchasedQuantity,PurchasedUnitID,PurchasedOn,ExpiresOn,IsManual)
    SELECT @SnapshotID,@Ingredient,@Unit,@Name,COALESCE(c.Name,N''),u.Abbreviation,@Quantity,0,@Quantity,1,@Quantity,@Unit,@Purchased,@Expires,1 FROM dbo.Units u CROSS JOIN dbo.Ingredients i LEFT JOIN dbo.IngredientCategories c ON c.IngredientCategoryID=i.IngredientCategoryID WHERE u.UnitID=@Unit AND i.IngredientID=@Ingredient;
   IF @@ROWCOUNT<>1 THROW 50000,N'Unknown unit',1;
   SET @ID=SCOPE_IDENTITY();INSERT dbo.AssistantAcceptedRows(JobID,RowIndex,EntityID,ReceiptHash) VALUES(@JobID,@Index,@ID,@Hash);
   FETCH NEXT FROM receipt_cursor INTO @Index,@Name,@Ingredient,@Quantity,@Unit,@Purchased,@Expires;
  END;
  CLOSE receipt_cursor;DEALLOCATE receipt_cursor;
  EXEC dbo.sp_WriteAudit @UserID,N'RECEIPT_ACCEPT',N'ShoppingSnapshot',@SnapshotID;
  SET @Result=(SELECT @SnapshotID AS id,CONVERT(BIGINT,1) AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT;SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_AddReviewedIngredient @UserID INT,@OperationID UNIQUEIDENTIFIER,@Name NVARCHAR(100),@DefaultUnitID INT
AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'add-reviewed-ingredient' AS kind,@Name AS name,@DefaultUnitID AS unit FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@ID INT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  IF NULLIF(LTRIM(RTRIM(@Name)),N'') IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.Units WHERE UnitID=@DefaultUnitID) THROW 50000,N'Invalid ingredient',1;
  INSERT dbo.Ingredients(Name,DefaultUnitID) VALUES(@Name,@DefaultUnitID);SET @ID=SCOPE_IDENTITY();
  EXEC dbo.sp_WriteAudit @UserID,N'INGREDIENT_CREATE',N'Ingredient',@ID;
  SET @Result=(SELECT @ID AS id,CONVERT(BIGINT,1) AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT;SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_SaveReviewedRecipe @UserID INT,@OperationID UNIQUEIDENTIFIER,@RecipeID INT=NULL,@CategoryID INT=NULL,@Title NVARCHAR(150),@Description NVARCHAR(MAX)=NULL,@Instructions NVARCHAR(MAX),
 @PrepTimeMinutes INT=NULL,@CookTimeMinutes INT=NULL,@Servings INT=NULL,@IngredientsJson NVARCHAR(MAX),@ExpectedVersion BINARY(8)=NULL,@PhotoMode NVARCHAR(10)=N'Keep',@SourceText NVARCHAR(MAX)=NULL,@SourceKind NVARCHAR(30)=N'reviewed'
AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'save-reviewed-recipe' AS kind,@RecipeID AS recipe,@CategoryID AS category,@Title AS title,@Description AS description,@Instructions AS instructions,@PrepTimeMinutes AS prep,@CookTimeMinutes AS cook,@Servings AS servings,JSON_QUERY(@IngredientsJson) AS ingredients,@ExpectedVersion AS version,@PhotoMode AS photo,@SourceText AS source,@SourceKind AS sourceKind FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX);
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  DECLARE @Inner UNIQUEIDENTIFIER=CONVERT(UNIQUEIDENTIFIER,HASHBYTES('MD5',CONVERT(NVARCHAR(36),@OperationID)+N':recipe-bundle'));
  EXEC dbo.sp_SaveRecipeBundle @UserID=@UserID,@OperationID=@Inner,@RecipeID=@RecipeID,@CategoryID=@CategoryID,@Title=@Title,@Description=@Description,@Instructions=@Instructions,@PrepTimeMinutes=@PrepTimeMinutes,@CookTimeMinutes=@CookTimeMinutes,@Servings=@Servings,@IngredientsJson=@IngredientsJson,@ExpectedVersion=@ExpectedVersion,@PhotoMode=@PhotoMode,@ResultOutput=@Result OUTPUT,@SuppressResult=1;
  SET @RecipeID=JSON_VALUE(@Result,'$.id');
  IF @SourceText IS NOT NULL
  BEGIN
   UPDATE dbo.RecipeSources SET SourceText=@SourceText,SourceKind=@SourceKind,UpdatedAt=SYSUTCDATETIME() WHERE RecipeID=@RecipeID;
   IF @@ROWCOUNT=0 INSERT dbo.RecipeSources(RecipeID,SourceText,SourceKind) VALUES(@RecipeID,@SourceText,@SourceKind);
   EXEC dbo.sp_WriteAudit @UserID,N'RECIPE_SOURCE',N'Recipe',@RecipeID;
  END;
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT;SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetAcceptedReceiptRows @UserID INT,@JobID UNIQUEIDENTIFIER
AS BEGIN SET NOCOUNT ON;
 SELECT a.RowIndex FROM dbo.AssistantAcceptedRows a JOIN dbo.AssistantJobs j ON j.JobID=a.JobID WHERE j.UserID=@UserID AND j.JobID=@JobID ORDER BY a.RowIndex;
END;
GO
