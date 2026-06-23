#!/usr/bin/env python3
"""Double fontSize values in restored files (first pass only)."""
import re

ui_dir = '/mnt/c/Unity/code/Assets/Scripts/UI'

files = [
    'SettingsMenuUI.cs',
    'PlayerFlagRegistrationWindow.cs',
    'HerbRespawnUI.cs',
    'GuardWorldSpaceHUD.cs',
    'AchievementSystem.cs',
    'DeathScreenUI.cs',
]

def double_fontsize(match):
    prefix = match.group(1)
    num = int(match.group(2))
    suffix = match.group(3)
    return f'{prefix}{num*2}{suffix}'

total = 0
for fname in files:
    fpath = f'{ui_dir}/{fname}'
    with open(fpath, 'r') as f:
        content = f.read()
    
    # Match fontSize = NUMBER followed by non-digit (handles , ; ) } \n etc)
    new_content, n = re.subn(
        r'(fontSize\s*=\s*)(\d+)([^0-9])',
        double_fontsize,
        content
    )
    
    if n > 0:
        with open(fpath, 'w') as f:
            f.write(new_content)
        total += n
        print(f"  {fname}: {n}")

print(f"\nTotal: {total}")

# Verify
print("\n=== VERIFICATION ===")
for fname in files:
    fpath = f'{ui_dir}/{fname}'
    with open(fpath, 'r') as f:
        for i, line in enumerate(f, 1):
            if 'fontSize' in line and any(c in line for c in '=1234567890'):
                print(f"  {fname}:{i}: {line.rstrip()}")