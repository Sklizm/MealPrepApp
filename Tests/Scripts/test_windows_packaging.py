#!/usr/bin/env python3
"""Check installer source contracts without pretending to run the Windows compiler."""
from pathlib import Path
import importlib.util
import json
import os
import re
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('inno_probe', ROOT / 'scripts/check-inno-setup.py')
inno_probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inno_probe)


class WindowsPackagingTests(unittest.TestCase):
    @unittest.skipIf(os.name == 'nt', 'The portable process harness uses a Unix executable fixture.')
    def test_probe_cli_runs_process_with_spaces_and_propagates_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            compiler = Path(directory) / 'compiler with spaces' / 'ISCC.exe'
            compiler.parent.mkdir()
            def write_compiler(version, exit_code):
                compiler.write_text('#!/usr/bin/env python3\n'
                    'from pathlib import Path\nimport sys\n'
                    'assert sys.argv[1] == "/O-"\n'
                    'assert "Output=no" in Path(sys.argv[2]).read_text()\n'
                    f'print("Compiler engine version: Inno Setup {version}")\n'
                    f'sys.exit({exit_code})\n')
                compiler.chmod(0o755)
            for version, native_exit, success in [('6.4.3', 0, True), ('6.4.30', 0, False), ('6.4.3', 2, False)]:
                write_compiler(version, native_exit)
                result = subprocess.run([sys.executable, str(ROOT / 'scripts/check-inno-setup.py'), str(compiler)],
                                        capture_output=True, text=True)
                self.assertEqual(result.returncode == 0, success, result.stdout + result.stderr)

    def test_compiler_probe_uses_loaded_engine_instead_of_launcher_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            compiler = Path(directory) / 'tool with spaces' / 'ISCC.exe'
            compiler.parent.mkdir()
            compiler.touch()
            probes = []
            def run(args, **options):
                self.assertEqual(args[:2], [str(compiler.resolve()), '/O-'])
                probe = Path(args[2])
                probes.append(probe)
                self.assertIn('Output=no', probe.read_text())
                self.assertIn('AppName={#ProbeAppName}', probe.read_text())
                self.assertTrue(options['capture_output'])
                return subprocess.CompletedProcess(args, 0,
                    'Launcher metadata: 0.0.0\r\nCompiler engine version: Inno Setup 6.4.3\r\n', '')
            with patch.object(inno_probe.subprocess, 'run', side_effect=run):
                self.assertEqual(inno_probe.verify_compiler(compiler, '6.4.3'), '6.4.3')
            self.assertTrue(probes)
            self.assertFalse(probes[0].exists())

    def test_compiler_probe_rejects_wrong_or_missing_engine_version(self):
        with tempfile.TemporaryDirectory() as directory:
            compiler = Path(directory) / 'ISCC.exe'
            compiler.touch()
            for output in ['Compiler engine version: Inno Setup 6.4.30\n', 'Compiler engine version: Inno Setup 6.5.4\n', 'Launcher metadata: 6.4.3\n']:
                with self.subTest(output=output), patch.object(inno_probe.subprocess, 'run',
                        return_value=subprocess.CompletedProcess([], 0, output, '')):
                    with self.assertRaises(ValueError): inno_probe.verify_compiler(compiler, '6.4.3')

    def test_compiler_probe_rejects_failed_compile_even_with_correct_version(self):
        with tempfile.TemporaryDirectory() as directory:
            compiler = Path(directory) / 'ISCC.exe'
            compiler.touch()
            result = subprocess.CompletedProcess([], 2,
                'Compiler engine version: Inno Setup 6.4.3\n', 'Preprocessor failed')
            with patch.object(inno_probe.subprocess, 'run', return_value=result):
                with self.assertRaisesRegex(ValueError, 'Preprocessor failed'):
                    inno_probe.verify_compiler(compiler, '6.4.3')

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
