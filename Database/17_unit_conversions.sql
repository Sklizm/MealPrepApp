USE MealPrepDB;
GO

IF OBJECT_ID(N'dbo.UnitConversions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UnitConversions (
        UnitConversionID INT IDENTITY(1,1) NOT NULL,
        FromUnitID       INT               NOT NULL,
        ToUnitID         INT               NOT NULL,
        Multiplier       DECIMAL(18, 8)    NOT NULL,
        CONSTRAINT PK_UnitConversions         PRIMARY KEY CLUSTERED (UnitConversionID),
        CONSTRAINT UQ_UnitConversions_From_To UNIQUE (FromUnitID, ToUnitID),
        CONSTRAINT FK_UnitConversions_FromUnit FOREIGN KEY (FromUnitID) REFERENCES dbo.Units(UnitID),
        CONSTRAINT FK_UnitConversions_ToUnit   FOREIGN KEY (ToUnitID)   REFERENCES dbo.Units(UnitID),
        CONSTRAINT CK_UnitConversions_Multiplier CHECK (Multiplier > 0)
    );
END
GO

;WITH pairs AS (
    SELECT f.UnitID AS FromUnitID, t.UnitID AS ToUnitID, v.Multiplier
    FROM (VALUES
        (N'g',  N'kg', CAST(0.00100000 AS DECIMAL(18, 8))),
        (N'kg', N'g',  CAST(1000.00000000 AS DECIMAL(18, 8))),
        (N'mg', N'g',  CAST(0.00100000 AS DECIMAL(18, 8))),
        (N'g',  N'mg', CAST(1000.00000000 AS DECIMAL(18, 8))),
        (N'oz', N'g',  CAST(28.34952313 AS DECIMAL(18, 8))),
        (N'g',  N'oz', CAST(0.03527396 AS DECIMAL(18, 8))),
        (N'lb', N'g',  CAST(453.59237000 AS DECIMAL(18, 8))),
        (N'g',  N'lb', CAST(0.00220462 AS DECIMAL(18, 8))),
        (N'ml', N'l',  CAST(0.00100000 AS DECIMAL(18, 8))),
        (N'l',  N'ml', CAST(1000.00000000 AS DECIMAL(18, 8)))
    ) v (FromAbbreviation, ToAbbreviation, Multiplier)
    JOIN dbo.Units f ON f.Abbreviation = v.FromAbbreviation
    JOIN dbo.Units t ON t.Abbreviation = v.ToAbbreviation
)
MERGE dbo.UnitConversions AS target
USING pairs AS source
ON target.FromUnitID = source.FromUnitID AND target.ToUnitID = source.ToUnitID
WHEN MATCHED AND target.Multiplier <> source.Multiplier THEN
    UPDATE SET Multiplier = source.Multiplier
WHEN NOT MATCHED BY TARGET THEN
    INSERT (FromUnitID, ToUnitID, Multiplier)
    VALUES (source.FromUnitID, source.ToUnitID, source.Multiplier);
GO
