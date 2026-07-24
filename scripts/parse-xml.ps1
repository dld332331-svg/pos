$path = "POS.Tests/coverage.opencover.xml"
$xml = [xml](Get-Content $path)
$s = $xml.SelectSingleNode("//CoverageSession/Summary")

$lineCov = [math]::Round([double]$s.sequenceCoverage, 2)
$branchCov = [math]::Round([double]$s.branchCoverage, 2)
$methodCov = [math]::Round([double]$s.visitedMethods / [double]$s.numMethods * 100, 2)

Write-Host ("OVERALL:")
Write-Host ("  Line Coverage:   " + $lineCov + "%")
Write-Host ("  Branch Coverage: " + $branchCov + "%")
Write-Host ("  Method Coverage: " + $methodCov + "%")
Write-Host ("  Classes:         " + $s.numClasses + " (" + $s.visitedClasses + " visited)")
Write-Host ("  Methods:         " + $s.numMethods + " (" + $s.visitedMethods + " visited)")
Write-Host ("  Files:           " + $s.numFiles)
Write-Host ""
Write-Host ("PER-ASSEMBLY:")
Write-Host ""

$modules = $xml.SelectNodes("//CoverageSession/Modules/Module")
foreach ($mod in $modules) {
    $name = $mod.ModuleName
    $ms = $mod.SelectSingleNode("Summary")
    if ($ms) {
        $ml = [math]::Round([double]$ms.sequenceCoverage, 2)
        $mb = [math]::Round([double]$ms.branchCoverage, 2)
        $mmC = $ms.numMethods
        $mvC = $ms.visitedMethods
        $mmPct = if ($mmC -gt 0) { [math]::Round($mvC / $mmC * 100, 2) } else { 100.00 }
        $files = $mod.SelectNodes("Files/File").Count
        Write-Host ("  " + $name.PadRight(30) + " Line:" + $ml.ToString().PadLeft(7) + "%  Branch:" + $mb.ToString().PadLeft(7) + "%  Method:" + $mmPct.ToString().PadLeft(7) + "%  Files:" + $files)
    }
}

Write-Host ""
Write-Host ("REPORT:")
$reportDir = "./coverage-report"
reportgenerator -reports:$path -targetdir:$reportDir -reporttypes:"Html;HtmlSummary" -title:"POS System Coverage" -verbosity:Warning 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host ("  Generated: " + (Resolve-Path $reportDir))
    Write-Host ("  Opening browser...")
    Start-Process (Join-Path $reportDir "index.html")
} else {
    Write-Host ("  ReportGenerator failed (exit: " + $LASTEXITCODE + ")")
}
