USE MealPrepDB;
GO

-- ===== sp_SaveDraft =====
-- Upsert a recipe draft. @DraftID NULL => insert a new draft; otherwise update the existing
-- one (owner-only). Drafts are intentionally unvalidated: any field may be NULL and the
-- ingredient lines are stored as an opaque JSON blob. Returns the DraftID.
--   THROW 50002 — not the owner
--   THROW 50003 — draft not found

CREATE OR ALTER PROCEDURE dbo.sp_SaveDraft
    @UserID           INT,
    @DraftID          INT            = NULL,
    @CategoryID       INT            = NULL,
    @Title            NVARCHAR(150)  = NULL,
    @Description      NVARCHAR(MAX)  = NULL,
    @Instructions     NVARCHAR(MAX)  = NULL,
    @PrepTimeMinutes  INT            = NULL,
    @CookTimeMinutes  INT            = NULL,
    @Servings         INT            = NULL,
    @IngredientsJson  NVARCHAR(MAX)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        IF @DraftID IS NULL
        BEGIN
            INSERT INTO dbo.RecipeDrafts
                (UserID, CategoryID, Title, Description, Instructions,
                 PrepTimeMinutes, CookTimeMinutes, Servings, IngredientsJson)
            VALUES
                (@UserID, @CategoryID, @Title, @Description, @Instructions,
                 @PrepTimeMinutes, @CookTimeMinutes, @Servings, @IngredientsJson);

            SET @DraftID = SCOPE_IDENTITY();

            EXEC dbo.sp_WriteAudit
                @UserID     = @UserID,
                @Action     = N'DRAFT_CREATE',
                @EntityType = N'RecipeDraft',
                @EntityID   = @DraftID,
                @Details    = @Title;
        END
        ELSE
        BEGIN
            DECLARE @OwnerID INT = (SELECT UserID FROM dbo.RecipeDrafts WHERE DraftID = @DraftID);

            IF @OwnerID IS NULL
                THROW 50003, N'Draft not found', 1;

            IF @OwnerID <> @UserID
                THROW 50002, N'Not authorized to modify this draft', 1;

            UPDATE dbo.RecipeDrafts
            SET CategoryID      = @CategoryID,
                Title           = @Title,
                Description     = @Description,
                Instructions    = @Instructions,
                PrepTimeMinutes = @PrepTimeMinutes,
                CookTimeMinutes = @CookTimeMinutes,
                Servings        = @Servings,
                IngredientsJson = @IngredientsJson,
                UpdatedAt       = SYSUTCDATETIME()
            WHERE DraftID = @DraftID;

            EXEC dbo.sp_WriteAudit
                @UserID     = @UserID,
                @Action     = N'DRAFT_UPDATE',
                @EntityType = N'RecipeDraft',
                @EntityID   = @DraftID,
                @Details    = @Title;
        END

        COMMIT TRAN;

        SELECT @DraftID AS DraftID;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_GetDrafts =====
-- Lists a user's drafts, newest first, for the "Ciorne" sidebar.

CREATE OR ALTER PROCEDURE dbo.sp_GetDrafts
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.DraftID,
        d.Title,
        d.CategoryID,
        c.Name AS CategoryName,
        d.UpdatedAt
    FROM dbo.RecipeDrafts AS d
    LEFT JOIN dbo.Categories AS c ON c.CategoryID = d.CategoryID
    WHERE d.UserID = @UserID
    ORDER BY d.UpdatedAt DESC;
END
GO

-- ===== sp_GetDraft =====
-- Loads one draft in full (incl. the ingredient JSON blob). Owner-only.
--   THROW 50002 — not the owner
--   THROW 50003 — draft not found

CREATE OR ALTER PROCEDURE dbo.sp_GetDraft
    @DraftID INT,
    @UserID  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OwnerID INT = (SELECT UserID FROM dbo.RecipeDrafts WHERE DraftID = @DraftID);

    IF @OwnerID IS NULL
        THROW 50003, N'Draft not found', 1;

    IF @OwnerID <> @UserID
        THROW 50002, N'Not authorized to view this draft', 1;

    SELECT
        DraftID, UserID, CategoryID, Title, Description, Instructions,
        PrepTimeMinutes, CookTimeMinutes, Servings, IngredientsJson, UpdatedAt
    FROM dbo.RecipeDrafts
    WHERE DraftID = @DraftID;
END
GO

-- ===== sp_DeleteDraft =====
-- Deletes a draft. Owner-only.
--   THROW 50002 — not the owner
--   THROW 50003 — draft not found

CREATE OR ALTER PROCEDURE dbo.sp_DeleteDraft
    @DraftID INT,
    @UserID  INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @OwnerID INT = (SELECT UserID FROM dbo.RecipeDrafts WHERE DraftID = @DraftID);

        IF @OwnerID IS NULL
            THROW 50003, N'Draft not found', 1;

        IF @OwnerID <> @UserID
            THROW 50002, N'Not authorized to delete this draft', 1;

        DELETE FROM dbo.RecipeDrafts WHERE DraftID = @DraftID;

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'DRAFT_DELETE',
            @EntityType = N'RecipeDraft',
            @EntityID   = @DraftID;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO
