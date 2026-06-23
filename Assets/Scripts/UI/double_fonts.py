#!/usr/bin/env python3
"""Double all fontSize values in UI .cs files, skipping already-doubled files."""
import re
import os
import sys

# Files already modified (skip these)
already_done = {'UIStyleManager.cs', 'HUD.cs', 'MinimapUI.cs'}

ui_dir = '/mnt/c/Unity/code/Assets/Scripts/UI'
cs_files = [f for f in os.listdir(ui_dir) if f.endswith('.cs') and f not in already_done]
cs_files.sort()

def double_fontsize(match):
    """Callback that doubles the fontSize value."""
    full = match.group(0)
    num = int(match.group(2))
    new_num = num * 2
    # Preserve exact spacing - replace only the number
    prefix = match.group(1)  # includes 'fontSize = '
    suffix = match.group(3)  # includes trailing comma or space
    return f'{prefix}{new_num}{suffix}'

total_changes = 0
for fname in cs_files:
    fpath = os.path.join(ui_dir, fname)
    with open(fpath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Match: fontSize = \d+  (with optional trailing comma/space/newline)
    # Pattern: fontSize = (number)
    new_content, n = re.subn(
        r'(fontSize\s*=\s*)(\d+)(\s*[,\)\n\r}])',
        double_fontsize,
        content
    )
    
    if n > 0:
        with open(fpath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        total_changes += n
        print(f"  {fname}: {n} fontSize values doubled")

print(f"\nTotal: {total_changes} fontSize values doubled across {sum(1 for f in cs_files if True)} files")

# Verify
print("\n=== VERIFICATION ===")
os.system(f'grep -c "fontSize" {ui_dir}/*.cs 2>/dev/null | sort -t: -k2 -rn | head -40')
print()
os.system(f'grep -o "fontSize" {ui_dir}/*.cs 2>/dev/null | wc -l')
