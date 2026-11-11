#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_DIR="${1:-$REPO_ROOT/dist/linux}"
APP_DIR="$(cd "$APP_DIR" && pwd)"
DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}"
ICON_DIR="$DATA_DIR/icons/hicolor/512x512/apps"
mkdir -p "$DATA_DIR/applications" "$ICON_DIR"
cp "$APP_DIR/mealprep.png" "$ICON_DIR/mealprep.png"
# Quote desktop Exec path; reject newlines and escape desktop-entry metacharacters.
python3 - "$APP_DIR" "$DATA_DIR/applications/mealprep.desktop" <<'PYCODE'
import pathlib, sys
app, destination = sys.argv[1:]
if '\n' in app or '\r' in app:
    raise SystemExit("Application path must not contain newlines")
executable = app + '/MealPrepApp.Desktop'
executable = executable.replace('\\', '\\\\').replace('"', '\\"').replace('$', '\\$').replace('`', '\\`').replace('%', '%%')
pathlib.Path(destination).write_text('[Desktop Entry]\nType=Application\nName=MealPrepApp\nExec="' + executable + '"\nIcon=mealprep\nTerminal=false\nCategories=Utility;Office;\nStartupWMClass=MealPrepApp.Desktop\n')
PYCODE
