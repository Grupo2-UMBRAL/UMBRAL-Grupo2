[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z]+-\d+$')]
    [string]$IssueId,

    [string]$WorktreeParent = '..',

    [switch]$Handoff
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
$targetFile = if ($Handoff) { $session.handoffFile } else { $session.workerLogFile }

if (-not (Test-Path -LiteralPath $targetFile)) {
    throw "Watch target does not exist: $targetFile"
}

Get-Content -LiteralPath $targetFile -Wait
