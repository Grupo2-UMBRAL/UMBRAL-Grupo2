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

$totalLinesValid = 0
$totalLinesCovered = 0
$projectSummaries = @()

foreach ($coverageFile in $coverageFiles) {
    [xml]$coverageXml = Get-Content -LiteralPath $coverageFile.FullName
    $coverageNode = $coverageXml.coverage

    if ($null -eq $coverageNode) {
        throw "Invalid cobertura report: $($coverageFile.FullName)"
    }

    $linesValid = [int]$coverageNode.'lines-valid'
    $linesCovered = [int]$coverageNode.'lines-covered'
    $percentage = if ($linesValid -eq 0) { 0 } else { [math]::Round(($linesCovered / $linesValid) * 100, 2) }

    $projectName = Split-Path -Leaf (Split-Path -Parent $coverageFile.DirectoryName)

    $projectSummaries += [pscustomobject]@{
        Project = $projectName
        LinesCovered = $linesCovered
        LinesValid = $linesValid
        Coverage = $percentage
        Report = $coverageFile.FullName
    }

    $totalLinesValid += $linesValid
    $totalLinesCovered += $linesCovered
}

$overallCoverage = if ($totalLinesValid -eq 0) {
    0
}
else {
    [math]::Round(($totalLinesCovered / $totalLinesValid) * 100, 2)
}

$summary = [pscustomobject]@{
    TemporaryThreshold = $TemporaryThreshold
    TargetThreshold = $TargetThreshold
    OverallCoverage = $overallCoverage
    LinesCovered = $totalLinesCovered
    LinesValid = $totalLinesValid
    Projects = $projectSummaries
}

$summaryJson = $summary | ConvertTo-Json -Depth 5
Write-Output $summaryJson

if ($env:GITHUB_STEP_SUMMARY) {
    @"
## Backend coverage

- Overall coverage: $overallCoverage%
- Temporary threshold: $TemporaryThreshold%
- Academic target: $TargetThreshold%
"@ | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Encoding utf8 -Append
}

if ($overallCoverage -lt $TemporaryThreshold) {
    throw "Backend coverage $overallCoverage% is below temporary threshold $TemporaryThreshold%"
}
