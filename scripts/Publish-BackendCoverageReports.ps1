param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsDirectory,

    [string]$OutputDirectory = "temp/validation/backend-coverage-report",

    [string]$ReportTypes = "Html;Cobertura;TextSummary",

    # Drop test assemblies and generated EF migrations so the report reflects production code.
    [string]$AssemblyFilters = "-*.UnitTests;-*.IntegrationTests",

    [string]$ClassFilters = "-*.Migrations.*"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resolvedResultsDirectory = if ([System.IO.Path]::IsPathRooted($ResultsDirectory)) {
    [System.IO.Path]::GetFullPath($ResultsDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ResultsDirectory))
}
$resolvedOutputDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}
$null = New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory
$hasHostDotnet = $null -ne (Get-Command dotnet -ErrorAction SilentlyContinue)

if (-not (Test-Path -LiteralPath $resolvedResultsDirectory)) {
    throw "Coverage results directory not found: $resolvedResultsDirectory"
}

$coverageFiles = Get-ChildItem -Path $resolvedResultsDirectory -Filter "coverage.cobertura.xml" -Recurse -File
if ($coverageFiles.Count -eq 0) {
    throw "No coverage.cobertura.xml files found under $resolvedResultsDirectory"
}

function Convert-ToContainerPath {
    param([string]$AbsolutePath)

    $relativePath = Resolve-Path -LiteralPath $AbsolutePath -Relative
    $trimmedRelativePath = $relativePath.TrimStart('.').TrimStart('\').Replace('\', '/')

    return "/workspace/$trimmedRelativePath"
}

if ($hasHostDotnet) {
    Push-Location $repositoryRoot
    try {
        & dotnet tool restore
        & dotnet tool run reportgenerator -- `
            "-reports:$resolvedResultsDirectory/**/coverage.cobertura.xml" `
            "-targetdir:$resolvedOutputDirectory" `
            "-reporttypes:$ReportTypes" `
            "-assemblyfilters:$AssemblyFilters" `
            "-classfilters:$ClassFilters"
    }
    finally {
        Pop-Location
    }
}
else {
    $containerResultsDirectory = Convert-ToContainerPath -AbsolutePath $resolvedResultsDirectory
    $containerOutputDirectory = Convert-ToContainerPath -AbsolutePath $resolvedOutputDirectory
    $reportGeneratorCommand = @(
        'dotnet tool restore',
        ("dotnet tool run reportgenerator -- '-reports:{0}/**/coverage.cobertura.xml' '-targetdir:{1}' '-reporttypes:{2}' '-assemblyfilters:{3}' '-classfilters:{4}'" -f $containerResultsDirectory, $containerOutputDirectory, $ReportTypes, $AssemblyFilters, $ClassFilters)
    ) -join ' && '

    Push-Location $repositoryRoot
    try {
        & docker compose --env-file ".env.example" -f "docker-compose.dev.yml" -f "docker-compose.utils.yml" run --rm repo-shell $reportGeneratorCommand
    }
    finally {
        Pop-Location
    }
}

$htmlReportPath = Join-Path $resolvedOutputDirectory "index.html"
$coberturaReportPath = Join-Path $resolvedOutputDirectory "Cobertura.xml"
$textSummaryPath = Join-Path $resolvedOutputDirectory "Summary.txt"

foreach ($expectedFile in @($htmlReportPath, $coberturaReportPath, $textSummaryPath)) {
    if (-not (Test-Path -LiteralPath $expectedFile)) {
        throw "ReportGenerator did not produce expected artifact: $expectedFile"
    }
}

[pscustomobject]@{
    HtmlReport = $htmlReportPath
    CoberturaReport = $coberturaReportPath
    TextSummary = $textSummaryPath
}
