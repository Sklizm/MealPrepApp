-- App security layer: dedicated low-privilege SQL login for the .NET app.
--
-- The login can ONLY EXECUTE stored procedures in dbo. Direct SELECT/INSERT/
-- UPDATE/DELETE on tables is explicitly DENIED. This makes SQL injection
-- structurally impossible from the app side.
--
-- First-time setup (login does not yet exist):
--   Either edit the :setvar AppPassword line below to your chosen password,
--   or delete that line and run with:
--     sqlcmd ... -v AppPassword="<your_chosen_password>" -i 09_app_role.sql
--   The password must satisfy SQL Server policy (length + complexity).
--
-- Rebuilds (login already exists at the server level): no action needed —
-- the CREATE LOGIN branch is skipped, so the empty default below is unused.
-- Note: a :setvar in the script overrides any -v from the command line, so
-- removing the line is required if you want to supply the password via -v.

:setvar AppLogin    mealprep_app
:setvar AppRole     mealprep_app_role
:setvar AppPassword ""

USE master;
GO

IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'$(AppLogin)')
    EXEC('CREATE LOGIN [$(AppLogin)] WITH PASSWORD = N''$(AppPassword)'', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;');
GO

USE MealPrepDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppLogin)')
    CREATE USER [$(AppLogin)] FOR LOGIN [$(AppLogin)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppRole)' AND type = 'R')
    CREATE ROLE [$(AppRole)];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'$(AppRole)' AND m.name = N'$(AppLogin)'
)
    ALTER ROLE [$(AppRole)] ADD MEMBER [$(AppLogin)];
GO

-- Grant EXECUTE on all current and future procs in dbo, plus the IntList TVP.
GRANT EXECUTE ON SCHEMA::dbo TO [$(AppRole)];
GRANT EXECUTE ON TYPE::dbo.IntList TO [$(AppRole)];

-- Belt-and-braces: explicitly deny direct DML on all tables in the schema.
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [$(AppRole)];
GO
