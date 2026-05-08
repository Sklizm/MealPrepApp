USE MealPrepDB;
GO

-- AuditLog: every state-changing stored proc writes one row here.

IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog (
        AuditLogID  BIGINT IDENTITY(1,1) NOT NULL,
        UserID      INT               NULL,
        Action      NVARCHAR(50)      NOT NULL,
        EntityType  NVARCHAR(50)      NULL,
        EntityID    INT               NULL,
        Details     NVARCHAR(500)     NULL,
        CreatedAt   DATETIME2(0)      NOT NULL
            CONSTRAINT DF_AuditLog_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AuditLog PRIMARY KEY CLUSTERED (AuditLogID)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AuditLog_UserID_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.AuditLog')
)
    CREATE INDEX IX_AuditLog_UserID_CreatedAt
        ON dbo.AuditLog(UserID, CreatedAt DESC);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AuditLog_Action_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.AuditLog')
)
    CREATE INDEX IX_AuditLog_Action_CreatedAt
        ON dbo.AuditLog(Action, CreatedAt DESC);
GO

-- TVP type for passing lists of IDs (used by sp_FindRecipesByIngredients).

IF NOT EXISTS (
    SELECT 1 FROM sys.types t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'dbo' AND t.name = N'IntList'
)
    CREATE TYPE dbo.IntList AS TABLE (ID INT NOT NULL PRIMARY KEY);
GO

-- Internal helper: every mutating proc calls this.
-- Created with CREATE OR ALTER so re-runs replace cleanly.

CREATE OR ALTER PROCEDURE dbo.sp_WriteAudit
    @UserID     INT          = NULL,
    @Action     NVARCHAR(50),
    @EntityType NVARCHAR(50) = NULL,
    @EntityID   INT          = NULL,
    @Details    NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AuditLog (UserID, Action, EntityType, EntityID, Details)
    VALUES (@UserID, @Action, @EntityType, @EntityID, @Details);
END
GO
