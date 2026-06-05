[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z]+-\d+$')]
    [string]$IssueId,

    [string]$WorktreeParent = '..'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $result = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        $joinedArguments = $Arguments -join ' '
        $message = ($result | Out-String).Trim()
        throw "git $joinedArguments failed. $message"
    }

    return $result
}

$normalizedIssueId = $IssueId.ToUpperInvariant()
$resolvedWorktreeParent = (Resolve-Path -LiteralPath $WorktreeParent).Path
$sessionFile = Join-Path -Path $resolvedWorktreeParent -ChildPath ("_runtime\{0}\session.json" -f $normalizedIssueId)

if (-not (Test-Path -LiteralPath $sessionFile)) {
    throw "Missing runtime session file: $sessionFile. Run initialize-ticket-runtime.ps1 first."
}

$session = Get-Content -LiteralPath $sessionFile -Raw | ConvertFrom-Json
$currentRepositoryRoot = (Invoke-Git -Arguments @('rev-parse', '--show-toplevel') | Select-Object -First 1).Trim()

$resolvedCurrentRepositoryRoot = (Resolve-Path -LiteralPath $currentRepositoryRoot).Path
$expectedWorktreePath = (Resolve-Path -LiteralPath $session.worktreePath).Path

if ($resolvedCurrentRepositoryRoot -ne $expectedWorktreePath) {
    throw "Wrong worktree. Expected '$expectedWorktreePath' but current root is '$resolvedCurrentRepositoryRoot'."
}

$currentBranchName = (Invoke-Git -Arguments @('rev-parse', '--abbrev-ref', 'HEAD') | Select-Object -First 1).Trim()

if ($currentBranchName -ne $session.branchName) {
    throw "Wrong branch. Expected '$($session.branchName)' but current branch is '$currentBranchName'."
}

[pscustomobject]@{
    IssueId      = $normalizedIssueId
    BranchName   = $currentBranchName
    WorktreePath = $expectedWorktreePath
    SessionFile  = $sessionFile
    Status       = 'ok'
}
