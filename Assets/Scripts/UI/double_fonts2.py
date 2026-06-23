#!/usr/bin/env python3
"""Double fontSize values in specific files, handling semicolons and other trailing chars."""
import re
import os

ui_dir = '/mnt/c/Unity/code/Assets/Scripts/UI'

# Files that need another pass
files_to_fix = [
    'RecipeWindow.cs',
    'AlchemyUI.cs',
    'NPCDialogueWindow.cs',
    'MonsterLevelLabel.cs',
    'SettingsMenuUI.cs',
    'PlayerFlagRegistrationWindow.cs',
    'ChurchUI.cs',
    'HerbRespawnUI.cs',
    'GuardWorldSpaceHUD.cs',
    'AchievementSystem.cs',
    'DeathScreenUI.cs',
]

def double_fontsize(match):
    full = match.group(0)
    num = int(match.group(2))
    new_num = num * 2
    prefix = match.group(1)
    suffix = match.group(3)
    return f'{prefix}{new_num}{suffix}'

total_changes = 0
for fname in files_to_fix:
    fpath = os.path.join(ui_dir, fname)
    if not os.path.exists(fpath):
        print(f"  {fname}: NOT FOUND")
        continue
    with open(fpath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Match: fontSize = \d+ followed by any non-digit char or end
    new_content, n = re.subn(
        r'(fontSize\s*=\s*)(\d+)([^0-9])',
        double_fontsize,
        content
    )
    
    if n > 0:
        with open(fpath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        total_changes += n
        print(f"  {fname}: {n} fontSize values doubled")

print(f"\nTotal: {total_changes} additional fontSize values doubled")

# Verify the missing ones
print("\n=== REMAINING fontSize in specific files ===")
for fname in files_to_fix:
    fpath = os.path.join(ui_dir, fname)
    if os.path.exists(fpath):
        with open(fpath, 'r') as f:
            for i, line in enumerate(f, 1):
                if 'fontSize' in line:
                    print(f"  {fname}:{i}: {line.rstrip()}")