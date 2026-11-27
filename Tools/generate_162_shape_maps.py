#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""[162-2차] 허브 모양 map 생성 — 기존 색 map(map_162_herb.json)의 color_ops와
shape 아키타입(derivative_shape.py ARCH/ATTR_MOD과 동일 규칙)을 결합해
Tools/map_162_herb_shape.json 산출. 40 entries, id 소문자 파일명 계약 유지.
"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from derivative_shape import ARCH, ATTR_MOD, SRC_BY_ATTR  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HERB_MAP = os.path.join(ROOT, 'Tools/map_162_herb.json')
OUT = os.path.join(ROOT, 'Tools/map_162_herb_shape.json')

def main():
    color_map = json.load(open(HERB_MAP, encoding='utf-8'))
    assert len(color_map) == 40, f'색 map 40종 기대: {len(color_map)}'
    entries = []
    for e in color_map:
        dst = e['dst']
        stem = os.path.basename(dst).replace('.glb', '')      # a1 ~ p10
        hid = stem.upper()                                     # A1 ~ P10
        attr, idx = hid[0], int(hid[1:])
        params = dict(ARCH[idx])
        lm, wm = ATTR_MOD[attr]
        params['length_scale'] = round(params['length_scale'] * lm, 4)
        params['width_scale'] = round(params['width_scale'] * wm, 4)
        entries.append({
            'src': SRC_BY_ATTR[attr],
            'dst': dst,
            'note': e['note'] + f' [{attr}{idx} 모양]',
            'shape': params,
            'color_ops': e['ops'],
        })
    json.dump(entries, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(f'{OUT}: {len(entries)} entries')
    # 서명 유니크 사전 검증(이론치) — (length, width) 조합 전수 유니크 확인
    sigs = {}
    for e in entries:
        p = e['shape']
        sigs.setdefault((p['length_scale'], p['width_scale']), []).append(os.path.basename(e['dst']))
    dups = {k: v for k, v in sigs.items() if len(v) > 1}
    print(f'이론 (length,width) 유니크: {len(sigs)}/40 — 중복 {len(dups)}', dups if dups else '')
    assert not dups, '이론 서명 중복 — 아키타입/모디파이어 조정 필요'
    print('OK')

if __name__ == '__main__':
    main()
