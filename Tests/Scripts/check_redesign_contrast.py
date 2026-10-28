#!/usr/bin/env python3
"""Validate semantic text pairs; write an auditable contrast report (no dependencies)."""
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[2]
NS = {'a': 'https://github.com/avaloniaui', 'x': 'http://schemas.microsoft.com/winfx/2006/xaml'}

def luminance(color):
    rgb = [int(color[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return sum(w * (v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4)
               for w, v in zip((.2126, .7152, .0722), rgb))

def ratio(a, b):
    low, high = sorted((luminance(a), luminance(b)))
    return (high + .05) / (low + .05)

root = ET.parse(ROOT / 'App/MealPrepApp.Desktop/Themes/Colors.axaml').getroot()
lines = []
pairs = [('TextPrimary', 'AppBackground'), ('TextPrimary', 'Surface'),
         ('TextSecondary', 'SurfaceSecondary'), ('TextSecondary', 'Surface'),
         ('OnPrimary', 'ActionPrimary'), ('OnPrimary', 'ActionHover'),
         ('OnPrimary', 'ActionPressed'), ('OnDanger', 'Danger'),
         ('Danger', 'Surface'), ('Accent', 'Surface'), ('TextPrimary', 'Selection')]
for theme in root.find('a:ResourceDictionary.ThemeDictionaries', NS):
    colors = {b.attrib['{' + NS['x'] + '}Key']: b.attrib['Color'] for b in theme}
    name = theme.attrib['{' + NS['x'] + '}Key']
    lines.append(name)
    for foreground, background in pairs:
        contrast = ratio(colors[foreground], colors[background])
        assert contrast >= 4.5, (name, foreground, background, contrast)
        lines.append(f'  {foreground} / {background}: {contrast:.2f}:1')
for resource in root.findall('a:SolidColorBrush', NS):
    key = resource.attrib['{' + NS['x'] + '}Key']
    if key.startswith('Meal'):
        contrast = ratio('#FFFFFF', resource.attrib['Color'])
        assert contrast >= 4.5, (key, contrast)
        lines.append(f'OnMeal / {key}: {contrast:.2f}:1')
(ROOT / 'docs/redesign/contrast.txt').write_text('\n'.join(lines) + '\n')
print(f'{len(pairs) * 2 + 6} semantic text pairs meet 4.5:1.')
