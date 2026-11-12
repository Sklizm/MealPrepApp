#!/usr/bin/env python3
"""Exercise the API test launcher without Docker, network access, or a live database."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class ApiRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        scripts = self.root / 'scripts'
        scripts.mkdir()
        shutil.copy(ROOT / 'scripts/test-api.sh', scripts)
        binary = self.root / 'bin'
        binary.mkdir()
        fake = binary / 'fixture'
        fake.write_text('''#!/usr/bin/env python3
import os
from pathlib import Path
import sys

tool = Path(sys.argv[0]).name
command = sys.argv[1]
root = Path(os.environ['FIXTURE_ROOT'])
with (root / 'calls').open('a') as log:
    log.write(f'{tool} {command}\\n')
if tool == 'dotnet':
    if '-c' not in sys.argv or sys.argv[sys.argv.index('-c') + 1] != 'Release':
        sys.exit(90)
    if command == 'build':
        if os.environ.get('FIXTURE_BUILD_FAIL'):
            sys.exit(23)
        (root / 'built').touch()
    elif command == 'run':
        if not (root / 'built').exists():
            sys.exit(91)
        sys.exit(int(os.environ.get('FIXTURE_CHECK_EXIT', '0')))
    else:
        sys.exit(92)
elif tool == 'docker':
    if command == 'run':
        print('disposable-fixture')
    elif command == 'port':
        print('127.0.0.1:31433')
    elif command not in ('exec', 'rm'):
        sys.exit(93)
''')
        fake.chmod(0o755)
        for tool in ('dotnet', 'docker'):
            (binary / tool).symlink_to(fake)
        self.env = {**os.environ, 'PATH': str(binary) + os.pathsep + os.environ['PATH'],
                    'FIXTURE_ROOT': str(self.root)}

    def run_script(self):
        result = subprocess.run(['bash', str(self.root / 'scripts/test-api.sh')],
                                cwd=self.root, env=self.env, text=True,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        return result, (self.root / 'calls').read_text().splitlines()

    def test_clean_checkout_builds_before_starting_disposable_database(self):
        result, calls = self.run_script()
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertLess(calls.index('dotnet build'), calls.index('docker run'))
        self.assertIn('dotnet run', calls)
        self.assertEqual(calls[-1], 'docker rm')

    def test_build_failure_exits_without_starting_database(self):
        self.env['FIXTURE_BUILD_FAIL'] = '1'
        result, calls = self.run_script()
        self.assertEqual(result.returncode, 23, result.stdout)
        self.assertEqual(calls, ['dotnet build'])

    def test_failed_checks_propagate_failure_and_remove_disposable_database(self):
        self.env['FIXTURE_CHECK_EXIT'] = '24'
        result, calls = self.run_script()
        self.assertEqual(result.returncode, 24, result.stdout)
        self.assertEqual(calls[-2:], ['dotnet run', 'docker rm'])


if __name__ == '__main__':
    unittest.main()
