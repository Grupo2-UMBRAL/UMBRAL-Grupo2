[CmdletBinding(SupportsShouldProcess = $true)]
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
$worktreePath = Join-Path -Path $resolvedWorktreeParent -ChildPath ('wt-{0}' -f $normalizedIssueId)

if (-not (Test-Path -LiteralPath $worktreePath)) {
    throw "Worktree path does not exist: $worktreePath"
}

$repositoryRoot = (Invoke-Git -Arguments @('rev-parse', '--show-toplevel') | Select-Object -First 1).Trim()
$resolvedRepositoryRoot = (Resolve-Path -LiteralPath $repositoryRoot).Path
$branchName = (Invoke-Git -Arguments @('-C', $worktreePath, 'rev-parse', '--abbrev-ref', 'HEAD') | Select-Object -First 1).Trim()

$runtimeRoot = Join-Path -Path $resolvedWorktreeParent -ChildPath '_runtime'
$issueRuntimeRoot = Join-Path -Path $runtimeRoot -ChildPath $normalizedIssueId
$sessionFile = Join-Path -Path $issueRuntimeRoot -ChildPath 'session.json'
$workerLogFile = Join-Path -Path $issueRuntimeRoot -ChildPath 'worker.log'
$handoffFile = Join-Path -Path $issueRuntimeRoot -ChildPath 'handoff.md'

if ($PSCmdlet.ShouldProcess($issueRuntimeRoot, "Initialize runtime metadata for $normalizedIssueId")) {
    $null = New-Item -ItemType Directory -Path $runtimeRoot -Force
    $null = New-Item -ItemType Directory -Path $issueRuntimeRoot -Force

    $session = [ordered]@{
        issueId        = $normalizedIssueId
        branchName     = $branchName
        worktreePath   = $worktreePath
        repositoryRoot = $resolvedRepositoryRoot
        runtimeRoot    = $issueRuntimeRoot
        workerLogFile  = $workerLogFile
        handoffFile    = $handoffFile
        createdAtUtc   = [DateTime]::UtcNow.ToString('o')
    }

    $session | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $sessionFile

    if (-not (Test-Path -LiteralPath $workerLogFile)) {
        @(
            ('[{0}] runtime initialized for {1}' -f ([DateTime]::UtcNow.ToString('o')), $normalizedIssueId)
            ('branch={0}' -f $branchName)
            ('worktree={0}' -f $worktreePath)
        ) | Set-Content -LiteralPath $workerLogFile
    }

    if (-not (Test-Path -LiteralPath $handoffFile)) {
        @(
            ('# Handoff {0}' -f $normalizedIssueId)
            ''
            '## Summary'
            ''
            '- pending'
        ) | Set-Content -LiteralPath $handoffFile
    }
}

[pscustomobject]@{
    IssueId       = $normalizedIssueId
    BranchName    = $branchName
    WorktreePath  = $worktreePath
    SessionFile   = $sessionFile
    WorkerLogFile = $workerLogFile
    HandoffFile   = $handoffFile
}
