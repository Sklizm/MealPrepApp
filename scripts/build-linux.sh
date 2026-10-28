#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$REPO_ROOT/dist/linux"

echo "=== Building MealPrepApp for Linux (x64) ==="

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

dotnet publish "$REPO_ROOT/App/MealPrepApp.Desktop/MealPrepApp.Desktop.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -o "$OUTPUT_DIR" \
    "$@"

chmod +x "$OUTPUT_DIR/MealPrepApp.Desktop"

cp "$REPO_ROOT/App/MealPrepApp.Desktop/Assets/mealprep.desktop" "$OUTPUT_DIR/"


echo "=== Build Complete! ==="
echo "Linux standalone binaries created at: $OUTPUT_DIR"
echo "To run: $OUTPUT_DIR/MealPrepApp.Desktop"
