@echo off
cd /d D:\jopos2\POS_System

echo ==========================================
echo   POS System - Code Coverage Analysis
echo   Threshold: 80%%
echo ==========================================
echo.

echo Killing lingering testhost processes...
taskkill /f /im testhost.exe >nul 2>&1
taskkill /f /im dotnet.exe >nul 2>&1

echo Running tests with code coverage...
dotnet test POS.Tests\POS.Tests.csproj ^
    --settings coverlet.runsettings ^
    -p:CollectCoverage=true ^
    -p:CoverletOutputFormat=opencover ^
    -p:CoverletOutput="coverage.opencover.xml" ^
    -p:Include="[POS.Application]*,[POS.Domain]*,[POS.Infrastructure]*,[POS.Reporting]*" ^
    -p:Exclude="[POS.Tests]*,[POS.Benchmarks]*,[*]Migrations.*,[*]*.Designer.*" ^
    -p:ExcludeByAttribute="GeneratedCodeAttribute,CompilerGeneratedAttribute" ^
    -p:ExcludeByFile="**/*.g.cs,**/*.g.i.cs"

if %ERRORLEVEL% NEQ 0 (
    echo X Tests failed! Coverage report not generated.
    exit /b %ERRORLEVEL%
)

echo.
echo Generating HTML coverage report...

where reportgenerator >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Installing ReportGenerator...
    dotnet tool install -g dotnet-reportgenerator-globaltool
)

reportgenerator ^
    -reports:"coverage.opencover.xml" ^
    -targetdir:"coverage-report" ^
    -reporttypes:"Html;HtmlSummary;CsvSummary" ^
    -title:"POS System Coverage" ^
    -verbosity:Warning

echo.
echo Coverage report generated in coverage-report/
echo Opening in browser...
start "" "coverage-report\index.html"
