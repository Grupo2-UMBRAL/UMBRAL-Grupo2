param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsDirectory,
    [int]$TemporaryThreshold = 10,
    [int]$TargetThreshold = 90
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ResultsDirectory)) {
    throw "Coverage results directory not found: $ResultsDirectory"
}

$coverageFiles = Get-ChildItem -Path $ResultsDirectory -Filter "coverage.cobertura.xml" -Recurse -File

if ($coverageFiles.Count -eq 0) {
    throw "No coverage.cobertura.xml files found under $ResultsDirectory"
}

# Gated metric is BRANCH coverage (professor's academic target is 90% branch, not line).
# Line coverage is still aggregated and reported as a secondary reference.
$totalBranchesValid = 0
$totalBranchesCovered = 0
$totalLinesValid = 0
$totalLinesCovered = 0
$projectSummaries = @()

function Get-Percentage {
    param([int]$Covered, [int]$Valid)
    # No branches/lines to cover is vacuously complete: report 100 so empty projects
    # never drag the aggregate below the gate.
    if ($Valid -eq 0) { return 100 }
    return [math]::Round(($Covered / $Valid) * 100, 2)
}

foreach ($coverageFile in $coverageFiles) {
    [xml]$coverageXml = Get-Content -LiteralPath $coverageFile.FullName
    $coverageNode = $coverageXml.coverage

    if ($null -eq $coverageNode) {
        throw "Invalid cobertura report: $($coverageFile.FullName)"
    }

    $branchesValid = [int]$coverageNode.'branches-valid'
    $branchesCovered = [int]$coverageNode.'branches-covered'
    $linesValid = [int]$coverageNode.'lines-valid'
    $linesCovered = [int]$coverageNode.'lines-covered'

    $branchPercentage = Get-Percentage -Covered $branchesCovered -Valid $branchesValid
    $linePercentage = Get-Percentage -Covered $linesCovered -Valid $linesValid

    $projectName = Split-Path -Leaf (Split-Path -Parent $coverageFile.DirectoryName)

    $projectSummaries += [pscustomobject]@{
        Project = $projectName
        BranchesCovered = $branchesCovered
        BranchesValid = $branchesValid
        BranchCoverage = $branchPercentage
        LinesCovered = $linesCovered
        LinesValid = $linesValid
        LineCoverage = $linePercentage
        Report = $coverageFile.FullName
    }

    $totalBranchesValid += $branchesValid
    $totalBranchesCovered += $branchesCovered
    $totalLinesValid += $linesValid
    $totalLinesCovered += $linesCovered
}

$overallBranchCoverage = Get-Percentage -Covered $totalBranchesCovered -Valid $totalBranchesValid
$overallLineCoverage = Get-Percentage -Covered $totalLinesCovered -Valid $totalLinesValid

$summary = [pscustomobject]@{
    Metric = "branch"
    TemporaryThreshold = $TemporaryThreshold
    TargetThreshold = $TargetThreshold
    OverallBranchCoverage = $overallBranchCoverage
    OverallLineCoverage = $overallLineCoverage
    BranchesCovered = $totalBranchesCovered
    BranchesValid = $totalBranchesValid
    LinesCovered = $totalLinesCovered
    LinesValid = $totalLinesValid
    Projects = $projectSummaries
}

$summaryJson = $summary | ConvertTo-Json -Depth 5
Write-Output $summaryJson

if ($env:GITHUB_STEP_SUMMARY) {
    @"
## Backend coverage

Gated metric: **branch coverage**.

- Overall branch coverage: $overallBranchCoverage%
- Overall line coverage (reference): $overallLineCoverage%
- Temporary threshold: $TemporaryThreshold%
- Academic target: $TargetThreshold% branch
"@ | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Encoding utf8 -Append
}

if ($overallBranchCoverage -lt $TemporaryThreshold) {
    throw "Backend branch coverage $overallBranchCoverage% is below temporary threshold $TemporaryThreshold%"
}
