# Private companion API

The ASP.NET Core 10 API provides authenticated Shopping, Pantry, catalog and
synchronization operations for the Android companion. Desktop clients retain their
existing direct SQL connection. No SQL credentials belong in Android packages.

## Configuration

Apply the existing database migrations through the verified-backup-first updater,
then apply `20261111_companion.sql`. Run disposable SQL and API checks first:

```bash
bash scripts/test-database.sh
bash scripts/test-api.sh
bash scripts/update-database.sh mealprep-db 20261111_companion.sql
```

Create a private settings file outside the source and publish directories, based on
`App/MealPrepApp.Api/appsettings.Local.template.json`. Supply the application SQL
login and a durable, base64-encoded random token key of at least 32 bytes. Restrict
that file to the service account. Keep the key across restarts for reproducible login retries. Existing sessions remain
valid until expiry, explicit revocation or a password change. Never commit it or place it in an APK or release archive.

```bash
dotnet publish App/MealPrepApp.Api -c Release -o dist/api
MEALPREP_API_SETTINGS=/path/to/private/settings.json dotnet dist/api/MealPrepApp.Api.dll
```

The API binds IPv4 loopback only, on port 18924 by default. Serve it privately with
[Tailscale Serve](https://tailscale.com/docs/reference/tailscale-cli/serve), using an
unused HTTPS port and preserving existing routes, for example:

```bash
tailscale serve --bg --https=9443 http://127.0.0.1:18924
```

Enter the resulting private HTTPS address in Android. Do not enable Funnel or publish
the backend port. `GET /health` returns protocol availability without account data.

## Sessions and contracts

- Login reuses existing password verification and account lockout. Device sessions
  expire after 30 days, are individually revocable, and become invalid after a password
  change. Only opaque-token hashes and credential fingerprints are stored in SQL.
- Login operation IDs reproduce the original opaque token through the durable server
  key. Reusing an operation ID with different session input fails.
- All protected endpoints derive the user from the verified bearer token. Client
  requests cannot select a user ID. Ownership is enforced again in stored procedures.
- Mutations carry a client operation ID and, for existing records, an expected version.
  Identical retries return the original result. Conflicts return HTTP 409 and require
  a reviewed resolution. `GET /v1/operations/{id}` recovers an owned operation result.
- Decimal quantities use invariant decimal strings in JSON; IDs and versions use
  integers. Calendar dates use `YYYY-MM-DD`; event timestamps use UTC offsets.
- `GET /v1/sync` returns a transactionally consistent full snapshot. It includes
  shopping tombstones and empty stock batches so deletions retain identity. Android
  reconciles this snapshot with its durable outbox rather than overwriting pending edits.
- Imports use purchased quantities and units. Repeated imports never add stock twice.
  An imported purchase cannot silently rewrite pantry stock; use a versioned correction.
- Exceptions return bounded error codes, without SQL text or connection details.
  Login and account operations are rate limited. Bearer tokens and request bodies
  are not logged by the application.

The versioned endpoints cover authentication, device sessions, saved shopping lists,
manual and mapped groceries, purchase import, batch stock adjustments, opt-in low-stock
thresholds, catalog lookup and synchronization. Recipe editing, planning, cooking and
reports remain desktop workflows.

## Verification

`Tests/MealPrepApp.ApiChecks` exercises real HTTP requests and the application SQL role
against a fresh SQL Server container. It checks authentication, lockout, session expiry,
revocation, password changes, account isolation, version conflicts, decimal transport,
idempotency, immutable imports, operation recovery, tombstones and stock summaries.
The script never targets a deployed database. Physical phone acceptance and live HTTPS
routing are separate checks.
