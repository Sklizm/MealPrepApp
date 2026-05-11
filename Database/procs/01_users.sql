USE MealPrepDB;
GO

-- ===== sp_RegisterUser =====
-- Inserts a new user. Username/Email uniqueness enforced by table constraints.
-- Returns: new UserID via SELECT.

CREATE OR ALTER PROCEDURE dbo.sp_RegisterUser
    @Username     NVARCHAR(50),
    @Email        NVARCHAR(255),
    @PasswordHash NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        INSERT INTO dbo.Users (Username, Email, PasswordHash)
        VALUES (@Username, @Email, @PasswordHash);

        DECLARE @NewID INT = SCOPE_IDENTITY();

        EXEC dbo.sp_WriteAudit
            @UserID     = @NewID,
            @Action     = N'USER_REGISTER',
            @EntityType = N'User',
            @EntityID   = @NewID;

        COMMIT TRAN;

        SELECT @NewID AS UserID;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_GetUserForLogin =====
-- Returns the data the app needs to verify a login attempt.
-- App is responsible for hashing the supplied password and comparing.

CREATE OR ALTER PROCEDURE dbo.sp_GetUserForLogin
    @UsernameOrEmail NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        UserID,
        Username,
        Email,
        PasswordHash,
        FailedLoginCount,
        LockedUntil,
        LastLoginAt
    FROM dbo.Users
    WHERE Username = @UsernameOrEmail
       OR Email    = @UsernameOrEmail;
END
GO

-- ===== sp_RecordLoginSuccess =====
-- Resets lockout state and stamps LastLoginAt.

CREATE OR ALTER PROCEDURE dbo.sp_RecordLoginSuccess
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        UPDATE dbo.Users
        SET LastLoginAt      = SYSUTCDATETIME(),
            FailedLoginCount = 0,
            LockedUntil      = NULL
        WHERE UserID = @UserID;

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'LOGIN_SUCCESS',
            @EntityType = N'User',
            @EntityID   = @UserID;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_RecordLoginFailure =====
-- Increments FailedLoginCount. Locks account after threshold.
-- @UserID can be NULL (failed login for non-existent user — still audited).

CREATE OR ALTER PROCEDURE dbo.sp_RecordLoginFailure
    @UserID INT = NULL,
    @AttemptedIdentifier NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @LockoutThreshold INT = 5;
    DECLARE @LockoutMinutes   INT = 15;

    BEGIN TRY
        BEGIN TRAN;

        IF @UserID IS NOT NULL
        BEGIN
            UPDATE dbo.Users
            SET FailedLoginCount = FailedLoginCount + 1,
                LockedUntil = CASE
                    WHEN FailedLoginCount + 1 >= @LockoutThreshold
                        THEN DATEADD(minute, @LockoutMinutes, SYSUTCDATETIME())
                    ELSE LockedUntil
                END
            WHERE UserID = @UserID;

            DECLARE @NewCount INT = (SELECT FailedLoginCount FROM dbo.Users WHERE UserID = @UserID);

            IF @NewCount = @LockoutThreshold
                EXEC dbo.sp_WriteAudit
                    @UserID = @UserID,
                    @Action = N'ACCOUNT_LOCKED',
                    @EntityType = N'User',
                    @EntityID = @UserID,
                    @Details = N'Lockout triggered by failed login threshold';
        END

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'LOGIN_FAILURE',
            @EntityType = N'User',
            @EntityID   = @UserID,
            @Details    = @AttemptedIdentifier;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_GetUserProfile =====
-- Safe read of user data for the Profile screen — never returns PasswordHash
-- or any of the lockout state. The app uses this; sp_GetUserForLogin stays
-- reserved for the login flow specifically.

CREATE OR ALTER PROCEDURE dbo.sp_GetUserProfile
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        UserID,
        Username,
        Email,
        CreatedAt,
        LastLoginAt
    FROM dbo.Users
    WHERE UserID = @UserID;
END
GO

-- ===== sp_ChangePassword =====
-- Rejects reuse of the last @HistoryDepth passwords (default 5).
-- THROW 50001 on reuse so the app can show a friendly error.

CREATE OR ALTER PROCEDURE dbo.sp_ChangePassword
    @UserID          INT,
    @NewPasswordHash NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @HistoryDepth INT = 5;

    BEGIN TRY
        BEGIN TRAN;

        -- Check current password too (it counts as "in history").
        IF EXISTS (
            SELECT 1 FROM dbo.Users
            WHERE UserID = @UserID AND PasswordHash = @NewPasswordHash
        )
            THROW 50001, N'Password reused', 1;

        IF EXISTS (
            SELECT 1 FROM (
                SELECT TOP (@HistoryDepth) PasswordHash
                FROM dbo.PasswordHistory
                WHERE UserID = @UserID
                ORDER BY ChangedAt DESC, PasswordHistoryID DESC
            ) recent
            WHERE recent.PasswordHash = @NewPasswordHash
        )
            THROW 50001, N'Password reused', 1;

        -- Move current password into history before overwriting.
        INSERT INTO dbo.PasswordHistory (UserID, PasswordHash)
        SELECT UserID, PasswordHash FROM dbo.Users WHERE UserID = @UserID;

        UPDATE dbo.Users
        SET PasswordHash = @NewPasswordHash
        WHERE UserID = @UserID;

        -- Prune older-than-N history rows.
        ;WITH ranked AS (
            SELECT PasswordHistoryID,
                   ROW_NUMBER() OVER (
                       PARTITION BY UserID
                       ORDER BY ChangedAt DESC, PasswordHistoryID DESC
                   ) AS rn
            FROM dbo.PasswordHistory
            WHERE UserID = @UserID
        )
        DELETE FROM dbo.PasswordHistory
        WHERE PasswordHistoryID IN (SELECT PasswordHistoryID FROM ranked WHERE rn > @HistoryDepth);

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'PASSWORD_CHANGE',
            @EntityType = N'User',
            @EntityID   = @UserID;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO
