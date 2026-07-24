<#
.SYNOPSIS
    Runs code coverage analysis and generates HTML report for the POS System.
.DESCRIPTION
    1. Runs dotnet test with coverlet MSBuild properties for code coverage
    2. Generates HTML report using ReportGenerator
    3. Opens the report in the default browser
    4. Exits with error code if coverage is below threshold

    Usage from project root:
        .\scripts\coverage.ps1
        .\scripts\coverage.ps1 -NoOpen -Threshold 85
.PARAMETER NoOpen
    Skip opening the report in the browser after generation.
.PARAMETER Threshold
    Minimum line coverage threshold (default: 80). Exits 1 if below.
.PARAMETER NoInstall
    Skip automatic ReportGenerator global tool installation.
#>
param(
    [switch]$NoOpen,
    [int]$Threshold = 80,
    [switch]$NoInstall
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$reportDir = "$root\coverage-report"
$coverageFile = "$root\coverage.opencover.xml"
$runsettings = "$root\coverlet.runsettings"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  POS System - Code Coverage Analysis"    -ForegroundColor Cyan
Write-Host "  Threshold: $Threshold%"                 -ForegroundColor Cyan
Write-Host "  Output:    $coverageFile"                -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Kill any lingering testhost processes that could lock DLLs
Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force

# Step 1: Run tests with coverage via MSBuild properties
Write-Host "-> Running tests with code coverage..." -ForegroundColor Yellow

$result = & dotnet test "$root\POS.Tests\POS.Tests.csproj" --settings "$runsettings" /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput="$coverageFile" /p:Include="[POS.Application]*,[POS.Domain]*,[POS.Infrastructure]*,[POS.Reporting]*" /p:Exclude="[POS.Tests]*,[POS.Benchmarks]*,[*]Migrations.*,[*]*.Designer.*" /p:ExcludeByAttribute="GeneratedCodeAttribute,CompilerGeneratedAttribute" /p:ExcludeByFile="**/*.g.cs,**/*.g.i.cs" 2>&1

Write-Host $result

if ($LASTEXITCODE -ne 0) {
    Write-Host "X Tests failed! Coverage report not generated." -ForegroundColor Red
    exit $LASTEXITCODE
}

# Step 2: Generate HTML report
Write-Host ""
Write-Host "-> Generating HTML coverage report..." -ForegroundColor Yellow

if (-not $NoInstall) {
    $rg = Get-Command reportgenerator -ErrorAction SilentlyContinue
    if (-not $rg) {
        Write-Host "  Installing ReportGenerator global tool..." -ForegroundColor DarkYellow
        dotnet tool install -g dotnet-reportgenerator-globaltool 2>&1 | Out-Null
    }
}

reportgenerator `
    -reports:"$coverageFile" `
    -targetdir:"$reportDir" `
    -reporttypes:"Html;HtmlSummary;CsvSummary" `
    -title:"POS System Coverage" `
    -historydir:"$reportDir\history" `
    -verbosity:Warning

Write-Host "  Report saved to: $reportDir" -ForegroundColor Green

# Step 3: Parse coverage percentage
Write-Host ""
Write-Host "-> Checking coverage threshold..." -ForegroundColor Yellow

$coverageXml = [xml](Get-Content $coverageFile)
$summary = $coverageXml.CoverageSession.Summary
$lineCov = [math]::Round([double]$summary.sequenceCoverage, 1)
$branchCov = [math]::Round([double]$summary.branchCoverage, 1)

$lineOk = $lineCov -ge $Threshold
$branchOk = $branchCov -ge $Threshold

Write-Host "  Line Coverage:   $lineCov%" -ForegroundColor $(if ($lineOk) { "Green" } else { "Red" })
Write-Host "  Branch Coverage: $branchCov%" -ForegroundColor $(if ($branchOk) { "Green" } else { "Red" })

# Step 4: Enforce threshold
if (-not $lineOk -or -not $branchOk) {
    Write-Host ""
    Write-Host "WARNING: Coverage below threshold of $Threshold%!" -ForegroundColor Red
    if (-not $lineOk) {
        Write-Host "  Line coverage $lineCov% is below $Threshold%" -ForegroundColor Red
    }
    if (-not $branchOk) {
        Write-Host "  Branch coverage $branchCov% is below $Threshold%" -ForegroundColor Red
    }
    Write-Host ""
    exit 1
}

# Step 5: Open report in browser
if (-not $NoOpen) {
    $reportPath = "$reportDir\index.html"
    if (Test-Path $reportPath) {
        Write-Host "-> Opening coverage report in browser..." -ForegroundColor Yellow
        Start-Process $reportPath
    }
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Complete!"                                 -ForegroundColor Cyan
Write-Host "  Line:   $lineCov%"                        -ForegroundColor Green
Write-Host "  Branch: $branchCov%"                      -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
