"""Check reviewed data integrity and meaningful import edge cases without a database."""
import csv
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('nutrition_generator', ROOT / 'scripts/generate-ingredient-nutrition.py')
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class NutritionCatalogTests(unittest.TestCase):
    def setUp(self):
        self.rows = generator.read_catalog()
        self.by_name = {r['name']: r for r in self.rows}

    def test_food_states_and_edible_basis(self):
        self.assertEqual('346', self.by_name['Hrisca']['calories'])
        self.assertEqual('100', self.by_name['Hrisca']['basis_quantity'])
        self.assertIn('dry', self.by_name['Hrisca']['source_description'])
        self.assertIn('raw', self.by_name['Naut uscat']['source_description'])
        self.assertIn('canned', self.by_name['Fasole alba conservata']['source_description'])
        self.assertIn('drained', self.by_name['Ton conservat']['source_description'])
        self.assertEqual('g', self.by_name['Suc de mere']['basis_unit'])
        self.assertIn('No g/ml conversion', self.by_name['Suc de mere']['notes'])

    def test_unknown_values_stay_missing(self):
        for name in ('Oase de porc', 'Leustean uscat'):
            self.assertEqual('needs_label', self.by_name[name]['status'])
            self.assertTrue(self.by_name[name]['notes'])
            self.assertTrue(all(self.by_name[name][k] == '' for k in generator.MACROS))
        self.assertEqual('reference', self.by_name['Apa plata']['status'])
        self.assertEqual('0', self.by_name['Apa plata']['calories'])

    def validate_modified(self, change):
        change(self.rows)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'nutrition.csv'
            with path.open('w', newline='') as stream:
                writer = csv.DictWriter(stream, fieldnames=list(self.rows[0]))
                writer.writeheader()
                writer.writerows(self.rows)
            with patch.object(generator, 'SOURCE', path):
                generator.read_catalog()

    def test_duplicate_or_missing_review_is_rejected(self):
        with self.assertRaises(ValueError):
            self.validate_modified(lambda rows: rows.append(dict(rows[0])))

    def test_unknown_cannot_be_zero_filled(self):
        with self.assertRaises(ValueError):
            self.validate_modified(lambda rows: self.by_name['Oase de porc'].update(calories='0'))

    def test_nonfinite_and_impossible_nutrients_are_rejected(self):
        with self.assertRaises(ValueError):
            self.validate_modified(lambda rows: rows[0].update(calories='NaN'))
        self.setUp()
        with self.assertRaises(ValueError):
            self.validate_modified(lambda rows: rows[0].update(saturated_fat_g='101'))

    def test_generated_sql_is_current(self):
        self.assertEqual(generator.OUTPUT.read_text(), generator.generate(self.rows))


if __name__ == '__main__':
    unittest.main()
