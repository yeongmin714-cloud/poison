#!/usr/bin/env python3
# Figma 저장 트리(figma-file.json) → 창별 전체 스펙 덤프 (타이포/음영/그리드)
# 산출: spec/<frame-id>.json + spec/SPEC-INDEX.md  (2026-10-08, 타이포·글래스 정합용)
import json, os, re

BASE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(BASE, '..', 'figma-file.json')
OUT = BASE

FRAMES = {
    '69:4':   ('soldier-management-ui',  '병사관리'),
    '84:4':   ('equipment-inventory-panel', '스테이터스+장비'),
    '15:4':   ('inventory-detail-storage', '인벤+창고'),
    '64:2':   ('crafting-panel-cooking',  '요리'),
    '64:337': ('crafting-panel-alchemy',  '연금'),
    '70:4':   ('loot-panel',              '전리품'),
    '93:230': ('quest-window',            '퀘스트창'),
    '23:6':   ('hud-canvas',              '상시HUD'),
}

def hexcol(paint):
    if not isinstance(paint, dict): return None
    c = paint.get('color')
    if not c:
        g = paint.get('gradientStops')
        if g:
            return 'grad(' + ','.join(f"{hexcol({'color': s['color']})}@{round(s['position'], 2)}" for s in g) + ')'
        return None
    return f"#{int(c['r']*255):02X}{int(c['g']*255):02X}{int(c['b']*255):02X}@{round(paint.get('opacity',1),2)}"

def node_spec(n, origin, depth):
    b = n.get('absoluteBoundingBox') or {}
    spec = {
        'id': n.get('id'), 'name': n.get('name'), 'type': n.get('type'), 'depth': depth,
        'x': round(b.get('x', 0) - origin['x'], 1), 'y': round(b.get('y', 0) - origin['y'], 1),
        'w': round(b.get('width', 0), 1), 'h': round(b.get('height', 0), 1),
    }
    if n.get('layoutMode'):
        spec['layout'] = n['layoutMode']
        if n.get('itemSpacing') is not None: spec['gap'] = round(n['itemSpacing'], 1)
        pads = [n.get('paddingLeft'), n.get('paddingTop'), n.get('paddingRight'), n.get('paddingBottom')]
        if any(p is not None for p in pads):
            spec['pad'] = [None if p is None else round(p, 1) for p in pads]
    if n.get('type') == 'TEXT':
        st = n.get('style', {})
        spec['text'] = (n.get('characters') or '')[:40]
        spec['fontSize'] = st.get('fontSize')
        spec['fontWeight'] = st.get('fontWeight') or st.get('fontStyle')
        spec['lineHeight'] = st.get('lineHeightPx')
        spec['letterSpacing'] = st.get('letterSpacing')
    fills = [hexcol(f) for f in (n.get('fills') or []) if isinstance(f, dict) and f.get('visible', True)]
    if fills: spec['fill'] = fills
    strokes = [hexcol(s) for s in (n.get('strokes') or []) if isinstance(s, dict) and s.get('visible', True)]
    if strokes:
        spec['stroke'] = strokes
        spec['strokeWeight'] = round(n.get('strokeWeight', 0), 2)
    if n.get('cornerRadius') is not None: spec['radius'] = round(n['cornerRadius'], 1)
    effects = []
    for e in (n.get('effects') or []):
        if not isinstance(e, dict): continue
        if e.get('visible', True) is False: continue
        col = e.get('color') or {}
        effects.append({
            'type': e.get('type'),
            'radius': round(e.get('radius', 0), 1),
            'y': round((e.get('offset') or {}).get('y', 0), 1),
            'spread': round(e.get('spread', 0), 1),
            'a': round(col.get('a', 0), 2),
            'color': hexcol(e),
        })
    if effects: spec['effects'] = effects
    return spec

def dump_frame(fid):
    d = json.load(open(SRC, encoding='utf-8'))
    nodes = {}
    def walk(n):
        if isinstance(n, dict):
            if n.get('id'): nodes[n['id']] = n
            for c in n.get('children', []) or []: walk(c)
    walk(d['document'])
    fr = nodes.get(fid)
    if fr is None:
        print(f"[MISS] {fid}"); return None
    b = fr.get('absoluteBoundingBox'); origin = {'x': b['x'], 'y': b['y']}
    specs = []
    def rec(nid, depth):
        specs.append(node_spec(nodes[nid], origin, depth))
        for c in nodes[nid].get('children', []) or []:
            rec(c['id'], depth + 1)
    rec(fid, 0)
    data = {'frame': fid, 'name': fr.get('name'), 'origin': [round(origin['x'],1), round(origin['y'],1)],
            'root': {'fill': [hexcol(f) for f in (fr.get('fills') or []) if isinstance(f,dict) and f.get('visible',True)]},
            'nodes': specs}
    out = os.path.join(OUT, f"{fid.replace(':','-')}.spec.json")
    json.dump(data, open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    return out, len(specs)

def md_table(fid, label):
    j = json.load(open(os.path.join(OUT, f"{fid.replace(':','-')}.spec.json"), encoding='utf-8'))
    lines = [f"### {label} `{fid}` — {j['name']}", "",
             "루트 fill: " + json.dumps(j['root']['fill']), "",
             "| 노드 | bounds(x,y,w,h) | 레이아웃 | 타이포 | 스타일 |", "|---|---|---|---|---|"]
    for n in j['nodes']:
        if n['depth'] > 3: continue
        bounds = f"({n['x']},{n['y']},{n['w']},{n['h']})"
        layout = f"{n.get('layout','')}" + (f" gap={n['gap']}" if 'gap' in n else "") + (f" pad={n['pad']}" if 'pad' in n else "")
        typo = f"{n.get('fontSize','')}px/{n.get('fontWeight','')}" if n['type'] == 'TEXT' else ""
        typo += f" '{n.get('text','')[:18]}'" if 'text' in n else ""
        style = []
        if 'fill' in n: style.append('fill=' + ','.join(n['fill']))
        if 'stroke' in n: style.append(f"stroke={','.join(n['stroke'])}@{n.get('strokeWeight','')}")
        if 'radius' in n: style.append(f"r={n['radius']}")
        for e in n.get('effects', []):
            style.append(f"{e['type']}(r={e['radius']},y={e['y']},a={e['a']})")
        lines.append(f"| {n['name']} | {bounds} | {layout} | {typo} | {' '.join(style)} |")
    return '\n'.join(lines) + '\n'

rows = []
for fid, (name, label) in FRAMES.items():
    r = dump_frame(fid)
    if r: rows.append((fid, label, r[1]))
    print(f"[OK] {fid} {label}: {r[1] if r else 0} nodes")

idx = ['# Figma 노드 스펙 인덱스 (2026-10-08, 타이포·글래스 정합용)', '',
       '생성: spec/dump_spec.py — figma-file.json 기준, 저장 리비전의 진실치. bounds는 프레임 origin 보정.', '']
for fid, label, cnt in rows:
    idx.append(f"- `{fid}` {label}: {cnt} nodes → {fid.replace(':','-')}.spec.json")
idx.append('')
for fid, (name, label) in FRAMES.items():
    try:
        idx.append(md_table(fid, label))
    except Exception as e:
        idx.append(f"### {label} `{fid}` 표 생성 실패: {e}")
open(os.path.join(OUT, 'SPEC-INDEX.md'), 'w', encoding='utf-8').write('\n'.join(idx))
print('SPEC-INDEX.md written')