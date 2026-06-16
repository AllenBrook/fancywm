@echo off
echo FancyWM has build scripts:
echo.
echo   AutoBuild_Framework.bat      - small output, requires .NET 10 on target PC
echo   AutoBuild_SelfContained.bat  - large output, no .NET install required
echo   AutoBuildAndRun.bat          - build (framework) and start FancyWM
echo.
echo After build: Release\latest (full) and Release\latestmin (incremental DLLs and exes)
echo Running framework-dependent build (default)...
echo.
call "%~dp0AutoBuild_Framework.bat"
exit /b %ERRORLEVEL%
