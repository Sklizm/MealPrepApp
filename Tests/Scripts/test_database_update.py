"""Verify migration orchestration with a fake Docker CLI; never connects to a database."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

REPOSITORY = Path(__file__).resolve().parents[2]

DOCKER = """#!/usr/bin/env python3
import json, os, pathlib, sys
args = sys.argv[1:]
with open(os.environ['DOCKER_TEST_LOG'], 'a') as log:
    log.write(json.dumps(args) + '\\n')
if args[0] == 'inspect':
    print('true')
if any('BACKUP DATABASE' in arg for arg in args) and os.environ.get('FAIL_BACKUP'):
    sys.exit(42)
if args[0] == 'cp' and '.bak' in args[1]:
    if os.environ.get('FAIL_BACKUP_COPY'):
        sys.exit(43)
    pathlib.Path(args[2]).write_bytes(b'fake verified backup')
"""

class DatabaseUpdateTests(unittest.TestCase):
    def run_update(self, **flags):
        with tempfile.TemporaryDirectory(prefix="mealprep-migration-test-") as directory:
            root = Path(directory)
            (root / "scripts").mkdir()
            (root / "Database").mkdir()
            (root / "bin").mkdir()
            shutil.copyfile(REPOSITORY / "scripts/update-database.sh",
                            root / "scripts/update-database.sh")
            docker = root / "bin/docker"
            docker.write_text(DOCKER)
            docker.chmod(0o755)
            log = root / "docker.log"
            env = dict(os.environ, PATH=str(root / "bin") + os.pathsep + os.environ["PATH"],
                       DOCKER_TEST_LOG=str(log), **flags)
            result = subprocess.run(["bash", str(root / "scripts/update-database.sh")],
                                    env=env, capture_output=True, text=True)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            backups = list((root / "backups").glob("*.bak"))
            backup_modes = [backup.stat().st_mode & 0o777 for backup in backups]
            return result, calls, backup_modes

    @staticmethod
    def is_migration(call):
        return "migrations/20261028_compatibility.sql" in call

    def test_backup_and_host_copy_precede_migration(self):
        result, calls, modes = self.run_update()
        self.assertEqual(0, result.returncode, result.stderr)
        backup = next(i for i, call in enumerate(calls)
                      if any("RESTORE VERIFYONLY" in arg for arg in call))
        copied = next(i for i, call in enumerate(calls)
                      if call[0] == "cp" and ".bak" in call[1])
        migration = next(i for i, call in enumerate(calls) if self.is_migration(call))
        self.assertLess(backup, copied)
        self.assertLess(copied, migration)
        self.assertEqual([0o600], modes)

    def test_backup_failure_prevents_migration(self):
        result, calls, _ = self.run_update(FAIL_BACKUP="1")
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(any(self.is_migration(call) for call in calls))
        self.assertNotIn("Database update completed", result.stdout)

    def test_backup_copy_failure_prevents_migration(self):
        result, calls, _ = self.run_update(FAIL_BACKUP_COPY="1")
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(any(self.is_migration(call) for call in calls))

if __name__ == "__main__":
    unittest.main()
