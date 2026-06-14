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

set "LAST_RELEASE_DIR=%RELEASE_ROOT%\last release"

if not exist "%LAST_RELEASE_DIR%" mkdir "%LAST_RELEASE_DIR%"

echo Syncing last release...
echo   From: %SOURCE_DIR%
echo   To  : %LAST_RELEASE_DIR%

robocopy "%SOURCE_DIR%" "%LAST_RELEASE_DIR%" /MIR /NFL /NDL /NJH /NJS /NC /NS >nul
if errorlevel 8 (
    echo [ERROR] Failed to sync last release folder.
    exit /b 1
)

exit /b 0
