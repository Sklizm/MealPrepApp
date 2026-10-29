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
         ('TextPrimary', 'SurfaceSecondary'), ('TextSecondary', 'AppBackground'),
         ('TextSecondary', 'SurfaceSecondary'), ('TextSecondary', 'Surface'),
         ('OnPrimary', 'ActionPrimary'), ('OnPrimary', 'ActionHover'),
         ('OnPrimary', 'ActionPressed'), ('OnDanger', 'Danger'),
         ('Danger', 'Surface'), ('Danger', 'Selection'), ('Danger', 'AppBackground'), ('Danger', 'SurfaceSecondary'),
         ('Accent', 'Surface'), ('TextPrimary', 'Selection'), ('ActionPrimary', 'Surface'),
         ('TextControlForeground', 'TextControlBackground'),
         ('TextControlForegroundFocused', 'TextControlBackgroundFocused'),
         ('TextControlPlaceholderForeground', 'TextControlBackground'),
         ('TextControlPlaceholderForegroundPointerOver', 'TextControlBackgroundPointerOver'),
         ('TextControlPlaceholderForegroundFocused', 'TextControlBackgroundFocused'),
         ('ComboBoxForeground', 'ComboBoxBackground'),
         ('ComboBoxForegroundFocusedPressed', 'ComboBoxBackgroundPressed'),
         ('ComboBoxPlaceHolderForeground', 'ComboBoxBackground'),
         ('ComboBoxItemForegroundSelected', 'ComboBoxItemBackgroundSelected'),
         ('ComboBoxItemForegroundSelectedPointerOver', 'ComboBoxItemBackgroundSelectedPointerOver'),
         ('CalendarDatePickerTextForeground', 'CalendarDatePickerBackground'),
         ('CalendarViewForeground', 'CalendarViewBackground'),
         ('CalendarViewOutOfScopeForeground', 'CalendarViewBackground'),
         ('MenuFlyoutItemForegroundPointerOver', 'MenuFlyoutItemBackgroundPointerOver'),
         ('ToolTipForeground', 'ToolTipBackground')]
for theme in root.find('a:ResourceDictionary.ThemeDictionaries', NS):
    colors = {b.attrib['{' + NS['x'] + '}Key']: b.attrib['Color'] for b in theme}
    name = theme.attrib['{' + NS['x'] + '}Key']
    lines.append(name)
    for foreground, background in pairs:
        contrast = ratio(colors[foreground], colors[background])
        assert contrast >= 4.5, (name, foreground, background, contrast)
        lines.append(f'  {foreground} / {background}: {contrast:.2f}:1')
    for indicator in ('Focus', 'Divider'):
        for background in ('AppBackground', 'Surface', 'SurfaceSecondary', 'Selection'):
            contrast = ratio(colors[indicator], colors[background])
            assert contrast >= 3, (name, indicator, background, contrast)
            lines.append(f'  {indicator} / {background}: {contrast:.2f}:1 (control)')
    # Fluent template aliases must stay in sync when a semantic palette is changed.
    aliases = {'SystemControlPageTextBaseHighBrush': 'TextPrimary',
               'SystemControlForegroundBaseHighBrush': 'TextPrimary',
               'SystemControlFocusVisualPrimaryBrush': 'Focus',
               'SystemControlBackgroundAltHighBrush': 'Surface',
               'SystemControlHighlightListAccentMediumBrush': 'Selection'}
    for alias, semantic in aliases.items():
        assert colors[alias] == colors[semantic], (name, alias, semantic)
assert root.find("x:Double[@x:Key='TextControlPlaceholderOpacity']", NS).text == '1'
for resource in root.findall('a:SolidColorBrush', NS):
    key = resource.attrib['{' + NS['x'] + '}Key']
    if key.startswith('Meal'):
        contrast = ratio('#FFFFFF', resource.attrib['Color'])
        assert contrast >= 4.5, (key, contrast)
        lines.append(f'OnMeal / {key}: {contrast:.2f}:1')
(ROOT / 'docs/redesign/contrast.txt').write_text('\n'.join(lines) + '\n')
print(f'{len(pairs) * 2 + 6} text pairs meet 4.5:1; 16 control/focus pairs meet 3:1; Fluent aliases agree.')
