USE MealPrepDB;
GO

MERGE dbo.Categories AS target
USING (VALUES
    (N'Breakfast', N'Morning meals to start the day'),
    (N'Lunch',     N'Midday meals'),
    (N'Dinner',    N'Evening meals'),
    (N'Snack',     N'Small bites between meals'),
    (N'Dessert',   N'Sweet course'),
    (N'Drink',     N'Beverages and smoothies')
) AS source (Name, Description)
ON target.Name = source.Name
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, Description)
    VALUES (source.Name, source.Description);
GO
