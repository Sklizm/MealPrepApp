USE MealPrepDB;
GO

-- Seed the 8 ingredient categories (MERGE on Name, idempotent).
MERGE dbo.IngredientCategories AS target
USING (VALUES
    (N'Produse'),
    (N'Lactate si oua'),
    (N'Carne si peste'),
    (N'Conserve'),
    (N'Condimente si ierburi'),
    (N'Cereale si paste'),
    (N'Bauturi'),
    (N'Altele')
) AS source (Name)
ON target.Name = source.Name
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name) VALUES (source.Name);
GO

-- Backfill IngredientCategoryID on the seeded Ingredients.
-- Idempotent: re-runs are no-ops because every assignment matches the current row.
-- Only updates rows where the category is currently NULL OR points to a different category
-- than what we want here. New/user-added ingredients with their own assignment are left alone.

;WITH wanted AS (
    SELECT
        i.IngredientID,
        c.IngredientCategoryID
    FROM dbo.Ingredients i
    JOIN (VALUES
        -- Cereale si paste
        (N'Flour',              N'Cereale si paste'),
        (N'Whole Wheat Flour',  N'Cereale si paste'),
        (N'Rice',               N'Cereale si paste'),
        (N'Pasta',              N'Cereale si paste'),
        (N'Rolled Oats',        N'Cereale si paste'),
        (N'Breadcrumbs',        N'Cereale si paste'),
        (N'Cornstarch',         N'Cereale si paste'),

        -- Condimente si ierburi
        (N'Salt',               N'Condimente si ierburi'),
        (N'Sugar',              N'Condimente si ierburi'),
        (N'Brown Sugar',        N'Condimente si ierburi'),
        (N'Black Pepper',       N'Condimente si ierburi'),
        (N'Paprika',            N'Condimente si ierburi'),
        (N'Oregano',            N'Condimente si ierburi'),
        (N'Basil',              N'Condimente si ierburi'),
        (N'Cumin',              N'Condimente si ierburi'),
        (N'Baking Powder',      N'Condimente si ierburi'),
        (N'Baking Soda',        N'Condimente si ierburi'),
        (N'Yeast',              N'Condimente si ierburi'),

        -- Conserve / sosuri / uleiuri
        (N'Olive Oil',          N'Conserve'),
        (N'Vegetable Oil',      N'Conserve'),
        (N'Soy Sauce',          N'Conserve'),
        (N'Vinegar',            N'Conserve'),
        (N'Mustard',            N'Conserve'),
        (N'Ketchup',            N'Conserve'),
        (N'Mayonnaise',         N'Conserve'),
        (N'Honey',              N'Conserve'),

        -- Lactate si oua
        (N'Egg',                N'Lactate si oua'),
        (N'Milk',               N'Lactate si oua'),
        (N'Cream',              N'Lactate si oua'),
        (N'Yogurt',             N'Lactate si oua'),
        (N'Cheese',             N'Lactate si oua'),
        (N'Butter',             N'Lactate si oua'),

        -- Produse (legume / fructe)
        (N'Onion',              N'Produse'),
        (N'Garlic',             N'Produse'),
        (N'Tomato',             N'Produse'),
        (N'Potato',             N'Produse'),
        (N'Carrot',             N'Produse'),
        (N'Bell Pepper',        N'Produse'),
        (N'Lemon',              N'Produse'),
        (N'Spinach',            N'Produse'),

        -- Carne si peste
        (N'Chicken Breast',     N'Carne si peste'),
        (N'Ground Beef',        N'Carne si peste'),
        (N'Salmon',             N'Carne si peste'),
        (N'Tofu',               N'Carne si peste')
    ) AS s(Name, CategoryName) ON s.Name = i.Name
    JOIN dbo.IngredientCategories c ON c.Name = s.CategoryName
)
UPDATE i
SET i.IngredientCategoryID = w.IngredientCategoryID
FROM dbo.Ingredients i
JOIN wanted w ON w.IngredientID = i.IngredientID
WHERE i.IngredientCategoryID IS NULL
   OR i.IngredientCategoryID <> w.IngredientCategoryID;
GO
