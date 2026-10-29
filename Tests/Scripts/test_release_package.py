#!/usr/bin/env python3
"""Release boundary checks: private files, permissions and artifact provenance."""
import importlib.util
import json
from pathlib import Path
import plistlib
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('release', ROOT / 'scripts/package-release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.folder = self.root / 'dist/linux'
        self.folder.mkdir(parents=True)
        self.exe = self.folder / 'MealPrepApp.Desktop'
        self.exe.write_text('fixture')
        self.exe.chmod(0o755)

    def test_rejects_private_files_and_directories(self):
        for name in ['appsettings.Local.json', '.env', 'backup.bak', 'key.pem', '.env.production', 'backups']:
            with self.subTest(name=name):
                path = self.folder / name
                path.write_text('private')
                with self.assertRaises(ValueError): release.validate(self.folder, self.exe)
                path.unlink()

    def test_requires_executable_and_rejects_links(self):
        self.exe.chmod(0o644)
        with self.assertRaises(ValueError): release.validate(self.folder, self.exe)
        self.exe.chmod(0o755)
        (self.folder / 'outside').symlink_to(self.root)
        with self.assertRaises(ValueError): release.validate(self.folder, self.exe)

    def test_archives_keep_permissions_and_bind_metadata_to_commit(self):
        config = {'version': '1.0.0-rc.1', 'requiredMigrations': ['compatibility', 'cooking']}
        (self.root / 'release.json').write_text(json.dumps(config))
        mac = self.root / 'dist/mac/MealPrepApp.app/Contents/MacOS'
        mac.mkdir(parents=True)
        (mac / self.exe.name).write_text('mac fixture')
        (mac / self.exe.name).chmod(0o755)
        output = self.root / 'archives'
        with patch.object(release, 'ROOT', self.root), patch.object(release.subprocess, 'check_output', return_value='a' * 40):
            for platform in release.ARCHIVES: release.package(platform, output)
            windows = self.root / 'dist/windows'
            windows.mkdir(parents=True)
            (windows / 'MealPrepApp.Desktop.exe').write_bytes(b'MZ fixture')
            release.prepare_windows()
            (output / release.installer_name()).write_bytes(b'MZ installer fixture')
            release.package('windows-x64', output)
            release.finalize(output)
            records = json.loads((output / 'release-manifest.json').read_text())
            self.assertEqual(len(records), 3)
            self.assertEqual(len((output / 'SHA256SUMS').read_text().splitlines()), 3)
            with tarfile.open(output / release.ARCHIVES['linux-x64']) as tar:
                self.assertEqual(tar.getmember('MealPrepApp-linux-x64/MealPrepApp.Desktop').mode & 0o777, 0o755)
            with patch.object(release.subprocess, 'check_output', return_value='b' * 40):
                with self.assertRaises(ValueError): release.finalize(output)
            release.package('linux-x64', output)
            self.assertFalse((output / 'SHA256SUMS').exists())
            self.assertFalse((output / 'release-manifest.json').exists())

    def test_windows_validation_rejects_private_payload_and_invalid_executables(self):
        windows = self.root / 'dist/windows'
        windows.mkdir(parents=True)
        exe = windows / 'MealPrepApp.Desktop.exe'
        exe.write_bytes(b'not an executable')
        with self.assertRaises(ValueError): release.validate(windows, exe, windows=True)
        exe.write_bytes(b'MZ fixture')
        release.validate(windows, exe, windows=True)
        (windows / 'appsettings.Local.json').write_text('private')
        with self.assertRaises(ValueError): release.validate(windows, exe, windows=True)

    def test_windows_installer_hash_and_source_must_match_metadata(self):
        (self.root / 'release.json').write_text(json.dumps({'version': '1.0.0-rc.1', 'requiredMigrations': []}))
        windows = self.root / 'dist/windows'
        windows.mkdir(parents=True)
        (windows / 'MealPrepApp.Desktop.exe').write_bytes(b'MZ fixture')
        output = self.root / 'release'
        output.mkdir()
        with patch.object(release, 'ROOT', self.root), patch.object(release.subprocess, 'check_output', return_value='a' * 40):
            release.prepare_windows()
            for platform in release.ARCHIVES:
                if platform == 'macos-arm64':
                    mac = self.root / 'dist/mac/MealPrepApp.app/Contents/MacOS'
                    mac.mkdir(parents=True)
                    exe = mac / 'MealPrepApp.Desktop'
                    exe.write_text('fixture')
                    exe.chmod(0o755)
                release.package(platform, output)
            installer = output / release.installer_name()
            installer.write_bytes(b'MZ fixture')
            release.package('windows-x64', output)
            installer.write_bytes(b'MZ tampered')
            with self.assertRaises(ValueError): release.finalize(output)
            with patch.object(release.subprocess, 'check_output', return_value='b' * 40):
                with self.assertRaises(ValueError): release.package('windows-x64', output)

    def test_repository_versions_are_consistent(self):
        config = json.loads((ROOT / 'release.json').read_text())
        props = ET.parse(ROOT / 'App/MealPrepApp.Desktop/Version.props')
        self.assertEqual(props.findtext('.//Version'), config['version'])
        with (ROOT / 'App/MealPrepApp.Desktop/Assets/Info.plist').open('rb') as stream:
            plist = plistlib.load(stream)
        self.assertEqual(plist['CFBundleShortVersionString'], config['version'].split('-')[0])
        self.assertEqual(plist['CFBundleVersion'], config['macBundleVersion'])


if __name__ == '__main__': unittest.main()
