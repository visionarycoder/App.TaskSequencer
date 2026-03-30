@echo off
REM Start Console Client
REM Prerequisites: Orleans silo must be running (start-orleans.bat or Aspire.AppHost)

setlocal enabledelayedexpansion

REM Get the script directory (solution root)
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

echo.
echo ========================================
echo   Console Client
echo ========================================
echo.
echo This client connects to Orleans silo and generates execution plans.
echo.
echo Prerequisites:
echo   - Orleans silo must be running (localhost:11111)
echo   - Start silo with: start-orleans.bat
echo   - Or use Aspire: start-all.bat
echo.
echo Starting Console Client...
echo.

cd /d "%SCRIPT_DIR%src\Client.Desktop.Console"
dotnet run

echo.
echo Console client stopped.
echo.
pause
