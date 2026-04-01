@echo off
REM Start Aspire.AppHost Only
REM Aspire orchestrates Orleans silo and Console client

setlocal enabledelayedexpansion

REM Get the script directory (solution root)
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

echo.
echo ========================================
echo   Aspire.AppHost (Orchestrator)
echo ========================================
echo.
echo Aspire will start:
echo   1. Orleans Silo (localhost:11111)
echo   2. Console Client (auto-connects to silo)
echo.
echo Aspire Dashboard: http://localhost:15000
echo.
echo Starting Aspire.AppHost...
echo.

cd /d "%SCRIPT_DIR%src\Hosting.AspireHost"
dotnet run

echo.
echo Aspire.AppHost stopped.
echo.
pause
