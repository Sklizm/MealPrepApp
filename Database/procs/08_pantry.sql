USE MealPrepDB;
GO

-- ===== sp_AddPantryItem =====
-- Upsert by (UserID, IngredientID, UnitID). If a row already exists for that
-- combo, ADD @Quantity to it; else insert. Lets the app's "I bought 500 g more
-- flour" flow stay a single proc call.

CREATE OR ALTER PROCEDURE dbo.sp_AddPantryItem
    @UserID       INT,
    @IngredientID INT,
    @UnitID       INT,
    @Quantity     DECIMAL(10, 2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Quantity IS NULL OR @Quantity <= 0
        THROW 50000, N'Quantity must be greater than 0', 1;

    BEGIN TRY
        BEGIN TRAN;

        MERGE dbo.UserPantry AS target
        USING (SELECT @UserID AS UserID, @IngredientID AS IngredientID, @UnitID AS UnitID) AS src
        ON target.UserID = src.UserID
           AND target.IngredientID = src.IngredientID
           AND target.UnitID = src.UnitID
        WHEN MATCHED THEN
            UPDATE SET Quantity  = target.Quantity + @Quantity,
                       UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN
            INSERT (UserID, IngredientID, UnitID, Quantity)
            VALUES (src.UserID, src.IngredientID, src.UnitID, @Quantity);

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'PANTRY_ADD',
            @EntityType = N'Ingredient',
            @EntityID   = @IngredientID;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_UpdatePantryQuantity =====
-- Sets an absolute quantity (not delta). Use this for "user edited the number
-- in the pantry list". To bump quantity, prefer sp_AddPantryItem.

CREATE OR ALTER PROCEDURE dbo.sp_UpdatePantryQuantity
    @UserPantryID INT,
    @UserID       INT,
    @Quantity     DECIMAL(10, 2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Quantity IS NULL OR @Quantity <= 0
        THROW 50000, N'Quantity must be greater than 0', 1;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @OwnerID INT = (SELECT UserID FROM dbo.UserPantry WHERE UserPantryID = @UserPantryID);

        IF @OwnerID IS NULL
            THROW 50003, N'Pantry item not found', 1;
        IF @OwnerID <> @UserID
            THROW 50002, N'Not authorized to modify this pantry item', 1;

        UPDATE dbo.UserPantry
        SET Quantity  = @Quantity,
            UpdatedAt = SYSUTCDATETIME()
        WHERE UserPantryID = @UserPantryID;

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'PANTRY_UPDATE',
            @EntityType = N'UserPantry',
            @EntityID   = @UserPantryID;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_RemovePantryItem =====
CREATE OR ALTER PROCEDURE dbo.sp_RemovePantryItem
    @UserPantryID INT,
    @UserID       INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @OwnerID INT = (SELECT UserID FROM dbo.UserPantry WHERE UserPantryID = @UserPantryID);

        IF @OwnerID IS NULL
            THROW 50003, N'Pantry item not found', 1;
        IF @OwnerID <> @UserID
            THROW 50002, N'Not authorized to delete this pantry item', 1;

        DELETE FROM dbo.UserPantry WHERE UserPantryID = @UserPantryID;

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'PANTRY_REMOVE',
            @EntityType = N'UserPantry',
            @EntityID   = @UserPantryID;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_GetPantry =====
-- Read-only list joined with ingredient + unit names, ordered by ingredient name.

CREATE OR ALTER PROCEDURE dbo.sp_GetPantry
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.UserPantryID,
        p.IngredientID,
        i.Name          AS IngredientName,
        p.UnitID,
        u.Name          AS UnitName,
        u.Abbreviation  AS UnitAbbreviation,
        p.Quantity,
        p.AddedAt,
        p.UpdatedAt
    FROM dbo.UserPantry p
    JOIN dbo.Ingredients i ON i.IngredientID = p.IngredientID
    JOIN dbo.Units u       ON u.UnitID       = p.UnitID
    WHERE p.UserID = @UserID
    ORDER BY i.Name, u.Abbreviation;
END
GO
