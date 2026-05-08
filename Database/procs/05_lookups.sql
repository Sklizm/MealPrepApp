USE MealPrepDB;
GO

-- ===== sp_GetUnits =====
-- Lookup. Sorted: weight, then volume, then count, then by name within group.

CREATE OR ALTER PROCEDURE dbo.sp_GetUnits
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UnitID, Name, Abbreviation, UnitType
    FROM dbo.Units
    ORDER BY
        CASE UnitType WHEN N'weight' THEN 1 WHEN N'volume' THEN 2 WHEN N'count' THEN 3 ELSE 4 END,
        Name;
END
GO

-- ===== sp_GetCategories =====
-- Lookup. Sorted by display name.

CREATE OR ALTER PROCEDURE dbo.sp_GetCategories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryID, Name, Description
    FROM dbo.Categories
    ORDER BY Name;
END
GO
