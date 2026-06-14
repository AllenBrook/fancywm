@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM 使用 UTF-8 控制台输出
chcp 65001 >nul

REM ========================================
REM 自动提交脚本（仅本地提交，不推送）
REM Notes file: .github\pending_commit_notes.md / .txt
REM Param:
REM   --check   validate only
REM   --dry-run stage + commit dry-run (no clear)
REM ========================================

set "REPO_ROOT=%~dp0"
for %%I in ("%REPO_ROOT%") do set "REPO_ROOT=%%~fI"
cd /d "%REPO_ROOT%"

set "NOTES_FILE_MD=%REPO_ROOT%\.github\pending_commit_notes.md"
set "NOTES_FILE_TXT=%REPO_ROOT%\.github\pending_commit_notes.txt"
set "NOTES_FILE="
set "CHECK_ONLY=0"
set "DRY_RUN=0"
set "CUR_BRANCH="
set "NOTES_HAS_CONTENT=0"

if not "%~1"=="" (
    for %%A in (%*) do (
        if /I "%%~A"=="--check" set "CHECK_ONLY=1"
        if /I "%%~A"=="--dry-run" set "DRY_RUN=1"
    )
)

if exist "%NOTES_FILE_MD%" set "NOTES_FILE=%NOTES_FILE_MD%"
if not defined NOTES_FILE if exist "%NOTES_FILE_TXT%" set "NOTES_FILE=%NOTES_FILE_TXT%"

echo [INFO] Repo : %REPO_ROOT%
echo [INFO] Notes(md): %NOTES_FILE_MD%
echo [INFO] Notes(txt): %NOTES_FILE_TXT%
echo [INFO] Args : %*

if not exist "%REPO_ROOT%\.git" (
    echo [ERROR] .git folder not found under repo root: %REPO_ROOT%
    exit /b 1
)

if not defined NOTES_FILE (
    echo [ERROR] Notes file not found.
    echo         Expected one of:
    echo         - %NOTES_FILE_MD%
    echo         - %NOTES_FILE_TXT%
    exit /b 1
)

call :CheckNotesHasContent "%NOTES_FILE%"
if "%NOTES_HAS_CONTENT%"=="0" (
    if /I "%NOTES_FILE%"=="%NOTES_FILE_MD%" if exist "%NOTES_FILE_TXT%" (
        call :CheckNotesHasContent "%NOTES_FILE_TXT%"
        if "%NOTES_HAS_CONTENT%"=="1" (
            set "NOTES_FILE=%NOTES_FILE_TXT%"
            echo [INFO] Fallback to non-empty txt notes: %NOTES_FILE%
        )
    )
)

echo [INFO] Notes(in use): %NOTES_FILE%

git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Current directory is not a Git repository: %REPO_ROOT%
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

call :CheckNotesHasContent "%NOTES_FILE%"

if "%CHECK_ONLY%"=="1" (
    echo [OK] Check passed:
    echo      Repo : %REPO_ROOT%
    echo      Notes: %NOTES_FILE%
    echo      Branch: %CUR_BRANCH%
    echo      Mode : local-commit-only
    if "%NOTES_HAS_CONTENT%"=="0" (
        echo      Notes state: empty - commit will be blocked
    ) else (
        for %%I in ("%NOTES_FILE%") do set "NOTES_SIZE=%%~zI"
        echo      Notes state: non-empty - ready to commit, size=!NOTES_SIZE! bytes
    )
    if "%DRY_RUN%"=="1" echo      Mode : dry-run
    exit /b 0
)

if "%NOTES_HAS_CONTENT%"=="0" (
    echo [WARN] 备注文件为空，已跳过本地提交：%NOTES_FILE%
    exit /b 2
)

echo [INFO] git add -A :/
git add -A :/
if errorlevel 1 (
    echo [ERROR] git add failed.
    exit /b 1
)

git diff --cached --quiet
if errorlevel 1 goto :HAS_CHANGES
echo [WARN] 当前无可提交变更，已跳过本地提交。
echo [INFO] 当前工作区状态：
git status --short --untracked-files=all
echo [INFO] 提示：若改动不在当前仓库，脚本不会产生新的本地提交。
exit /b 2

:HAS_CHANGES
if "%DRY_RUN%"=="1" (
    echo [INFO] DRY-RUN: git commit --dry-run --cleanup=verbatim -F "%NOTES_FILE%"
    git commit --dry-run --cleanup=verbatim -F "%NOTES_FILE%"
    if errorlevel 1 (
        echo [ERROR] git commit dry-run failed.
        exit /b 1
    )
    echo [OK] Dry-run succeeded. 本脚本不会推送，也不会清空备注文件。
    exit /b 0
)

echo [INFO] git commit --cleanup=verbatim -F "%NOTES_FILE%"
git commit --cleanup=verbatim -F "%NOTES_FILE%"
if errorlevel 1 (
    echo [ERROR] git commit failed.
    exit /b 1
)

echo [INFO] 本地提交成功，准备清空备注文件...
type nul > "%NOTES_FILE%"

echo [OK] 已完成本地提交，并清空备注文件：%NOTES_FILE%
exit /b 0

:CheckNotesHasContent
set "NOTES_HAS_CONTENT=0"
if not exist "%~1" exit /b 0
set "TARGET_NOTES=%~1"
for /f "delims=" %%I in ('powershell -NoProfile -Command "$p=$env:TARGET_NOTES; $t=[IO.File]::ReadAllText($p); if([string]::IsNullOrWhiteSpace($t)){''} else {'HAS'}"') do set "NOTES_HAS_CONTENT=1"
exit /b 0
