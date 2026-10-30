# Release management

For installation, use the [platform guides](../platforms/) and published GitHub release assets. This guide describes how to prepare, validate, and manually publish a release.

`release.json` is the release metadata source. `Version.props` supplies assembly and
numeric installer versions; the macOS plist supplies numeric bundle defaults. Regression
checks prevent version drift. Never reuse or overwrite a published version/tag.

## Verify and prepare a draft

1. Work on `main`. Update release metadata, assembly/plist versions, and `docs/releases/v<version>.md` when preparing a new version. Numeric installer/file versions have four components; macOS bundle versions remain numeric.
2. Push `main` and inspect **Compatibility**. Independent jobs run desktop tests on all three OSes, compile WPF, create self-contained platform packages, test Windows installation, and run presentation, packaging, updater, and disposable SQL checks. A WPF failure does not hide installer or application-test results. Combined release integrity is checked after all platform packages succeed.
3. Run **Draft release candidate** manually on `main`. It reruns Compatibility for that exact dispatch SHA, collects all three packages, verifies matching source/version/migrations, and creates a tag plus a **draft** release. It does not publish automatically. Prerelease flags follow the version suffix.
4. Download the assets, verify their hashes, and perform native acceptance. Record the tested commit/version, OS, scaling, result, and defects. Publish the draft manually only when the required checks pass.

An existing tag aborts the workflow. If tag creation succeeds but upload fails, inspect
the tag's SHA and any draft first; remove only the failed candidate after verifying it
was never published, then retry. Do not silently replace tags or published downloads.
The workflow must be registered on GitHub's default branch before manual dispatch works.

## Build the same files locally

On a Windows x64 worker with the prerequisites in the [Windows guide](../platforms/windows.md):

```powershell
pwsh -File scripts/build-windows.ps1
pwsh -File scripts/test-windows-install.ps1
```

On a .NET host with the macOS/Linux runtime packs available:

```bash
bash scripts/build-mac.sh -m:1
python3 scripts/package-release.py macos-arm64
bash scripts/build-linux.sh -m:1
python3 scripts/package-release.py linux-x64
```

Build every platform from the same clean commit. Collect these outputs into `dist/release/`:

- `MealPrepApp-<version>-windows-x64-setup.exe` and its `.exe.metadata.json` sidecar;
- `MealPrepApp-macos-arm64.tar.gz`;
- `MealPrepApp-linux-x64.tar.gz`.

Then run:

```bash
python3 scripts/package-release.py finalize
```

Finalization produces `SHA256SUMS` and `release-manifest.json`, requiring all three
platforms to match the selected commit, version, and migration list. The Windows sidecar
binds the installer bytes to its build metadata. These checks establish artifact
consistency, not signing or independent supply-chain attestation.

Payload validation rejects local settings, private-key/backup files, and symlinks.
Unix archives retain executable permissions. The Windows pipeline validates its payload
before compiler input and includes the same metadata inside the installation. The
compiler is pinned to Inno Setup 6.4.3 in `packaging/windows/inno-setup.json`.
CI downloads the versioned official GitHub asset, checks its published SHA-256 before
execution, installs it to a dedicated tool directory, and passes that compiler path to
the build. This avoids dependence on Chocolatey's available package versions.
Both setup and build verify the loaded compiler engine using a small output-disabled
compile with a preprocessor expression. `ISCC.exe` launcher file metadata is not used
as the engine version. The probe must compile successfully and report the pinned version.
Packages remain unsigned and macOS is unnotarized.

## Acceptance

| Area | Required evidence |
|---|---|
| Automated desktop tests | Passing Linux, Windows, and macOS test results for the release SHA |
| Windows install | Disposable CI install, first-launch window, reinstall, shortcuts, registry, uninstall, and user-data preservation |
| Windows interactive acceptance | Setup wizard, first connection, sign-in, application flow, update from a previous version, and uninstall on a native machine |
| macOS | Download approval, Applications install, first connection, sign-in, native menus/file pickers, update and uninstall |
| Linux / Hyprland | Native dependencies, XWayland launch, launcher, first connection, sign-in and update; Nix reproducibility tracked separately |
| Application flow | Find → scale → cook/plan → reopen saved checklist → mark purchases → explicit pantry import; dirty-editor guards and keyboard navigation |
| Database | Disposable SQL suite and existing deployment's required migration state; no live migration runs as part of release packaging |
| Download integrity | Hash checks and matching source/version/migrations for every attached asset |

Headless rendering and cross-publishing do not establish native acceptance. A reinstall
smoke test checks installer identity and data retention; native upgrades from an older
released version remain a separate acceptance check. See [implementation evidence](implementation-validation.md)
and [earlier redesign evidence](../redesign/validation.md).
