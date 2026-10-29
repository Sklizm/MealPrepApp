#!/usr/bin/env bash
# Run after main is pushed and its Compatibility CI succeeds.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == main ]] || { echo 'Switch to main first.' >&2; exit 1; }
[[ -z "$(git status --porcelain)" ]] || { echo 'Commit or save local changes first.' >&2; exit 1; }
command -v gh >/dev/null || { echo 'Install and authenticate GitHub CLI first.' >&2; exit 1; }
git fetch --prune origin '+refs/heads/*:refs/remotes/origin/*'
main_sha="$(git rev-parse HEAD)"
[[ "$main_sha" == "$(git rev-parse origin/main)" ]] || { echo 'Local and remote main differ; integrate and verify before cleanup.' >&2; exit 1; }
# Audit everything before deleting anything.
while read -r ref; do
    [[ "$ref" == refs/remotes/origin/HEAD || "$ref" == refs/remotes/origin/main ]] && continue
    git merge-base --is-ancestor "$ref" "$main_sha" || { echo "Unmerged remote branch: $ref" >&2; exit 1; }
done < <(git for-each-ref --format='%(refname)' refs/remotes/origin/)
while read -r branch; do
    git merge-base --is-ancestor "$branch" "$main_sha" || { echo "Unmerged local branch: $branch" >&2; exit 1; }
done < <(git for-each-ref --format='%(refname:short)' refs/heads/)
verified_sha="$(gh run list --workflow compatibility.yml --branch main --commit "$main_sha" --status success --limit 1 --json headSha --jq '.[0].headSha // empty')"
[[ "$verified_sha" == "$main_sha" ]] || { echo 'Successful Compatibility CI is required for this exact main commit.' >&2; exit 1; }
gh repo edit --default-branch main
while read -r ref branch_sha; do
    [[ "$ref" == refs/remotes/origin/HEAD || "$ref" == refs/remotes/origin/main ]] && continue
    branch="${ref#refs/remotes/origin/}"
    git push --force-with-lease="refs/heads/$branch:$branch_sha" origin ":refs/heads/$branch"
done < <(git for-each-ref --format='%(refname) %(objectname)' refs/remotes/origin/)
while read -r branch; do
    [[ "$branch" == main ]] || git branch -d "$branch"
done < <(git for-each-ref --format='%(refname:short)' refs/heads/)
git remote set-head origin main
echo 'main is the only maintained branch. Commit history and tags are preserved.'
