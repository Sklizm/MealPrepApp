#!/usr/bin/env python3
"""Reject tracked paths that cannot be checked out consistently on Windows."""
from pathlib import PurePosixPath
import subprocess


def check_paths(paths):
    errors = []
    prefixes = {}
    reserved = {'CON', 'PRN', 'AUX', 'NUL'} | {
        f'{prefix}{digit}' for prefix in ('COM', 'LPT') for digit in '123456789¹²³'
    }
    for path in paths:
        parts = PurePosixPath(path).parts
        for index, part in enumerate(parts):
            if (any(char in '<>:"\\|?*' or ord(char) < 32 for char in part)
                    or part.endswith((' ', '.'))
                    or part.split('.')[0].rstrip(' ').upper() in reserved):
                errors.append(f'Windows-incompatible path: {path}')
                break
            prefix = '/'.join(parts[:index + 1])
            previous = prefixes.setdefault(prefix.casefold(), prefix)
            if previous != prefix:
                errors.append(f'Case-colliding paths: {previous} and {prefix}')
    return sorted(set(errors))


if __name__ == '__main__':
    tracked = subprocess.check_output(['git', 'ls-files', '-z']).decode('utf-8').split('\0')
    paths = [path for path in tracked if path]
    errors = check_paths(paths)
    if errors:
        raise SystemExit('\n'.join(errors))
    print(f'{len(paths)} tracked paths are compatible with Windows checkout.')
