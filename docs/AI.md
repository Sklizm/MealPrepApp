# Meal assistant

The optional meal assistant supports pantry suggestions, recipe text/link imports,
new recipe drafts, seven-day plans, and recipe/receipt photos on desktop and Android.
All results are reviewed before publication. Suggestions and plans never deduct stock.

## Setup and free-tier operation

The API uses Gemini `gemini-3.1-flash-lite`. It is disabled by default. Create a dedicated
Google AI Studio project **without linking a billing account** and confirm that the
model is available on that project's free tier. Free-tier quotas and availability
vary; this application does not promise a fixed allowance.

Google may use free-tier inputs and responses to improve its products, including
human review. Submit only non-sensitive food content. Each account must accept the
notice before cloud requests or photo uploads. Operators must check the provider's
current regional and age restrictions before offering the feature to users.

- [Gemini pricing](https://ai.google.dev/gemini-api/docs/pricing)
- [Billing and free-tier projects](https://ai.google.dev/gemini-api/docs/billing)
- [Free-tier data terms](https://ai.google.dev/gemini-api/terms#data-use-unpaid)

Use the private API settings file described in [API setup](companion/API.md), outside
the repository and publish directory. Add the following section, placing the actual
key only in that private file:

```json
"Ai": {
  "Enabled": false,
  "FreeTierVerified": false,
  "ApiKey": "",
  "Model": "gemini-3.1-flash-lite",
  "Tasks": ["suggest", "import", "generate", "plan", "recipe-photo", "receipt"]
}
```

Run the disposable SQL and API suites before applying `20261113_assistant.sql` through
the [verified-backup-first updater](database.md). Deploy the API before the clients.
The migration adds assistant records and recipe provenance without resetting existing
users, recipes, inventory, or sessions. The recipe-bundle procedure gains optional
output parameters; existing desktop/WPF calls retain their result contract.

After independently confirming the project's free tier and testing a small synthetic
request, set `FreeTierVerified` and `Enabled` to `true` and restart the API. The flag is
an operator confirmation, not an automatic Google billing-status check. Keep billing
unlinked: an application flag cannot prevent charges if the operator later enables
paid billing on the provider project. No paid provider, model fallback, grounding,
embedding service, or automatic quota upgrade is configured.

Remove tasks from `Tasks` to disable them independently. Keep the supported model ID;
other model IDs disable cloud assistance until explicitly implemented and evaluated.
One API service instance processes one provider request at a time. Quota responses
pause new requests until the reported cooldown, without an automatic paid retry.

## Desktop and Android

On desktop, open **Meal assistant** from the application menu, Recipes, Pantry,
or Planner. Completed requests open their review tab automatically. Connect to the same private HTTPS API and account used by the Android app.
The API and desktop SQL database identities must match. Only the server address is
remembered; passwords are not stored and desktop bearer tokens remain in memory.
Subsequent desktop sign-ins attempt the optional API connection without blocking
ordinary database workflows when it is unavailable.

Android adds Recipes and Plan destinations while retaining Shopping and Pantry.
Recipe/plan drafts and cached context remain editable offline. Cloud generation,
publication, plan application, and purchase acceptance require connectivity. Signing
out explicitly clears local account drafts along with shopping changes. Room 1 → 2
adds a separate draft table and preserves the existing shopping cache and outbox.

Use **Review import** first for pasted recipes and structured recipe pages. This uses
the deterministic parser and consumes no model quota. Unsupported pages or ambiguous
content can be submitted explicitly for AI assistance. Map every ingredient and unit,
resolve quantities, and check instructions before saving. New ingredients require a
separate explicit action. Reviewed recipe source text is stored with the recipe.

Recipe links can also attach the page's recipe photo. The import previews the photo
and prepares a resized JPEG and a cropped thumbnail. Skip the photo in review to
save just the recipe. If the website blocks the image or provides an unsupported
format, the recipe text remains available.

For plans, choose the start date, servings, meal slots, ingredient exclusions, maximum
cooking time, variety, and leftover preferences. Existing meals are preserved unless
selected for replacement. Cooked or consumed entries cannot be replaced. Pinned
saved-recipe suggestions survive regeneration; publish generated drafts before pinning
or applying them. Review new recipes separately before applying their plan.

Preview shopping and nutrition before applying. The application calculates quantities
and shortages, reuses compatible units, and counts shared batches once. It simulates
pantry allocation across cooking batches so the same stock is not spent twice. Prepared
portions retain their nutrition snapshot. Missing nutrition remains explicitly incomplete.
Plan application is atomic, audited, idempotent, and checked against reviewed versions.

For photos, select an image or use the Android camera. Crop, rotate, and irreversibly
cover personal details before confirming upload. Images are re-encoded without metadata.
Receipt extraction leaves uncertain quantities unresolved and never infers expiry.
Accepted rows become reviewed shopping purchases; importing them into pantry remains
the separate explicit Shopping action. Accepted row identities prevent duplicate stock
or purchases from retries; suspected matching receipts require explicit review.

## Recovery, privacy, and contracts

- Authenticated `/v1/ai` routes expose capabilities, preferences/consent, locally recorded
  usage, uploads, durable jobs, cancellation, and owned results. User IDs come from the
  verified bearer session, never from client-selected account fields.
- Recipe browsing/editing/import preview and plan preview/application use `/v1/recipes`
  and `/v1/plans`. Quantities retain the existing decimal-string API convention.
- Job submissions and domain mutations use operation IDs. Confirmed requests survive
  lost responses; **Recover interrupted save** replays the original operation. Conflicts
  require review. Reconnecting does not automatically publish an offline draft.
- Uploads are limited to 8 MiB and 24 megapixels before normalization. Server upload
  files are removed after processing or within 24 hours. Proposals and their food
  context expire after seven days; audit/usage metadata and published recipe sources
  remain. Accepted purchases follow normal shopping retention.
- Diagnostics exclude provider keys, passwords, raw prompts, uploaded images, and
  provider response bodies. The model has no database tools or execution privileges.
- A provider interruption is recorded as failed and requires explicit resubmission.
  Cancellation and session/consent checks prevent accepting a cancelled job's result.

## Verification

```bash
dotnet test Tests/MealPrepApp.Tests/MealPrepApp.Tests.csproj -c Release
python3 Tests/Scripts/check_localization.py
bash scripts/test-database.sh
bash scripts/test-api.sh
```

The API suite injects a fake provider and never calls a paid or free model endpoint.
The SQL suite tests migration repeatability, ownership, audited writes, and receipt/plan
idempotency. The normal Android Gradle tests, lint, assembly, and device tests remain
described in the [Android guide](companion/ANDROID.md).

For environments that cannot open VSTest sockets, the direct runner supports all
parameterless test fixtures and inline theory cases:

```bash
dotnet build Tests/MealPrepApp.RegressionRunner -c Release -m:1 -p:UseSharedCompilation=false
dotnet Tests/MealPrepApp.RegressionRunner/bin/Release/net10.0/MealPrepApp.RegressionRunner.dll --all
```

The optional `scripts/verify-android-cached.py` requires explicit `JAVA_HOME` (JDK 17),
`ANDROID_HOME` (platform 35), and the pinned dependencies already in Gradle caches.
It can use `MEALPREP_ANDROID_DEPENDENCY_RES` for a previously merged dependency-resource
cache. It independently checks current string references, runs Room/KSP, compiles the
current resources, Kotlin, JVM tests, and instrumentation sources, and runs JVM tests.
It does **not** assemble an APK, resolve a fresh Gradle dependency graph, or execute an
Android device test.

Verification includes the disposable SQL suite and 88 API checks. Android verification
includes 19 JVM tests, lint, a signed upgrade, Room migration with existing data, and
navigation checks in all three languages and both themes. Camera and process recovery
need separate device checks.
