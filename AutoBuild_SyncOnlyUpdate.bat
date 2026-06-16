@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SOURCE_DIR=%~1"
set "RELEASE_ROOT=%~2"

if "%SOURCE_DIR%"=="" (
    echo [ERROR] AutoBuild_SyncOnlyUpdate.bat: source directory not specified.
    exit /b 1
)

if "%RELEASE_ROOT%"=="" (
    echo [ERROR] AutoBuild_SyncOnlyUpdate.bat: release root not specified.
    exit /b 1
)

if not exist "%SOURCE_DIR%\FancyWM-GUI.exe" (
    echo [ERROR] Source build output not found: %SOURCE_DIR%\FancyWM-GUI.exe
    exit /b 1
)

set "LATEST_MIN_DIR=%RELEASE_ROOT%\latestmin"

if not exist "%LATEST_MIN_DIR%" mkdir "%LATEST_MIN_DIR%"

echo Syncing latestmin (incremental DLLs)...
echo   From: %SOURCE_DIR%
echo   To  : %LATEST_MIN_DIR%

set "COPY_FAILED=0"

for %%F in (FancyWM.dll FancyWM.exe FancyWM-GUI.exe FancyWM-GUI.dll) do (
    if not exist "%SOURCE_DIR%\%%F" (
        echo [ERROR] Required update file not found: %SOURCE_DIR%\%%F
        set "COPY_FAILED=1"
    ) else (
        copy /Y "%SOURCE_DIR%\%%F" "%LATEST_MIN_DIR%\" >nul
        if errorlevel 1 set "COPY_FAILED=1"
    )
)

for /d %%D in ("%SOURCE_DIR%\*") do (
    if exist "%%D\FancyWM.resources.dll" (
        set "LOCALE_DIR=%%~nxD"
        if not exist "%LATEST_MIN_DIR%\!LOCALE_DIR!" mkdir "%LATEST_MIN_DIR%\!LOCALE_DIR!"
        copy /Y "%%D\FancyWM.resources.dll" "%LATEST_MIN_DIR%\!LOCALE_DIR!\" >nul
        if errorlevel 1 set "COPY_FAILED=1"
    )
)

if "%COPY_FAILED%"=="1" (
    echo [ERROR] Failed to sync one or more latestmin files.
    exit /b 1
)

exit /b 0
