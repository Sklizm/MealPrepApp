USE MealPrepDB;
GO

-- Idempotent column additions to dbo.Users for security state.

IF COL_LENGTH(N'dbo.Users', N'LastLoginAt') IS NULL
    ALTER TABLE dbo.Users ADD LastLoginAt DATETIME2(0) NULL;
GO

IF COL_LENGTH(N'dbo.Users', N'FailedLoginCount') IS NULL
    ALTER TABLE dbo.Users
        ADD FailedLoginCount INT NOT NULL
            CONSTRAINT DF_Users_FailedLoginCount DEFAULT 0;
GO

IF COL_LENGTH(N'dbo.Users', N'LockedUntil') IS NULL
    ALTER TABLE dbo.Users ADD LockedUntil DATETIME2(0) NULL;
GO

-- Password history table: keeps recent hashes per user so
-- sp_ChangePassword can reject reuse.

IF OBJECT_ID(N'dbo.PasswordHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordHistory (
        PasswordHistoryID INT IDENTITY(1,1) NOT NULL,
        UserID            INT               NOT NULL,
        PasswordHash      NVARCHAR(255)     NOT NULL,
        ChangedAt         DATETIME2(0)      NOT NULL
            CONSTRAINT DF_PasswordHistory_ChangedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PasswordHistory       PRIMARY KEY CLUSTERED (PasswordHistoryID),
        CONSTRAINT FK_PasswordHistory_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PasswordHistory_UserID_ChangedAt'
      AND object_id = OBJECT_ID(N'dbo.PasswordHistory')
)
    CREATE INDEX IX_PasswordHistory_UserID_ChangedAt
        ON dbo.PasswordHistory(UserID, ChangedAt DESC);
GO
