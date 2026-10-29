#!/usr/bin/env python3
"""Verify the loaded compiler engine and preprocessor with an output-disabled compile."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
PROBE = r'''#define ProbeAppName "MealPrep compiler probe"
[Setup]
AppName={#ProbeAppName}
AppVersion=0.0.0
DefaultDirName={localappdata}\MealPrepCompilerProbe
PrivilegesRequired=lowest
Uninstallable=no
Output=no
'''


def verify_compiler(compiler, expected_version):
    compiler = Path(compiler).resolve()
    if not compiler.is_file():
        raise ValueError(f'Compiler not found: {compiler}')
    with tempfile.TemporaryDirectory(prefix='mealprep-iscc-') as directory:
        probe = Path(directory) / 'probe.iss'
        probe.write_text(PROBE, encoding='utf-8')
        result = subprocess.run([str(compiler), '/O-', str(probe)], capture_output=True,
                                text=True, encoding='utf-8', errors='replace', timeout=60)
        output = result.stdout + '\n' + result.stderr
        if result.returncode != 0:
            raise ValueError(f'Compiler probe failed (exit {result.returncode}):\n{output}')
        version = re.search(r'^Compiler engine version:[^\r\n]*?\b(\d+\.\d+\.\d+)\b', output, re.MULTILINE)
        if not version:
            raise ValueError(f'Compiler did not identify its loaded engine:\n{output}')
        actual = version.group(1)
        if actual != expected_version:
            raise ValueError(f'Expected Inno Setup {expected_version}; loaded compiler engine is {actual}')
        return actual


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('compiler', type=Path)
    args = parser.parse_args()
    expected = json.loads((ROOT / 'packaging/windows/inno-setup.json').read_text())['version']
    try:
        actual = verify_compiler(args.compiler, expected)
    except (OSError, ValueError, subprocess.TimeoutExpired) as error:
        parser.exit(1, f'{error}\n')
    print(f'Inno Setup {actual}: compiler engine and preprocessor probe passed (output disabled).')
