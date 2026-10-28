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

dotnet publish "$REPO_ROOT/App/MealPrepApp.Desktop/MealPrepApp.Desktop.csproj" \
    -c Release \
    -r osx-arm64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -o "$MACOS_DIR" \
    "$@"

cp "$REPO_ROOT/App/MealPrepApp.Desktop/Assets/Info.plist" "$APP_BUNDLE/Contents/Info.plist"

chmod +x "$MACOS_DIR/MealPrepApp.Desktop"


echo "=== Build Complete! ==="
echo "Application bundle created at: $APP_BUNDLE"
echo "To run: open \"$APP_BUNDLE\""
