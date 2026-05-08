USE MealPrepDB;
GO

MERGE dbo.Units AS target
USING (VALUES
    (N'Gram',        N'g',     N'weight'),
    (N'Kilogram',    N'kg',    N'weight'),
    (N'Milligram',   N'mg',    N'weight'),
    (N'Ounce',       N'oz',    N'weight'),
    (N'Pound',       N'lb',    N'weight'),
    (N'Milliliter',  N'ml',    N'volume'),
    (N'Liter',       N'l',     N'volume'),
    (N'Teaspoon',    N'tsp',   N'volume'),
    (N'Tablespoon',  N'tbsp',  N'volume'),
    (N'Cup',         N'cup',   N'volume'),
    (N'Piece',       N'pc',    N'count'),
    (N'Pinch',       N'pinch', N'count')
) AS source (Name, Abbreviation, UnitType)
ON target.Name = source.Name
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, Abbreviation, UnitType)
    VALUES (source.Name, source.Abbreviation, source.UnitType);
GO
