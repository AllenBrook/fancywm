@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "WINDOWS_SDK_VERSION=10.0.19041.0"
set "RUNTIME_IDENTIFIER=win-x64"

for /f "usebackq delims=" %%T in (`powershell -NoProfile -Command "Get-Date -Format 'yyyyMMdd_HHmmss_fff'"`) do set "BUILD_TIMESTAMP=%%T"

set "RELEASE_ROOT=%~dp0Release\SelfContained"
set "OUTPUT_DIR=%RELEASE_ROOT%\%BUILD_TIMESTAMP%"

echo ========================================
echo FancyWM AutoBuild - Self-Contained
echo ========================================
echo Timestamp : %BUILD_TIMESTAMP%
echo Output    : %OUTPUT_DIR%
echo SDK       : Windows %WINDOWS_SDK_VERSION%
echo Runtime   : %RUNTIME_IDENTIFIER% (bundled, no .NET install required)
echo.

echo [1/4] Checking local dependencies...
set "MISSING_DEPS=0"

call :CheckDependency "winman\src\WinMan\WinMan.csproj"
call :CheckDependency "winman-windows\src\WinMan.Windows\WinMan.Windows.csproj"
call :CheckDependency "ModernWpf\ModernWpf\ModernWpf.csproj"

if "%MISSING_DEPS%"=="1" (
    echo.
    echo ERROR: Required source code is missing in your local folder.
    echo Please ensure winman, winman-windows, and ModernWpf are present.
    goto :error
)

echo [2/4] Restoring packages...
dotnet restore "FancyWM.GUI\FancyWM.GUI.csproj" -r %RUNTIME_IDENTIFIER%
if errorlevel 1 goto :error

echo [3/4] Publishing self-contained Release...
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

dotnet publish "FancyWM.GUI\FancyWM.GUI.csproj" -c Release -r %RUNTIME_IDENTIFIER% --self-contained true -o "%OUTPUT_DIR%" --no-restore
if errorlevel 1 goto :error

if not exist "%OUTPUT_DIR%\FancyWM-GUI.exe" (
    echo Publish output not found: %OUTPUT_DIR%\FancyWM-GUI.exe
    goto :error
)

echo [4/4] Done.
echo.
echo ========================================
echo Build succeeded!
echo Output: %OUTPUT_DIR%
echo Copy this entire folder to another PC - no .NET runtime needed.
echo Config (settings.json, themes, logs) is stored in this folder.
echo ========================================
exit /b 0

:CheckDependency
if exist "%~1" (
    echo   [OK] %~1
) else (
    echo   [MISSING] %~1
    set "MISSING_DEPS=1"
)
exit /b 0

:error
echo.
echo ========================================
echo Build failed!
echo ========================================
exit /b 1
