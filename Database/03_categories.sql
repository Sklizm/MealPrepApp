USE MealPrepDB;
GO

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories (
        CategoryID    INT IDENTITY(1,1) NOT NULL,
        Name          NVARCHAR(50)      NOT NULL,
        Description   NVARCHAR(255)     NULL,
        CONSTRAINT PK_Categories      PRIMARY KEY CLUSTERED (CategoryID),
        CONSTRAINT UQ_Categories_Name UNIQUE (Name)
    );
END
GO
