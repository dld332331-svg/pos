$json = Get-Content -Raw POS.Tests/coverage_fresh.json | ConvertFrom-Json

$infraResults = @()

foreach ($assembly in $json.PSObject.Properties) {
    if ($assembly.Name -ne "POS.Infrastructure.dll") { continue }

    foreach ($file in $assembly.Value.PSObject.Properties) {
        $filePath = $file.Name
        $shortPath = $filePath -replace '^.*POS\.Infrastructure\\', ''

        foreach ($class in $file.Value.PSObject.Properties) {
            $className = $class.Name
            $methodDetails = @()
            $classTotalBranches = 0
            $classCoveredBranches = 0
            $classUncoveredBranches = 0

            foreach ($method in $class.Value.PSObject.Properties) {
                $methodSig = $method.Name
                $simpleName = $methodSig
                if ($simpleName -match '::([^(]+)') { $simpleName = $Matches[1] }
                $totalBr = 0; $coveredBr = 0; $uncoveredBr = 0

                if ($method.Value.Branches) {
                    foreach ($b in $method.Value.Branches) {
                        $totalBr++
                        if ($b.Hits -gt 0) { $coveredBr++ } else { $uncoveredBr++ }
                    }
                }

                $classTotalBranches += $totalBr
                $classCoveredBranches += $coveredBr
                $classUncoveredBranches += $uncoveredBr

                if ($uncoveredBr -gt 0) {
                    $methodDetails += "$simpleName ($uncoveredBr/$totalBr branches)"
                }
            }

            if ($classTotalBranches -gt 0) {
                $branchCov = [math]::Round($classCoveredBranches / $classTotalBranches * 100, 1)
                $infraResults += [PSCustomObject]@{
                    File = $shortPath
                    Class = $className
                    TotalBranches = $classTotalBranches
                    CoveredBranches = $classCoveredBranches
                    UncoveredBranches = $classUncoveredBranches
                    BranchCoverage = $branchCov
                    UncoveredMethods = ($methodDetails -join "; ")
                }
            }
        }
    }
}

$sorted = $infraResults | Sort-Object UncoveredBranches -Descending

Write-Host "=========================================="
Write-Host "  POS.Infrastructure - Per-Class Branches"
Write-Host "=========================================="
Write-Host ""

$totalB = ($sorted | Measure-Object -Property TotalBranches -Sum).Sum
$totalU = ($sorted | Measure-Object -Property UncoveredBranches -Sum).Sum
Write-Host ("Total classes with branches: {0}" -f $sorted.Count)
Write-Host ("Total branches: {0}" -f $totalB)
Write-Host ("Uncovered branches: {0}" -f $totalU)
Write-Host ("Overall branch coverage: {0}%" -f [math]::Round(($totalB - $totalU) / $totalB * 100, 1))
Write-Host ""

Write-Host ("{0,-5} {1,-30} {2,-45} {3,-10} {4,-10} {5,-10}" -f "Rank","File","Class","Branches","Uncovered","Branch%")
Write-Host ("-" * 110)

$i = 1
foreach ($r in $sorted) {
    Write-Host ("{0,-5} {1,-30} {2,-45} {3,-10} {4,-10} {5,6}%" -f $i, $r.File, $r.Class, $r.TotalBranches, $r.UncoveredBranches, $r.BranchCoverage)
    $i++
}

Write-Host ""
Write-Host "=== Methods with Uncovered Branches ==="
Write-Host ""

foreach ($r in $sorted) {
    if ($r.UncoveredBranches -gt 0) {
        Write-Host ("--- {0} ({1}/{2} branches, {3}%) ---" -f $r.Class, $r.UncoveredBranches, $r.TotalBranches, $r.BranchCoverage)
        Write-Host ("    File: {0}" -f $r.File)
        $methods = $r.UncoveredMethods -split "; "
        foreach ($m in $methods) {
            Write-Host ("    > {0}" -f $m)
        }
        Write-Host ""
    }
}

$sorted | Export-Csv -Path "infra_branch_analysis.csv" -NoTypeInformation
Write-Host "Saved to infra_branch_analysis.csv"
