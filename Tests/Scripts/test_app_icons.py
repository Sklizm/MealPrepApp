#!/usr/bin/env python3
"""Verify icon containers and exercise platform bundle/launcher installation wiring."""
import os
from pathlib import Path
import plistlib
import shutil
import struct
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zlib

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'App/MealPrepApp.Desktop/Assets'
ICONS = ASSETS / 'Icons'


def png_size(data):
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    position = 8
    dimensions = None
    compressed = b''
    while position < len(data):
        size = struct.unpack_from('>I', data, position)[0]
        kind = data[position + 4:position + 8]
        payload = data[position + 8:position + 8 + size]
        crc = struct.unpack_from('>I', data, position + 8 + size)[0]
        assert zlib.crc32(kind + payload) & 0xffffffff == crc
        if kind == b'IHDR':
            dimensions = struct.unpack_from('>II', payload)
            assert payload[8:10] == bytes([8, 6]), 'Expected 8-bit RGBA with transparency'
        if kind == b'IDAT': compressed += payload
        position += size + 12
        if kind == b'IEND': break
    assert position == len(data) and dimensions
    width, height = dimensions
    assert len(zlib.decompress(compressed)) == (1 + width * 4) * height
    return dimensions


class AppIconTests(unittest.TestCase):
    def test_platform_containers_have_decodable_small_and_retina_representations(self):
        self.assertEqual(png_size((ICONS / 'app-icon.png').read_bytes()), (1024, 1024))
        self.assertEqual(png_size((ICONS / 'app-icon-512.png').read_bytes()), (512, 512))
        ico = (ICONS / 'app-icon.ico').read_bytes()
        reserved, kind, count = struct.unpack_from('<HHH', ico)
        self.assertEqual((reserved, kind, count), (0, 1, 7))
        sizes = []
        for index in range(count):
            width, height, _, _, planes, bits, length, offset = struct.unpack_from('<BBBBHHII', ico, 6 + 16 * index)
            expected = (width or 256, height or 256)
            self.assertEqual((planes, bits), (1, 32))
            self.assertEqual(png_size(ico[offset:offset + length]), expected)
            sizes.append(expected[0])
        self.assertEqual(sizes, [16, 24, 32, 48, 64, 128, 256])
        icns = (ICONS / 'app-icon.icns').read_bytes()
        self.assertEqual(icns[:4], b'icns')
        self.assertEqual(struct.unpack_from('>I', icns, 4)[0], len(icns))
        entries, offset = {}, 8
        while offset < len(icns):
            kind, length = struct.unpack_from('>4sI', icns, offset)
            self.assertGreater(length, 8)
            entries[kind.decode()] = png_size(icns[offset + 8:offset + length])
            offset += length
        self.assertEqual(offset, len(icns))
        self.assertEqual(entries, {kind: (size, size) for kind, size in {
            'icp4': 16, 'icp5': 32, 'icp6': 64, 'ic07': 128, 'ic08': 256, 'ic09': 512,
            'ic10': 1024, 'ic11': 32, 'ic12': 64, 'ic13': 256, 'ic14': 512}.items()})

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        scripts = self.root / 'scripts'; scripts.mkdir()
        for name in ('build-mac.sh', 'build-linux.sh', 'install-linux.sh'):
            shutil.copyfile(ROOT / 'scripts' / name, scripts / name)
        shutil.copytree(ASSETS, self.root / 'App/MealPrepApp.Desktop/Assets')
        shutil.copyfile(ROOT / 'release.json', self.root / 'release.json')
        binaries = self.root / 'test-bin'; binaries.mkdir()
        # Exercise packaging scripts with a tiny publish payload; .NET compilation runs separately.
        publisher = binaries / 'dotnet'
        publisher.write_text('#!/usr/bin/env python3\nimport pathlib,sys\np=pathlib.Path(sys.argv[sys.argv.index("-o")+1]);p.mkdir(parents=True,exist_ok=True)\ne=p/"MealPrepApp.Desktop";e.write_text("fixture");e.chmod(0o755)\n')
        publisher.chmod(0o755)
        self.environment = {**os.environ, 'PATH': str(binaries) + os.pathsep + os.environ['PATH']}

    def run_script(self, name, *arguments, **environment):
        subprocess.run(['bash', str(self.root / 'scripts' / name), *map(str, arguments)],
                       env={**self.environment, **environment}, check=True, capture_output=True, text=True)

    def test_mac_build_installs_the_icon_named_by_the_bundle(self):
        self.run_script('build-mac.sh')
        bundle = self.root / 'dist/mac/MealPrepApp.app'
        with (bundle / 'Contents/Info.plist').open('rb') as stream:
            info = plistlib.load(stream)
        self.assertEqual(info['CFBundleIconFile'], 'MealPrepApp.icns')
        icon = bundle / 'Contents/Resources' / info['CFBundleIconFile']
        self.assertEqual(icon.read_bytes(), (ICONS / 'app-icon.icns').read_bytes())

    def test_linux_build_and_install_resolve_a_real_launcher_icon(self):
        self.run_script('build-linux.sh')
        folder = self.root / 'Meal Prep "$literal" %'; shutil.move(self.root / 'dist/linux', folder)
        data = self.root / 'user data'
        self.run_script('install-linux.sh', folder, XDG_DATA_HOME=str(data))
        desktop = (data / 'applications/mealprep.desktop').read_text()
        self.assertIn('\nIcon=mealprep\n', desktop)
        self.assertIn('\nStartupWMClass=MealPrepApp.Desktop\n', desktop)
        self.assertIn(r'\$literal', desktop); self.assertIn('%%', desktop)
        installed = data / 'icons/hicolor/512x512/apps/mealprep.png'
        self.assertEqual(installed.read_bytes(), (ICONS / 'app-icon-512.png').read_bytes())

    def test_windows_executable_and_installer_reference_the_same_icon(self):
        project_path = ROOT / 'App/MealPrepApp.Desktop/MealPrepApp.Desktop.csproj'
        project = ET.parse(project_path)
        icon = project.findtext('.//ApplicationIcon')
        self.assertEqual((project_path.parent / icon).resolve(), (ICONS / 'app-icon.ico').resolve())
        installer_path = ROOT / 'packaging/windows/MealPrepApp.iss'
        line = next(line for line in installer_path.read_text().splitlines() if line.startswith('SetupIconFile='))
        installer_icon = installer_path.parent / line.split('=', 1)[1].replace('\\', '/')
        self.assertEqual(installer_icon.resolve(), (ICONS / 'app-icon.ico').resolve())


if __name__ == '__main__': unittest.main()
