---
tags: [session, cross-platform, avalonia, nixos, macos]
date: 2026-10-19
---

# 2026-10-19 — Cross-Platform Avalonia Client for macOS and NixOS

## Context & Objectives
- The user wanted to "mac-ify" the MealPrepApp previously made for Windows so it runs on their MacBook (macOS Apple Silicon) and workstation (NixOS with Umbriel & Noctalia and Hyprland).
- The database is hosted 24/7 in Docker on their Home Server host.
- The original Windows WPF application in `App/MealPrepApp/` must be 100% preserved for university evaluation/practica grading.

## Work Completed

1. **Shared Core Extraction (`App/MealPrepApp.Core` - .NET 10)**
   - Extracted all models, Dapper repositories, database connection factory, and services.
   - Decoupled all 37 ViewModels from Windows desktop APIs:
     - Introduced `IFilePickerService` for cross-platform open/save dialogs (Excel export, photo import).
     - Decoupled image handling to raw `byte[] PhotoBytes`.
     - Replaced WPF `ICollectionView` in `IngredienteListViewModel` with clean observable groupings.
     - Decoupled `IDialogService` with type-safe modal view model dispatch.

2. **Windows WPF Project Preservation (`App/MealPrepApp`)**
   - Added `ProjectReference` to `MealPrepApp.Core`.
   - Implemented `WpfFilePickerService` and updated `DialogService`.
   - Removed duplicated models, repositories, and view models. WPF app compiles and runs against Core.

3. **Avalonia UI Desktop Client (`App/MealPrepApp.Desktop` - Avalonia 11.2)**
   - Window styling matching the Forest/Moss/Cream palette.
   - macOS native title bar integration (`ExtendClientAreaToDecorationsHint="True"`, traffic lights padding) and top `NativeMenu`.
   - Full views implemented:
     - Auth: `LoginWindow`, `LoginView`, `RegisterView`, `ProfileView`, `ChangePasswordDialog`, `ForgotPasswordDialog`.
     - Dashboard: `AcasaView`.
     - Recipes: `ReteteListView`, `ReteteDetailView`, `ReteteEditorView`.
     - Pantry & Shopping: `IngredienteRootView`, `IngredienteListView`, `FrigiderView`, `ShoppingListView`, `PantryItemDialog`, `IngredientAddDialog`, `IngredientNutritionDialog`.
     - Meal Planning: `PlanificareRootView`, `PlanificareWeeklyView` (7x4 slot grid with category chips), `PlanificareMonthlyView`, `PlanMealDialog`.
     - Reports: `RapoarteRootView`, `StatisticiLunareView`, `PlanSaptamanalPrintView`, `ListaCumparaturiPrintView`.

4. **Home Server 24/7 SQL Server (`docker-compose.db.yml`)**
   - Configured SQL Server 2022 container with persistent volume on port 1433.
   - Executed full database migration (`run_all.sql`).
   - Verified least-privilege `mealprep_app` login.

5. **Packaging & NixOS Integration**
   - `flake.nix` and `shell.nix` providing required Skia and Wayland runtime dependencies (`libX11`, `libXcursor`, `wayland`, `libGL`, `fontconfig`).
   - `scripts/build-mac.sh`: Publishes self-contained `MealPrepApp.app` bundle for macOS Apple Silicon (`osx-arm64`).
   - `scripts/build-linux.sh`: Publishes standalone single-file binary for Linux.

6. **Automated Verification**
   - Both `MealPrepApp.Core` and `MealPrepApp.Desktop` verified compiling with 0 errors in the .NET 10 SDK environment.
   - Verified standalone publishing for both `linux-x64` and `osx-arm64`.
   - Pushed branch `feature/cross-platform-avalonia` to GitHub.
