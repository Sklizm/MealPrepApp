USE MealPrepDB;
GO

-- Common ingredient nutrition seed.
-- Values are practical demo defaults, not medical/dietitian-grade data.
-- Preserve manually edited nutrition rows: this seed inserts only missing rows.
-- If Codrin corrects a value in the app, re-running run_all.sql will not overwrite it.

;WITH source AS (
    SELECT
        v.IngredientName,
        v.BasisQuantity,
        v.BasisUnitAbbreviation,
        v.Calories,
        v.ProteinGrams,
        v.CarbsGrams,
        v.FatGrams
    FROM (VALUES
        -- Camara — mostly per 100 g unless a spoon unit is more practical in recipes.
        (N'Sare',                 CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(0.00 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2)),  CAST(0.00 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2))),
        (N'Zahar',                CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(387.00 AS DECIMAL(10,2)), CAST(0.00 AS DECIMAL(10,2)),  CAST(100.00 AS DECIMAL(10,2)), CAST(0.00 AS DECIMAL(10,2))),
        (N'Zahar brun',           CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(380.00 AS DECIMAL(10,2)), CAST(0.10 AS DECIMAL(10,2)),  CAST(98.00 AS DECIMAL(10,2)),  CAST(0.00 AS DECIMAL(10,2))),
        (N'Faina',                CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(364.00 AS DECIMAL(10,2)), CAST(10.30 AS DECIMAL(10,2)), CAST(76.30 AS DECIMAL(10,2)),  CAST(1.00 AS DECIMAL(10,2))),
        (N'Faina integrala',      CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(340.00 AS DECIMAL(10,2)), CAST(13.20 AS DECIMAL(10,2)), CAST(72.00 AS DECIMAL(10,2)),  CAST(2.50 AS DECIMAL(10,2))),
        (N'Praf de copt',         CAST(1.00 AS DECIMAL(10,2)),   N'tsp',  CAST(2.00 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2)),  CAST(1.30 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2))),
        (N'Bicarbonat de sodiu',  CAST(1.00 AS DECIMAL(10,2)),   N'tsp',  CAST(0.00 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2)),  CAST(0.00 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2))),
        (N'Drojdie',              CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(325.00 AS DECIMAL(10,2)), CAST(40.40 AS DECIMAL(10,2)), CAST(41.20 AS DECIMAL(10,2)),  CAST(7.60 AS DECIMAL(10,2))),
        (N'Amidon de porumb',     CAST(1.00 AS DECIMAL(10,2)),   N'tbsp', CAST(30.00 AS DECIMAL(10,2)),  CAST(0.00 AS DECIMAL(10,2)),  CAST(7.00 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2))),
        (N'Pesmet',               CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(395.00 AS DECIMAL(10,2)), CAST(13.40 AS DECIMAL(10,2)), CAST(72.00 AS DECIMAL(10,2)),  CAST(5.30 AS DECIMAL(10,2))),
        (N'Orez',                 CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(365.00 AS DECIMAL(10,2)), CAST(7.10 AS DECIMAL(10,2)),  CAST(80.00 AS DECIMAL(10,2)),  CAST(0.70 AS DECIMAL(10,2))),
        (N'Paste',                CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(371.00 AS DECIMAL(10,2)), CAST(13.00 AS DECIMAL(10,2)), CAST(75.00 AS DECIMAL(10,2)),  CAST(1.50 AS DECIMAL(10,2))),
        (N'Fulgi de ovaz',        CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(389.00 AS DECIMAL(10,2)), CAST(16.90 AS DECIMAL(10,2)), CAST(66.30 AS DECIMAL(10,2)),  CAST(6.90 AS DECIMAL(10,2))),
        (N'Miere',                CAST(1.00 AS DECIMAL(10,2)),   N'tbsp', CAST(64.00 AS DECIMAL(10,2)),  CAST(0.10 AS DECIMAL(10,2)),  CAST(17.30 AS DECIMAL(10,2)),  CAST(0.00 AS DECIMAL(10,2))),

        -- Uleiuri si condimente.
        (N'Ulei de masline',      CAST(100.00 AS DECIMAL(10,2)), N'ml',   CAST(824.00 AS DECIMAL(10,2)), CAST(0.00 AS DECIMAL(10,2)),  CAST(0.00 AS DECIMAL(10,2)),   CAST(91.60 AS DECIMAL(10,2))),
        (N'Ulei vegetal',         CAST(100.00 AS DECIMAL(10,2)), N'ml',   CAST(884.00 AS DECIMAL(10,2)), CAST(0.00 AS DECIMAL(10,2)),  CAST(0.00 AS DECIMAL(10,2)),   CAST(100.00 AS DECIMAL(10,2))),
        (N'Unt',                  CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(717.00 AS DECIMAL(10,2)), CAST(0.90 AS DECIMAL(10,2)),  CAST(0.10 AS DECIMAL(10,2)),   CAST(81.10 AS DECIMAL(10,2))),
        (N'Sos de soia',          CAST(1.00 AS DECIMAL(10,2)),   N'tbsp', CAST(8.00 AS DECIMAL(10,2)),   CAST(1.30 AS DECIMAL(10,2)),  CAST(0.80 AS DECIMAL(10,2)),   CAST(0.10 AS DECIMAL(10,2))),
        (N'Otet',                 CAST(100.00 AS DECIMAL(10,2)), N'ml',   CAST(18.00 AS DECIMAL(10,2)),  CAST(0.00 AS DECIMAL(10,2)),  CAST(0.00 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2))),
        (N'Mustar',               CAST(1.00 AS DECIMAL(10,2)),   N'tbsp', CAST(10.00 AS DECIMAL(10,2)),  CAST(0.60 AS DECIMAL(10,2)),  CAST(0.60 AS DECIMAL(10,2)),   CAST(0.60 AS DECIMAL(10,2))),
        (N'Ketchup',              CAST(1.00 AS DECIMAL(10,2)),   N'tbsp', CAST(17.00 AS DECIMAL(10,2)),  CAST(0.20 AS DECIMAL(10,2)),  CAST(4.50 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2))),
        (N'Maioneza',             CAST(1.00 AS DECIMAL(10,2)),   N'tbsp', CAST(94.00 AS DECIMAL(10,2)),  CAST(0.10 AS DECIMAL(10,2)),  CAST(0.10 AS DECIMAL(10,2)),   CAST(10.30 AS DECIMAL(10,2))),

        -- Lactate si oua.
        (N'Ou',                   CAST(1.00 AS DECIMAL(10,2)),   N'pc',   CAST(72.00 AS DECIMAL(10,2)),  CAST(6.30 AS DECIMAL(10,2)),  CAST(0.40 AS DECIMAL(10,2)),   CAST(4.80 AS DECIMAL(10,2))),
        (N'Lapte',                CAST(100.00 AS DECIMAL(10,2)), N'ml',   CAST(61.00 AS DECIMAL(10,2)),  CAST(3.20 AS DECIMAL(10,2)),  CAST(4.80 AS DECIMAL(10,2)),   CAST(3.30 AS DECIMAL(10,2))),
        (N'Smantana',             CAST(100.00 AS DECIMAL(10,2)), N'ml',   CAST(193.00 AS DECIMAL(10,2)), CAST(2.10 AS DECIMAL(10,2)),  CAST(3.40 AS DECIMAL(10,2)),   CAST(19.00 AS DECIMAL(10,2))),
        (N'Iaurt',                CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(61.00 AS DECIMAL(10,2)),  CAST(3.50 AS DECIMAL(10,2)),  CAST(4.70 AS DECIMAL(10,2)),   CAST(3.30 AS DECIMAL(10,2))),
        (N'Branza',               CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(264.00 AS DECIMAL(10,2)), CAST(14.20 AS DECIMAL(10,2)), CAST(4.10 AS DECIMAL(10,2)),   CAST(21.30 AS DECIMAL(10,2))),

        -- Legume si fructe. Piece values are practical approximations for common medium pieces.
        (N'Ceapa',                CAST(1.00 AS DECIMAL(10,2)),   N'pc',   CAST(44.00 AS DECIMAL(10,2)),  CAST(1.20 AS DECIMAL(10,2)),  CAST(10.30 AS DECIMAL(10,2)),  CAST(0.10 AS DECIMAL(10,2))),
        (N'Usturoi',              CAST(1.00 AS DECIMAL(10,2)),   N'pc',   CAST(4.00 AS DECIMAL(10,2)),   CAST(0.20 AS DECIMAL(10,2)),  CAST(1.00 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2))),
        (N'Rosie',                CAST(1.00 AS DECIMAL(10,2)),   N'pc',   CAST(22.00 AS DECIMAL(10,2)),  CAST(1.10 AS DECIMAL(10,2)),  CAST(4.80 AS DECIMAL(10,2)),   CAST(0.20 AS DECIMAL(10,2))),
        (N'Cartof',               CAST(1.00 AS DECIMAL(10,2)),   N'pc',   CAST(164.00 AS DECIMAL(10,2)), CAST(4.30 AS DECIMAL(10,2)),  CAST(37.00 AS DECIMAL(10,2)),  CAST(0.20 AS DECIMAL(10,2))),
        (N'Morcov',               CAST(1.00 AS DECIMAL(10,2)),   N'pc',   CAST(25.00 AS DECIMAL(10,2)),  CAST(0.60 AS DECIMAL(10,2)),  CAST(6.00 AS DECIMAL(10,2)),   CAST(0.10 AS DECIMAL(10,2))),
        (N'Ardei gras',           CAST(1.00 AS DECIMAL(10,2)),   N'pc',   CAST(31.00 AS DECIMAL(10,2)),  CAST(1.00 AS DECIMAL(10,2)),  CAST(7.00 AS DECIMAL(10,2)),   CAST(0.30 AS DECIMAL(10,2))),
        (N'Lamaie',               CAST(1.00 AS DECIMAL(10,2)),   N'pc',   CAST(17.00 AS DECIMAL(10,2)),  CAST(0.60 AS DECIMAL(10,2)),  CAST(5.40 AS DECIMAL(10,2)),   CAST(0.20 AS DECIMAL(10,2))),
        (N'Spanac',               CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(23.00 AS DECIMAL(10,2)),  CAST(2.90 AS DECIMAL(10,2)),  CAST(3.60 AS DECIMAL(10,2)),   CAST(0.40 AS DECIMAL(10,2))),

        -- Carne si peste.
        (N'Piept de pui',         CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(165.00 AS DECIMAL(10,2)), CAST(31.00 AS DECIMAL(10,2)), CAST(0.00 AS DECIMAL(10,2)),   CAST(3.60 AS DECIMAL(10,2))),
        (N'Carne tocata de vita', CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(254.00 AS DECIMAL(10,2)), CAST(17.20 AS DECIMAL(10,2)), CAST(0.00 AS DECIMAL(10,2)),   CAST(20.00 AS DECIMAL(10,2))),
        (N'Somon',                CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(208.00 AS DECIMAL(10,2)), CAST(20.40 AS DECIMAL(10,2)), CAST(0.00 AS DECIMAL(10,2)),   CAST(13.40 AS DECIMAL(10,2))),
        (N'Tofu',                 CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(76.00 AS DECIMAL(10,2)),  CAST(8.10 AS DECIMAL(10,2)),  CAST(1.90 AS DECIMAL(10,2)),   CAST(4.80 AS DECIMAL(10,2))),

        -- Ierburi si mirodenii, per teaspoon because recipe quantities are usually tiny.
        (N'Piper negru',          CAST(100.00 AS DECIMAL(10,2)), N'g',    CAST(251.00 AS DECIMAL(10,2)), CAST(10.40 AS DECIMAL(10,2)), CAST(64.00 AS DECIMAL(10,2)),  CAST(3.30 AS DECIMAL(10,2))),
        (N'Boia de ardei',        CAST(1.00 AS DECIMAL(10,2)),   N'tsp',  CAST(6.00 AS DECIMAL(10,2)),   CAST(0.30 AS DECIMAL(10,2)),  CAST(1.20 AS DECIMAL(10,2)),   CAST(0.30 AS DECIMAL(10,2))),
        (N'Oregano',              CAST(1.00 AS DECIMAL(10,2)),   N'tsp',  CAST(3.00 AS DECIMAL(10,2)),   CAST(0.10 AS DECIMAL(10,2)),  CAST(0.70 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2))),
        (N'Busuioc',              CAST(1.00 AS DECIMAL(10,2)),   N'tsp',  CAST(1.00 AS DECIMAL(10,2)),   CAST(0.10 AS DECIMAL(10,2)),  CAST(0.10 AS DECIMAL(10,2)),   CAST(0.00 AS DECIMAL(10,2))),
        (N'Chimion',              CAST(1.00 AS DECIMAL(10,2)),   N'tsp',  CAST(8.00 AS DECIMAL(10,2)),   CAST(0.40 AS DECIMAL(10,2)),  CAST(0.90 AS DECIMAL(10,2)),   CAST(0.50 AS DECIMAL(10,2)))
    ) AS v (IngredientName, BasisQuantity, BasisUnitAbbreviation, Calories, ProteinGrams, CarbsGrams, FatGrams)
)
MERGE dbo.IngredientNutrition AS target
USING (
    SELECT
        i.IngredientID,
        source.BasisQuantity,
        u.UnitID AS BasisUnitID,
        source.Calories,
        source.ProteinGrams,
        source.CarbsGrams,
        source.FatGrams
    FROM source
    JOIN dbo.Ingredients i ON i.Name = source.IngredientName
    JOIN dbo.Units u ON u.Abbreviation = source.BasisUnitAbbreviation
) AS source
ON target.IngredientID = source.IngredientID
WHEN NOT MATCHED BY TARGET THEN
    INSERT (IngredientID, BasisQuantity, BasisUnitID, Calories, ProteinGrams, CarbsGrams, FatGrams)
    VALUES (source.IngredientID, source.BasisQuantity, source.BasisUnitID, source.Calories,
            source.ProteinGrams, source.CarbsGrams, source.FatGrams);
GO
