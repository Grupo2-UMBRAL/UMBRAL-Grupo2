[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z]+-\d+$')]
    [string]$IssueId,

    [Parameter(Mandatory = $true)]
    [ValidateSet('feature', 'fix', 'chore')]
    [string]$BranchType,

    [Parameter(Mandatory = $true)]
    [string]$Slug,

    [string]$BaseRef = 'main',

    [string]$WorktreeParent = '..'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RuntimePaths {
    param(
        [Parameter(Mandatory = $true)]
        [string]$IssueId,

        [Parameter(Mandatory = $true)]
        [string]$ResolvedWorktreeParent
    )

    $runtimeRoot = Join-Path -Path $ResolvedWorktreeParent -ChildPath '_runtime'
    $issueRuntimeRoot = Join-Path -Path $runtimeRoot -ChildPath $IssueId

    return @{
        RuntimeRoot       = $runtimeRoot
        IssueRuntimeRoot  = $issueRuntimeRoot
        SessionFile       = Join-Path -Path $issueRuntimeRoot -ChildPath 'session.json'
        WorkerLogFile     = Join-Path -Path $issueRuntimeRoot -ChildPath 'worker.log'
        HandoffFile       = Join-Path -Path $issueRuntimeRoot -ChildPath 'handoff.md'
    }
}

function Normalize-Slug {
    param([string]$Value)

    $normalized = $Value.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $normalized = $normalized.Trim('-')

    if ([string]::IsNullOrWhiteSpace($normalized)) {
        throw 'Slug must contain at least one alphanumeric character.'
    }

    return $normalized
}

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

$repositoryRoot = (Invoke-Git -Arguments @('rev-parse', '--show-toplevel') | Select-Object -First 1).Trim()
$resolvedRepositoryRoot = (Resolve-Path -LiteralPath $repositoryRoot).Path
$resolvedWorktreeParent = (Resolve-Path -LiteralPath $WorktreeParent).Path

$normalizedIssueId = $IssueId.ToUpperInvariant()
$normalizedSlug = Normalize-Slug -Value $Slug
$branchName = '{0}/{1}-{2}' -f $BranchType, $normalizedIssueId, $normalizedSlug
$worktreePath = Join-Path -Path $resolvedWorktreeParent -ChildPath ('wt-{0}' -f $normalizedIssueId)
$runtimePaths = Get-RuntimePaths -IssueId $normalizedIssueId -ResolvedWorktreeParent $resolvedWorktreeParent

if (Test-Path -LiteralPath $worktreePath) {
    throw "Worktree path already exists: $worktreePath"
}

$baseRefExists = (& git rev-parse --verify $BaseRef 2>$null)
if ($LASTEXITCODE -ne 0) {
    throw "Base ref does not exist: $BaseRef"
}

$existingBranch = (& git branch --list $branchName | Out-String).Trim()
if ($existingBranch) {
    throw "Branch already exists: $branchName"
}

if ($PSCmdlet.ShouldProcess($worktreePath, "Create worktree for $branchName from $BaseRef")) {
    Invoke-Git -Arguments @('worktree', 'add', $worktreePath, '-b', $branchName, $BaseRef) | Out-Null

    $null = New-Item -ItemType Directory -Path $runtimePaths.RuntimeRoot -Force
    $null = New-Item -ItemType Directory -Path $runtimePaths.IssueRuntimeRoot -Force

    $session = [ordered]@{
        issueId        = $normalizedIssueId
        branchName     = $branchName
        baseRef        = $BaseRef
        worktreePath   = $worktreePath
        repositoryRoot = $resolvedRepositoryRoot
        runtimeRoot    = $runtimePaths.IssueRuntimeRoot
        workerLogFile  = $runtimePaths.WorkerLogFile
        handoffFile    = $runtimePaths.HandoffFile
        createdAtUtc   = [DateTime]::UtcNow.ToString('o')
    }

    $session | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $runtimePaths.SessionFile

    if (-not (Test-Path -LiteralPath $runtimePaths.WorkerLogFile)) {
        @(
            ('[{0}] runtime initialized for {1}' -f ([DateTime]::UtcNow.ToString('o')), $normalizedIssueId)
            ('branch={0}' -f $branchName)
            ('worktree={0}' -f $worktreePath)
        ) | Set-Content -LiteralPath $runtimePaths.WorkerLogFile
    }

    if (-not (Test-Path -LiteralPath $runtimePaths.HandoffFile)) {
        @(
            ('# Handoff {0}' -f $normalizedIssueId)
            ''
            '## Summary'
            ''
            '- pending'
            ''
            '## Files touched'
            ''
            '- pending'
            ''
            '## Tests'
            ''
            '- pending'
            ''
            '## Risks'
            ''
            '- pending'
            ''
            '## Next action'
            ''
            '- pending'
        ) | Set-Content -LiteralPath $runtimePaths.HandoffFile
    }
}

[pscustomobject]@{
    IssueId        = $normalizedIssueId
    BranchName     = $branchName
    BaseRef        = $BaseRef
    WorktreePath   = $worktreePath
    RepositoryRoot = $resolvedRepositoryRoot
    SessionFile    = $runtimePaths.SessionFile
    WorkerLogFile  = $runtimePaths.WorkerLogFile
    HandoffFile    = $runtimePaths.HandoffFile
}
