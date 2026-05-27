@echo off
setlocal

set "PROJECT=%~dp0MealPrepApp\MealPrepApp.csproj"
set "PUBLISH_DIR=%~dp0publish\MealPrepApp-win-x64"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: dotnet was not found on PATH. Install the .NET 10 SDK on this Windows machine.
    exit /b 1
)

echo Publishing MealPrepApp as a self-contained Windows x64 executable...
echo Project: %PROJECT%
echo Output : %PUBLISH_DIR%

dotnet publish "%PROJECT%" /p:PublishProfile=Windows-x64-Folder
if errorlevel 1 (
    echo.
    echo ERROR: dotnet publish failed. Fix the build error above, then run this script again.
    exit /b 1
)

if not exist "%PUBLISH_DIR%\MealPrepApp.exe" (
    echo.
    echo ERROR: Publish completed but MealPrepApp.exe was not found in %PUBLISH_DIR%.
    exit /b 1
)

echo.
echo SUCCESS: Created %PUBLISH_DIR%\MealPrepApp.exe
echo.
echo Before running it on another machine:
echo   1. Copy appsettings.Local.template.json to appsettings.Local.json in the same folder as MealPrepApp.exe.
echo   2. Replace __SET_APP_PASSWORD__ with the real mealprep_app password.
echo   3. Make sure SQL Server is reachable from that machine.
echo.
endlocal
