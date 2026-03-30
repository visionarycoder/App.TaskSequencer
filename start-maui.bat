@echo off
REM Start MAUI Desktop Client
REM Prerequisites: Orleans silo must be running (start-orleans.bat or Aspire.AppHost)

setlocal enabledelayedexpansion

REM Get the script directory (solution root)
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

echo.
echo ========================================
echo   MAUI Desktop Client
echo ========================================
echo.
echo This client connects to Orleans silo.
echo.
echo Prerequisites:
echo   - Orleans silo must be running (localhost:11111)
echo   - Start silo with: start-orleans.bat
echo   - Or use Aspire: start-all.bat
echo.
echo Starting MAUI Desktop App...
echo.

cd /d "%SCRIPT_DIR%src\Client.Desktop.Maui"
dotnet run -f net10.0-windows10.0.20348

echo.
echo MAUI Desktop app stopped.
echo.
pause
