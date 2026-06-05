---
name: git-no-ff-merge
description: Enforce a non-fast-forward merge git workflow by creating a fix/feature branch, staging and committing changes with Conventional Commits, and merging back using the --no-ff flag to preserve branch history in the git graph. Use when the user requests staging/committing changes to a separate branch and merging back to the main/develop branch, or when they mention non-fast-forward, no-ff merge, or preserving the git graph.
---

# Git Non-Fast-Forward Merge

Automate committing local changes onto a branch and merging back to the base branch using a non-fast-forward merge to preserve the branch structure in the git graph.

## Quick start

Run the helper script directly:
```powershell
powershell -ExecutionPolicy Bypass -File .agents/skills/git-no-ff-merge/scripts/git-no-ff-merge.ps1 -BranchName "fix/some-issue" -CommitMessage "fix(web): prevent crash" -BaseBranch "develop"
```

## Workflows

### Committing and Merging preserves Git Graph
When a user asks to commit their changes onto a separate branch and merge them back to `develop` or `main`:

1. Ensure the working tree has modified files (`git status`).
2. Run the helper script with the target branch name, commit message (following Conventional Commits), and base branch.
3. Review the printed git graph to verify the merge commit is present:
   `git log -n 5 --graph --oneline`
