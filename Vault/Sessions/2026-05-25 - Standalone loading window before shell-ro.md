---
tags: [session, wpf, loading, shell, ro]
---

# 2026-05-25 — Fereastra separata de loading inainte de shell

## Context
Codrin a cerut schimbarea overlay-ului din shell intr-o fereastra separata de loading, afisata inainte ca aplicatia principala sa apara. Shell-ul trebuie sa ramana ascuns pana la finalul incarcarii.

## Modificari
- S-au adaugat `Views/StartupLoadingWindow.xaml` si `.xaml.cs`: fereastra chrome-less, stil cream/olive, spinner animat si text romanesc.
- Fluxul de login din `LoginWindow.xaml.cs` afiseaza loading window, ascunde login-ul, creeaza/initializeaza shell-ul si impune minim 3.5 secunde.
- `ShellWindow.xaml.cs` primeste `InitializeBeforeShowAsync()`; vechea initializare pe `Loaded` a fost scoasa.
- Overlay-ul anterior din `ShellWindow.xaml` si starea aferenta din `ShellViewModel` au fost eliminate.
- `StartupLoadingWindow` a fost inregistrata in DI.
- Testele statice verifica noua fereastra pre-shell.

## Verificare
- `.hermes/tests/test_drafts_static.py` a trecut.
- XAML parse OK pentru `App.xaml`, `ShellWindow.xaml`, `LoginWindow.xaml`, `StartupLoadingWindow.xaml`.
- `git diff --check` a trecut.
- Build local a esuat conform asteptarii din cauza lipsei WindowsDesktop targets pe Fedora.

## Test Windows recomandat
Login valid, confirmare ca login-ul dispare, loading window apare, shell-ul nu este vizibil, loading-ul tine ~3.5 secunde, shell-ul apare dupa inchidere si Acasa este deja incarcat.
