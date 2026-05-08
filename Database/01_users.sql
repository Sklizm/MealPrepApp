USE MealPrepDB;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        UserID        INT IDENTITY(1,1) NOT NULL,
        Username      NVARCHAR(50)      NOT NULL,
        Email         NVARCHAR(255)     NOT NULL,
        PasswordHash  NVARCHAR(255)     NOT NULL,
        CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Users         PRIMARY KEY CLUSTERED (UserID),
        CONSTRAINT UQ_Users_Username UNIQUE (Username),
        CONSTRAINT UQ_Users_Email    UNIQUE (Email)
    );
END
GO
