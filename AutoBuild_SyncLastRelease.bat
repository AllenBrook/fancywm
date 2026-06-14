@echo off
setlocal EnableExtensions

set "SOURCE_DIR=%~1"
set "RELEASE_ROOT=%~2"

if "%SOURCE_DIR%"=="" (
    echo [ERROR] AutoBuild_SyncLastRelease.bat: source directory not specified.
    exit /b 1
)

if "%RELEASE_ROOT%"=="" (
    echo [ERROR] AutoBuild_SyncLastRelease.bat: release root not specified.
    exit /b 1
)

if not exist "%SOURCE_DIR%\FancyWM-GUI.exe" (
    echo [ERROR] Source build output not found: %SOURCE_DIR%\FancyWM-GUI.exe
    exit /b 1
)

set "LATEST_DIR=%RELEASE_ROOT%\latest"

if not exist "%LATEST_DIR%" mkdir "%LATEST_DIR%"

echo Syncing latest...
echo   From: %SOURCE_DIR%
echo   To  : %LATEST_DIR%

robocopy "%SOURCE_DIR%" "%LATEST_DIR%" /MIR /NFL /NDL /NJH /NJS /NC /NS >nul
if errorlevel 8 (
    echo [ERROR] Failed to sync latest folder.
    exit /b 1
)

exit /b 0
