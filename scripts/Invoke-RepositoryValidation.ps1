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
$null = New-Item -ItemType Directory -Force -Path $artifactRoot, $testResultsDirectory
$hasHostDotnet = $null -ne (Get-Command dotnet -ErrorAction SilentlyContinue)

$webDirectory = Join-Path $repositoryRoot "src/apps/web"
$mobileDirectory = Join-Path $repositoryRoot "src/apps/mobile"

$backendProjects = @(
    "src/services/building-blocks/Umbral.ServiceDefaults.Tests/Umbral.ServiceDefaults.Tests.csproj",
    "src/services/identity-access/Umbral.IdentityAccess.Api/Umbral.IdentityAccess.Api.csproj",
    "src/services/identity-access/Umbral.IdentityAccess.Api.Tests/Umbral.IdentityAccess.Api.Tests.csproj",
    "src/services/mission-design/Umbral.MissionDesign.Api/Umbral.MissionDesign.Api.csproj",
    "src/services/mission-design/Umbral.MissionDesign.Api.Tests/Umbral.MissionDesign.Api.Tests.csproj",
    "src/services/scoring-audit/Umbral.ScoringAudit.Api/Umbral.ScoringAudit.Api.csproj",
    "src/services/scoring-audit/Umbral.ScoringAudit.Api.Tests/Umbral.ScoringAudit.Api.Tests.csproj",
    "src/services/session-operations/Umbral.SessionOperations.Api/Umbral.SessionOperations.Api.csproj",
    "src/services/session-operations/Umbral.SessionOperations.Api.Tests/Umbral.SessionOperations.Api.Tests.csproj"
)

$backendTestProjects = @(
    "src/services/building-blocks/Umbral.ServiceDefaults.Tests/Umbral.ServiceDefaults.Tests.csproj",
    "src/services/identity-access/Umbral.IdentityAccess.Api.Tests/Umbral.IdentityAccess.Api.Tests.csproj",
    "src/services/mission-design/Umbral.MissionDesign.Api.Tests/Umbral.MissionDesign.Api.Tests.csproj",
    "src/services/scoring-audit/Umbral.ScoringAudit.Api.Tests/Umbral.ScoringAudit.Api.Tests.csproj",
    "src/services/session-operations/Umbral.SessionOperations.Api.Tests/Umbral.SessionOperations.Api.Tests.csproj"
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

    $npmCommand = Get-Command npm -ErrorAction SilentlyContinue

    if ($null -eq $npmCommand -and $IsWindows) {
        $npmCommand = Get-Command npm.cmd -ErrorAction SilentlyContinue
    }

    if ($null -ne $npmCommand) {
        Push-Location $WorkingDirectory
        try {
            & $npmCommand.Source @CommandArgs
        }
        finally {
            Pop-Location
        }
        return
    }

    Push-Location $repositoryRoot
    try {
        & docker compose --env-file ".env.example" -f "docker-compose.dev.yml" -f "docker-compose.utils.yml" run --rm $ComposeService @CommandArgs
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
        & docker compose --env-file ".env.example" -f "docker-compose.dev.yml" -f "docker-compose.utils.yml" run --rm --entrypoint dotnet dotnet-sdk @Arguments
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
}

if (-not $SkipComposeSmoke -and $Scope -eq "Full") {
    $composeSmokeArguments = @{
        ArtifactDirectory = (Join-Path $ArtifactDirectory "compose")
    }

    if (-not [string]::IsNullOrWhiteSpace($EnvironmentFilePath)) {
        $composeSmokeArguments.EnvironmentFilePath = $EnvironmentFilePath
    }

    & (Join-Path $PSScriptRoot "Invoke-ComposeSmokeValidation.ps1") @composeSmokeArguments
}
elseif (-not $SkipComposeSmoke -and $Scope -ne "Full") {
    Write-Warning "Compose smoke skipped because -Scope $Scope only supports code-scope validation. Use -Scope Full for compose smoke."
}

Write-Output "Repository validation passed."
