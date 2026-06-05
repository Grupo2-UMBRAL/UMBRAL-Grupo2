[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z]+-\d+$')]
    [string]$IssueId,

    [Parameter(Mandatory = $true)]
    [string]$Message,

    [string]$Phase = 'worker',

    [string]$WorktreeParent = '..'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$normalizedIssueId = $IssueId.ToUpperInvariant()
$resolvedWorktreeParent = (Resolve-Path -LiteralPath $WorktreeParent).Path
$sessionFile = Join-Path -Path $resolvedWorktreeParent -ChildPath ("_runtime\{0}\session.json" -f $normalizedIssueId)

if (-not (Test-Path -LiteralPath $sessionFile)) {
    throw "Missing runtime session file: $sessionFile. Run initialize-ticket-runtime.ps1 first."
}

$session = Get-Content -LiteralPath $sessionFile -Raw | ConvertFrom-Json
$logLine = '[{0}] [{1}] {2}' -f ([DateTime]::UtcNow.ToString('o')), $Phase, $Message
Add-Content -LiteralPath $session.workerLogFile -Value $logLine

[pscustomobject]@{
    IssueId      = $normalizedIssueId
    WorkerLogFile = $session.workerLogFile
    Logged       = $true
}
