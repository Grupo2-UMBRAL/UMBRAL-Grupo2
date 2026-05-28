[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z]+-\d+$')]
    [string]$IssueId,

    [string]$WorktreeParent = '..',

    [switch]$DeleteBranch,

    [switch]$Force
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
$issueRuntimeRoot = Join-Path -Path (Join-Path -Path $resolvedWorktreeParent -ChildPath '_runtime') -ChildPath $normalizedIssueId

if (-not (Test-Path -LiteralPath $worktreePath)) {
    throw "Worktree path does not exist: $worktreePath"
}

$branchName = (Invoke-Git -Arguments @('-C', $worktreePath, 'rev-parse', '--abbrev-ref', 'HEAD') | Select-Object -First 1).Trim()

$removeArguments = @('worktree', 'remove')
if ($Force) {
    $removeArguments += '--force'
}
$removeArguments += $worktreePath

if ($PSCmdlet.ShouldProcess($worktreePath, "Remove worktree for $branchName")) {
    Invoke-Git -Arguments $removeArguments | Out-Null
}

if ($DeleteBranch) {
    $deleteArguments = @('branch')
    if ($Force) {
        $deleteArguments += '-D'
    }
    else {
        $deleteArguments += '-d'
    }
    $deleteArguments += $branchName

    if ($PSCmdlet.ShouldProcess($branchName, 'Delete local branch after worktree removal')) {
        Invoke-Git -Arguments $deleteArguments | Out-Null
    }
}

if (Test-Path -LiteralPath $issueRuntimeRoot) {
    if ($PSCmdlet.ShouldProcess($issueRuntimeRoot, 'Remove runtime metadata and logs for ticket')) {
        Remove-Item -LiteralPath $issueRuntimeRoot -Recurse -Force
    }
}

[pscustomobject]@{
    IssueId       = $normalizedIssueId
    BranchName    = $branchName
    WorktreePath  = $worktreePath
    BranchDeleted = [bool]$DeleteBranch
    RuntimePath   = $issueRuntimeRoot
}
