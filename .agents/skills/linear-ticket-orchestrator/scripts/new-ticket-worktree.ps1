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
}

[pscustomobject]@{
    IssueId        = $normalizedIssueId
    BranchName     = $branchName
    BaseRef        = $BaseRef
    WorktreePath   = $worktreePath
    RepositoryRoot = $resolvedRepositoryRoot
}
