#!/usr/bin/env python3
"""Validate platform payloads, create archives and bind release assets to source/version."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parent.parent
ARCHIVES = {"macos-arm64": "MealPrepApp-macos-arm64.tar.gz", "linux-x64": "MealPrepApp-linux-x64.tar.gz"}
PLATFORMS = [*ARCHIVES, "windows-x64"]


def configuration():
    config = json.loads((ROOT / "release.json").read_text())
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", config["version"]):
        raise ValueError("Invalid release version")
    return config


def source_commit():
    return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()


def metadata(platform):
    config = configuration()
    return {"version": config["version"], "sourceCommit": source_commit(), "platform": platform,
            "requiredMigrations": config["requiredMigrations"], "signed": False,
            "nativeAcceptance": "pending; see release evidence"}


def verify_metadata(record, platform):
    for key, value in metadata(platform).items():
        if record.get(key) != value:
            raise ValueError(f"Release {key} does not match selected source/version/platform")


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def installer_name():
    return f'MealPrepApp-{configuration()["version"]}-windows-x64-setup.exe'


def validate(folder, executable, *, windows=False):
    if not folder.is_dir() or folder.is_symlink():
        raise ValueError("Package root must be a real directory")
    for path in folder.rglob("*"):
        name = path.name.lower()
        if path.is_symlink():
            raise ValueError(f"Symlink not allowed in package: {path}")
        if name.startswith(".env.") or name in {"appsettings.local.json", "backups", ".env", "id_ed25519", "id_rsa"} or name.endswith((".bak", ".pfx", ".p12", ".pem", ".key")):
            raise ValueError(f"Private file or directory in package: {path}")
    if not executable.is_file():
        raise ValueError(f"Missing executable: {executable}")
    if windows:
        with executable.open("rb") as stream:
            if stream.read(2) != b"MZ":
                raise ValueError("Windows executable must have a PE header")
    elif not os.access(executable, os.X_OK):
        raise ValueError(f"Missing executable permission: {executable}")


def prepare_windows():
    source = ROOT / "dist/windows"
    validate(source, source / "MealPrepApp.Desktop.exe", windows=True)
    (source / "release-metadata.json").write_text(json.dumps(metadata("windows-x64"), indent=2) + "\n")


def package(platform, output):
    output.mkdir(parents=True, exist_ok=True)
    # A rebuilt asset invalidates any previously combined release summary.
    for name in ("SHA256SUMS", "release-manifest.json"):
        (output / name).unlink(missing_ok=True)
    if platform == "windows-x64":
        source = ROOT / "dist/windows"
        validate(source, source / "MealPrepApp.Desktop.exe", windows=True)
        record = json.loads((source / "release-metadata.json").read_text())
        verify_metadata(record, platform)
        installer = output / installer_name()
        with installer.open("rb") as stream:
            if stream.read(2) != b"MZ":
                raise ValueError("Invalid Windows installer")
        (output / (installer.name + ".metadata.json")).write_text(
            json.dumps({**record, "file": installer.name, "sha256": digest(installer)}, indent=2) + "\n")
        print(installer)
        return
    source = ROOT / ("dist/mac/MealPrepApp.app" if platform == "macos-arm64" else "dist/linux")
    executable = source / ("Contents/MacOS/MealPrepApp.Desktop" if platform == "macos-arm64" else "MealPrepApp.Desktop")
    validate(source, executable)
    (source / "release-metadata.json").write_text(json.dumps(metadata(platform), indent=2) + "\n")
    archive = output / ARCHIVES[platform]
    with tarfile.open(archive, "w:gz") as tar:
        tar.add(source, arcname="MealPrepApp.app" if platform == "macos-arm64" else "MealPrepApp-linux-x64")
    print(archive)


def finalize(output):
    records = []
    for platform in PLATFORMS:
        if platform == "windows-x64":
            path = output / installer_name()
            record = json.loads((output / (path.name + ".metadata.json")).read_text())
            if record.get("file") != path.name or record.get("sha256") != digest(path):
                raise ValueError("Installer does not match its build metadata")
        else:
            path = output / ARCHIVES[platform]
            with tarfile.open(path, "r:gz") as tar:
                member = ("MealPrepApp.app" if platform == "macos-arm64" else "MealPrepApp-linux-x64") + "/release-metadata.json"
                record = json.load(tar.extractfile(member))
        verify_metadata(record, platform)
        records.append({**record, "file": path.name, "sha256": digest(path)})
    (output / "SHA256SUMS").write_text("".join(f"{r['sha256']}  {r['file']}\n" for r in records))
    (output / "release-manifest.json").write_text(json.dumps(records, indent=2) + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("platform", choices=[*PLATFORMS, "windows-payload", "finalize"])
    parser.add_argument("--output", type=Path, default=ROOT / "dist/release")
    args = parser.parse_args()
    if args.platform == "finalize":
        finalize(args.output)
    elif args.platform == "windows-payload":
        prepare_windows()
    else:
        package(args.platform, args.output)
