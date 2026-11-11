# Inventory, cooking and recovery

## Stock and readiness

Pantry totals remain the default. Expand an ingredient to inspect purchase batches;
expiry and purchase dates are optional calendar dates. Existing stock migrates into
undated batches without inventing dates. Past-date stock stays visible for review
and does not count toward default readiness. Unknown expiry remains unknown.

Recipe readiness scales to the selected servings and considers actual available
quantities. It ranks before titles and pagination. Compatible units use the existing
conversion map; pieces, mass and volume never convert through an assumed density.
Inspect ingredient shortages or **Needs review** before cooking.

Catalog edits preserve ingredient identity and existing recipe quantities. Changing
a default unit affects future entry defaults. Batch corrections and removals are
explicit and audited. Undo uses a compensating operation with version checks; later
stock usage can make reversal unsafe.

## Shopping

Remaining groceries appear first. Week shortcuts, per-item saves and visible retries
support quick checkoffs without rebuilding the list. Saved history, custom date
ranges and exports are secondary controls. Required quantities stay separate from
actual purchased quantities. Manual text groceries can be unmapped until pantry
import, which requires catalog mapping and a reviewed quantity and unit.

Bulk pantry import is explicit and idempotent. Correct an already imported purchase
through a stock adjustment, rather than rewriting inventory through the checklist.
Neither shopping checkmarks nor cooking ingredient checkmarks deduct raw stock.

## Planned batches and leftovers

Plan one cooking batch and allocate its portions across several meals. Shopping
counts that batch's ingredients once. Allocations cannot exceed planned or actual
available portions. Copying a week or applying a template creates fresh planned
batches while preserving sharing within that copied week.

Cooking opens at the current numbered step. Ingredients collapse, servings and
checkmarks persist, and several named timers can run. Timers support pause, resume
and an extra minute. Native notifications have an in-app fallback. The desktop app
must remain running for alarms; saved deadlines allow recovery after restarting.

**Finish cooking** reviews actual production, ingredients to deduct, consumed
portions and remaining prepared stock. Eligible batches are suggested in expiry
order; the review never silently discards or deducts stock. Actual production stores
a recipe/nutrition snapshot. Later leftover consumption does not deduct raw stock
again. Explicit discard events record waste; plans alone never establish that food
was cooked, consumed or wasted.

Home opens today's recipes, starts cooking with planned servings, and offers
prepared-portion consumption. Reports distinguish planned, cooked, consumed and
discarded activity, upcoming shortages, repetition and nutrition completeness.

## Editing and imports

Choose and preview a photo in the recipe editor. Recipe content and its photo save
consistently. Inline ingredient creation retains the current recipe. Local recovery
drafts save after a short idle interval, including unresolved text and selected
photos. Restore a draft explicitly; it does not replace the published recipe.

Paste a recipe, an ingredient list, or an HTTPS page containing structured Recipe
data. Deterministic parsing suggests existing ingredients and units in a review
screen, preserves the source, and leaves ambiguous quantities unresolved. Confirm
before publishing. Fractions and regional decimal notation are supported; ambiguous
separators and ranges require review. Unsupported sites fall back to paste. Requests
are bounded and internal-network destinations are rejected.

Recovery files are private to the desktop account and database connection. They
remain local to that installation. Device transfers require separately saving the
published recipe or a deliberate secure transfer of private application data.
