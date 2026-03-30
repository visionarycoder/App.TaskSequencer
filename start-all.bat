@echo off
REM Start All Services: Aspire.AppHost, Orleans Silo, Console, and MAUI
REM This is the recommended entry point - Aspire orchestrates everything

setlocal enabledelayedexpansion

REM Get the script directory (solution root)
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

echo.
echo ========================================
echo   App.TaskSequencer - Full Startup
echo ========================================
echo.
echo This will start:
echo   1. Aspire.AppHost (orchestrator)
echo   2. Orleans Silo (grain host)
echo   3. Console Client (task plan generator)
echo.
pause

REM Start Aspire.AppHost
echo.
echo Starting Aspire.AppHost...
echo.
start "Aspire.AppHost" cmd /k "cd /d "%SCRIPT_DIR%src\Aspire.AppHost" && dotnet run"

REM Give Aspire time to start
timeout /t 3 /nobreak

REM Start MAUI App in separate window (optional - can run independently)
echo.
echo [Optional] Starting MAUI Desktop App...
echo.
start "MAUI.Desktop" cmd /k "cd /d "%SCRIPT_DIR%src\Client.Desktop.Maui" && dotnet run -f net10.0-windows10.0.20348"

echo.
echo ========================================
echo   All services started!
echo ========================================
echo.
echo Aspire Dashboard:  http://localhost:15000
echo Orleans Silo:      localhost:11111
echo.
echo Console output and status visible in separate windows above.
echo Close any window to stop that service.
echo.
pause
