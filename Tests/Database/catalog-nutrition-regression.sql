-- Disposable database only; preserve existing edits, audit inserts and do not fake unknown values.
USE MealPrepDB;
GO
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE n SET Calories=123.45, ProteinGrams=9.87
FROM dbo.IngredientNutrition n JOIN dbo.Ingredients i ON i.IngredientID=n.IngredientID
WHERE i.Name=N'Hrisca';
SELECT * INTO #NutritionSnapshot FROM dbo.IngredientNutrition;
SELECT COUNT_BIG(*) AS AuditCount INTO #NutritionAudit FROM dbo.AuditLog;
GO
:r migrations/20261111_ingredient_nutrition.sql
:r migrations/20261111_ingredient_nutrition.sql
GO
IF EXISTS (SELECT * FROM #NutritionSnapshot EXCEPT SELECT * FROM dbo.IngredientNutrition)
 OR EXISTS (SELECT * FROM dbo.IngredientNutrition EXCEPT SELECT * FROM #NutritionSnapshot)
    THROW 51000, N'Repeated nutrition import changed existing rows or timestamps.', 1;
IF (SELECT COUNT_BIG(*) FROM dbo.AuditLog) <> (SELECT AuditCount FROM #NutritionAudit)
    THROW 51000, N'Repeated nutrition import generated duplicate audits.', 1;

DELETE n FROM dbo.IngredientNutrition n JOIN dbo.Ingredients i ON i.IngredientID=n.IngredientID
WHERE i.Name=N'Rucola';
GO
:r migrations/20261111_ingredient_nutrition.sql
:r migrations/20261111_ingredient_nutrition.sql
GO
IF NOT EXISTS (SELECT 1 FROM dbo.IngredientNutrition n
 JOIN dbo.Ingredients i ON i.IngredientID=n.IngredientID
 JOIN dbo.Units u ON u.UnitID=n.BasisUnitID
 WHERE i.Name=N'Rucola' AND n.BasisQuantity=100 AND u.Abbreviation=N'g'
 AND n.Calories=25 AND n.ProteinGrams=2.58 AND n.CarbsGrams=3.65 AND n.FatGrams=0.66)
    THROW 51000, N'Rucola nutrition was not imported on the correct 100 g basis.', 1;
IF (SELECT COUNT_BIG(*) FROM dbo.AuditLog) <> (SELECT AuditCount+1 FROM #NutritionAudit)
    THROW 51000, N'Nutrition insertion was not audited exactly once.', 1;
IF EXISTS (SELECT 1 FROM dbo.IngredientNutrition n JOIN dbo.Ingredients i ON i.IngredientID=n.IngredientID
 WHERE i.Name IN (N'Oase de porc',N'Leustean uscat'))
    THROW 51000, N'Unverified nutrition must not be replaced by invented or zero values.', 1;
ROLLBACK;
PRINT N'Nutrition catalog preservation, basis and auditing checks passed.';
GO
