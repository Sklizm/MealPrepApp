#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_DIR="${1:-$REPO_ROOT/dist/linux}"
APP_DIR="$(cd "$APP_DIR" && pwd)"
DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}"
mkdir -p "$DATA_DIR/applications"
# Quote desktop Exec path; reject newlines and escape desktop-entry metacharacters.
python3 - "$APP_DIR" "$DATA_DIR/applications/mealprep.desktop" <<'PYCODE'
import pathlib, sys
app, destination = sys.argv[1:]
if '\n' in app or '\r' in app:
    raise SystemExit("Application path must not contain newlines")
executable = app + '/MealPrepApp.Desktop'
executable = executable.replace('\\', '\\\\').replace('"', '\\"').replace('$', '\\$').replace('`', '\\`').replace('%', '%%')
pathlib.Path(destination).write_text('[Desktop Entry]\nType=Application\nName=MealPrepApp\nExec="' + executable + '"\nTerminal=false\nCategories=Utility;Office;\n')
PYCODE
