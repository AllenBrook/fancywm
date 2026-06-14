@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "PID=%~1"
if not defined PID exit /b 1

:waitloop
tasklist /FI "PID eq %PID%" 2>nul | find "%PID%" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

if exist "FancyWM.bat" (
    start "" /D "%~dp0" cmd /c "call FancyWM.bat"
    exit /b 0
)

if exist "FancyWM-GUI.exe" (
    start "" "%~dp0FancyWM-GUI.exe"
    exit /b 0
)

if exist "FancyWM.exe" (
    start "" "%~dp0FancyWM.exe"
    exit /b 0
)

exit /b 1
