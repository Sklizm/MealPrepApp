# Release candidates

Release metadata is in `release.json`. Desktop assembly defaults are in `Version.props`; a regression check prevents version drift. macOS uses numeric bundle versions plus the complete candidate version in `MealPrepReleaseVersion`; Settings displays the assembly's candidate version.

## Prepare a draft

1. Push the final source commit to `feature/cross-platform-avalonia` and inspect Compatibility CI. Do not merge automatically.
2. Run **Draft release candidate** manually, selecting that branch. The workflow uses the dispatch commit SHA for both its reusable verification workflow and release tagging. It reruns every required check and only then creates `v1.0.0-rc.1` and a draft prerelease with archives, checksums and provenance metadata. It never publishes it. An existing tag aborts the run rather than overwriting a candidate.
3. GitHub requires a dispatch workflow definition on the default branch before it can be manually triggered. If it is not registered yet, submit a reviewed PR to register the workflows and their required scripts/metadata on the default branch, then dispatch using the feature branch. This implementation does not merge that PR automatically.
4. If tag creation succeeds but release upload fails, inspect the run and the tag's SHA. Remove only the failed candidate tag/draft after verifying no published release uses it, then retry. The workflow deliberately refuses automatic replacement.

The shared workflow follows [GitHub's reusable workflow model](https://docs.github.com/en/actions/how-tos/reuse-automations/reuse-workflows). Tests and SQL logs are retained as Actions artifacts; package artifacts are available from ordinary CI runs too.

## Build the same files locally

From a clean checkout of the candidate commit:

```bash
bash scripts/build-mac.sh -m:1
python3 scripts/package-release.py macos-arm64
bash scripts/build-linux.sh -m:1
python3 scripts/package-release.py linux-x64
python3 scripts/package-release.py finalize
```

Output: `dist/release/`, including both archives, `SHA256SUMS`, and `release-manifest.json`. Archive validation rejects local settings, private key/backup files and symlinks, checks executable permissions, and records source SHA/version/migration requirements. Finalization rejects mixed source commits or versions. It does not validate native execution.

## Local verification

The candidate preparation passed a Release compilation (zero warnings/errors), four
archive/version boundary tests, three backup-updater orchestration tests, 28 semantic
contrast checks, all 21 direct regression methods, macOS/Linux self-contained builds and workflow YAML
parsing. A redundant build-server-disabled compilation attempt timed out after 90 seconds;
the initial Release compilation succeeded. Full hosted Actions validation remains pending;
YAML parsing alone does not validate GitHub execution.

## Acceptance evidence

| Check | Current evidence |
|---|---|
| Live migrations and SQL suite | Both reported passed by Codrin from normal SSH |
| Independent deployed inspection | Pending Docker access; run `bash scripts/verify-deployed-database.sh` (read-only object checks) |
| Current GitHub CI | Pending access; this agent cannot resolve GitHub from the execution sandbox |
| Native Mac and Hyprland full flow | Pending; no desktop sessions available to this agent |
| Extracted candidate download launches and hashes | Pending creation/download of CI assets |

Record OS, display scaling, source SHA, result and observed defects for each native run. Exercise find → scale → cook/plan → generate/reopen checklist → mark purchases → explicit pantry import; repeat import and week copying; verify dirty-editor choices, timer navigation, focus, shortcuts, menus, file pickers, themes and 860×600/1280×800/1440×900 layouts at 100/150/200% scaling.

Keep the candidate draft until these checks pass. Update evidence and release notes before publication. Nix reproducibility, signing/notarization and in-app updates are separate work.
