---
tags: [session, wpf, loading, shell, ro]
---

# 2026-05-25 — Loading screen dupa login

## Context
Codrin a cerut un loading screen simplu, intuitiv si fluid dupa autentificare, fara lag vizibil.

## Modificari
- `ShellViewModel` a primit stare de initializare si mesaj de incarcare.
- `InitializeAsync()` a fost rearanjat astfel incat shell-ul sa poata afisa overlay-ul inainte de incarcarea Acasa.
- S-a adaugat `Task.Yield()` si o durata minima de 650ms pentru a evita flash-ul.
- `ShellWindow.xaml` a primit overlay full-window: fundal intunecat, card cream, spinner olive, titlu `Se incarca aplicatia` si mesaj live.
- `ShellWindow.xaml.cs` a primit fade-in/fade-out si blocarea clickurilor in spate.
- Testele statice au fost extinse pentru loading overlay.
- TODO a fost actualizat cu implementarea si verificarea Windows necesara.

## Verificare
- `.hermes/tests/test_drafts_static.py` a trecut.
- XAML parse OK pentru `App.xaml`, `ShellWindow.xaml`, `LoginWindow.xaml`.
- `git diff --check` a trecut.

## Nota build
Fedora nu poate construi proiectul WPF WindowsDesktop local; verificarea ramane pe Windows/.NET 10.

## Test Windows recomandat
Logare, confirmare overlay fluid, spinner animat, disparitie dupa incarcarea Acasa, fara clickuri in spate si repetare corecta dupa logout/login.
