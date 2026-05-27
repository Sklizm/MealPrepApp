#!/usr/bin/env python3
"""
Generatorul raportului de practica (.docx) pentru proiectul MealPrep.

Ruleaza in venv-ul din Raport/.venv (vezi Raport/README.md):
    Raport/.venv/bin/python Raport/build_report.py

Produce: Raport/Raport_practica.docx, cu formatarea ceruta de indrumar
(A4; margini 30/20/20/10 mm; corp Times New Roman 12 justify 1.5; titluri
TNR 14 aldin centrat; cod Courier New 10; legende sub figuri; numerotare
pagini jos-centrat; cuprins si bibliografie ca elemente native Word).
"""

import os
from docx import Document

import docx_helpers as h
import content as c
import content_anexe as ca

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Raport_practica.docx")


def build():
    doc = Document()

    # Stiluri globale + actualizarea automata a campurilor (TOC/PAGE).
    h.configure_styles(doc)
    h.enable_auto_update_fields(doc)
    doc.core_properties.title = "Raport privind stagiul de practica — MealPrep"
    doc.core_properties.subject = "Aplicatie de gestionare a retetelor si planificare a meselor"
    doc.core_properties.author = "Codrin"
    doc.core_properties.keywords = "MealPrep, WPF, SQL Server, practica, raport"

    # Sectiunea 1: foaia de titlu (A4, fara numar de pagina).
    sec1 = doc.sections[0]
    h.set_a4_margins(sec1)
    h.add_page_number_footer(sec1, show=False)
    c.title_page(doc, h)

    # Sectiunea 2: restul raportului (A4, cu numar de pagina jos-centrat).
    sec2 = h.new_section_page(doc)
    h.set_a4_margins(sec2)
    h.add_page_number_footer(sec2, show=True)

    c.cuprins(doc, h)
    h.page_break(doc)

    c.introducere(doc, h)
    c.continut(doc, h)
    c.concluzie(doc, h)
    c.bibliografie(doc, h)

    h.page_break(doc)
    ca.anexe(doc, h)

    doc.save(OUT)
    print(f"OK — raport generat: {OUT}")


if __name__ == "__main__":
    build()
