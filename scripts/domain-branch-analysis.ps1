<#
.SYNOPSIS
    Generates a per-class breakdown of uncovered branches in POS.Domain.
#>

$json = Get-Content -Raw coverage_fresh.json | ConvertFrom-Json

$domainResults = @()

foreach ($assembly in $json.PSObject.Properties) {
    if ($assembly.Name -ne "POS.Domain.dll") { continue }

    foreach ($file in $assembly.Value.PSObject.Properties) {
        $filePath = $file.Name

        foreach ($class in $file.Value.PSObject.Properties) {
            $className = $class.Name
            $methods = $class.Value

            $classTotalBranches = 0
            $classCoveredBranches = 0
            $classUncoveredBranches = 0
            $classTotalLines = 0
            $classCoveredLines = 0
            $methodDetails = @()

            foreach ($method in $methods.PSObject.Properties) {
                $methodSig = $method.Name
                $methodData = $method.Value

                $simpleName = $methodSig
                if ($simpleName -match '::([^(]+)') {
                    $simpleName = $Matches[1]
                }

                $totalLines = 0; $coveredLines = 0
                if ($methodData.Lines) {
                    foreach ($line in $methodData.Lines.PSObject.Properties) {
                        $totalLines++
                        if ($line.Value -gt 0) { $coveredLines++ }
                    }
                }

                $totalBr = 0; $coveredBr = 0; $uncoveredBr = 0
                if ($methodData.Branches) {
                    foreach ($b in $methodData.Branches) {
                        $totalBr++
                        if ($b.Hits -gt 0) { $coveredBr++ } else { $uncoveredBr++ }
                    }
                }

                $classTotalBranches += $totalBr
                $classCoveredBranches += $coveredBr
                $classUncoveredBranches += $uncoveredBr
                $classTotalLines += $totalLines
                $classCoveredLines += $coveredLines

                if ($uncoveredBr -gt 0) {
                    $methodDetails += [PSCustomObject]@{
                        Method = $simpleName
                        TotalBranches = $totalBr
                        UncoveredBranches = $uncoveredBr
                        BranchCoverage = [math]::Round(($coveredBr / [math]::Max($totalBr, 1)) * 100, 1)
                    }
                }
            }

            $branchCov = if ($classTotalBranches -gt 0) { [math]::Round($classCoveredBranches / $classTotalBranches * 100, 1) } else { 100.0 }
            $lineCov = if ($classTotalLines -gt 0) { [math]::Round($classCoveredLines / $classTotalLines * 100, 1) } else { 100.0 }

            $domainResults += [PSCustomObject]@{
                Class = $className
                File = $filePath
                TotalLines = $classTotalLines
                LineCoverage = $lineCov
                TotalBranches = $classTotalBranches
                CoveredBranches = $classCoveredBranches
                UncoveredBranches = $classUncoveredBranches
                BranchCoverage = $branchCov
                UncoveredMethods = ($methodDetails | ForEach-Object { "$($_.Method) ($($_.UncoveredBranches)/$($_.TotalBranches) branches)" }) -join "; "
            }
        }
    }
}

# Sort by uncovered branches descending
$sorted = $domainResults | Sort-Object UncoveredBranches -Descending

Write-Host "=========================================="
Write-Host "  POS.Domain - Per-Class Branch Coverage"
Write-Host "=========================================="
Write-Host ""

$totalB = ($sorted | Measure-Object -Property TotalBranches -Sum).Sum
$totalU = ($sorted | Measure-Object -Property UncoveredBranches -Sum).Sum
Write-Host ("Total classes: {0}" -f $sorted.Count)
Write-Host ("Total branches: {0}" -f $totalB)
Write-Host ("Uncovered branches: {0}" -f $totalU)
Write-Host ("Overall branch coverage: {0}%" -f [math]::Round(($totalB - $totalU) / $totalB * 100, 1))
Write-Host ""

Write-Host ("{0,-5} {1,-50} {2,-8} {3,-10} {4,-10} {5,-10} {6,-9}" -f "Rank", "Class", "Lines", "Line %", "Branches", "Uncovered", "Branch %")
Write-Host ("-" * 100)

$i = 1
foreach ($r in $sorted) {
    $classShort = if ($r.Class.Length -gt 50) { $r.Class.Substring(0,47) + "..." } else { $r.Class }
    $branchPct = if ($r.TotalBranches -gt 0) { "$($r.BranchCoverage)%" } else { "N/A" }
    $linePct = "$($r.LineCoverage)%"
    Write-Host ("{0,-5} {1,-50} {2,-8} {3,-10} {4,-10} {5,-10} {6,-9}" -f $i, $classShort, $r.TotalLines, $linePct, $r.TotalBranches, $r.UncoveredBranches, $branchPct)
    $i++
}

Write-Host ""
Write-Host "=== Methods with Uncovered Branches (details) ==="
Write-Host ""

foreach ($r in $sorted) {
    if ($r.UncoveredBranches -gt 0 -and $r.UncoveredMethods) {
        Write-Host ("--- {0} ({1}/{2} branches uncovered, {3}%) ---" -f $r.Class, $r.UncoveredBranches, $r.TotalBranches, $r.BranchCoverage)
        $methods = $r.UncoveredMethods -split "; "
        foreach ($m in $methods) {
            Write-Host ("    - {0}" -f $m)
        }
        Write-Host ""
    }
}

# Export to CSV
$domainResults | Sort-Object UncoveredBranches -Descending | Export-Csv -Path "domain_branch_analysis.csv" -NoTypeInformation
Write-Host "Full report saved to domain_branch_analysis.csv"
