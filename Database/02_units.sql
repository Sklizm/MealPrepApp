USE MealPrepDB;
GO

IF OBJECT_ID(N'dbo.Units', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Units (
        UnitID        INT IDENTITY(1,1) NOT NULL,
        Name          NVARCHAR(50)      NOT NULL,
        Abbreviation  NVARCHAR(10)      NOT NULL,
        UnitType      NVARCHAR(20)      NOT NULL,
        CONSTRAINT PK_Units         PRIMARY KEY CLUSTERED (UnitID),
        CONSTRAINT UQ_Units_Name    UNIQUE (Name),
        CONSTRAINT CK_Units_UnitType CHECK (UnitType IN (N'weight', N'volume', N'count'))
    );
END
GO
