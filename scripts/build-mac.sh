#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$REPO_ROOT/dist/mac"
APP_BUNDLE="$OUTPUT_DIR/MealPrepApp.app"
MACOS_DIR="$APP_BUNDLE/Contents/MacOS"
RESOURCES_DIR="$APP_BUNDLE/Contents/Resources"

echo "=== Building MealPrepApp for macOS (Apple Silicon osx-arm64) ==="

rm -rf "$OUTPUT_DIR"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"

APP_VERSION="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["version"])' "$REPO_ROOT/release.json")"

dotnet publish "$REPO_ROOT/App/MealPrepApp.Desktop/MealPrepApp.Desktop.csproj" \
    -c Release \
    -p:Version="$APP_VERSION" \
    -r osx-arm64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -o "$MACOS_DIR" \
    "$@"

cp "$REPO_ROOT/App/MealPrepApp.Desktop/Assets/Info.plist" "$APP_BUNDLE/Contents/Info.plist"

python3 - "$APP_BUNDLE/Contents/Info.plist" "$REPO_ROOT/release.json" <<'PYVERSION'
import json, plistlib, sys
path, config = sys.argv[1:]
release = json.load(open(config))
with open(path, 'rb') as f: info = plistlib.load(f)
info['CFBundleShortVersionString'] = release['version'].split('-')[0]
info['CFBundleVersion'] = release['macBundleVersion']
info['MealPrepReleaseVersion'] = release['version']
with open(path, 'wb') as f: plistlib.dump(info, f)
PYVERSION

chmod +x "$MACOS_DIR/MealPrepApp.Desktop"


echo "=== Build Complete! ==="
echo "Application bundle created at: $APP_BUNDLE"
echo "To run: open \"$APP_BUNDLE\""
