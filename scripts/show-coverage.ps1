param()

$ErrorActionPreference = "Stop"

# Find coverage file
$coverageFile = "coverage.opencover.xml"
if (-not (Test-Path $coverageFile)) {
    $coverageFile = "POS.Tests/coverage.opencover.xml"
}
if (-not (Test-Path $coverageFile)) {
    Write-Host "Coverage file not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Coverage file: $coverageFile" -ForegroundColor Cyan
Write-Host ""

# Parse XML
$xml = [xml](Get-Content $coverageFile)
$summary = $xml.CoverageSession.Summary

Write-Host "================================" -ForegroundColor Green
Write-Host "  POS System - Coverage Summary " -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""

$lineCov = [math]::Round([double]$summary.sequenceCoverage, 2)
$branchCov = [math]::Round([double]$summary.branchCoverage, 2)
$methodCov = [math]::Round([double]$summary.methodCoverage, 2)

Write-Host ("  Line Coverage:   " + $lineCov + "%") -ForegroundColor $(if ($lineCov -ge 80) { "Green" } else { "Red" })
Write-Host ("  Branch Coverage: " + $branchCov + "%") -ForegroundColor $(if ($branchCov -ge 60) { "Green" } else { "Red" })
Write-Host ("  Method Coverage: " + $methodCov + "%") -ForegroundColor $(if ($methodCov -ge 60) { "Green" } else { "Red" })
Write-Host ("  Classes:         " + $summary.numClasses)
Write-Host ("  Methods:         " + $summary.numMethods)
Write-Host ("  Files:           " + $summary.numFiles)
Write-Host ""

Write-Host "  --- Per-Assembly Breakdown ---" -ForegroundColor Yellow
Write-Host ""

# Parse per-assembly
$modules = @($xml.CoverageSession.Modules.Module)
foreach ($mod in $modules) {
    $name = $mod.ModuleName -replace '\.dll$', ''
    $ml = [math]::Round([double]$mod.Summary.sequenceCoverage, 2)
    $mb = [math]::Round([double]$mod.Summary.branchCoverage, 2)
    $mm = [math]::Round([double]$mod.Summary.methodCoverage, 2)
    Write-Host ("  " + $name.PadRight(30) + "  Line: " + $ml.ToString().PadLeft(6) + "%  Branch: " + $mb.ToString().PadLeft(6) + "%  Method: " + $mm.ToString().PadLeft(6) + "%")
}

Write-Host ""
Write-Host "================================" -ForegroundColor Green
Write-Host "  Generating HTML Report "        -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Green
Write-Host ""

# Generate HTML report
$reportDir = "./coverage-report"
reportgenerator -reports:$coverageFile -targetdir:$reportDir -reporttypes:"Html;HtmlSummary" -title:"POS System Coverage" -verbosity:Warning 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host ("Report generated: " + (Resolve-Path $reportDir)) -ForegroundColor Green
    Write-Host "Opening in browser..." -ForegroundColor Yellow
    Start-Process (Join-Path $reportDir "index.html")
} else {
    Write-Host "Report generation failed (exit code: $LASTEXITCODE)" -ForegroundColor Red
}
