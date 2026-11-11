-- Disposable database only. Exercise collisions, nutrition preservation and repeat imports.
USE MealPrepDB;
GO
SET XACT_ABORT ON;
BEGIN TRAN;
-- Simulate a pre-existing user entry with the same name and different settings.
UPDATE dbo.Ingredients SET DefaultUnitID = (SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'kg'),
    IngredientCategoryID = (SELECT IngredientCategoryID FROM dbo.IngredientCategories WHERE Name=N'Altele')
WHERE Name=N'Hrisca';
UPDATE n SET Calories=123
FROM dbo.IngredientNutrition n JOIN dbo.Ingredients i ON i.IngredientID=n.IngredientID
WHERE i.Name=N'Hrisca';
SELECT * INTO #IngredientsBefore FROM dbo.Ingredients;
SELECT * INTO #NutritionBefore FROM dbo.IngredientNutrition;
SELECT COUNT_BIG(*) AS RowsBefore INTO #AuditBefore FROM dbo.AuditLog;
GO
:r migrations/20261111_moldova_ingredients.sql
:r migrations/20261111_moldova_ingredients.sql
GO
IF EXISTS (SELECT * FROM #IngredientsBefore EXCEPT SELECT * FROM dbo.Ingredients)
    THROW 51000, N'Catalog import changed existing ingredient IDs or settings.', 1;
IF EXISTS (SELECT * FROM #NutritionBefore EXCEPT SELECT * FROM dbo.IngredientNutrition)
 OR EXISTS (SELECT * FROM dbo.IngredientNutrition EXCEPT SELECT * FROM #NutritionBefore)
    THROW 51000, N'Catalog import changed nutrition.', 1;
IF (SELECT COUNT_BIG(*) FROM dbo.AuditLog) <> (SELECT RowsBefore FROM #AuditBefore)
    THROW 51000, N'Repeat catalog import generated duplicate audit events.', 1;

-- Simulate one missing ingredient on an older deployment.
DELETE n FROM dbo.IngredientNutrition n JOIN dbo.Ingredients i ON i.IngredientID=n.IngredientID WHERE i.Name=N'Rucola';
DELETE dbo.Ingredients WHERE Name=N'Rucola';
GO
:r migrations/20261111_moldova_ingredients.sql
:r migrations/20261111_moldova_ingredients.sql
GO
IF (SELECT COUNT(*) FROM dbo.Ingredients WHERE Name=N'Rucola') <> 1
    THROW 51000, N'Missing catalog ingredient was not imported exactly once.', 1;
IF (SELECT COUNT_BIG(*) FROM dbo.AuditLog) <> (SELECT RowsBefore+1 FROM #AuditBefore)
    THROW 51000, N'Catalog addition was not audited exactly once.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Ingredients i
 JOIN dbo.Units u ON u.UnitID=i.DefaultUnitID
 JOIN dbo.IngredientCategories c ON c.IngredientCategoryID=i.IngredientCategoryID
 JOIN dbo.AuditLog a ON a.EntityID=i.IngredientID AND a.EntityType=N'Ingredient' AND a.Action=N'INGREDIENT_ADD'
 WHERE i.Name=N'Rucola' AND u.Abbreviation=N'g' AND c.Name=N'Produse')
    THROW 51000, N'Imported ingredient has missing unit, category or audit record.', 1;
IF EXISTS (SELECT 1 FROM dbo.IngredientNutrition n JOIN dbo.Ingredients i ON i.IngredientID=n.IngredientID WHERE i.Name=N'Rucola')
    THROW 51000, N'Unknown nutrition must remain missing.', 1;
ROLLBACK;
PRINT N'Moldova catalog preservation and repeat-import checks passed.';
GO
