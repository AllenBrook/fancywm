@echo off
setlocal EnableExtensions

cd /d "%~dp0"

echo ========================================
echo FancyWM AutoBuild + Run
echo ========================================
echo.

call "%~dp0AutoBuild.bat"
if errorlevel 1 (
    echo.
    echo Build failed. FancyWM was not started.
    exit /b 1
)

set "RELEASE_ROOT=%~dp0Release\Framework"
set "OUTPUT_DIR=%RELEASE_ROOT%\latest"

if not exist "%OUTPUT_DIR%\FancyWM-GUI.exe" (
    echo Executable not found: %OUTPUT_DIR%\FancyWM-GUI.exe
    exit /b 1
)

echo.
echo Starting FancyWM...
echo   %OUTPUT_DIR%\FancyWM-GUI.exe
start "" /D "%OUTPUT_DIR%" "%OUTPUT_DIR%\FancyWM-GUI.exe"

echo.
echo ========================================
echo Build succeeded and FancyWM started.
echo ========================================
exit /b 0
