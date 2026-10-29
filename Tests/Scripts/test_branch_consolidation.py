#!/usr/bin/env python3
"""Exercise destructive cleanup against disposable local repositories, never GitHub."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class BranchConsolidationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.remote = self.root / 'origin.git'
        self.repo = self.root / 'checkout'
        self.repo.mkdir()
        self.git('init', '--bare', '--initial-branch=main', str(self.remote))
        self.git('init', '--initial-branch=main')
        self.git('config', 'user.name', 'Fixture')
        self.git('config', 'user.email', 'fixture@example.invalid')
        (self.repo / 'scripts').mkdir()
        shutil.copy(ROOT / 'scripts/consolidate-main.sh', self.repo / 'scripts')
        self.git('add', '.')
        self.git('commit', '-m', 'fixture')
        self.git('remote', 'add', 'origin', str(self.remote))
        self.git('push', '-u', 'origin', 'main')
        self.git('branch', 'old-topic')
        self.git('push', 'origin', 'old-topic')
        self.git('remote', 'set-head', 'origin', 'main')
        self.bin = self.root / 'bin'
        self.bin.mkdir()
        gh = self.bin / 'gh'
        gh.write_text('''#!/usr/bin/env python3
import os, subprocess, sys
if sys.argv[1:3] == ['run', 'list']:
    if os.environ.get('FIXTURE_CI', 'success') != 'success': sys.exit(0)
    if os.environ.get('FIXTURE_MOVE_BRANCH'):
        subprocess.run(['git', '--git-dir=' + os.environ['FIXTURE_REMOTE'], 'update-ref', 'refs/heads/old-topic', os.environ['FIXTURE_MOVE_BRANCH']], check=True)
    print(subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip())
elif sys.argv[1:] == ['repo', 'edit', '--default-branch', 'main']:
    pass
else:
    sys.exit(1)
''')
        gh.chmod(0o755)
        self.env = {**os.environ, 'PATH': str(self.bin) + os.pathsep + os.environ['PATH'],
                    'FIXTURE_REMOTE': str(self.remote)}

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.repo, stderr=subprocess.STDOUT, text=True).strip()

    def cleanup(self):
        return subprocess.run(['bash', 'scripts/consolidate-main.sh'], cwd=self.repo, env=self.env,
                              text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

    def remote_branches(self):
        return self.git('--git-dir=' + str(self.remote), 'for-each-ref', '--format=%(refname:short)', 'refs/heads/').splitlines()

    def test_deletes_only_merged_branches_after_verified_ci(self):
        sha = self.git('rev-parse', 'HEAD')
        result = self.cleanup()
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertEqual(self.remote_branches(), ['main'])
        self.assertEqual(self.git('branch', '--format=%(refname:short)'), 'main')
        self.assertEqual(self.git('rev-parse', 'HEAD'), sha)

    def test_unmerged_remote_branch_blocks_all_deletion(self):
        self.git('switch', '-c', 'unique')
        (self.repo / 'unique.txt').write_text('unmerged work')
        self.git('add', '.')
        self.git('commit', '-m', 'unique work')
        self.git('push', 'origin', 'unique')
        self.git('switch', 'main')
        result = self.cleanup()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Unmerged remote branch', result.stdout)
        self.assertEqual(self.remote_branches(), ['main', 'old-topic', 'unique'])

    def test_missing_ci_blocks_deletion(self):
        self.env['FIXTURE_CI'] = 'pending'
        result = self.cleanup()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Successful Compatibility CI is required', result.stdout)
        self.assertEqual(self.remote_branches(), ['main', 'old-topic'])

    def test_branch_that_moves_after_audit_is_preserved(self):
        new_sha = self.git('commit-tree', 'HEAD^{tree}', '-p', 'HEAD', '-m', 'new remote work')
        self.git('push', 'origin', new_sha + ':refs/fixtures/new-work')
        self.env['FIXTURE_MOVE_BRANCH'] = new_sha
        result = self.cleanup()
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(self.remote_branches(), ['main', 'old-topic'])
        self.assertEqual(self.git('--git-dir=' + str(self.remote), 'rev-parse', 'old-topic'), new_sha)


if __name__ == '__main__':
    unittest.main()
