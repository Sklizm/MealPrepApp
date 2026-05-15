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
        (N'Faina',                  N'Cereale si paste'),
        (N'Faina integrala',        N'Cereale si paste'),
        (N'Orez',                   N'Cereale si paste'),
        (N'Paste',                  N'Cereale si paste'),
        (N'Fulgi de ovaz',          N'Cereale si paste'),
        (N'Pesmet',                 N'Cereale si paste'),
        (N'Amidon de porumb',       N'Cereale si paste'),

        -- Condimente si ierburi
        (N'Sare',                   N'Condimente si ierburi'),
        (N'Zahar',                  N'Condimente si ierburi'),
        (N'Zahar brun',             N'Condimente si ierburi'),
        (N'Piper negru',            N'Condimente si ierburi'),
        (N'Boia de ardei',          N'Condimente si ierburi'),
        (N'Oregano',                N'Condimente si ierburi'),
        (N'Busuioc',                N'Condimente si ierburi'),
        (N'Chimion',                N'Condimente si ierburi'),
        (N'Praf de copt',           N'Condimente si ierburi'),
        (N'Bicarbonat de sodiu',    N'Condimente si ierburi'),
        (N'Drojdie',                N'Condimente si ierburi'),

        -- Conserve / sosuri / uleiuri
        (N'Ulei de masline',        N'Conserve'),
        (N'Ulei vegetal',           N'Conserve'),
        (N'Sos de soia',            N'Conserve'),
        (N'Otet',                   N'Conserve'),
        (N'Mustar',                 N'Conserve'),
        (N'Ketchup',                N'Conserve'),
        (N'Maioneza',               N'Conserve'),
        (N'Miere',                  N'Conserve'),

        -- Lactate si oua
        (N'Ou',                     N'Lactate si oua'),
        (N'Lapte',                  N'Lactate si oua'),
        (N'Smantana',               N'Lactate si oua'),
        (N'Iaurt',                  N'Lactate si oua'),
        (N'Branza',                 N'Lactate si oua'),
        (N'Unt',                    N'Lactate si oua'),

        -- Produse (legume / fructe)
        (N'Ceapa',                  N'Produse'),
        (N'Usturoi',                N'Produse'),
        (N'Rosie',                  N'Produse'),
        (N'Cartof',                 N'Produse'),
        (N'Morcov',                 N'Produse'),
        (N'Ardei gras',             N'Produse'),
        (N'Lamaie',                 N'Produse'),
        (N'Spanac',                 N'Produse'),

        -- Carne si peste
        (N'Piept de pui',           N'Carne si peste'),
        (N'Carne tocata de vita',   N'Carne si peste'),
        (N'Somon',                  N'Carne si peste'),
        (N'Tofu',                   N'Carne si peste')
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
