$json = Get-Content -Raw POS.Tests/coverage_fresh.json | ConvertFrom-Json

Write-Host "=== ArabicName Branch Coverage ==="
Write-Host ""

foreach ($assembly in $json.PSObject.Properties) {
    if ($assembly.Name -eq "POS.Domain.dll") {
        foreach ($file in $assembly.Value.PSObject.Properties) {
            if ($file.Name -like "*ArabicName*") {
                foreach ($class in $file.Value.PSObject.Properties) {
                    Write-Host ("Class: " + $class.Name)
                    foreach ($method in $class.Value.PSObject.Properties) {
                        $simple = $method.Name
                        if ($simple -match '::([^(]+)') { $simple = $Matches[1] }
                        Write-Host ("  Method: " + $simple)
                        $total = 0; $hits = 0; $unhit = 0
                        if ($method.Value.Branches) {
                            foreach ($b in $method.Value.Branches) {
                                $total++
                                if ($b.Hits -gt 0) { $hits++ } else { $unhit++ }
                            }
                        }
                        Write-Host ("    Branches: " + $total + " total, " + $hits + " hit, " + $unhit + " unhit")
                        if ($unhit -gt 0) {
                            foreach ($b in $method.Value.Branches) {
                                if ($b.Hits -eq 0) {
                                    Write-Host ("      UNCOVERED: Line " + $b.Line + " Path " + $b.Path)
                                }
                            }
                        }
                    }
                    Write-Host ""
                }
            }
        }
    }
}

Write-Host "=== Money Branch Coverage ==="
foreach ($assembly in $json.PSObject.Properties) {
    if ($assembly.Name -eq "POS.Domain.dll") {
        foreach ($file in $assembly.Value.PSObject.Properties) {
            if ($file.Name -like "*Money.cs" -and $file.Name -notlike "*MoneyPolicy*") {
                foreach ($class in $file.Value.PSObject.Properties) {
                    Write-Host ("Class: " + $class.Name)
                    foreach ($method in $class.Value.PSObject.Properties) {
                        $simple = $method.Name
                        if ($simple -match '::([^(]+)') { $simple = $Matches[1] }
                        Write-Host ("  Method: " + $simple)
                        $total = 0; $hits = 0; $unhit = 0
                        if ($method.Value.Branches) {
                            foreach ($b in $method.Value.Branches) {
                                $total++
                                if ($b.Hits -gt 0) { $hits++ } else { $unhit++ }
                            }
                        }
                        $status = if ($unhit -gt 0) { "UNCOVERED" } else { "OK" }
                        Write-Host ("    Branches: " + $total + " total, " + $hits + " hit, " + $unhit + " unhit  [" + $status + "]")
                    }
                    Write-Host ""
                }
            }
        }
    }
}

# Overall stats
$totalBranches = 0
$totalHit = 0
$totalUnhit = 0
foreach ($assembly in $json.PSObject.Properties) {
    if ($assembly.Name -eq "POS.Domain.dll") {
        foreach ($file in $assembly.Value.PSObject.Properties) {
            foreach ($class in $file.Value.PSObject.Properties) {
                foreach ($method in $class.Value.PSObject.Properties) {
                    if ($method.Value.Branches) {
                        foreach ($b in $method.Value.Branches) {
                            $totalBranches++
                            if ($b.Hits -gt 0) { $totalHit++ } else { $totalUnhit++ }
                        }
                    }
                }
            }
        }
    }
}
$cov = if ($totalBranches -gt 0) { [math]::Round($totalHit / $totalBranches * 100, 1) } else { 100.0 }
Write-Host ("=== POS.Domain Overall: " + $totalBranches + " branches, " + $totalHit + " hit, " + $totalUnhit + " unhit = " + $cov + "% ===")
