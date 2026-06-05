[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)]
    [string]$BranchName,

    [Parameter(Mandatory=$true)]
    [string]$CommitMessage,

    [Parameter(Mandatory=$false)]
    [string]$BaseBranch = "develop"
)

function Test-GitCommand {
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Git command failed. Aborting."
        exit $LASTEXITCODE
    }
}

# 1. Check if git repository
git rev-parse --is-inside-work-tree > $null 2>&1
Test-GitCommand

# 2. Check if there are changes
$status = git status --porcelain
if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host "No changes to commit. Aborting."
    exit 0
}

Write-Host "Starting git-no-ff-merge flow..."
Write-Host "Creating branch: $BranchName"
git checkout -b $BranchName
Test-GitCommand

Write-Host "Staging changes..."
git add -A
Test-GitCommand

Write-Host "Committing changes..."
git commit -m $CommitMessage
Test-GitCommand

Write-Host "Switching back to base branch: $BaseBranch"
git checkout $BaseBranch
Test-GitCommand

Write-Host "Merging branch $BranchName into $BaseBranch with --no-ff..."
git merge --no-ff $BranchName -m "Merge branch '$BranchName' into $BaseBranch"
Test-GitCommand

Write-Host "Flow completed successfully!"
Write-Host "Recent graph log:"
git log -n 5 --oneline --graph
