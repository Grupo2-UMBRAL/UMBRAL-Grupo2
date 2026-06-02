[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z]+-\d+$')]
    [string]$IssueId,

    [string]$WorktreeParent = '..',

    [string]$RemoteName = 'origin',

    [string]$IntegrationBranch = 'develop'
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

$branchName = (Invoke-Git -Arguments @('-C', $worktreePath, 'rev-parse', '--abbrev-ref', 'HEAD') | Select-Object -First 1).Trim()
$statusLines = @(Invoke-Git -Arguments @('-C', $worktreePath, 'status', '--short'))
if ($statusLines.Count -gt 0) {
    throw "Worktree has uncommitted changes. Clean or commit before sync. $($statusLines -join '; ')"
}

$upstreamRef = '{0}/{1}' -f $RemoteName, $IntegrationBranch

if ($PSCmdlet.ShouldProcess($branchName, "Fetch $upstreamRef and rebase ticket branch")) {
    Invoke-Git -Arguments @('fetch', $RemoteName, $IntegrationBranch) | Out-Null
    Invoke-Git -Arguments @('-C', $worktreePath, 'rebase', $upstreamRef) | Out-Null
}

[pscustomobject]@{
    IssueId           = $normalizedIssueId
    BranchName        = $branchName
    WorktreePath      = $worktreePath
    IntegrationBranch = $IntegrationBranch
    UpstreamRef       = $upstreamRef
}
