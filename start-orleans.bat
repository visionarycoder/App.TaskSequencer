@echo off
REM Start Orleans Silo Only
REM Use this if you want to run the silo separately without Aspire

setlocal enabledelayedexpansion

REM Get the script directory (solution root)
set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

echo.
echo ========================================
echo   Orleans Silo Host
echo ========================================
echo.
echo Starting Orleans silo on localhost:11111...
echo.

cd /d "%SCRIPT_DIR%src\App.TaskSequencer.OrleansHost"
dotnet run

echo.
echo Orleans silo stopped.
echo.
pause
