#!/usr/bin/env python3
"""Validate published folders and create release archives or combined checksums."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parent.parent
ARCHIVES = {"macos-arm64": "MealPrepApp-macos-arm64.tar.gz", "linux-x64": "MealPrepApp-linux-x64.tar.gz"}


def configuration():
    return json.loads((ROOT / "release.json").read_text())


def validate(folder, executable):
    if not folder.is_dir() or folder.is_symlink():
        raise ValueError("Package root must be a real directory")
    for path in folder.rglob("*"):
        name = path.name.lower()
        if path.is_symlink():
            raise ValueError(f"Symlink not allowed in package: {path}")
        if name.startswith(".env.") or name in {"appsettings.local.json", "backups", ".env", "id_ed25519", "id_rsa"} or name.endswith((".bak", ".pfx", ".p12", ".pem", ".key")):
            raise ValueError(f"Private file or directory in package: {path}")
    if not executable.is_file() or not os.access(executable, os.X_OK):
        raise ValueError(f"Missing executable permission: {executable}")


def package(platform, output):
    config = configuration()
    source = ROOT / ("dist/mac/MealPrepApp.app" if platform == "macos-arm64" else "dist/linux")
    executable = source / ("Contents/MacOS/MealPrepApp.Desktop" if platform == "macos-arm64" else "MealPrepApp.Desktop")
    validate(source, executable)
    sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    metadata = {"version": config["version"], "sourceCommit": sha, "platform": platform,
                "requiredMigrations": config["requiredMigrations"], "signed": False,
                "nativeAcceptance": "pending; see release evidence"}
    (source / "release-metadata.json").write_text(json.dumps(metadata, indent=2) + "\n")
    output.mkdir(parents=True, exist_ok=True)
    archive = output / ARCHIVES[platform]
    with tarfile.open(archive, "w:gz") as tar:
        tar.add(source, arcname="MealPrepApp.app" if platform == "macos-arm64" else "MealPrepApp-linux-x64")
    print(archive)


def finalize(output):
    records = []
    for platform, name in ARCHIVES.items():
        path = output / name
        with tarfile.open(path, "r:gz") as tar:
            metadata_member = ("MealPrepApp.app" if platform == "macos-arm64" else "MealPrepApp-linux-x64") + "/release-metadata.json"
            metadata = json.load(tar.extractfile(metadata_member))
        expected_sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        if metadata["sourceCommit"] != expected_sha or metadata["version"] != configuration()["version"] or metadata["platform"] != platform:
            raise ValueError("Release assets do not match the selected source/version/platform")
        with path.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        if metadata["requiredMigrations"] != configuration()["requiredMigrations"]:
            raise ValueError("Release migration metadata differs from source")
        records.append({**metadata, "file": name, "sha256": digest})
    (output / "SHA256SUMS").write_text("".join(f"{r['sha256']}  {r['file']}\n" for r in records))
    (output / "release-manifest.json").write_text(json.dumps(records, indent=2) + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("platform", choices=[*ARCHIVES, "finalize"])
    parser.add_argument("--output", type=Path, default=ROOT / "dist/release")
    args = parser.parse_args()
    if args.platform == "finalize":
        finalize(args.output)
    else:
        package(args.platform, args.output)
