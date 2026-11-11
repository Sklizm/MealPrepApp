# Android companion

The native Android app supports Android 8 and later. Shopping and Pantry use the
same account as the desktop app, through a private HTTPS API. Recipe editing,
planning, cooking and reports stay on desktop. Connect to the private network or
VPN before signing in; ask the server administrator for the HTTPS address.

## Shopping offline

Open an existing saved list while connected. Afterwards its checklist, actual
purchased quantities and manual groceries remain editable offline. Changes save to
Room immediately and enter a durable outbox. Refresh, foreground entry, reconnect
and constrained WorkManager jobs attempt synchronization. Android may defer
background work. Cached pantry data remains readable offline.

Required and purchased quantities are separate. Buying 1 litre for a recipe needing
200 ml adds the litre when you explicitly import it. Manual text groceries need no
catalog match for shopping; pantry import requires an ingredient and a unit.
Imports, stock corrections, thresholds and new saved lists need connectivity and a
fresh review. Interrupted online saves retain the original request: use **Review an
interrupted save** to confirm and retry it without applying the operation twice.

A version conflict keeps both local and server values. Refresh the conflict and
choose the saved value or explicitly keep your change. A deleted server item can
be recreated as a new manual grocery. No conflict silently overwrites another
client's change. Signing out warns about pending work; clearing requires an
explicit choice and affects that account's cache and queue. Reauthenticate after a
session expires to send retained work.

## Pantry and reminders

Compatible mass or volume units combine in the overview. Pieces never convert to
mass or volume. Expand an ingredient for individual purchase batches, unknown
expiry, past dates, and explicit quantity correction or waste. Past-date batches
remain visible and are excluded from the available quantity. Low-stock thresholds
are optional per ingredient and compatible unit.

Expiry reminders default to three days ahead, grouped into one daily notification
around 09:00 device time. Settings persist the advance period, notification choice,
language (English, Romanian or Russian) and System/Light/Dark appearance. Android
scheduling, battery limits and denied notification permission can delay or prevent
notifications. Cached information is used when offline; refresh for current stock.

## Build and sign

Use JDK 17, Android SDK platform/build tools 35 and the pinned Gradle wrapper:

```bash
cd App/MealPrepApp.Android
./gradlew :app:testDebugUnitTest :app:lintDebug :app:assembleDebugAndroidTest
./gradlew :app:connectedDebugAndroidTest
```

The second command needs an emulator or device. Unit tests alone do not prove Room,
Compose, process recovery or physical notification behavior. Do not install a debug
APK over an existing signed release; their signing identities differ.

Create a dedicated durable MealPrepApp signing key outside Git. Back it up securely;
future in-place updates require the same key. Never reuse another application's key.
For a distributable APK, set these environment variables and build:

```bash
export MEALPREP_ANDROID_KEYSTORE=/private/path/mealprep-release.p12
export MEALPREP_ANDROID_KEY_ALIAS=mealprep-release
# Supply MEALPREP_ANDROID_STORE_PASSWORD and MEALPREP_ANDROID_KEY_PASSWORD securely.
./gradlew :app:assembleRelease
```

The APK is `app/build/outputs/apk/release/app-release.apk`. Verify its certificate and
SHA-256 before delivery. An unsigned release is not a distributable update.

Device credentials use Android Keystore-backed AES-GCM encryption. Passwords are
used for login only. SQL credentials and the server token key never belong in the
APK. Backup is disabled; local pending work is specific to that installation.
