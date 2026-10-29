# Contributing

## One maintained branch

`main` contains application code, database scripts, reports, and instructions for all
platforms. Work directly on `main`; pull before editing and avoid rewriting shared history.
Save local work before switching an older checkout from a feature branch.

Existing platform folders keep their names to preserve project references and grading
commands. Avalonia is the distributed client on Windows, macOS, and Linux. WPF remains
in `App/MealPrepApp` for grading; historical WinForms and Vault design snapshots are
reference material, not alternative supported builds.

## Checks

Run the [README verification commands](README.md#verification). Build the platform you
changed using its [platform guide](docs/platforms/). Windows CI must also compile WPF.
Connection setup and shared logic have database-free regressions; run SQL tests against
the disposable container, never by resetting the live database.

Keep Core independent of Avalonia and WPF. C# database access calls stored procedures;
mutations retain UTC timestamps and auditing. Keep deployment credentials out of Git,
packages, screenshots, test logs, and diagnostic messages.

## Consolidating older branches

After the implementation is committed locally, fast-forward local `main` to that commit
only if `main` is its ancestor. Fetch the remote before pushing; merge and validate any
new remote work instead of force-pushing. Push `main`, then wait for its Compatibility run
to pass. The cleanup helper requires GitHub CLI authentication:

```bash
bash scripts/consolidate-main.sh
```

The helper requires a clean checkout on `main`, freshly fetches remote heads, verifies
local and remote `main` agree, checks successful CI for that exact commit, and rejects
any unmerged remote or local branch. Only after these checks does it set GitHub's default
branch to `main` and delete merged branches. Remote deletion uses expected-commit leases
so a branch that moves after the audit is preserved. If remote access or CI is unavailable,
leave the branches intact and retry after resolving it. Tags and commit history are retained.

## Releases and historical material

See [release preparation](docs/releases/README.md). Builds run automatically; release
publication remains manual. Do not claim native tests or signing passed from a source build.

`Raport/` contains the Romanian university report. `Vault/` and the earlier redesign and
compatibility notes preserve dated decisions and evidence. Use the README and platform
guides for current setup instructions.
