#!/usr/bin/env python3
"""Collect exact-commit CI packages and a verified signed APK into a draft release."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parents[1]
ANDROID = 'App/MealPrepApp.Android'
PACKAGE = 'app.mealprep.companion'


def command(*args):
    return subprocess.check_output(list(map(str, args)), cwd=ROOT, text=True).strip()


def execute(*args):
    subprocess.run(list(map(str, args)), cwd=ROOT, check=True)


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def verify_android(apk, metadata_path, build_tools):
    record = json.loads(metadata_path.read_text())
    if record.get('package') != PACKAGE or record.get('apkSha256') != digest(apk):
        raise ValueError('Android package identity or APK hash does not match its build record')
    source = record.get('sourceCommit', '')
    if not re.fullmatch(r'[0-9a-f]{40}', source):
        raise ValueError('Android build record needs a full source commit')
    tree = command('git', 'rev-parse', f'{source}:{ANDROID}')
    if tree != command('git', 'rev-parse', f'HEAD:{ANDROID}'):
        raise ValueError('Android source has changed since this APK was built; rebuild and sign it')
    if command('git', 'status', '--porcelain', '--', ANDROID):
        raise ValueError('Android source has uncommitted changes')
    certificate = record.get('signerCertificateSha256', '').lower()
    if not re.fullmatch(r'[0-9a-f]{64}', certificate):
        raise ValueError('Android build record needs the expected signing certificate SHA-256')
    suffix = '.bat' if os.name == 'nt' else ''
    signatures = command(build_tools / f'apksigner{suffix}', 'verify', '--print-certs', apk)
    signers = re.findall(r'Signer #\d+ certificate SHA-256 digest: ([0-9a-fA-F]+)', signatures)
    if [value.lower() for value in signers] != [certificate]:
        raise ValueError('APK signing certificate does not match the original release identity')
    badging = command(build_tools / ('aapt2.exe' if os.name == 'nt' else 'aapt2'), 'dump', 'badging', apk)
    package = re.search(r"package: name='([^']+)' versionCode='(\d+)' versionName='([^']+)'", badging)
    if not package or package.groups() != (PACKAGE, str(record.get('versionCode')), record.get('version')):
        raise ValueError('APK package/version does not match its build record')
    gradle = (ROOT / ANDROID / 'app/build.gradle.kts').read_text()
    version = re.search(r'versionName\s*=\s*"([^"]+)"', gradle).group(1)
    code = int(re.search(r'versionCode\s*=\s*(\d+)', gradle).group(1))
    if version != record['version'] or code != record['versionCode']:
        raise ValueError('APK version does not match the selected Android source')
    # Only public artifact provenance leaves the host; private build notes stay local.
    return {'platform': 'android', 'package': PACKAGE, 'version': version, 'versionCode': code,
            'sourceCommit': source, 'androidSourceTree': tree,
            'releaseCommit': command('git', 'rev-parse', 'HEAD'), 'signed': True,
            'signerCertificateSha256': certificate, 'sha256': record['apkSha256'],
            'file': f'MealPrepApp-Android-{version}.apk',
            'nativeAcceptance': 'pending; see companion verification'}


def require_draft(release, tag, tag_commit, source):
    if not release.get('isDraft') or release.get('tagName') != tag or tag_commit != source:
        raise ValueError('Existing release must be an unpublished draft for this exact tag and commit')


def add_android(output, apk, record, version):
    records = json.loads((output / 'release-manifest.json').read_text())
    if len(records) != 3 or {item['platform'] for item in records} != {'linux-x64', 'macos-arm64', 'windows-x64'}:
        raise ValueError('Expected a verified desktop manifest containing all three platforms')
    for item in records:
        if item['version'] != version or item['sourceCommit'] != record['releaseCommit']:
            raise ValueError('Desktop and Android release provenance do not match')
        if digest(output / item['file']) != item['sha256']:
            raise ValueError('Desktop asset changed after verification')
    if digest(apk) != record['sha256']:
        raise ValueError('APK changed after verification')
    shutil.copyfile(apk, output / record['file'])
    public_record = {**record, 'releaseVersion': version}
    (output / (record['file'] + '.metadata.json')).write_text(json.dumps(public_record, indent=2) + '\n')
    records.append(public_record)
    (output / 'release-manifest.json').write_text(json.dumps(records, indent=2) + '\n')
    (output / 'SHA256SUMS').write_text(''.join(f"{item['sha256']}  {item['file']}\n" for item in records))


def load_packager():
    spec = importlib.util.spec_from_file_location('package_release', ROOT / 'scripts/package-release.py')
    packaging = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(packaging)
    return packaging


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apk', type=Path, required=True)
    parser.add_argument('--android-metadata', type=Path, required=True)
    parser.add_argument('--android-build-tools', type=Path, required=True,
                        help='Android SDK build-tools directory containing apksigner and aapt2')
    parser.add_argument('--check-only', action='store_true', help='Validate Android locally without contacting GitHub')
    args = parser.parse_args()
    apk = args.apk.resolve()
    android = verify_android(apk, args.android_metadata.resolve(), args.android_build_tools.resolve())
    version = json.loads((ROOT / 'release.json').read_text())['version']
    notes = ROOT / f'docs/releases/v{version}.md'
    if not notes.is_file():
        raise ValueError(f'Missing release notes: {notes}')
    print(f'Verified signed Android {android["version"]}; its source matches this release.', flush=True)
    if args.check_only:
        return
    if command('git', 'branch', '--show-current') != 'main' or command('git', 'status', '--porcelain'):
        raise ValueError('Use a clean main checkout with all release changes committed and pushed')
    source = command('git', 'rev-parse', 'HEAD')
    repo = command('gh', 'repo', 'view', '--json', 'nameWithOwner', '--jq', '.nameWithOwner')

    def gh_json(*arguments):
        return json.loads(command('gh', *arguments, '--repo', repo))

    remote = command('gh', 'api', f'repos/{repo}/git/ref/heads/main', '--jq', '.object.sha')
    if remote != source:
        raise ValueError('Push main first; local HEAD must equal GitHub main')
    tag = f'v{version}'
    releases = gh_json('release', 'list', '--limit', '100', '--json', 'tagName,isDraft')
    existing = next((release for release in releases if release['tagName'] == tag), None)
    if existing:
        commit = command('gh', 'api', f'repos/{repo}/commits/{tag}', '--jq', '.sha')
        require_draft(existing, tag, commit, source)
    else:
        probe = subprocess.run(['gh', 'api', f'repos/{repo}/git/ref/tags/{tag}'], cwd=ROOT,
                               text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if probe.returncode == 0 or 'HTTP 404' not in probe.stderr:
            raise ValueError('Tag already exists without a matching draft, or tag lookup failed; inspect GitHub first')
    runs = []
    for attempt in range(6):
        runs = gh_json('run', 'list', '--workflow', 'compatibility.yml', '--branch', 'main',
                       '--commit', source, '--event', 'push', '--limit', '20',
                       '--json', 'databaseId,headSha,status,conclusion,url')
        if runs:
            break
        time.sleep(5)
    if not runs:
        raise ValueError('No Compatibility run exists for this commit; push main and retry')
    run = next((run for run in runs if run['conclusion'] == 'success'), runs[0])
    run_id = str(run['databaseId'])
    if run['headSha'] != source:
        raise ValueError('CI run does not match the selected commit')
    if run['status'] != 'completed':
        execute('gh', 'run', 'watch', run_id, '--exit-status', '--interval', '15', '--repo', repo)
    run = gh_json('run', 'view', run_id, '--json', 'headSha,status,conclusion,url')
    if run['headSha'] != source or run['status'] != 'completed' or run['conclusion'] != 'success':
        raise ValueError(f'Compatibility must pass for this commit: {run["url"]}')
    packaging = load_packager()
    candidates = ROOT / 'dist/release-candidates'
    candidates.mkdir(parents=True, exist_ok=True)
    output = Path(tempfile.mkdtemp(prefix=f'{tag}-{source[:8]}-', dir=candidates))
    with tempfile.TemporaryDirectory() as temp:
        downloaded = Path(temp)
        execute('gh', 'run', 'download', run_id,
                '--pattern', 'package-*', '--dir', downloaded, '--repo', repo)
        expected = [*packaging.ARCHIVES.values(), packaging.installer_name(), packaging.installer_name() + '.metadata.json']
        for name in expected:
            matches = list(downloaded.rglob(name))
            if len(matches) != 1 or matches[0].is_symlink():
                raise ValueError(f'Expected exactly one CI asset: {name}')
            shutil.copyfile(matches[0], output / name)
    packaging.finalize(output)
    add_android(output, apk, android, version)
    release_notes = output / 'release-notes.md'
    release_notes.write_text(notes.read_text() + f'\nSource: `{source}`\n\nAutomated evidence: {run["url"]}\n')
    print(f'Four verified installable packages and checksums prepared at {output}', flush=True)
    assets = sorted(path for path in output.iterdir() if path != release_notes)
    if existing:
        current = gh_json('release', 'view', tag, '--json', 'tagName,isDraft,url')
        require_draft(current, tag, command('gh', 'api', f'repos/{repo}/commits/{tag}', '--jq', '.sha'), source)
        execute('gh', 'release', 'upload', tag, *assets, '--clobber', '--repo', repo)
        execute('gh', 'release', 'edit', tag, '--notes-file', release_notes, '--repo', repo)
    else:
        # GitHub may defer a draft's implicit tag creation until publication.
        command('gh', 'api', f'repos/{repo}/git/refs', '--method', 'POST',
                '-f', f'ref=refs/tags/{tag}', '-f', f'sha={source}')
        execute('gh', 'release', 'create', tag, '--verify-tag', '--draft',
                *(['--prerelease'] if '-' in version else []), '--title', f'MealPrepApp {version}',
                '--notes-file', release_notes, '--repo', repo, *assets)
    release = gh_json('release', 'view', tag, '--json', 'tagName,isDraft,url')
    require_draft(release, tag, command('gh', 'api', f'repos/{repo}/commits/{tag}', '--jq', '.sha'), source)
    with tempfile.TemporaryDirectory() as temp:
        execute('gh', 'release', 'download', tag, '--dir', temp, '--repo', repo)
        for asset in assets:
            if digest(Path(temp) / asset.name) != digest(asset):
                raise ValueError(f'Uploaded release asset failed verification: {asset.name}')
    print(f'Uploaded and verified draft release: {release["url"]}')
    print('The draft is ready for native acceptance; it has not been published.')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit(f'Release preparation stopped: {error}')
