# Native acceptance record

Status on 2026-10-29: pending on all three platforms. The headless and cross-publish
results in [README.md](README.md) are separate evidence.

Record the exact commit, package hash, OS version, display scale and result for each run.
Do not mark a row passed from headless renders or a successful build.

| Acceptance | Windows x64 | macOS arm64 | Linux / Hyprland |
|---|---|---|---|
| Native controls, menus, focus and file pickers | Pending | Pending | Pending |
| Light, Dark and System changes across pages/popups/dialogs | Pending | Pending | Pending |
| All seven hover/focus sequences, hold, 180 ms reset, rapid re-entry | Pending | Pending | Pending |
| Reduced motion during animation; completed/idle icons do no animation work | Pending | Pending | Pending |
| Tab, arrows, Enter/Space, search/save/back shortcuts and English, Russian, and Romanian accessibility names | Pending | Pending | Pending |
| Reject unsaved-editor departure; original rail destination stays selected | Pending | Pending | Pending |
| 860×600, 1280×800, 1440×900 windows, footer access and rail scrolling | Pending | Pending | Pending |
| Actual OS/display scaling at 100%, 150% and 200% | Pending | Pending | Pending |
| Recipe → scaling/cooking → planning → saved shopping → explicit pantry import | Pending | Pending | Pending |
| Lookup/network failure, retry, rapid snapshot changes and mutation validation | Pending | Pending | Pending |
| First connection, sign-in, update and uninstall with user data retained | Pending | Pending | Pending |

For the workflow, keep a cooking checklist/timer active while navigating, change recipe
filters and return, reopen saved purchase state, import an item once, then revisit the
pantry and verify no duplicate import. Exercise failures and recovery with the same user
and database. Use a disposable test deployment for destructive test setup.

Compatibility CI must pass for the tested commit before release acceptance. Windows WPF
compilation and native installer execution belong to its Windows jobs. Native acceptance
remains necessary after CI passes. Publish a release only through the existing manual flow.
