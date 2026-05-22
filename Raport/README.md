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
| `content_anexe.py` | anexe: A1 listing complet (citit din fișierele reale), A2 Claude Code, A3 Obsidian, A4 Git |
| `assets/` | diagrame și capturi de ecran (de adăugat) |

Listingul de cod din anexe este **citit direct din fișierele reale** ale proiectului la generare, deci
rămâne mereu sincronizat cu codul.

## De completat manual (în Word)

1. **Foaia de titlu** — se înlocuiesc câmpurile `[...]` cu modelul oficial al instituției.
2. **Capturile de ecran** — se lipesc în chenarele „Figura N — …” (capturi de pe VM-ul Windows).
3. **Cuprinsul** — clic dreapta pe cuprins › *Update Field* (sau `F9`) pentru a-l popula/actualiza.
