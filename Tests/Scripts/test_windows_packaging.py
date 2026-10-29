#!/usr/bin/env python3
"""Check installer source contracts without pretending to run the Windows compiler."""
from pathlib import Path
import json
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]


class WindowsPackagingTests(unittest.TestCase):
    def test_compiler_pin_identifies_a_versioned_official_asset_and_checksum(self):
        tool = json.loads((ROOT / 'packaging/windows/inno-setup.json').read_text())
        self.assertRegex(tool['version'], r'^\d+\.\d+\.\d+$')
        tag = 'is-' + tool['version'].replace('.', '_')
        self.assertEqual(tool['releaseUrl'], f'https://github.com/jrsoftware/issrc/releases/tag/{tag}')
        self.assertEqual(tool['downloadUrl'],
                         f'https://github.com/jrsoftware/issrc/releases/download/{tag}/innosetup-{tool["version"]}.exe')
        self.assertRegex(tool['sha256'], r'^[0-9a-f]{64}$')

    def test_installer_preprocessor_guards_are_balanced_and_cover_build_inputs(self):
        source = (ROOT / 'packaging/windows/MealPrepApp.iss').read_text()
        guards, active = set(), []
        for line in source.splitlines():
            stripped = line.strip()
            if stripped.startswith(('#ifdef ', '#ifndef ', '#if ')):
                active.append(stripped)
                if stripped.startswith('#ifndef '): guards.add(stripped.split()[1])
            elif stripped == '#endif':
                self.assertTrue(active, 'Unexpected #endif (possibly a guard swallowed by a comment)')
                active.pop()
            elif stripped.startswith('#error '):
                self.assertTrue(active, '#error must be conditional')
        self.assertFalse(active, 'Unclosed installer preprocessor guards')
        used_macros = set(re.findall(r'\{#(\w+)\}', source))
        self.assertEqual(used_macros, guards)
        build = (ROOT / 'scripts/build-windows.ps1').read_text()
        supplied = set(re.findall(r'/D(\w+)=', build))
        self.assertEqual(used_macros, supplied)

    def test_installer_and_application_use_the_same_running_mutex(self):
        source = (ROOT / 'packaging/windows/MealPrepApp.iss').read_text()
        program = (ROOT / 'App/MealPrepApp.Desktop/Program.cs').read_text()
        installer_mutex = re.search(r'^AppMutex=(.+)$', source, re.M).group(1)
        application_mutex = re.search(r'new Mutex\(false, "([^"]+)"\)', program).group(1)
        self.assertEqual(installer_mutex, application_mutex)


if __name__ == '__main__':
    unittest.main()
