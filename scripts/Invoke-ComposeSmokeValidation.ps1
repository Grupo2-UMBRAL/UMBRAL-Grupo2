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
    do {
        if (-not [string]::IsNullOrWhiteSpace($ContainerName)) {
            $containerState = (& docker inspect --format "{{.State.Status}}" $ContainerName 2>$null).Trim()
            if ($containerState -in @("exited", "dead")) {
                throw "$Name container entered '$containerState' state while waiting for HTTP 200: $ContainerName"
            }
        }

        try {
            if ((Get-HttpStatusCode -Uri $Uri -TimeoutSeconds 15) -eq 200) {
                return
            }
        }
        catch {
        }

        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)

    throw "$Name did not return HTTP 200 within $TimeoutSeconds seconds: $Uri"
}

function Get-HttpStatusCode {
    param(
        [string]$Uri,
        [int]$TimeoutSeconds = 15
    )

    $curlCommand = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($null -eq $curlCommand) {
        $curlCommand = Get-Command curl -CommandType Application -ErrorAction SilentlyContinue
    }

    if ($null -ne $curlCommand) {
        $temporaryOutputFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.IO.Path]::GetRandomFileName())
        try {
            $statusCode = & $curlCommand.Source `
                --silent `
                --show-error `
                --location `
                --max-time $TimeoutSeconds `
                --output $temporaryOutputFile `
                --write-out "%{http_code}" `
                $Uri

            if ($LASTEXITCODE -ne 0) {
                throw "curl failed for $Uri with exit code $LASTEXITCODE"
            }

            return [int]$statusCode
        }
        finally {
            Remove-Item -LiteralPath $temporaryOutputFile -ErrorAction SilentlyContinue
        }
    }

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)

    try {
        $response = $client.GetAsync($Uri).GetAwaiter().GetResult()
        return [int]$response.StatusCode
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
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

    Wait-HttpOk -Uri "http://localhost:$edgeProxyPort/health" -Name "edge-proxy health" -ContainerName "umbral-edge-proxy"
    Wait-HttpOk -Uri "http://localhost:$userManagementPort/health" -Name "user-management health" -ContainerName "umbral-user-management-service"
    Wait-HttpOk -Uri "http://localhost:$missionManagementPort/health" -Name "mission-management health" -ContainerName "umbral-mission-management-service"
    Wait-HttpOk -Uri "http://localhost:$SessionManagementPort/health" -Name "session-management health" -ContainerName "umbral-session-management-service"
    Wait-HttpOk -Uri "http://localhost:$scoringAuditPort/health" -Name "scoring-monitoring health" -ContainerName "umbral-scoring-monitoring-service"
    Wait-HttpOk -Uri "http://localhost:$edgeProxyPort/auth/realms/$realm/.well-known/openid-configuration" -Name "Keycloak discovery" -ContainerName "umbral-keycloak"

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

