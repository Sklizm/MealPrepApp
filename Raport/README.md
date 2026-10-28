# Raport de practică — generator `.docx`

Generează `Raport_practica.docx` (proiectul MealPrep) cu formatarea cerută de îndrumar:
A4; margini stânga 30 / sus 20 / jos 20 / dreapta 10 mm; corp Times New Roman 12, justify, 1.5 rânduri;
titluri TNR 14 aldin centrat; cod Courier New 10; legende sub figuri; numerotare pagini jos-centrat;
cuprins (TOC) și bibliografie native Word.

## Cum se rulează

```bash
# o singură dată: mediul virtual + dependința
python3 -m venv Raport/.venv
Raport/.venv/bin/pip install -r Raport/requirements.txt

# generarea raportului
Raport/.venv/bin/python Raport/build_report.py
# -> Raport/Raport_practica.docx
```

Validare vizuală opțională (PDF):

```bash
libreoffice --headless --convert-to pdf Raport/Raport_practica.docx
```

## Structura

| Fișier | Rol |
|---|---|
| `build_report.py` | orchestratorul: stiluri + asamblarea secțiunilor |
| `docx_helpers.py` | toate regulile de formatare (margini, fonturi, cod, figuri, TOC, numerotare) |
| `content.py` | proza românească: foaie de titlu, introducere, conținut, concluzie, bibliografie |
| `content_anexe.py` | anexe: A1 listing complet (citit din fișierele reale), A2 Claude Code/Codex/Hermes Agent, A3 Obsidian, A4 Git, A5 checklist cerințe |
| `assets/` | diagrame și capturi de ecran (de adăugat) |

Listingurile sunt citite din fișierele existente la generare, însă lista de căi este fixă
în `content_anexe.py`. După separarea în Core/Desktop/WPF, unele căi istorice nu mai există
și sunt omise de generator. Actualizați lista înainte de a afirma că anexele acoperă codul curent.

## De completat manual (în Word)

1. **Foaia de titlu** — se înlocuiesc câmpurile `[...]` cu modelul oficial al instituției.
2. **Capturile de ecran** — se lipesc în chenarele „Figura N — …” (capturi de pe VM-ul Windows).
3. **Cuprinsul** — clic dreapta pe cuprins › *Update Field* (sau `F9`) pentru a-l popula/actualiza.

## Starea documentației după redesign

Clientul Avalonia pentru macOS/Linux are acum teme System/Light/Dark, navigare laterală,
scalarea porțiilor, mod de gătit, planificare săptămânală și liste de cumpărături salvate.
Clientul WPF rămâne pentru evaluarea practicii. Acest README actualizează instrucțiunile;
raportul `.docx` și proza din `content.py` nu au fost regenerate sau rescrise automat.

- [Arhitectura, compilarea și configurarea curentă](../README.md)
- [Implementarea și capturile Avalonia](../docs/redesign/README.md)
- [Dovezi de validare și verificări rămase](../docs/redesign/validation.md)

Capturile din `docs/redesign/screenshots/` provin din randări headless cu date demonstrative.
Etichetați-le ca atare dacă le folosiți în raport. Pentru demonstrarea clientului WPF,
folosiți capturi native din Windows; pentru macOS/Linux, completați verificările native.
Nu prezentați migrarea bazei de date sau testele SQL noi ca executate fără dovezile respective.

Pentru versiunea candidat, consultați [procedura de release](../docs/releases/README.md).
Rezultatele CI și testele native trebuie documentate separat; existența arhivei nu confirmă
acceptarea completă a aplicației.
