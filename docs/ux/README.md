# Inventory and workflow upgrade

The additive inventory foundation introduces dated purchase batches, audited stock
corrections, version checks and operation IDs while retaining the legacy pantry
procedures and combined totals. Existing quantities migrate into undated batches;
known timestamps remain timestamps. No purchase, expiry or waste dates are inferred.

Readiness uses selected servings and explicitly supported compatible-unit conversions.
Expired batches are excluded by the client's calendar date. Unknown expiry remains
eligible. Incompatible stock quantities require review rather than a density guess.
Readiness ranks the complete filtered result before pagination. Consumption proposals
use earliest expiry first, with undated batches last, and do not change stock.

Every inventory mutation has a client operation ID. A retry returns the original
result; reuse with a different payload fails. Corrections require the observed batch
version. Undo is a compensating event and fails after subsequent use of that batch.

## Shopping

Shopping defaults to remaining groceries and offers week shortcuts. Saved lists,
custom dates, exports and conversion notes are available in secondary controls.
Checklist changes update immediately and save individually without replacing rows.
Failed changes retain a Retry action; version conflicts require an explicit choice
to discard the local change and load the server copy.

Manual groceries work without catalog mappings. Pantry import requires an ingredient
and unit. Required quantities remain separate from purchased quantities: a purchased
litre is imported as a litre even when the plan requires 200 ml. Single and bulk
imports show a review and remain idempotent. Imported quantities cannot be rewritten;
use an explicit stock correction. Deleted shopping records remain as sync tombstones.

## Cooking and prepared portions

Cooking opens the current numbered step with Previous/Next navigation and a
collapsible ingredient checklist. Named timers support pause/resume and adding a
minute. Local account-scoped recovery saves the recipe, servings, step, checks and
timer deadlines. Alarms require the app to remain running; missed deadlines are
shown after recovery. Native notifications and sounds are best effort, with an
in-app fallback when unavailable.

Finish cooking opens an explicit review of actual production, ingredient deductions,
consumption and leftover expiry. Deductions propose eligible batches in expiry order.
No checkmark changes stock. A batch can supply several planned meals, generating raw
ingredient demand once. Cooking records recipe/nutrition snapshots. Consuming prepared
portions never deducts raw stock, and reservations protect portions allocated to meals.
Reports can distinguish planned, cooked, consumed and explicitly discarded activity.

## Database upgrade

On a database host, after the disposable suite passes:

```bash
bash scripts/test-database.sh
bash scripts/update-database.sh mealprep-db 20261111_inventory.sql
bash scripts/update-database.sh mealprep-db 20261111_shopping.sql
bash scripts/update-database.sh mealprep-db 20261111_batch_meals.sql
```

Apply the existing compatibility and cooking-redesign migrations first when needed.
The updater takes and verifies a copy-only checksum backup before applying changes.
Client installers never migrate the database.

## Verification scope

`Tests/Database/inventory-regression.sql` covers precision, compatibility totals,
operation retries, ownership, stale versions, undo and readiness ordering. The SQL
script also reconstructs predecessor inventory storage in a disposable container
and verifies quantities, known timestamps and unknown dates across repeated upgrades.
`InventoryTests` covers scaling, duplicate ingredient demands, compatible and
incompatible units, mixed expiry and consumption-review ordering.

These checks do not establish native desktop, physical Android or live deployment
acceptance. The wider desktop, cooking, batch planning and companion upgrade is still
in development.
