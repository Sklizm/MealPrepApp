# Localization validation

Validation performed on Linux in a restricted, headless environment on 2026-10-30.
This record separates local build/render evidence from native release acceptance.

## Local checks

- Avalonia and test projects compile with the .NET 10 SDK.
- Standard VSTest cannot start its local TCP listener in this sandbox; use the existing
  in-process regression runner for local execution. Standard xUnit discovery remains in CI.
- Packaging helpers: 6 release-package, 7 Windows-packaging, 4 branch-maintenance,
  and 3 database-updater tests pass.
- Contrast checker: 70 text pairs satisfy 4.5:1 and 16 control/focus pairs satisfy 3:1.
- The Windows Desktop reference pack is absent locally. The WPF cross-build cannot
  restore it while NuGet is unreachable; the Windows CI job remains required.

The in-process runner passes **61/61** regression methods, including **11 localization
checks**. Those cover preference migration and persistence, catalog/placeholder parity,
plural forms, regional formats, open windows and native-menu labels, a pending connection
request, calendar labels with unchanged date/input, disposal, clipboard and spreadsheet
headings, and preservation of a dirty editor, cached pantry rows, and a running timer.
Switching alone causes no repository reads or collection mutations in the state test.

All **463 resource keys** match across the three catalogs. Resource references, Markdown
file links, and archived wiki links resolve. Repository path and packaging checks pass.
Changed SQL files contain comments only; SQL statements and seed values remain identical.
The disposable SQL suite could not run because access to the Docker socket is denied.

The refreshed render matrix contains **396 PNG captures**: 132 per language. Each language
covers nine page variants (all seven sections plus recipe detail and cooking) at three
sizes in both themes, with 150% and 200% exports at the default size. Authentication,
Settings, connection and application dialogs have both themes at all three export scales.
See the per-language manifests and [matrix metadata](render-matrix.json).
Representative English and Russian captures were visually inspected, including Cyrillic,
long category names, navigation, Settings, and connection setup. Compact category names
use ellipsis with full tooltips and accessible labels.

GitHub was unreachable during this local validation. Remote Compatibility CI, repository
description updates, and push could not be verified here. No release was published.

## Native acceptance

| Acceptance | Windows | macOS | Linux |
|---|---|---|---|
| First launch and upgrade with missing language preference | Pending | Pending | Pending |
| Live switching with a dirty editor, cooking timer, open dialogs and active request | Pending | Pending | Pending |
| Native menu labels and keyboard/screen-reader navigation in all languages | Pending | Pending | Pending |
| Native 100%, 150% and 200% display scaling | Pending | Pending | Pending |
| Install, launch, update and uninstall for the final commit | Pending | Pending | Pending |

Headless bitmap exports are layout evidence, not native display-scaling acceptance.
Release publication remains manual. Compatibility CI and GitHub metadata updates require
remote access and must be recorded for the final pushed commit.
