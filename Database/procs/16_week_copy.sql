USE MealPrepDB;
GO
CREATE OR ALTER FUNCTION dbo.fn_WeekCopy(@UserID INT,@Source DATE,@Target DATE)
RETURNS TABLE AS RETURN (
 SELECT DISTINCT p.RecipeID,p.CategoryID,DATEADD(DAY,DATEDIFF(DAY,@Source,p.PlannedDate),@Target) AS PlannedDate,
 p.Servings,p.Notes
 FROM dbo.MealPlanEntries p WHERE p.UserID=@UserID AND p.PlannedDate>=@Source AND p.PlannedDate<DATEADD(DAY,7,@Source)
);
GO
CREATE OR ALTER PROCEDURE dbo.sp_PreviewWeekCopy @UserID INT,@Source DATE,@Target DATE
AS BEGIN
 SET NOCOUNT ON;
 DECLARE @Token VARBINARY(32)=HASHBYTES('SHA2_256',(SELECT * FROM dbo.fn_WeekCopy(@UserID,@Source,@Target) ORDER BY PlannedDate,CategoryID,RecipeID,Servings,Notes FOR JSON PATH));
 SELECT c.*,r.Title AS RecipeTitle,cat.Name AS CategoryName,@Token AS PreviewToken,
 CONVERT(BIT,CASE WHEN EXISTS(SELECT 1 FROM dbo.MealPlanEntries t WHERE t.UserID=@UserID AND t.RecipeID=c.RecipeID AND t.PlannedDate=c.PlannedDate AND t.CategoryID=c.CategoryID) THEN 1 ELSE 0 END) AS IsDuplicate
 FROM dbo.fn_WeekCopy(@UserID,@Source,@Target)c JOIN dbo.Recipes r ON r.RecipeID=c.RecipeID JOIN dbo.Categories cat ON cat.CategoryID=c.CategoryID
 ORDER BY c.PlannedDate,c.CategoryID,c.RecipeID;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_CopyWeek @UserID INT,@Source DATE,@Target DATE,@PreviewToken VARBINARY(32)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Source IS NULL OR @Target IS NULL OR @PreviewToken IS NULL THROW 50000,N'Preview required',1;
 BEGIN TRY BEGIN TRAN;
 -- Serialize copies per user, including empty target ranges. Participating calls take this lock before reads.
 DECLARE @LockResult INT,@Resource NVARCHAR(255)=CONCAT(N'MealPrep.WeekCopy.',@UserID);
 EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @LockResult<0 THROW 50000,N'Week copy is busy',1;
 DECLARE @Locked INT;
 SELECT @Locked=COUNT(*) FROM dbo.MealPlanEntries WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@UserID AND
 ((PlannedDate>=@Source AND PlannedDate<DATEADD(DAY,7,@Source)) OR (PlannedDate>=@Target AND PlannedDate<DATEADD(DAY,7,@Target)));
 DECLARE @Actual VARBINARY(32)=HASHBYTES('SHA2_256',(SELECT * FROM dbo.fn_WeekCopy(@UserID,@Source,@Target) ORDER BY PlannedDate,CategoryID,RecipeID,Servings,Notes FOR JSON PATH));
 IF @Actual<>@PreviewToken THROW 50004,N'Plan changed; preview again',1;
 DECLARE @Created TABLE(ID INT);
 ;WITH ranked AS (SELECT *,ROW_NUMBER() OVER(PARTITION BY RecipeID,CategoryID,PlannedDate ORDER BY COALESCE(Servings,0),COALESCE(Notes,N'')) AS rn FROM dbo.fn_WeekCopy(@UserID,@Source,@Target))
 INSERT dbo.MealPlanEntries(UserID,RecipeID,CategoryID,PlannedDate,Servings,Notes)
 OUTPUT inserted.MealPlanEntryID INTO @Created
 SELECT @UserID,c.RecipeID,c.CategoryID,c.PlannedDate,c.Servings,c.Notes FROM ranked c
 WHERE rn=1 AND NOT EXISTS(SELECT 1 FROM dbo.MealPlanEntries t WITH(UPDLOCK,HOLDLOCK) WHERE t.UserID=@UserID AND t.RecipeID=c.RecipeID AND t.PlannedDate=c.PlannedDate AND t.CategoryID=c.CategoryID);
 INSERT dbo.AuditLog(UserID,Action,EntityType,EntityID) SELECT @UserID,N'MEAL_COPY',N'MealPlanEntry',ID FROM @Created;
 DECLARE @Count INT=(SELECT COUNT(*) FROM @Created);
 COMMIT;
 SELECT @Count;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
