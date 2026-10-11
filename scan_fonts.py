import re, glob, os
ROOT = "/mnt/c/Unity/code/Assets/Scripts/UI/Toolkit"
NUM = re.compile(r'\b(\d+(?:\.\d+)?)f?\b')
LINEASSIGN = re.compile(r'\.fontSize\s*=\s*([^;]+);')
FAMILY = {9.6,12,13.2,14.4,15.6,16.8,19.2,21.6,24,26.4,31.2,38.4}

for path in sorted(glob.glob(ROOT+"/*.cs")):
    with open(path, encoding='utf-8') as f:
        for i, line in enumerate(f, 1):
            for m in LINEASSIGN.finditer(line):
                expr = m.group(1).strip()
                nums = [n for n in NUM.findall(expr)]
                if not nums: continue
                lm = re.fullmatch(r'(\d+(?:\.\d+)?)f?', expr)
                if lm: continue  # plain literal handled elsewhere
                tag = "SPECIAL" if any(float(x) not in FAMILY and float(x) not in (0,) for x in nums) else "family/scale"
                print(f"{tag} {path}:{i} fontSize = {expr}")
