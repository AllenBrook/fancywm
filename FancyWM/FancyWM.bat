@echo off
cd /d "%~dp0"
if exist "FancyWM-GUI.exe" (
    start "" "FancyWM-GUI.exe" %*
    exit /b 0
)
if exist "FancyWM.exe" (
    start "" "FancyWM.exe" %*
    exit /b 0
)
exit /b 1
