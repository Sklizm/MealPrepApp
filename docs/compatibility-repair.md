# Compatibility repair status

Historical compatibility repair record; source baseline: `5315468`. The later
[cooking redesign](redesign/README.md) builds on these repairs. Use its
[current validation evidence](redesign/validation.md) and the
[main setup instructions](../README.md) for the present source. Counts and limitations
below describe the repair checkpoints and may have been superseded. The agent has not
applied the production migrations.

Implemented:
- One-way navigation selection and non-throwing unsupported converter directions.
- Awaitable shared dialogs; real Avalonia modality and false results on window-close.
- Desktop login/shell window coordination, per-session DI scope, detached shell handlers.
- Local redacted exception diagnostics and guarded navigation/operation failures.
- Recipe category/search/list-mode composition, 40-row paging in both clients, latest-request guards.
- Cancellation-aware Dapper calls and list queries; ingredient search respects category.
- One query per recipe page with stored thumbnails, replacing sequential full-photo reads.
- Portable image validation (10 MiB/25 MP), orientation, JPEG resizing and thumbnails
  using the existing SkiaSharp stack. Removed the initially selected ImageSharp dependency
  after its Release license requirement blocked compilation.
- Bitmap lifetime tied to image controls.
- File-picker filters and local date conversion.
- Disabled email-only reset in shared UI and SQL; deployment role explicitly denies it.
- BCrypt verification against current/five previous passwords; credential-version guard.
- Atomic recipe rowversion predicate; serialized pantry upserts.
- Shopping lists retain demand when recipe servings are unspecified.
- Desktop builds and release scripts exclude deployment configuration; development-path
  and per-user desktop settings loading are supported.
- SDK 10 requirement, Linux launcher installer, Nix dependency bootstrap generator.
- Cross-platform CI, focused headless/Core tests and disposable SQL regression runner.

Validation evidence:
- Bash syntax checks for all scripts.
- XML parsing for XAML/AXAML/project files; JSON syntax for SDK settings.
- git diff --check.
- Source inspection of dialog callers and stored-procedure interfaces.
- Verified .NET SDK 10.0.113 and runtime 10.0.13 on ideapad-server.
- Parsed 139 C# files with the installed Roslyn compiler libraries: zero syntax errors.
  That syntax check was followed by the Release compilation and direct checks below.
- Release compilation of Core, Desktop (including compiled XAML), test project and direct
  runner passes with Avalonia 11.3.18 and PKCS 9.0.21.
- Codrin reports the earlier standard dotnet test run succeeded after the DataGrid version fix
  (before the populated-template regression was added).
  The command included NuGetAuditMode=all; no full output was supplied, so a warning-free
  security audit is not independently confirmed.
- Eleven regression methods now pass through direct in-process execution: navigation binding,
  modal close, all feature view construction, activation of all five real shell tabs,
  DataGrid rendering and selection, disabled password recovery, BCrypt salt/verification,
  image resizing, invalid-image rejection, timeout mapping, and real populated feature
  templates with command/parameter assertions.
- User-executed disposable SQL regression run passed: stale saves, owner rejection,
  rollback of invalid recipe creation and its audit event, successful update auditing,
  credential-version rejection, and disabled unsafe password reset. Schema ran twice.
  This does not test simultaneous writers, real-data upgrade preservation, shopping-list
  math, nutrition conversions, or authorization through the low-privilege app login.
- Framework-dependent publish and Linux x64 self-contained packaging succeeded; output
  excludes appsettings.Local.json. Linux runtime/Skia assets are present. Native GUI launch
  remains unverified. macOS arm64 self-contained packaging also succeeded after the
  user ran the build script. Verified Mach-O arm64 executable, bundle plist, native
  runtime/Avalonia/Skia libraries, .NET 10.0.13, and exclusion of local credentials.
- Added a reproducible direct runner under Tests/MealPrepApp.RegressionRunner. It does not
  replace standard VSTest/xUnit execution or platform smoke tests.

Not yet verified:
- .NET and cached NuGet packages are now available. New package downloads still require
  a normal network-enabled shell. Offline restore used NuGetAudit=false because network
  access is denied; this is not a clean security audit. Nix remains unavailable.
- Standard dotnet test remains blocked inside the sandbox by local TCP restrictions,
  but Codrin confirms it passed from normal SSH after the dependency correction.
- Docker socket access remains denied to the agent. Codrin ran scripts/test-database.sh
  from normal SSH and supplied the successful log (also read from /tmp/mealprep-db-tests.log).
  Fresh schema creation, a second migration run, and the scripted SQL assertions passed.
  Production database deployment remains outstanding.
- WPF compilation on Windows has not been verified.
- Nix deps.json is deliberately an empty bootstrap. Generate real dependency metadata and
  flake.lock with bash scripts/update-nix-deps.sh on a networked Nix host before claiming
  reproducible builds. Both generated artifacts must be reviewed and committed.
- Codrin confirms the Mac app opens and reaches registration. Registration currently
  fails with SQL error 258; Mac nc to configured 192.168.100.109:1433 also times out.
  This confirms TCP reachability is blocked before database authentication. Current
  server addresses and published database port are awaiting normal-shell verification.
- Full native Mac and NixOS/Hyprland smoke tests remain required. No virtual X server
  is installed on the headless build host for a real Linux GUI launch.
- The Mac tab crash is now confirmed by Codrin’s captured stack trace and reproduced
  with populated recipe/ingredient templates on the server. The reproduced failure is
  fixed; native retesting of the rebuilt bundle remains required.
- Existing stored photos fall back to full bytes in the paged list until reuploaded,
  retaining their previews. New uploads use small thumbnails.
- Shopping-list cross-unit normalization remains unsupported (exact-unit matching).
- Full view-by-view WPF/Avalonia parity and compiled-binding migration remain unfinished.
- Native Wayland, drag rescheduling and native printing are not added by this repair.
- Active credential rotation needs a deployment environment; changing repository templates
  does not rotate deployed credentials.
- Current direct SQL architecture trusts clients with shared database access and user IDs;
  UI login is not a server-enforced security boundary. A backend auth migration is separate.

Dependency update validation:
- Core explicitly references System.Security.Cryptography.Pkcs 9.0.21 to address
  [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) and
  [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx).
- Desktop runtime and headless test packages move together from Avalonia 11.2.3 to 11.3.18.
  DataGrid remains exactly pinned to [11.2.3], independently of the runtime release.
  The attempted DataGrid 11.3.18 does not exist; NuGet selected 12.0.0 and failed restore.
  The corrected graph contains no Avalonia 12 packages. The retained DataGrid declares
  Avalonia >= 11.2.3, and the rendering/row-selection check passes on runtime 11.3.18.
  [Avalonia.FreeDesktop 11.3.18](https://www.nuget.org/packages/Avalonia.FreeDesktop/11.3.18)
  requires Tmds.DBus.Protocol >= 0.21.3, patched for
  [GHSA-xrw6-gwf8-vvr9](https://github.com/tmds/Tmds.DBus/security/advisories/GHSA-xrw6-gwf8-vvr9).
  The 0.21 protocol release changed binary APIs, so a direct override on Avalonia 11.2.3
  was avoided.
- Offline restore from the newly cached packages and Release builds now pass.
  Nine direct in-process regression methods pass on the corrected graph.
  Standard VSTest execution also passed from normal SSH, as reported by Codrin.
- The successful user-run command enabled NuGetAuditMode=all. Confirm no remaining
  vulnerability warnings in its output; desktop D-Bus smoke tests remain outstanding.

Validation:
    dotnet test Tests/MealPrepApp.Tests/MealPrepApp.Tests.csproj -c Release
    dotnet run --project Tests/MealPrepApp.RegressionRunner -c Release  # direct checks
    dotnet build App/MealPrepApp/MealPrepApp.csproj -c Release  # Windows
    bash scripts/test-database.sh                           # disposable container only
    bash scripts/build-mac.sh                               # macOS arm64
    bash scripts/build-linux.sh                             # Linux x64

Deployment:
1. Back up the existing database. Run isolated tests before applying schema/procedure changes.
2. For an existing database, use scripts/update-database.sh after the disposable suite
   passes. Apply the compatibility update followed by 20261028_cooking_redesign.sql,
   following the current rollout instructions linked above. Use run_all.sql for new setup.
   Existing login passwords remain unchanged.
3. Deploy both clients together: password changes now require the expected current hash.
   Older clients will fail password changes safely rather than bypassing the history checks.
4. Create per-user settings at:
   macOS: ~/Library/Application Support/MealPrepApp/appsettings.Local.json
   Linux: ~/.local/share/MealPrepApp/appsettings.Local.json
   The exact root follows .NET LocalApplicationData (and platform environment settings).
   Copy the template and set the deployed credentials; server ports use host,1433.
5. Smoke-test each navigation tab/subtab, dialog save/cancel/close, CRUD, photo upload,
   weekly/monthly planning, shopping lists, exports, offline DB, and repeated login/logout.
   Check logs under the same per-user MealPrepApp/logs directory.

Standard tests (user-reported), direct regression checks and disposable SQL checks pass.
The full CI suite, remaining database scenarios, final audit review and platform smoke
tests must finish before release.

Packaging permission repair:
- Publishing originally tried copying a local settings file into an old build directory
  whose copy was owned by nobody. Sixteen incorrectly owned ignored generated files were
  removed from Desktop bin/obj; source settings were preserved.
- Desktop CopyToOutputDirectory for appsettings.Local.json is now Never, matching publish
  exclusion. Build scripts accept extra dotnet publish arguments for offline validation.
- Linux packaging then completed successfully with --no-restore. Codrin subsequently
  completed macOS packaging from normal SSH; the resulting bundle passed artifact checks.
- Transfer archive: dist/MealPrepApp-macos-arm64.tar.gz (contains the .app bundle, preserves
  executable permissions, excludes local settings). Native launch has not been tested.

Connection failure handling:
- SQL error 258 / timeout now has a Romanian network/server availability message.
  Added mappings for common reachability, database login and database access errors.
- Caught SqlExceptions now write safe numeric error/state/class, connection IDs and
  native error numbers to local diagnostics, omitting messages, settings and credentials.
- These changes compile in Release; they are not yet in the transferred Mac bundle.
  Correcting the connection route should not require reinstalling the app.

Confirmed populated-template tab crash:
- Mac stack trace: ArgumentException, Unable to resolve type vm:IngredienteListViewModel,
  at IngredienteListView.axaml:56 during deferred row-template construction/layout.
- A new test using production DI, real navigation, real view models, and sample rows
  reproduced the corresponding failure in ReteteListView.axaml:87 before the fix.
- Removed 12 runtime view-model casts from ancestor command paths across recipe/draft
  lists, ingredients, pantry, and monthly/weekly calendars. Commands now resolve directly
  through the ancestor DataContext.
- Expanded regression checks render each affected populated template and assert the
  actual command and row parameter are bound. All 11 direct checks pass. These checks
  bypass database reads and therefore do not replace SQL integration tests.
- Rebuilt the macOS self-contained bundle and refreshed dist/MealPrepApp-macos-arm64.tar.gz.
  The Mac per-user Tailscale connection setting is external and need not be recopied.

Live Recipes error 2812:
- The new Recipes query calls dbo.sp_BrowseRecipes. The user's live database reports a
  missing procedure; only the disposable database was migrated earlier. The exact
  missing object has not been queried on the live instance from the sandbox.
- Added Database/migrations/20261028_compatibility.sql: transactional column/procedure
  update, no seeds or login password changes, baseline checks, and a read under the
  actual app database user's permissions.
- Added scripts/update-database.sh: stages the current SQL files, creates a COPY_ONLY
  CHECKSUM backup, runs RESTORE VERIFYONLY, copies the backup to ignored backups/ with
  mode 600, then applies the compatibility migration. A failed backup or backup copy
  stops the update. The database administrator password stays inside the container.
- Three mocked orchestration checks pass, including backup/copy failure paths. These
  do not execute SQL. The extended disposable database suite now simulates missing
  Browse/Thumbnail objects, migrates twice, and checks preserved users/recipes/photos.
  This extended SQL run has not yet executed because Docker is unavailable in the sandbox.
- The live update has NOT been run by the agent. Run the extended disposable checks
  successfully before applying the live update, with clients closed:
      bash scripts/test-database.sh && bash scripts/update-database.sh
- Error 2812 now maps to a database-update message in source; no Mac rebuild is
  required to fix the missing live procedure once the database is migrated.
