USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_CreateDeviceSession @UserID INT,@SessionID UNIQUEIDENTIFIER,@TokenHash BINARY(32),@ExpectedPasswordHash NVARCHAR(255),@DeviceName NVARCHAR(80)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @SessionID IS NULL OR NULLIF(LTRIM(RTRIM(@DeviceName)),N'') IS NULL THROW 50000,N'Invalid device session',1;
 BEGIN TRY BEGIN TRAN;
 DECLARE @Actual NVARCHAR(255),@LockedUntil DATETIME2(0),@RequestHash BINARY(32)=HASHBYTES('SHA2_256',CONCAT(@DeviceName,N'|',@ExpectedPasswordHash));
 SELECT @Actual=PasswordHash,@LockedUntil=LockedUntil FROM dbo.Users WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@UserID;
 IF @Actual IS NULL OR @Actual<>@ExpectedPasswordHash OR @LockedUntil>SYSUTCDATETIME() THROW 50004,N'Credentials changed or account locked',1;
 IF EXISTS(SELECT 1 FROM dbo.DeviceSessions WITH(UPDLOCK,HOLDLOCK) WHERE SessionID=@SessionID AND (UserID<>@UserID OR RequestHash<>@RequestHash OR TokenHash<>@TokenHash)) THROW 50004,N'Operation ID reused',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.DeviceSessions WHERE SessionID=@SessionID)
 BEGIN
  INSERT dbo.DeviceSessions(SessionID,UserID,TokenHash,CredentialHash,RequestHash,DeviceName,ExpiresAt) VALUES(@SessionID,@UserID,@TokenHash,HASHBYTES('SHA2_256',@Actual),@RequestHash,@DeviceName,DATEADD(DAY,30,SYSUTCDATETIME()));
  EXEC dbo.sp_WriteAudit @UserID,N'DEVICE_LOGIN',N'DeviceSession',NULL;
 END;
 COMMIT;
 SELECT s.SessionID,s.UserID,u.Username,s.DeviceName,s.CreatedAt,s.ExpiresAt,s.RevokedAt FROM dbo.DeviceSessions s JOIN dbo.Users u ON u.UserID=s.UserID WHERE SessionID=@SessionID;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ResolveDeviceSession @TokenHash BINARY(32)
AS BEGIN
 SET NOCOUNT ON;
 SELECT s.SessionID,s.UserID,u.Username,s.DeviceName,s.CreatedAt,s.ExpiresAt,s.RevokedAt FROM dbo.DeviceSessions s JOIN dbo.Users u ON u.UserID=s.UserID
 WHERE s.TokenHash=@TokenHash AND s.RevokedAt IS NULL AND s.ExpiresAt>SYSUTCDATETIME() AND s.CredentialHash=HASHBYTES('SHA2_256',u.PasswordHash);
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetDeviceSessions @UserID INT
AS BEGIN SET NOCOUNT ON; SELECT SessionID,DeviceName,CreatedAt,ExpiresAt,RevokedAt FROM dbo.DeviceSessions WHERE UserID=@UserID ORDER BY CreatedAt DESC;END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_RevokeDeviceSession @UserID INT,@OperationID UNIQUEIDENTIFIER,@SessionID UNIQUEIDENTIFIER
AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'revoke-device' AS kind,@SessionID AS id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX);
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.DeviceSessions WHERE SessionID=@SessionID AND UserID=@UserID) THROW 50002,N'Not authorized',1;
  UPDATE dbo.DeviceSessions SET RevokedAt=SYSUTCDATETIME() WHERE SessionID=@SessionID AND UserID=@UserID AND RevokedAt IS NULL;
  IF @@ROWCOUNT>0 EXEC dbo.sp_WriteAudit @UserID,N'DEVICE_REVOKE',N'DeviceSession',NULL;
  SET @Result=N'{"id":0,"version":1}';EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT;SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetCompanionSnapshot @UserID INT
AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_GetShoppingSnapshots @UserID;
 EXEC dbo.sp_GetShoppingChanges @UserID;
 EXEC dbo.sp_GetInventoryBatches @UserID,1;
 EXEC dbo.sp_GetStockThresholds @UserID;
 EXEC dbo.sp_GetIngredients;
 EXEC dbo.sp_GetUnits;
 EXEC dbo.sp_GetUnitMap;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_CreateCompanionList @UserID INT,@OperationID UNIQUEIDENTIFIER,@StartDate DATE,@EndDate DATE
AS BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @StartDate IS NULL OR @EndDate IS NULL OR @EndDate<@StartDate THROW 50000,N'Invalid dates',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'create-list' AS kind,@StartDate AS startDate,@EndDate AS endDate FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@ID INT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  INSERT dbo.ShoppingSnapshots(UserID,StartDate,EndDate) VALUES(@UserID,@StartDate,@EndDate);SET @ID=SCOPE_IDENTITY();
  EXEC dbo.sp_WriteAudit @UserID,N'SHOPPING_CREATE',N'ShoppingSnapshot',@ID;
  SET @Result=(SELECT @ID AS id,CONVERT(BIGINT,1) AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT;SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
