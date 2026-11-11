# Companion verification

The Android companion uses package `app.mealprep.companion`, version 1.0.1,
code 2 and Room schema 1. It uses its own durable release-signing identity, outside
Git. Distribution is manual; automated builds do not publish releases.

Verified implementation evidence:

- 108 desktop/Core xUnit tests, disposable SQL migration and regression suites, and
  58 real HTTP/API integration checks against a disposable SQL Server.
- Legacy WPF cross-build, Linux self-contained publish, and native X11 startup
  on NixOS with the development shell (including ICE/SM session libraries).
- Desktop headless screenshot matrix: English, Romanian and Russian; Light/Dark;
  860×600, 1280×800 and 1440×900, with additional scale captures at 1280×800.
- 13 Android JVM tests covering ordered durable operations, lost responses, expired
  sessions, conflicts, exact decimals, compatible units, expiry and account keys.
- Android debug and signed release builds, lint with no errors, and matching source
  hashes across the build hosts. Dependency-update suggestions are lint warnings.
- Android 15 ARM64 emulator: Room reopen and queued actual purchases, idempotent
  response recovery, expired-session retention, explicit conflict resolution,
  non-blocking local edits during network waits, account isolation, interrupted
  stock requests, current-session revocation after reauthentication, Compose login and
  three-language/theme screenshots.
- Separate airplane-mode seed, force-stop and recovery instrumentation phases.
- Signed 1.0.0 → 1.0.1 install-over upgrade and launch with denied notifications.
- Android HTTPS health and anonymous-401 checks against a private Serve endpoint.
  No synthetic accounts or test stock are promoted to a live database.

[Shopping, Light](screenshots/shopping-en-light.png) ·
[Shopping, Dark](screenshots/shopping-en-dark.png) ·
[Pantry, Light](screenshots/pantry-en-light.png) ·
[Pantry, Dark](screenshots/pantry-en-dark.png) ·
[Settings, Light](screenshots/settings-en-light.png) ·
[Settings, Dark](screenshots/settings-en-dark.png)

Emulator evidence does not establish physical-phone acceptance. A phone still needs
installation, existing-account login, reconnect and two-device conflict checks,
background scheduling, denied notification permissions and the daily grouped
reminder. Android 8 runtime acceptance remains separate from minSdk/lint checks.
Native desktop keyboard, file picker, notification, display-scaling and installer
acceptance also remain separate from cross-builds and headless rendering. Keep
release publication manual until the required device checks are recorded.
