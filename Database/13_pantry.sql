USE MealPrepDB;
GO

-- UserPantry: what each user currently has in stock, per ingredient+unit.
-- UQ (UserID, IngredientID, UnitID) — sp_AddPantryItem MERGEs against this key
-- so adding the same ingredient+unit again just bumps the quantity instead of
-- creating duplicate rows.
--
-- No unit conversion in v1: "500 g flour" and "2 cups flour" are two distinct
-- rows, and the shopping list joins on the exact (IngredientID, UnitID).

IF OBJECT_ID(N'dbo.UserPantry', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserPantry (
        UserPantryID INT IDENTITY(1,1) NOT NULL,
        UserID       INT               NOT NULL,
        IngredientID INT               NOT NULL,
        UnitID       INT               NOT NULL,
        Quantity     DECIMAL(10, 2)    NOT NULL,
        AddedAt      DATETIME2(0)      NOT NULL
            CONSTRAINT DF_UserPantry_AddedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt    DATETIME2(0)      NULL,
        CONSTRAINT PK_UserPantry              PRIMARY KEY CLUSTERED (UserPantryID),
        CONSTRAINT UQ_UserPantry_User_Ing_Unit UNIQUE (UserID, IngredientID, UnitID),
        CONSTRAINT FK_UserPantry_Users        FOREIGN KEY (UserID)       REFERENCES dbo.Users(UserID)         ON DELETE CASCADE,
        CONSTRAINT FK_UserPantry_Ingredients  FOREIGN KEY (IngredientID) REFERENCES dbo.Ingredients(IngredientID),
        CONSTRAINT FK_UserPantry_Units        FOREIGN KEY (UnitID)       REFERENCES dbo.Units(UnitID),
        CONSTRAINT CK_UserPantry_Quantity     CHECK (Quantity > 0)
    );
END
GO

-- UserID is leading column of the UQ so a separate UserID index would be redundant.
-- IngredientID and UnitID get their own indexes for the shopping-list join performance
-- and for the RESTRICT delete checks on Ingredients/Units.

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_UserPantry_IngredientID'
                 AND object_id = OBJECT_ID(N'dbo.UserPantry'))
    CREATE INDEX IX_UserPantry_IngredientID ON dbo.UserPantry(IngredientID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_UserPantry_UnitID'
                 AND object_id = OBJECT_ID(N'dbo.UserPantry'))
    CREATE INDEX IX_UserPantry_UnitID ON dbo.UserPantry(UnitID);
GO
