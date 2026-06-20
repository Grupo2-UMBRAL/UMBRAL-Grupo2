param(
    [string]$ArtifactDirectory = "temp/validation",
    [string]$EnvironmentFilePath,
    [switch]$SkipComposeSmoke,
    [ValidateSet("Full", "Frontend", "Web", "Mobile", "Backend")]
    [string]$Scope = "Full",
    [int]$TemporaryCoverageThreshold = 10,
    [int]$TargetCoverage = 90
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ArtifactDirectory))
$testResultsDirectory = Join-Path $artifactRoot "TestResults"
$backendCoverageReportDirectory = Join-Path $artifactRoot "backend-coverage-report"
$null = New-Item -ItemType Directory -Force -Path $artifactRoot, $testResultsDirectory, $backendCoverageReportDirectory
$hasHostDotnet = $null -ne (Get-Command dotnet -ErrorAction SilentlyContinue)

$envFile = if (-not [string]::IsNullOrWhiteSpace($EnvironmentFilePath)) {
    if ([System.IO.Path]::IsPathRooted($EnvironmentFilePath)) {
        $EnvironmentFilePath
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $EnvironmentFilePath))
    }
} elseif (Test-Path (Join-Path $repositoryRoot ".env")) {
    Join-Path $repositoryRoot ".env"
} else {
    Join-Path $repositoryRoot ".env.example"
}

$webDirectory = Join-Path $repositoryRoot "src/apps/web"
$mobileDirectory = Join-Path $repositoryRoot "src/apps/mobile"

$backendProjects = @(
    "src/shared/Umbral.ServiceDefaults.Tests/Umbral.ServiceDefaults.Tests.csproj",
    "src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj",
    "src/services/mission-management/MissionManagement.Api.Tests/MissionManagement.Api.Tests.csproj",
    "src/services/scoring-monitoring/ScoringMonitoring.Api/ScoringMonitoring.Api.csproj",
    "src/services/scoring-monitoring/ScoringMonitoring.Api.Tests/ScoringMonitoring.Api.Tests.csproj",
    "src/services/session-management/SessionManagement.Api/SessionManagement.Api.csproj",
    "src/services/session-management/SessionManagement.Api.Tests/SessionManagement.Api.Tests.csproj"
)

$backendTestProjects = @(
    "src/shared/Umbral.ServiceDefaults.Tests/Umbral.ServiceDefaults.Tests.csproj",
    "src/services/mission-management/MissionManagement.Api.Tests/MissionManagement.Api.Tests.csproj",
    "src/services/scoring-monitoring/ScoringMonitoring.Api.Tests/ScoringMonitoring.Api.Tests.csproj",
    "src/services/session-management/SessionManagement.Api.Tests/SessionManagement.Api.Tests.csproj"
)

function Test-Scope {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$AllowedScopes
    )

    return $AllowedScopes -contains $Scope
}

function Invoke-NpmCommand {
    param(
        [string]$WorkingDirectory,
        [string[]]$CommandArgs,
        [string]$ComposeService
    )

    $isWindowsHost = $env:OS -eq "Windows_NT"

    $npmCommand = if ($isWindowsHost) {
        Get-Command npm.cmd -ErrorAction SilentlyContinue
    } else {
        Get-Command npm -ErrorAction SilentlyContinue
    }

    if ($null -eq $npmCommand -and $isWindowsHost) {
        $npmCommand = Get-Command npm -ErrorAction SilentlyContinue
    }

    if ($null -ne $npmCommand) {
        Push-Location $WorkingDirectory
        try {
            & ($npmCommand.Source) @CommandArgs
            if ($LASTEXITCODE -ne 0) {
                throw "Command '$($npmCommand.Source) $($CommandArgs -join ' ')' failed with exit code $LASTEXITCODE."
            }
        }
        finally {
            Pop-Location
        }
        return
    }

    Push-Location $repositoryRoot
    try {
        & docker compose --env-file "$envFile" -f "docker-compose.dev.yml" -f "docker-compose.utils.yml" run --rm $ComposeService @CommandArgs
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose run $ComposeService $($CommandArgs -join ' ') failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-DotnetCommand {
    param([string[]]$Arguments)

    if ($hasHostDotnet) {
        Push-Location $repositoryRoot
        try {
            & dotnet @Arguments
        }
        finally {
            Pop-Location
        }
        return
    }

    Push-Location $repositoryRoot
    try {
        & docker compose --env-file "$envFile" -f "docker-compose.dev.yml" -f "docker-compose.utils.yml" run --rm --entrypoint dotnet dotnet-sdk @Arguments
    }
    finally {
        Pop-Location
    }
}

& (Join-Path $PSScriptRoot "Test-VersionedSecrets.ps1") -RepositoryRoot $repositoryRoot

if (Test-Scope -AllowedScopes @("Full", "Frontend", "Web")) {
    Invoke-NpmCommand -WorkingDirectory $webDirectory -ComposeService "web-package-manager" -CommandArgs @("ci")
    Invoke-NpmCommand -WorkingDirectory $webDirectory -ComposeService "web-package-manager" -CommandArgs @("run", "lint")
    Invoke-NpmCommand -WorkingDirectory $webDirectory -ComposeService "web-package-manager" -CommandArgs @("run", "typecheck")
    Invoke-NpmCommand -WorkingDirectory $webDirectory -ComposeService "web-package-manager" -CommandArgs @("run", "build")
}

if (Test-Scope -AllowedScopes @("Full", "Frontend", "Mobile")) {
    Invoke-NpmCommand -WorkingDirectory $mobileDirectory -ComposeService "mobile-package-manager" -CommandArgs @("ci")
    Invoke-NpmCommand -WorkingDirectory $mobileDirectory -ComposeService "mobile-package-manager" -CommandArgs @("run", "typecheck")
    Invoke-NpmCommand -WorkingDirectory $mobileDirectory -ComposeService "mobile-package-manager" -CommandArgs @("run", "build")
}

if (Test-Scope -AllowedScopes @("Full", "Backend")) {
    foreach ($coverageArtifactDirectory in @($testResultsDirectory, $backendCoverageReportDirectory)) {
        if (Test-Path -LiteralPath $coverageArtifactDirectory) {
            Remove-Item -LiteralPath $coverageArtifactDirectory -Recurse -Force
        }

        $null = New-Item -ItemType Directory -Force -Path $coverageArtifactDirectory
    }

    foreach ($project in $backendProjects) {
        Invoke-DotnetCommand -Arguments @("build", $project, "--configuration", "Release")
    }

    foreach ($testProject in $backendTestProjects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($testProject)
        $projectResultsDirectory = Join-Path $testResultsDirectory $projectName
        $null = New-Item -ItemType Directory -Force -Path $projectResultsDirectory
        $dotnetResultsDirectory = if ($hasHostDotnet) {
            $projectResultsDirectory
        }
        else {
            "/workspace/$((Resolve-Path -Relative $projectResultsDirectory).TrimStart('.').TrimStart('\').Replace('\', '/'))"
        }

        Invoke-DotnetCommand -Arguments @(
            "test",
            $testProject,
            "--configuration", "Release",
            "--collect:XPlat Code Coverage",
            "--results-directory", $dotnetResultsDirectory
        )
    }

    $coverageSummary = & (Join-Path $PSScriptRoot "Get-BackendCoverageSummary.ps1") `
        -ResultsDirectory $testResultsDirectory `
        -TemporaryThreshold $TemporaryCoverageThreshold `
        -TargetThreshold $TargetCoverage

    $coverageSummary | Out-File -FilePath (Join-Path $artifactRoot "backend-coverage-summary.json") -Encoding utf8

    & (Join-Path $PSScriptRoot "Publish-BackendCoverageReports.ps1") `
        -ResultsDirectory $testResultsDirectory `
        -OutputDirectory $backendCoverageReportDirectory | Out-Null
}

if (-not $SkipComposeSmoke -and $Scope -eq "Full") {
    $composeSmokeArguments = @{
        ArtifactDirectory = (Join-Path $ArtifactDirectory "compose")
        EnvironmentFilePath = $envFile
    }

    & (Join-Path $PSScriptRoot "Invoke-ComposeSmokeValidation.ps1") @composeSmokeArguments
}
elseif (-not $SkipComposeSmoke -and $Scope -ne "Full") {
    Write-Warning "Compose smoke skipped because -Scope $Scope only supports code-scope validation. Use -Scope Full for compose smoke."
}

Write-Output "Repository validation passed."

