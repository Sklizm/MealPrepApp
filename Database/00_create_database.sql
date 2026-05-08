-- MealPrepDB: database creation
-- Run against the master database on your SQL Server container.
-- Idempotent: safe to re-run.

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'MealPrepDB')
BEGIN
    CREATE DATABASE MealPrepDB;
END
GO

USE MealPrepDB;
GO
