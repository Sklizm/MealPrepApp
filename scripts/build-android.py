#!/usr/bin/env python3
"""Build Android without putting signing credentials in command arguments or source."""
import argparse
import json
import os
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--signing-settings', type=Path, help='Private JSON containing MEALPREP_ANDROID_* environment variables')
parser.add_argument('--release', action='store_true')
args = parser.parse_args()
env = os.environ.copy()
if args.signing_settings:
    private = json.loads(args.signing_settings.read_text())
    required = {'MEALPREP_ANDROID_KEYSTORE', 'MEALPREP_ANDROID_STORE_PASSWORD',
                'MEALPREP_ANDROID_KEY_ALIAS', 'MEALPREP_ANDROID_KEY_PASSWORD'}
    if set(private) != required or not all(isinstance(value, str) and value for value in private.values()):
        parser.error('Invalid signing settings')
    env.update(private)
if args.release and not all(env.get(key) for key in ('MEALPREP_ANDROID_KEYSTORE', 'MEALPREP_ANDROID_STORE_PASSWORD',
                                                    'MEALPREP_ANDROID_KEY_ALIAS', 'MEALPREP_ANDROID_KEY_PASSWORD')):
    parser.error('Release builds require the dedicated private signing identity')
root = Path(__file__).resolve().parents[1] / 'App/MealPrepApp.Android'
wrapper = 'gradlew.bat' if os.name == 'nt' else './gradlew'
tasks = [':app:assembleRelease'] if args.release else [':app:testDebugUnitTest', ':app:lintDebug', ':app:assembleDebug', ':app:assembleDebugAndroidTest']
subprocess.run([wrapper, '--no-daemon', *tasks], cwd=root, env=env, check=True)
