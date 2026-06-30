param(
    [string]$ArtifactDirectory = "temp/validation",
    [string]$EnvironmentFilePath,
    [switch]$SkipComposeSmoke,
    [ValidateSet("Full", "Frontend", "Web", "Mobile", "Backend", "BackendUnit", "BackendIntegration")]
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
    "src/shared/Umbral.ServiceDefaults.UnitTests/Umbral.ServiceDefaults.UnitTests.csproj",
    "src/services/user-management/UserManagement.Api/UserManagement.Api.csproj",
    "src/services/user-management/tests/UserManagement.UnitTests/UserManagement.UnitTests.csproj",
    "src/services/user-management/tests/UserManagement.IntegrationTests/UserManagement.IntegrationTests.csproj",
    "src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj",
    "src/services/mission-management/tests/MissionManagement.UnitTests/MissionManagement.UnitTests.csproj",
    "src/services/mission-management/tests/MissionManagement.IntegrationTests/MissionManagement.IntegrationTests.csproj",
    "src/services/scoring-monitoring/ScoringMonitoring.Api/ScoringMonitoring.Api.csproj",
    "src/services/scoring-monitoring/tests/ScoringMonitoring.UnitTests/ScoringMonitoring.UnitTests.csproj",
    "src/services/scoring-monitoring/tests/ScoringMonitoring.IntegrationTests/ScoringMonitoring.IntegrationTests.csproj",
    "src/services/session-management/SessionManagement.Api/SessionManagement.Api.csproj",
    "src/services/session-management/tests/SessionManagement.UnitTests/SessionManagement.UnitTests.csproj",
    "src/services/session-management/tests/SessionManagement.IntegrationTests/SessionManagement.IntegrationTests.csproj"
)

$backendUnitTestProjects = @(
    "src/shared/Umbral.ServiceDefaults.UnitTests/Umbral.ServiceDefaults.UnitTests.csproj",
    "src/services/user-management/tests/UserManagement.UnitTests/UserManagement.UnitTests.csproj",
    "src/services/mission-management/tests/MissionManagement.UnitTests/MissionManagement.UnitTests.csproj",
    "src/services/scoring-monitoring/tests/ScoringMonitoring.UnitTests/ScoringMonitoring.UnitTests.csproj",
    "src/services/session-management/tests/SessionManagement.UnitTests/SessionManagement.UnitTests.csproj"
)

# Integration lane: WebApplicationFactory + (for Postgres-backed cases) a Docker engine.
$backendIntegrationTestProjects = @(
    "src/services/user-management/tests/UserManagement.IntegrationTests/UserManagement.IntegrationTests.csproj",
    "src/services/mission-management/tests/MissionManagement.IntegrationTests/MissionManagement.IntegrationTests.csproj",
    "src/services/scoring-monitoring/tests/ScoringMonitoring.IntegrationTests/ScoringMonitoring.IntegrationTests.csproj",
    "src/services/session-management/tests/SessionManagement.IntegrationTests/SessionManagement.IntegrationTests.csproj"
)

# Unit lane first (fast, no Docker) so it fails before the slower integration lane runs.
$backendTestProjects = $backendUnitTestProjects + $backendIntegrationTestProjects

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

    # Native tools (npm/vite/rolldown) write benign warnings to stderr. Under the
    # script-level $ErrorActionPreference = "Stop", Windows PowerShell 5.1 turns those
    # stderr lines into terminating errors before the exit-code check runs, failing the
    # gate even when the command succeeded. Judge success by exit code only.
    $ErrorActionPreference = 'Continue'

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
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
            }
        }
        finally {
            Pop-Location
        }
        return
    }

    Push-Location $repositoryRoot
    try {
        & docker compose --env-file "$envFile" -f "docker-compose.dev.yml" -f "docker-compose.utils.yml" run --rm --entrypoint dotnet dotnet-sdk @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose run dotnet-sdk $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
        }
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

if (Test-Scope -AllowedScopes @("Full", "Backend", "BackendUnit", "BackendIntegration")) {
    foreach ($coverageArtifactDirectory in @($testResultsDirectory, $backendCoverageReportDirectory)) {
        if (Test-Path -LiteralPath $coverageArtifactDirectory) {
            Remove-Item -LiteralPath $coverageArtifactDirectory -Recurse -Force
        }

        $null = New-Item -ItemType Directory -Force -Path $coverageArtifactDirectory
    }

    $selectedBuildProjects = if ($Scope -eq "BackendUnit") { $backendUnitTestProjects } else { $backendProjects }
    $selectedTestProjects = switch ($Scope) {
        "BackendUnit" { $backendUnitTestProjects }
        "BackendIntegration" { $backendIntegrationTestProjects }
        default { $backendTestProjects }
    }

    foreach ($project in $selectedBuildProjects) {
        Invoke-DotnetCommand -Arguments @("build", $project, "--configuration", "Release")
    }

    foreach ($testProject in $selectedTestProjects) {
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

