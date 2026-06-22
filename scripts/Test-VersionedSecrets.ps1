param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$allowedPaths = @(
    ".env.example",
    "docs/architecture/local-infrastructure.md",
    "infra/keycloak/import/umbral-realm.json",
    "infra/keycloak/verify/README.md",
    "src/apps/mobile/README.md",
    "src/apps/web/README.md"
    )

$quotedLiteralPattern = '(?im)^\s*["'']?(?:[A-Za-z0-9_.-]*?(?:password|secret|api[_-]?key)|client[_-]?secret)\b["'']?\s*[:=]\s*["''](?!\$)(?!https?://)[^"'']{8,}["'']'
$envLiteralPattern = '(?im)^\s*(?:export\s+)?(?:[A-Z0-9_]*?(?:PASSWORD|SECRET|API_KEY)|CLIENT_SECRET)\s*=\s*(?!\$)(?!https?://)[^\s#]{8,}'
$jwtLiteralPattern = '(?m)\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b'
$privateKeyPattern = '(?m)-----BEGIN [A-Z ]*PRIVATE KEY-----'

Push-Location $RepositoryRoot
try {
    $trackedFiles = git ls-files
}
finally {
    Pop-Location
}

$findings = New-Object System.Collections.Generic.List[object]

foreach ($trackedFile in $trackedFiles) {
    if ($allowedPaths -contains $trackedFile) {
        continue
    }

    $fullPath = Join-Path $RepositoryRoot $trackedFile

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        continue
    }

    if ($trackedFile -match '\.(png|jpg|jpeg|gif|ico|pdf|pfx|pem|key|woff2?)$') {
        continue
    }

    $content = Get-Content -LiteralPath $fullPath -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) {
        continue
    }

    $assignmentMatches = [regex]::Matches($content, $quotedLiteralPattern)
    $envLiteralMatches = [regex]::Matches($content, $envLiteralPattern)
    $jwtLiteralMatches = [regex]::Matches($content, $jwtLiteralPattern)
    $privateKeyMatches = [regex]::Matches($content, $privateKeyPattern)

    $allMatches = @($assignmentMatches) + @($envLiteralMatches) + @($jwtLiteralMatches) + @($privateKeyMatches)

    foreach ($match in $allMatches) {
        $lineNumber = ($content.Substring(0, $match.Index) -split "\r?\n").Count
        $findings.Add([pscustomobject]@{
            Path = $trackedFile
            Line = $lineNumber
            Match = $match.Value.Trim()
        })
    }
}

if ($findings.Count -gt 0) {
    $findings | Format-Table -AutoSize | Out-String | Write-Error
    throw "Potential versioned secrets found."
}

Write-Output "No unexpected versioned secrets detected."

