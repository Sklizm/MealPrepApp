#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != Darwin ]]; then
    echo "Run this installer on macOS." >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SOURCE="${1:-$REPO_ROOT/dist/mac/MealPrepApp.app}"
APPLICATIONS_DIR="${2:-/Applications}"

if [[ ! -f "$SOURCE/Contents/Info.plist" || ! -x "$SOURCE/Contents/MacOS/MealPrepApp.Desktop" ]]; then
    echo "No built MealPrepApp bundle at: $SOURCE" >&2
    echo "Build it first with: bash scripts/build-mac.sh" >&2
    exit 1
fi

mkdir -p "$APPLICATIONS_DIR"
APPLICATIONS_DIR="$(cd "$APPLICATIONS_DIR" && pwd)"
SOURCE="$(cd "$SOURCE" && pwd)"
DESTINATION="$APPLICATIONS_DIR/MealPrepApp.app"
if [[ "$SOURCE" == "$DESTINATION" ]]; then
    echo "Already installed at: $DESTINATION"
    exit 0
fi
if [[ ! -w "$APPLICATIONS_DIR" ]]; then
    echo "Cannot write to $APPLICATIONS_DIR. Use Finder to copy the app there, or install for your user:" >&2
    echo 'bash scripts/install-mac.sh /path/to/MealPrepApp.app "$HOME/Applications"' >&2
    exit 1
fi
if [[ -L "$DESTINATION" || ( -e "$DESTINATION" && ! -d "$DESTINATION" ) ]]; then
    echo "Refusing to replace a symlink or non-directory: $DESTINATION" >&2
    exit 1
fi

# Copy completely before replacing an existing installation; keep it for rollback.
STAGING="$(mktemp -d "$APPLICATIONS_DIR/.mealprep-install.XXXXXX")"
cleanup() {
    if [[ -d "$STAGING/previous.app" && ! -e "$DESTINATION" ]]; then
        mv "$STAGING/previous.app" "$DESTINATION"
    fi
    rm -rf "$STAGING"
}
trap cleanup EXIT
ditto "$SOURCE" "$STAGING/MealPrepApp.app"
if [[ -d "$DESTINATION" ]]; then
    mv "$DESTINATION" "$STAGING/previous.app"
fi
mv "$STAGING/MealPrepApp.app" "$DESTINATION"

echo "Installed: $DESTINATION"
echo "Launch with: open \"$DESTINATION\""
