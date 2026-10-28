USE MealPrepDB;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshotItems i JOIN dbo.ShoppingSnapshots s ON s.SnapshotID=i.SnapshotID JOIN dbo.Users u ON u.UserID=s.UserID WHERE u.Username=N'regression' AND i.IsPurchased=1 AND i.ImportedAt IS NOT NULL)
 THROW 51000,N'Migration lost shopping progress',1;
IF NOT EXISTS(SELECT 1 FROM dbo.MealPlanEntries WHERE Notes=N'Preserve me' AND Servings=3) THROW 51000,N'Migration lost planned meals',1;
PRINT N'Redesign migration preservation checks passed';
GO
