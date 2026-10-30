#!/usr/bin/env python3
"""Validate embedded catalogs, resource references, and public Markdown file links."""
import json
import re
import subprocess
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[2]
CATALOGS = ROOT / "App/MealPrepApp.Core/Localization/Catalogs"
catalogs = {language: json.loads((CATALOGS / f"{language}.json").read_text()) for language in ("en", "ru", "ro")}
errors = []
reference = catalogs["en"]
for language, catalog in catalogs.items():
    if catalog.keys() != reference.keys():
        errors.append(f"{language}: resource keys differ")
    for key, text in catalog.items():
        if not text.strip():
            errors.append(f"{language}/{key}: empty translation")
        placeholders = lambda value: sorted(re.findall(r"\{(\d+)(?:[^}]*)\}", value))
        if key in reference and placeholders(text) != placeholders(reference[key]):
            errors.append(f"{language}/{key}: placeholder mismatch")

for folder in (ROOT / "App/MealPrepApp.Core", ROOT / "App/MealPrepApp.Desktop"):
    for path in folder.rglob("*"):
        if path.suffix not in (".cs", ".axaml") or any(part in ("obj", "bin") for part in path.parts):
            continue
        source = path.read_text()
        keys = re.findall(r'L\.(?:Text|Message)\("([\w.]+)"', source)
        keys += re.findall(r"\{loc:(?:Loc|Format) ([\w.]+)", source)
        keys += [f"{key}.{form}" for key in re.findall(r'L\.Count\("([\w.]+)"', source) for form in ("One", "Few", "Many")]
        for key in keys:
            if key.startswith("Count.") and not key.endswith((".One", ".Few", ".Many")):
                continue
            if key not in reference:
                errors.append(f"{path.relative_to(ROOT)}: unknown key {key}")

paths = set(subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT).decode().split("\0"))
for name in sorted(paths):
    path = ROOT / name
    if path.suffix != ".md" or not path.is_file():
        continue
    text = re.sub(r"```.*?```", "", path.read_text(), flags=re.S)
    for target in re.findall(r"\[[^\]]*\]\(([^)]+)\)", text):
        target = target.split("#", 1)[0]
        if not target or re.match(r"[a-z]+:", target) or target.startswith("/"):
            continue
        if not (path.parent / unquote(target)).exists():
            errors.append(f"{name}: missing linked file {target}")

    if "Vault" in path.parts:
        for target in re.findall(r"\[\[([^\]|]+)", text):
            target = target.split("#", 1)[0]
            candidates = [path.parent / f"{target}.md", ROOT / "Vault" / f"{target}.md"]
            if target and not any(candidate.is_file() for candidate in candidates) and not any((ROOT / "Vault").rglob(Path(target).name + ".md")):
                errors.append(f"{name}: missing wiki link {target}")

if errors:
    raise SystemExit("\n".join(errors))
print(f"{len(reference)} keys match across English, Russian, and Romanian; resource references and Markdown/wiki links resolve.")
