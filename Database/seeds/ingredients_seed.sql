USE MealPrepDB;
GO

-- Common ingredients seed. Idempotent via MERGE keyed on Name.
-- Default units are resolved by Abbreviation against dbo.Units, so this file
-- stays human-readable and survives unit ID renumbering.
--
-- LEFT JOIN on Units: if an abbreviation is missing (e.g. typo or unit
-- removed), DefaultUnitID will be NULL — the ingredient is still seeded.
--
-- Names are in Romanian (without diacritics, to match the convention used
-- in IngredientCategories). An English-language version of the app will
-- need a localization layer rather than alternate seed rows.

MERGE dbo.Ingredients AS target
USING (
    SELECT s.Name, u.UnitID AS DefaultUnitID
    FROM (VALUES
        -- Camara
        (N'Sare',                   N'g'),
        (N'Zahar',                  N'g'),
        (N'Zahar brun',             N'g'),
        (N'Faina',                  N'g'),
        (N'Faina integrala',        N'g'),
        (N'Praf de copt',           N'tsp'),
        (N'Bicarbonat de sodiu',    N'tsp'),
        (N'Drojdie',                N'g'),
        (N'Amidon de porumb',       N'tbsp'),
        (N'Pesmet',                 N'g'),
        (N'Orez',                   N'g'),
        (N'Paste',                  N'g'),
        (N'Fulgi de ovaz',          N'g'),
        (N'Miere',                  N'tbsp'),

        -- Uleiuri si condimente
        (N'Ulei de masline',        N'ml'),
        (N'Ulei vegetal',           N'ml'),
        (N'Unt',                    N'g'),
        (N'Sos de soia',            N'tbsp'),
        (N'Otet',                   N'ml'),
        (N'Mustar',                 N'tbsp'),
        (N'Ketchup',                N'tbsp'),
        (N'Maioneza',               N'tbsp'),

        -- Lactate si oua
        (N'Ou',                     N'pc'),
        (N'Lapte',                  N'ml'),
        (N'Smantana',               N'ml'),
        (N'Iaurt',                  N'g'),
        (N'Branza',                 N'g'),

        -- Legume si fructe
        (N'Ceapa',                  N'pc'),
        (N'Usturoi',                N'pc'),
        (N'Rosie',                  N'pc'),
        (N'Cartof',                 N'pc'),
        (N'Morcov',                 N'pc'),
        (N'Ardei gras',             N'pc'),
        (N'Lamaie',                 N'pc'),
        (N'Spanac',                 N'g'),

        -- Carne si peste
        (N'Piept de pui',           N'g'),
        (N'Carne tocata de vita',   N'g'),
        (N'Somon',                  N'g'),
        (N'Tofu',                   N'g'),

        -- Ierburi si mirodenii
        (N'Piper negru',            N'g'),
        (N'Boia de ardei',          N'tsp'),
        (N'Oregano',                N'tsp'),
        (N'Busuioc',                N'tsp'),
        (N'Chimion',                N'tsp')
    ) AS s(Name, UnitAbbr)
    LEFT JOIN dbo.Units u ON u.Abbreviation = s.UnitAbbr
) AS source
ON target.Name = source.Name
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, DefaultUnitID)
    VALUES (source.Name, source.DefaultUnitID);
GO
