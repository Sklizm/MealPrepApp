#!/usr/bin/env python3
"""Check APK provenance and draft-release boundaries without contacting GitHub."""
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('candidate', ROOT / 'scripts/create-release-candidate.py')
candidate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(candidate)


class ReleaseCandidateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        gradle = self.root / candidate.ANDROID / 'app/build.gradle.kts'
        gradle.parent.mkdir(parents=True)
        gradle.write_text('versionName = "1.0.1"\nversionCode = 2\n')
        self.apk = self.root / 'signed.apk'
        self.apk.write_bytes(b'fixture APK bytes')
        self.metadata = self.root / 'private-build-record.json'
        self.source = 'a' * 40
        self.release = 'b' * 40
        self.tree = 'c' * 40
        self.certificate = 'd' * 64
        self.record = {'package': candidate.PACKAGE, 'version': '1.0.1', 'versionCode': 2,
                       'sourceCommit': self.source, 'apkSha256': candidate.digest(self.apk),
                       'signerCertificateSha256': self.certificate,
                       'privateBuildNotes': 'must never leave the host'}
        self.save_record()
        self.addCleanup(patch.stopall)
        patch.object(candidate, 'ROOT', self.root).start()
        self.tool = patch.object(candidate, 'command', side_effect=self.command).start()

    def save_record(self):
        self.metadata.write_text(json.dumps(self.record))

    def command(self, *args):
        if args[:3] == ('git', 'rev-parse', 'HEAD'):
            return self.release
        if args[:2] == ('git', 'rev-parse'):
            return self.tree
        if args[:2] == ('git', 'status'):
            return ''
        if 'verify' in args:
            return f'Signer #1 certificate SHA-256 digest: {self.certificate}'
        if 'badging' in args:
            return f"package: name='{candidate.PACKAGE}' versionCode='2' versionName='1.0.1'"
        raise AssertionError(f'Unexpected command: {args}')

    def verify(self):
        return candidate.verify_android(self.apk, self.metadata, self.root)

    def test_verified_apk_keeps_original_source_and_omits_private_notes(self):
        record = self.verify()
        self.assertEqual(record['sourceCommit'], self.source)
        self.assertEqual(record['releaseCommit'], self.release)
        self.assertEqual(record['androidSourceTree'], self.tree)
        self.assertTrue(record['signed'])
        self.assertNotIn('privateBuildNotes', record)
        self.assertNotIn('must never leave the host', json.dumps(record))

    def test_changed_apk_is_rejected_before_signing_tools_run(self):
        self.apk.write_bytes(b'changed APK')
        with self.assertRaisesRegex(ValueError, 'hash'):
            self.verify()
        self.tool.assert_not_called()

    def test_changed_android_source_requires_rebuild(self):
        self.tool.side_effect = lambda *args: 'e' * 40 if args == ('git', 'rev-parse', f'HEAD:{candidate.ANDROID}') else self.command(*args)
        with self.assertRaisesRegex(ValueError, 'source has changed'):
            self.verify()

    def test_different_signing_identity_is_rejected(self):
        self.record['signerCertificateSha256'] = 'e' * 64
        self.save_record()
        with self.assertRaisesRegex(ValueError, 'signing certificate'):
            self.verify()

    def test_wrong_package_or_version_is_rejected(self):
        self.tool.side_effect = lambda *args: "package: name='other.app' versionCode='2' versionName='1.0.1'" if 'badging' in args else self.command(*args)
        with self.assertRaisesRegex(ValueError, 'package/version'):
            self.verify()

    def test_apk_must_match_current_source_version(self):
        (self.root / candidate.ANDROID / 'app/build.gradle.kts').write_text('versionName = "1.0.2"\nversionCode = 3\n')
        with self.assertRaisesRegex(ValueError, 'selected Android source'):
            self.verify()

    def test_published_release_or_wrong_tag_commit_cannot_be_overwritten(self):
        candidate.require_draft({'isDraft': True, 'tagName': 'v1'}, 'v1', self.release, self.release)
        for record, tag_commit in [({'isDraft': False, 'tagName': 'v1'}, self.release),
                                   ({'isDraft': True, 'tagName': 'v2'}, self.release),
                                   ({'isDraft': True, 'tagName': 'v1'}, self.source)]:
            with self.subTest(record=record, tag_commit=tag_commit):
                with self.assertRaisesRegex(ValueError, 'unpublished draft'):
                    candidate.require_draft(record, 'v1', tag_commit, self.release)

    def desktop_manifest(self):
        records = []
        for platform in ['linux-x64', 'macos-arm64', 'windows-x64']:
            asset = self.root / f'{platform}.fixture'
            asset.write_bytes(platform.encode())
            records.append({'platform': platform, 'version': '1.0.0-rc.2',
                            'sourceCommit': self.release, 'file': asset.name,
                            'sha256': candidate.digest(asset)})
        (self.root / 'release-manifest.json').write_text(json.dumps(records))

    def test_all_four_installable_assets_have_checksums_and_separate_versions(self):
        self.desktop_manifest()
        candidate.add_android(self.root, self.apk, self.verify(), '1.0.0-rc.2')
        records = json.loads((self.root / 'release-manifest.json').read_text())
        self.assertEqual(len(records), 4)
        self.assertEqual(len((self.root / 'SHA256SUMS').read_text().splitlines()), 4)
        android = records[-1]
        self.assertEqual(android['version'], '1.0.1')
        self.assertEqual(android['releaseVersion'], '1.0.0-rc.2')
        self.assertEqual(candidate.digest(self.root / android['file']), android['sha256'])

    def test_asset_tampering_after_verification_stops_assembly(self):
        for changed in ['linux-x64.fixture', 'signed.apk']:
            with self.subTest(changed=changed):
                self.apk.write_bytes(b'fixture APK bytes')
                self.desktop_manifest()
                record = self.verify()
                (self.root / changed).write_bytes(b'tampered')
                with self.assertRaisesRegex(ValueError, 'changed after verification'):
                    candidate.add_android(self.root, self.apk, record, '1.0.0-rc.2')

    def test_full_release_selects_successful_exact_commit_and_verifies_all_uploads(self):
        version = '1.0.0-rc.2'
        (self.root / 'release.json').write_text(json.dumps({'version': version}))
        notes = self.root / f'docs/releases/v{version}.md'
        notes.parent.mkdir(parents=True)
        notes.write_text('Release candidate fixture')
        remote = self.root / 'remote-assets'
        remote.mkdir()
        run = {'databaseId': 43, 'headSha': self.release, 'status': 'completed',
               'conclusion': 'success', 'url': 'https://example.invalid/run/43'}
        draft = {'tagName': f'v{version}', 'isDraft': True, 'url': 'https://example.invalid/draft'}

        def command(*args):
            if args[:3] == ('git', 'branch', '--show-current'):
                return 'main'
            if args[0] != 'gh':
                return self.command(*args)
            if args[1:3] == ('repo', 'view'):
                return 'fixture/mealprep'
            if args[1] == 'api':
                if args[2].endswith('/git/refs'):
                    self.assertIn(f'sha={self.release}', args)
                    return '{}'
                return self.release
            if args[1:3] == ('release', 'list'):
                return '[]'
            if args[1:3] == ('run', 'list'):
                return json.dumps([{**run, 'databaseId': 44, 'conclusion': 'failure'}, run])
            if args[1:3] == ('run', 'view'):
                self.assertEqual(args[3], '43')
                return json.dumps(run)
            if args[1:3] == ('release', 'view'):
                return json.dumps(draft)
            raise AssertionError(args)

        installer = f'MealPrepApp-{version}-windows-x64-setup.exe'
        archives = {'linux-x64': 'MealPrepApp-linux-x64.tar.gz', 'macos-arm64': 'MealPrepApp-macos-arm64.tar.gz'}

        def execute(*args):
            if args[1:3] == ('run', 'download'):
                self.assertEqual(args[3], '43')
                folder = Path(args[args.index('--dir') + 1]) / 'package-fixture'
                folder.mkdir()
                for name in [*archives.values(), installer, installer + '.metadata.json']:
                    (folder / name).write_text(name)
            elif args[1:3] == ('release', 'create'):
                self.assertIn('--draft', args)
                self.assertIn('--verify-tag', args)
                for value in args:
                    if isinstance(value, Path) and value.is_file() and value.name != 'release-notes.md':
                        shutil.copyfile(value, remote / value.name)
            elif args[1:3] == ('release', 'download'):
                folder = Path(args[args.index('--dir') + 1])
                for asset in remote.iterdir():
                    shutil.copyfile(asset, folder / asset.name)
            else:
                raise AssertionError(args)

        def finalize(output):
            records = [{'platform': platform, 'version': version, 'sourceCommit': self.release,
                        'file': name, 'sha256': candidate.digest(output / name)}
                       for platform, name in {**archives, 'windows-x64': installer}.items()]
            (output / 'release-manifest.json').write_text(json.dumps(records))

        self.tool.side_effect = command
        packager = SimpleNamespace(ARCHIVES=archives, installer_name=lambda: installer, finalize=finalize)
        argv = ['candidate', '--apk', str(self.apk), '--android-metadata', str(self.metadata),
                '--android-build-tools', str(self.root)]
        with patch.object(sys, 'argv', argv), patch.object(candidate, 'load_packager', return_value=packager), \
                patch.object(candidate, 'execute', side_effect=execute), \
                patch.object(candidate.subprocess, 'run', return_value=subprocess.CompletedProcess([], 1, '', 'HTTP 404')):
            candidate.main()
        self.assertEqual(len(list(remote.iterdir())), 8)
        self.assertEqual(len((remote / 'SHA256SUMS').read_text().splitlines()), 4)


if __name__ == '__main__':
    unittest.main()
