#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""[162차] 약초/마약/조합물약/버프물약 전종 파생 GLB 색 배분표 생성 — 결정론.

입력: Assets/Resources/GAME_DATA.md (§1 재료 40종, §2.1~2.4 조합 82행, §2.5 마약 10단계)
      TestOutput/162_herb_texture_stats.json (원본 텍스처 실측 평균색)
출력: Tools/map_162_herb.json / map_162_drug.json / map_162_combo.json / map_162_buff.json

배분 설계 (계획 docs/162_HERB_DRUG_VISUAL_DISTINCTION_PLAN.md §1):
- 약초: 밴드 중심 = 원본 텍스처 실측 hue (상대 오프셋 — 구분성 유지)
  hue 오프셋 5단계 [-24,-12,0,+12,+24] × 명도 2단계(6~10번 −14%) = 10종/밴드
  2색 스킴은 액센트 존 부재(텍스처 기반, 단일 머티리얼)로 폴백 → 전체 틴트
  틴트 = 목표 평균색(HSV 회전+명도) / 텍스처 실측 평균색 (요소별 나눗셈 보정)
- 마약: 액체 hue 260°→350° + sat 0.5→0.85 + val 0.95→0.45 (중독성 그라데이션), 유리 알파 0.28
- 조합: 액체 hue = 재료1/2 밴드위치의 각도평균, sat 0.8 / val 0.46 표준, 유리 알파 0.28
- 버프: potion_hp 원본 — hp_small(hue2/표준) hp_big(hue2/명도↑) attack(적) defense(청) agility(황)
"""
import colorsys, json, os, re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = os.path.join(ROOT, 'Assets/Resources/GAME_DATA.md')
STATS = os.path.join(ROOT, 'TestOutput/162_herb_texture_stats.json')
OUT = os.path.join(ROOT, 'Tools')

# ── 원본 GLB 실측치 (TestOutput/derivative_probe.json — potion-bottle 액체 z1) ──
BOTTLE_LIQUID = {'hue': 99.90577221870001, 'sat': 0.5185578214532001, 'val': 0.09084171056747437}
GLASS_ALPHA = 0.28  # [92차] 유리 계약

stats = json.load(open(STATS, encoding='utf-8'))
TEX = {k: dict(hsv=v['hsv'], mean=[c/255.0 for c in v['mean_rgb']]) for k, v in stats.items()}

# ── GAME_DATA 파싱 ──
content = open(GAME, encoding='utf-8').read()

# §1 재료: | A1 | 쓴풀 | 설명 | → name→id
herb_by_name = {}
sec1 = content[content.find('## 🌿 1'):content.find('## 🧪 2')]
for m in re.finditer(r'^\|\s*([A-Z]\d{1,2})\s*\|\s*([^|]+)\|', sec1, re.M):
    herb_by_name[m.group(2).strip()] = m.group(1).strip()
assert len(herb_by_name) == 40, f'재료 40종 파싱 실패: {len(herb_by_name)}'

# 밴드 중심 = 텍스처 실측 hue
BAND = {'A': TEX['herb_red']['hsv'][0], 'M': TEX['herb_purple']['hsv'][0],
        'H': TEX['herb_green']['hsv'][0], 'P': TEX['herb_blue']['hsv'][0]}
HUE_OFF = [-24.0, -12.0, 0.0, 12.0, 24.0]

def band_of(herb_id): return BAND[herb_id[0]]

def herb_target_hsv(herb_id):
    idx = int(herb_id[1:])
    off = HUE_OFF[(idx - 1) % 5]
    dm = 0.86 if idx >= 6 else 1.0
    t = TEX[f'herb_{"red" if herb_id[0]=="A" else "purple" if herb_id[0]=="M" else "green" if herb_id[0]=="H" else "blue"}']['hsv']
    return ((band_of(herb_id) + off) % 360.0, t[1], t[2] * dm), t

def tint_rgb(target_hsv, tex_hsv):
    """텍스처 곱연산 보정 틴트 = 목표평균색 / 실측평균색(요소별), 클램프 [0,1]."""
    rt = colorsys.hsv_to_rgb(target_hsv[0]/360, target_hsv[1], min(1.0, target_hsv[2]))
    mean = tuple(TEX[f'herb_{"red" if True else ""}']['mean'])  # placeholder replaced below
    return rt, mean

def build_herb_map():
    entries = []
    base_src = {'A': 'Assets/Resources/Models/UserProvided/herb_red.glb',
                'M': 'Assets/Resources/Models/UserProvided/herb_purple.glb',
                'H': 'Assets/Resources/Models/UserProvided/herb_green.glb',
                'P': 'Assets/Resources/Models/UserProvided/herb_blue.glb'}
    for name, hid in sorted(herb_by_name.items(), key=lambda kv: kv[1]):
        attr = hid[0]
        (h, s, v), tex_hsv = herb_target_hsv(hid)
        rt = colorsys.hsv_to_rgb(h/360.0, s, min(1.0, v))
        mean = TEX['herb_red' if attr=='A' else 'herb_purple' if attr=='M' else 'herb_green' if attr=='H' else 'herb_blue']['mean']
        bcf = [max(0.0, min(1.0, rt[i]/mean[i] if mean[i] > 1e-6 else rt[i])) for i in range(3)]
        entries.append({
            'src': base_src[attr],
            'dst': f'Assets/Resources/Models/UserProvided/{hid.lower()}.glb',
            'note': f'{hid} {name}',
            'ops': [{'zone': 0, 'rgb': [round(c, 5) for c in bcf]}],
        })
    return entries

# ── 마약 10단계 (§2.5) ──
def liquid_ops(target_hue, target_sat, target_val):
    dh = target_hue - BOTTLE_LIQUID['hue']
    # 최단 회전 (±180)
    while dh > 180.0: dh -= 360.0
    while dh < -180.0: dh += 360.0
    return {'hue-deg': round(dh, 5), 'sat': round(target_sat / BOTTLE_LIQUID['sat'], 5),
            'val': round(target_val / BOTTLE_LIQUID['val'], 5)}

def build_drug_map():
    entries = []
    for k in range(1, 11):
        t = (k - 1) / 9.0
        hue = 260.0 + (350.0 - 260.0) * t
        sat = 0.50 + (0.85 - 0.50) * t
        val = 0.95 - (0.95 - 0.45) * t
        entries.append({
            'src': 'Assets/Resources/Models/Polyfork/potion-bottle-099ad7.glb',
            'dst': f'Assets/Resources/Models/UserProvided/drug_{k:02d}.glb',
            'note': f'마약 단계 {k} (중독성 그라데이션)',
            'ops': [{'zone': 0, 'alpha': GLASS_ALPHA},
                    dict({'zone': 1}, **liquid_ops(hue, sat, val))],
        })
    return entries

# ── 조합 82행 (§2.1~2.4) ──
def ang_mean(h1, h2):
    d = ((h2 - h1 + 180.0) % 360.0) - 180.0
    return (h1 + d / 2.0) % 360.0

def build_combo_map():
    sec2 = content[content.find('## 🧪 2'):content.find('### 2.5')]
    rows = re.findall(r'^\|\s*([^|]+)\|\s*([^|]+)\|\s*([^|]+)\|\s*([^|]+)\|', sec2, re.M)
    entries, skipped = [], []
    for r in rows:
        n1, n2, result, effect = [x.strip() for x in r]
        if n1.startswith(':') or n2.startswith(':') or result.startswith(':'): continue
        if n1 in ('재료1',) or n2 in ('재료2',) or result in ('결과물',): continue
        if n1 not in herb_by_name or n2 not in herb_by_name:
            skipped.append((n1, n2, result)); continue
        id1, id2 = herb_by_name[n1], herb_by_name[n2]
        h1 = band_of(id1) + HUE_OFF[(int(id1[1:]) - 1) % 5]
        h2 = band_of(id2) + HUE_OFF[(int(id2[1:]) - 1) % 5]
        hue = ang_mean((h1 % 360.0), (h2 % 360.0))
        dim1 = 0.86 if int(id1[1:]) >= 6 else 1.0
        val = 0.46 * dim1
        cid = f'combo_{id1}_{id2}' if id1 < id2 else f'combo_{id2}_{id1}'
        entries.append({
            'src': 'Assets/Resources/Models/Polyfork/potion-bottle-099ad7.glb',
            'dst': f'Assets/Resources/Models/UserProvided/{cid.lower()}.glb',
            'note': f'{result} ({id1}+{id2})',
            'ops': [{'zone': 0, 'alpha': GLASS_ALPHA},
                    dict({'zone': 1}, **liquid_ops(hue, 0.80, val))],
        })
    return entries, skipped

# ── 버프 5종 ──
def build_buff_map():
    src = 'Assets/Resources/Models/Polyfork/potion-bottle-099ad7.glb'
    dst = lambda mid: f'Assets/Resources/Models/UserProvided/{mid}.glb'
    specs = [
        ('potion_hp_small', 2.0,  0.85, 0.46, '체력 소형 — potion_hp와 동일 계열(표준)'),
        ('potion_hp_big',   2.0,  0.95, 0.62, '체력 대형 — 동 hue 명도↑'),
        ('potion_attack',   355.0, 0.90, 0.50, '공격 버프 — 진적'),
        ('potion_defense',  211.0, 0.80, 0.55, '방어 버프 — 청'),
        ('potion_agility',  55.0,  0.85, 0.60, '민첩 버프 — 황금'),
    ]
    return [{'src': src, 'dst': dst(mid), 'note': note,
             'ops': [{'zone': 0, 'alpha': GLASS_ALPHA},
                     dict({'zone': 1}, **liquid_ops(h, s, v))]}
            for mid, h, s, v, note in specs]

if __name__ == '__main__':
    herb = build_herb_map()
    drug = build_drug_map()
    combo, skipped = build_combo_map()
    buff = build_buff_map()
    for name, data in [('map_162_herb', herb), ('map_162_drug', drug),
                       ('map_162_combo', combo), ('map_162_buff', buff)]:
        path = os.path.join(OUT, name + '.json')
        json.dump(data, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        print(f'{path}: {len(data)} entries')
    print('조합 스킵(미매핑 재료):', skipped if skipped else '없음')
    # 자가 검증: 조합 id 형식
    import re as _re
    pat = _re.compile(r'^combo_[a-z]\d{1,2}_[a-z]\d{1,2}\.glb$')
    bad = [e for e in combo if not pat.match(os.path.basename(e['dst']))]
    assert not bad, f'id 형식 위반: {bad[:3]}'
    print('조합 id 형식 검증 OK:', len(combo), 'entries')
