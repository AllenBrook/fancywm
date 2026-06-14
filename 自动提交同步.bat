@echo off
setlocal EnableExtensions EnableDelayedExpansion

chcp 65001 >nul

set "REPO_ROOT=%~dp0"
for %%I in ("%REPO_ROOT%") do set "REPO_ROOT=%%~fI"
cd /d "%REPO_ROOT%"

set "COMMIT_SCRIPT=%REPO_ROOT%\自动提交.bat"
set "CHECK_ONLY=0"
set "DRY_RUN=0"
set "CUR_BRANCH="

if not "%~1"=="" (
    for %%A in (%*) do (
        if /I "%%~A"=="--check" set "CHECK_ONLY=1"
        if /I "%%~A"=="--dry-run" set "DRY_RUN=1"
    )
)

echo [INFO] Repo : %REPO_ROOT%
echo [INFO] Commit script : %COMMIT_SCRIPT%
echo [INFO] Args : %*

if not exist "%REPO_ROOT%\.git" (
    echo [ERROR] .git folder not found under repo root: %REPO_ROOT%
    exit /b 1
)

if not exist "%COMMIT_SCRIPT%" (
    echo [ERROR] 自动提交脚本不存在：%COMMIT_SCRIPT%
    exit /b 1
)

for /f "delims=" %%I in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set "CUR_BRANCH=%%I"
if not defined CUR_BRANCH (
    echo [ERROR] Cannot detect current branch.
    exit /b 1
)
if /I "%CUR_BRANCH%"=="HEAD" (
    echo [ERROR] Detached HEAD is not supported by this script.
    echo         Please switch to a branch first.
    exit /b 1
)

if "%CHECK_ONLY%"=="1" (
    call "%COMMIT_SCRIPT%" --check
    if errorlevel 1 (
        echo [ERROR] 自动提交检查失败。
        exit /b 1
    )
    echo [OK] Check passed. 同步脚本将推送到分支：%CUR_BRANCH%
    exit /b 0
)

call "%COMMIT_SCRIPT%" %*
set "COMMIT_RC=%errorlevel%"

if "%COMMIT_RC%"=="1" (
    echo [ERROR] 自动提交执行失败，已停止同步。
    exit /b 1
)

if "%COMMIT_RC%"=="2" (
    echo [INFO] 自动提交已跳过（无变更或备注为空），继续执行推送流程。
) else (
    echo [INFO] 自动提交执行完成，继续执行推送流程。
)

if "%DRY_RUN%"=="1" (
    echo [INFO] DRY-RUN: git push --dry-run --progress origin %CUR_BRANCH%:%CUR_BRANCH%
    git push --dry-run --progress origin %CUR_BRANCH%:%CUR_BRANCH%
    if errorlevel 1 (
        echo [ERROR] git push dry-run failed.
        exit /b 1
    )
    echo [OK] Dry-run 同步成功（未实际推送）。
    exit /b 0
)

echo [INFO] git push --progress origin %CUR_BRANCH%:%CUR_BRANCH%
git push --progress origin %CUR_BRANCH%:%CUR_BRANCH%
if errorlevel 1 (
    echo [ERROR] git push failed.
    exit /b 1
)

echo [OK] 同步完成：已推送到 %CUR_BRANCH% 分支。
exit /b 0
