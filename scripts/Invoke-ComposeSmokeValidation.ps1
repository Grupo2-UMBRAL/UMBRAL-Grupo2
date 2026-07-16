#Requires -Version 7.0

param(
    [string]$ArtifactDirectory = (Join-Path (Join-Path $PSScriptRoot "..") "temp/validation/compose"),
    [string]$EnvironmentFilePath,
    [switch]$LeaveRunning
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifactRoot = if ([System.IO.Path]::IsPathRooted($ArtifactDirectory)) {
    [System.IO.Path]::GetFullPath($ArtifactDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ArtifactDirectory))
}
$null = New-Item -ItemType Directory -Force -Path $artifactRoot

$envFile = if ([string]::IsNullOrWhiteSpace($EnvironmentFilePath)) {
    $localEnv = Join-Path $repositoryRoot ".env"
    if (Test-Path $localEnv) {
        $localEnv
    } else {
        Join-Path $repositoryRoot ".env.example"
    }
}
elseif ([System.IO.Path]::IsPathRooted($EnvironmentFilePath)) {
    [System.IO.Path]::GetFullPath($EnvironmentFilePath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $EnvironmentFilePath))
}
$composeFiles = @("-f", "docker-compose.dev.yml")
$composeWithUtils = @("-f", "docker-compose.dev.yml", "-f", "docker-compose.utils.yml")

function Invoke-ComposeCommand {
    param(
        [string[]]$Arguments,
        [string[]]$Files = $composeFiles
    )

    Push-Location $repositoryRoot
    try {
        & docker compose --env-file $envFile @Files @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose failed: $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Get-ComposeEnvironment {
    param([string]$Path)

    $values = @{}
    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            continue
        }

        $parts = $line -split "=", 2
        if ($parts.Count -eq 2) {
            $values[$parts[0]] = $parts[1]
        }
    }

    return $values
}

function Wait-HttpOk {
    param(
        [string]$Uri,
        [string]$Name,
        [string]$ContainerName,
        [int]$TimeoutSeconds = 180
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempts = 0
    $lastOutcome = "no attempt completed"
    do {
        if (-not [string]::IsNullOrWhiteSpace($ContainerName)) {
            $containerState = (& docker inspect --format "{{.State.Status}}" $ContainerName 2>$null).Trim()
            if ($containerState -in @("exited", "dead")) {
                throw "$Name container entered '$containerState' state while waiting for HTTP 200: $ContainerName"
            }
        }

        $attempts++
        try {
            $statusCode = Get-HttpStatusCode -Uri $Uri -TimeoutSeconds 15
            if ($statusCode -eq 200) {
                return
            }

            $lastOutcome = "last response was HTTP $statusCode"
        }
        catch {
            $lastOutcome = "last probe threw: $($_.Exception.Message)"
        }

        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)

    throw "$Name did not return HTTP 200 within $TimeoutSeconds seconds: $Uri ($attempts attempts, $lastOutcome)"
}

function Get-HttpStatusCode {
    param(
        [string]$Uri,
        [int]$TimeoutSeconds = 15
    )

    # -SkipHttpErrorCheck returns the status instead of throwing on 4xx/5xx, so the caller can
    # tell "the app answered 503" apart from "nothing answered". -NoProxy keeps the loopback
    # probe off any proxy the runner exports via http_proxy/HTTP_PROXY.
    $response = Invoke-WebRequest `
        -Uri $Uri `
        -Method Get `
        -TimeoutSec $TimeoutSeconds `
        -MaximumRedirection 5 `
        -SkipHttpErrorCheck `
        -NoProxy `
        -ErrorAction Stop

    return [int]$response.StatusCode
}

function Wait-ContainerHealthy {
    param(
        [string]$ContainerName,
        [int]$TimeoutSeconds = 180
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $status = (& docker inspect --format "{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}" $ContainerName 2>$null).Trim()
        if ($status -eq "healthy" -or $status -eq "running") {
            return
        }

        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)

    throw "Container did not become healthy: $ContainerName"
}

function Assert-ContainerNamesAvailable {
    param([string[]]$ContainerNames)

    $existingContainers = (& docker ps -a --format "{{.Names}}" 2>$null) -split "\r?\n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $conflicts = $ContainerNames | Where-Object { $existingContainers -contains $_ }

    if ($conflicts.Count -gt 0) {
        throw "Compose smoke cannot start because these container names already exist: $($conflicts -join ', '). Stop or remove the existing stack first."
    }
}

function Get-ExcludedTcpPortRanges {
    if (-not $IsWindows) {
        return @()
    }

    $ranges = @()
    $lines = & netsh interface ipv4 show excludedportrange protocol=tcp 2>$null
    foreach ($line in $lines) {
        if ($line -match "^\s*(\d+)\s+(\d+)(?:\s+\*)?\s*$") {
            $ranges += [pscustomobject]@{
                Start = [int]$matches[1]
                End = [int]$matches[2]
            }
        }
    }

    return $ranges
}

function Test-TcpPortListening {
    param([int]$Port)

    $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    return [bool]($listeners | Where-Object { $_.Port -eq $Port } | Select-Object -First 1)
}

function Assert-HostPortsAvailable {
    param([int[]]$Ports)

    $excludedRanges = Get-ExcludedTcpPortRanges
    $conflicts = @()

    foreach ($port in ($Ports | Sort-Object -Unique)) {
        if (Test-TcpPortListening -Port $port) {
            $conflicts += "Port $port is already listening on the host."
            continue
        }

        $excludedRange = $excludedRanges | Where-Object { $port -ge $_.Start -and $port -le $_.End } | Select-Object -First 1
        if ($null -ne $excludedRange) {
            $conflicts += "Port $port is inside the Windows excluded TCP range $($excludedRange.Start)-$($excludedRange.End)."
        }
    }

    if ($conflicts.Count -gt 0) {
        throw "Compose smoke cannot bind the required host ports. $($conflicts -join ' ')"
    }
}

$environmentValues = Get-ComposeEnvironment -Path $envFile
$edgeProxyPort = $environmentValues["EDGE_PROXY_PORT"]
$userManagementPort = $environmentValues["USER_MANAGEMENT_PORT"]
$missionManagementPort = $environmentValues["MISSION_MANAGEMENT_PORT"]
$SessionManagementPort = $environmentValues["SESSION_OPERATIONS_PORT"]
$scoringAuditPort = $environmentValues["SCORING_MONITORING_PORT"]
$realm = $environmentValues["KEYCLOAK_REALM"]

$composeConfigLog = Join-Path $artifactRoot "compose-config.txt"
$composeUpLog = Join-Path $artifactRoot "compose-up.txt"
$composePsLog = Join-Path $artifactRoot "compose-ps.txt"
$composeLogsLog = Join-Path $artifactRoot "compose-logs.txt"
$authSmokeLog = Join-Path $artifactRoot "auth-smoke-tests.txt"
$reservedContainerNames = @(
    "umbral-postgres",
    "umbral-rabbitmq",
    "umbral-keycloak",
    "umbral-mission-management-service",
    "umbral-session-management-service",
    "umbral-scoring-monitoring-service",
    "umbral-user-management-service",
    "umbral-edge-proxy"
)
$requiredHostPorts = @(
    [int]$environmentValues["POSTGRES_PORT"],
    [int]$environmentValues["RABBITMQ_AMQP_PORT"],
    [int]$environmentValues["RABBITMQ_MANAGEMENT_PORT"],
    [int]$environmentValues["KEYCLOAK_PORT"],
    [int]$edgeProxyPort,
    [int]$userManagementPort,
    [int]$missionManagementPort,
    [int]$SessionManagementPort,
    [int]$scoringAuditPort
)

try {
    Assert-ContainerNamesAvailable -ContainerNames $reservedContainerNames
    Assert-HostPortsAvailable -Ports $requiredHostPorts
    Invoke-ComposeCommand -Arguments @("config", "--no-interpolate") | Out-File -FilePath $composeConfigLog -Encoding utf8
    Invoke-ComposeCommand -Arguments @("up", "-d", "--build", "postgres", "rabbitmq", "keycloak", "mission-management-service", "session-management-service", "scoring-monitoring-service", "user-management-service", "edge-proxy") | Out-File -FilePath $composeUpLog -Encoding utf8
    Invoke-ComposeCommand -Arguments @("ps") | Out-File -FilePath $composePsLog -Encoding utf8

    Wait-ContainerHealthy -ContainerName "umbral-postgres"
    Wait-ContainerHealthy -ContainerName "umbral-rabbitmq"
    Wait-ContainerHealthy -ContainerName "umbral-keycloak"
    Wait-ContainerHealthy -ContainerName "umbral-mission-management-service"
    Wait-ContainerHealthy -ContainerName "umbral-session-management-service"
    Wait-ContainerHealthy -ContainerName "umbral-scoring-monitoring-service"
    Wait-ContainerHealthy -ContainerName "umbral-user-management-service"
    Wait-ContainerHealthy -ContainerName "umbral-edge-proxy"

    Wait-HttpOk -Uri "http://127.0.0.1:$edgeProxyPort/health" -Name "edge-proxy health" -ContainerName "umbral-edge-proxy"
    Wait-HttpOk -Uri "http://127.0.0.1:$userManagementPort/health" -Name "user-management health" -ContainerName "umbral-user-management-service"
    Wait-HttpOk -Uri "http://127.0.0.1:$missionManagementPort/health" -Name "mission-management health" -ContainerName "umbral-mission-management-service"
    Wait-HttpOk -Uri "http://127.0.0.1:$SessionManagementPort/health" -Name "session-management health" -ContainerName "umbral-session-management-service"
    Wait-HttpOk -Uri "http://127.0.0.1:$scoringAuditPort/health" -Name "scoring-monitoring health" -ContainerName "umbral-scoring-monitoring-service"
    Wait-HttpOk -Uri "http://127.0.0.1:$edgeProxyPort/auth/realms/$realm/.well-known/openid-configuration" -Name "Keycloak discovery" -ContainerName "umbral-keycloak"

    Invoke-ComposeCommand -Files $composeWithUtils -Arguments @("run", "--rm", "auth-smoke-tests") | Out-File -FilePath $authSmokeLog -Encoding utf8

    Write-Output "Compose smoke validation passed."
}
catch {
    try {
        Invoke-ComposeCommand -Arguments @("ps") | Out-File -FilePath $composePsLog -Encoding utf8
    }
    catch {
    }

    try {
        Invoke-ComposeCommand -Arguments @("logs", "--no-color") | Out-File -FilePath $composeLogsLog -Encoding utf8
    }
    catch {
    }

    throw
}
finally {
    if (-not $LeaveRunning) {
        try {
            Invoke-ComposeCommand -Arguments @("down", "--volumes", "--remove-orphans") | Out-Null
        }
        catch {
        }
    }
}

