# Moldova ingredient catalog

The catalog adds 189 generic cooking ingredients to the 44 starter entries (233 on
a fresh database). Research checked Moldovan retailer pages on 11 November 2026:
Linella, Kaufland Moldova, METRO Moldova, MegaAlina, Carneco, George Standard and
GustaPro. Each CSV row records its source URL, review date and evidence type.

This is a broad, curated starting catalog, not an exhaustive inventory or live stock
feed. A retailer listing or named assortment/category establishes local availability;
it does not guarantee stock at every branch. Seasonal fruit and vegetables remain
useful ingredients even outside their season. Imported foods sold locally are included.
Brands, pack sizes and most fruit cultivars are consolidated into generic ingredients.
Dry legumes, canned legumes, fresh produce and preserved foods have distinct names.
Existing starter ingredients and user entries are preserved, including tofu, which
has a dedicated Linella category. Further additions require local retail evidence.

Names follow the existing Romanian database naming convention. The app preserves
ingredient names as stored when switching interface languages. Categories retain
the existing translated identities. Solid additions use grams, liquids milliliters;
no unverified piece weights or density conversions are introduced.

[Reviewed nutrition references](NUTRITION.md) now cover 225 of the 233 catalog entries.
The nutrition import fills missing calorie and macro rows while preserving existing
values. Eight entries remain unresolved, with reasons recorded in the CSV. Additional
available nutrients are retained in the reference file. Do not interpret missing
values as zero or copy values between raw, cooked, dried and canned ingredients.

Edit `moldova_ingredients.csv`, then regenerate and validate:

```bash
python3 scripts/generate-moldova-catalog.py
python3 scripts/generate-moldova-catalog.py --check
python3 Tests/Scripts/test_database_update.py
bash scripts/test-database.sh
```

The generated seed inserts missing names only, resolves categories and units strictly,
and writes one UTC audit event per inserted ingredient in the same transaction. Repeat
imports preserve existing IDs, units, categories, nutrition and recipe/pantry links.
It does not rename, delete or recategorize existing ingredients with matching names.
`run_all.sql` includes the import for new databases. Existing databases use the
[verified-backup-first update procedure](../../docs/database.md#moldova-ingredient-catalog).

The disposable SQL regression checks insert auditing, repeated imports, preservation
of a colliding user ingredient and custom nutrition, and unknown nutrition handling.
Running the Python generator check validates provenance fields and generated content;
it does not execute SQL or recheck remote retailer stock.
