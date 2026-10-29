# Installer and repository consolidation validation

Local verification on 2026-10-29, host `ideapad-server` (Linux), .NET SDK 10.0.113.
These results cover the implementation change, not a published or signed release.

## Passed locally

- Release build of the Avalonia application, tests, and direct regression runner: zero warnings/errors.
- 28 direct in-process application regressions, including connection precedence, malformed-file recovery, private atomic saves, placeholder detection, password masking, failed test/retry/cancellation, save/continue without network access or restart, and resolution of WPF shared markup types against the compiled Core assembly.
- Six release tests covering private files, executable permissions, symlinks, Windows payload checks, installer hash/source binding, archive metadata, and version consistency.
- Four branch-cleanup integration tests against disposable local Git repositories: successful cleanup, unmerged-work rejection, missing-CI rejection, and preservation of a branch moved after audit.
- Three Windows packaging contract tests: the versioned official compiler asset/checksum pin, balanced preprocessor guards/build inputs, and the installer/application mutex contract. These are source checks, not native installer compilation. The compiler downloader replaces the unavailable Chocolatey 6.4.3 package with the official release asset and published SHA-256.
- Three existing database-updater orchestration tests and 28 semantic contrast checks.
- macOS Apple Silicon and Linux x64 self-contained publishing and archive creation using cached runtime packs. This establishes packaging, not native macOS execution.
- Shell syntax, workflow YAML parsing, local documentation links, and whitespace checks.
- Follow-up Windows audit: all 36 WPF shared namespace declarations reference `MealPrepApp.Core`; all 32 WPF XAML files parse and their named static resources resolve within the application's resources. The malformed installer preprocessor line was corrected. CI now checks WPF, application tests, and packaging independently and retains Windows installer/startup logs on failure.

## Remaining external checks

| Check | Status / limitation |
|---|---|
| Standard xUnit/VSTest execution | Local TCP listener denied by sandbox; direct runner passed instead. Standard execution remains configured in CI. |
| Disposable SQL suite | Docker socket access denied. No live database migration or reset was attempted. |
| Windows installer and WPF | Windows compiler/runtime packs and a Windows session are unavailable here. CI is configured to compile the actual setup executable and exercise installation, startup, reinstall, uninstall, shortcuts, registry, and settings retention. No actual Windows setup executable was produced locally. |
| Native application acceptance | Windows interactive install, macOS launch/approval, and Linux/Hyprland desktop flows require their target environments. |
| Remote consolidation | Fetch/push to GitHub cannot resolve `github.com` in this environment. Local remote-tracking refs show all old branches contained in the implementation's ancestry, but are not sufficient evidence for deletion. |
| Release publication | No draft or public release was created. |

The local work is consolidated onto `main` while remote cleanup remains pending.
After pushing `main` from a connected shell and passing Compatibility for its exact SHA,
run `bash scripts/consolidate-main.sh` with authenticated GitHub CLI. It fetches and
audits branches again before changing the default branch or deleting anything.
