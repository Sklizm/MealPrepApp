USE MealPrepDB;
GO

-- Common ingredients seed. Idempotent via MERGE keyed on Name.
-- Default units are resolved by Abbreviation against dbo.Units, so this file
-- stays human-readable and survives unit ID renumbering.
--
-- LEFT JOIN on Units: if an abbreviation is missing (e.g. typo or unit
-- removed), DefaultUnitID will be NULL — the ingredient is still seeded.

MERGE dbo.Ingredients AS target
USING (
    SELECT s.Name, u.UnitID AS DefaultUnitID
    FROM (VALUES
        -- Pantry
        (N'Salt',               N'g'),
        (N'Sugar',              N'g'),
        (N'Brown Sugar',        N'g'),
        (N'Flour',              N'g'),
        (N'Whole Wheat Flour',  N'g'),
        (N'Baking Powder',      N'tsp'),
        (N'Baking Soda',        N'tsp'),
        (N'Yeast',              N'g'),
        (N'Cornstarch',         N'tbsp'),
        (N'Breadcrumbs',        N'g'),
        (N'Rice',               N'g'),
        (N'Pasta',              N'g'),
        (N'Rolled Oats',        N'g'),
        (N'Honey',              N'tbsp'),

        -- Oils & condiments
        (N'Olive Oil',          N'ml'),
        (N'Vegetable Oil',      N'ml'),
        (N'Butter',             N'g'),
        (N'Soy Sauce',          N'tbsp'),
        (N'Vinegar',            N'ml'),
        (N'Mustard',            N'tbsp'),
        (N'Ketchup',            N'tbsp'),
        (N'Mayonnaise',         N'tbsp'),

        -- Dairy & eggs
        (N'Egg',                N'pc'),
        (N'Milk',               N'ml'),
        (N'Cream',              N'ml'),
        (N'Yogurt',             N'g'),
        (N'Cheese',             N'g'),

        -- Produce
        (N'Onion',              N'pc'),
        (N'Garlic',             N'pc'),
        (N'Tomato',             N'pc'),
        (N'Potato',             N'pc'),
        (N'Carrot',             N'pc'),
        (N'Bell Pepper',        N'pc'),
        (N'Lemon',              N'pc'),
        (N'Spinach',            N'g'),

        -- Proteins
        (N'Chicken Breast',     N'g'),
        (N'Ground Beef',        N'g'),
        (N'Salmon',             N'g'),
        (N'Tofu',               N'g'),

        -- Herbs / spices
        (N'Black Pepper',       N'g'),
        (N'Paprika',            N'tsp'),
        (N'Oregano',            N'tsp'),
        (N'Basil',              N'tsp'),
        (N'Cumin',              N'tsp')
    ) AS s(Name, UnitAbbr)
    LEFT JOIN dbo.Units u ON u.Abbreviation = s.UnitAbbr
) AS source
ON target.Name = source.Name
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, DefaultUnitID)
    VALUES (source.Name, source.DefaultUnitID);
GO
