---
tags: [session, wpf, exe, publish, verification, ro]
---

# 2026-05-26 — Publicare Windows exe

## Context
Codrin a prioritizat transformarea MealPrepApp intr-un livrabil Windows `.exe`.

## Implementat
- Profil publish Windows x64: `Windows-x64-Folder.pubxml`, `Release`, `net10.0-windows`, `win-x64`, self-contained, single-file, trimming dezactivat pentru siguranta WPF.
- Script `App/publish-windows-exe.cmd` pentru publicare cu o singura comanda pe Windows/VM-ul Ritei.
- `appsettings.Local.template.json` ca template sigur fara secrete.
- `MealPrepApp.csproj` copiaza `appsettings.json`, exclude realul `appsettings.Local.json` din publish si include template-ul.
- Teste statice pentru profil, script, template, docs si `.gitignore`.
- README actualizat cu instructiuni de publish/configurare.

## Verificare locala
- `.hermes/tests/test_drafts_static.py` a trecut.
- `git diff --check` fara erori.
- `dotnet publish` local a esuat conform asteptarii pe Fedora, deoarece lipsesc targeturile WPF WindowsDesktop si SDK-ul local este .NET 9.

## Gap
Publicarea reala `.exe` trebuie verificata pe Windows/VM.

## Cum se publica pe Windows
Din radacina repo-ului:

```cmd
App\publish-windows-exe.cmd
```

Output asteptat:

```text
App\publish\MealPrepApp-win-x64\MealPrepApp.exe
```

Inainte de rulare, copiaza `appsettings.Local.template.json` ca `appsettings.Local.json` in folderul publish si pune parola reala pentru `mealprep_app`.
