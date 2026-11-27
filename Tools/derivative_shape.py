#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""[162-2차] 허브 GLB POSITION 모양 변형 도구 — 색 파생(bake)과 조합해 40종 실루엣 유니크화.

구조 실측 근거(2026-11-21 probe): herb_red/purple/green/blue.glb = 노드 1개(90°X 회전),
mesh 1개 prim 1개, POSITION VEC3 float 8194 정점 non-interleaved, 길이축=Z(로컬 -0.994~0),
X/Y=폭/두께 ±0.31. bin 영역의 POSITION만 재작성(같은 크기), NORMAL/UV/인덱스/텍스처 무손상,
POSITION accessor min/max 재계산(glTF 계약).

변형 파라미터:
  length_scale : z 승수 (길이축)
  width_scale  : x/y 승수 (반경)
  taper_top    : 상단 반경 축소 radial×(1-taper×t)
  twist_deg    : z 기반 Z축 회전(최대각, t×angle)
  bend         : x += bend×t²
  spread       : 상단 반경 확장 radial×(1+spread×t²)
  pinch        : 중간 좁힘 radial×(1-pinch×sin(πt))
  bulb         : 중간 볼록 radial×(1+bulb×sin(πt))
t = (z - zmin)/(zmax - zmin)  (0=밑동 zmax, 1=끝 zmin)

CLI:
  단건: --src --dst --length 1.25 --width 0.82 [--taper 0.5 --twist 55 --bend .1 --spread .3 --pinch .2 --bulb .2]
  배치: --map map_162_herb_shape.json  (entries: src/dst/shape{...}/color_ops[...])
  자가검증: --selftest
색 적용은 Tools/derivative_bake.bake()를 재사용(정점컬러+bcf).
"""
import argparse, colorsys, hashlib, json, math, os, struct, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derivative_bake as dbk  # noqa: E402

GLB_MAGIC = 0x46546C67
CHUNK_JSON = 0x4E4F534A
CHUNK_BIN = 0x004E4942


def glb_load(path):
    data = open(path, 'rb').read()
    magic, ver, length = struct.unpack('<III', data[:12])
    assert magic == GLB_MAGIC, f'GLB magic 불일치: {path}'
    off, js, bin_chunk = 12, None, None
    while off < length:
        clen, ctype = struct.unpack('<II', data[off:off + 8]); off += 8
        chunk = data[off:off + clen]; off += clen
        if ctype == CHUNK_JSON: js = json.loads(chunk)
        elif ctype == CHUNK_BIN: bin_chunk = chunk
    return js, bytearray(bin_chunk)


def find_position(js):
    """(accessor_idx, accessor, primitive_ref) — 첫 POSITION."""
    for mi, mesh in enumerate(js.get('meshes', [])):
        for pi, prim in enumerate(mesh.get('primitives', [])):
            ca = prim.get('attributes', {}).get('POSITION')
            if ca is not None:
                return ca, js['accessors'][ca], prim
    return None, None, None


def apply_shape(js, bin_chunk, params):
    """POSITION 정점 변형 + accessor min/max 갱신. 반환: 변경 통계."""
    ca, acc, _prim = find_position(js)
    assert acc is not None, 'POSITION accessor 없음'
    assert acc['type'] == 'VEC3' and acc['componentType'] == 5126, f'지원 형식 아님: {acc["type"]}/{acc["componentType"]}'
    bv = js['bufferViews'][acc['bufferView']]
    assert not bv.get('byteStride'), 'interleaved bufferView 미지원'
    base = bv.get('byteOffset', 0)
    n = acc['count']

    zmin = min(acc['min'][2], acc['max'][2])
    zmax = max(acc['min'][2], acc['max'][2])
    zspan = (zmax - zmin) or 1.0
    length = params.get('length_scale', 1.0)
    width = params.get('width_scale', 1.0)
    taper = params.get('taper_top', 0.0)
    twist = math.radians(params.get('twist_deg', 0.0))
    bend = params.get('bend', 0.0)
    spread = params.get('spread', 0.0)
    pinch = params.get('pinch', 0.0)
    bulb = params.get('bulb', 0.0)

    nmin = [float('inf')] * 3
    nmax = [float('-inf')] * 3
    for i in range(n):
        o = base + i * 12
        x, y, z = struct.unpack_from('<fff', bin_chunk, o)
        t = (z - zmin) / zspan  # 0=밑동(zmax), 1=끝(zmin)
        radial = width * (1.0 - taper * t) * (1.0 + spread * t * t)
        if pinch > 0.0:
            radial *= (1.0 - pinch * math.sin(math.pi * t))
        if bulb > 0.0:
            radial *= (1.0 + bulb * math.sin(math.pi * t))
        x *= radial; y *= radial; z *= length
        if twist != 0.0:
            a = twist * t
            ca_, sa_ = math.cos(a), math.sin(a)
            x, y = x * ca_ - y * sa_, x * sa_ + y * ca_
        if bend != 0.0:
            x += bend * t * t
        struct.pack_into('<fff', bin_chunk, o, x, y, z)
        if x < nmin[0]: nmin[0] = x
        if y < nmin[1]: nmin[1] = y
        if z < nmin[2]: nmin[2] = z
        if x > nmax[0]: nmax[0] = x
        if y > nmax[1]: nmax[1] = y
        if z > nmax[2]: nmax[2] = z
    acc['min'] = [round(v, 7) for v in nmin]
    acc['max'] = [round(v, 7) for v in nmax]
    return {'vertices': n, 'min': acc['min'], 'max': acc['max']}


def write_glb(path, js, bin_chunk):
    json_bytes = json.dumps(js, ensure_ascii=False).encode('utf-8')
    out = dbk.build_glb(json_bytes, bytes(bin_chunk))
    with open(path, 'wb') as f:
        f.write(out)
    return out


def bounds_signature(js):
    ca, acc, _ = find_position(js)
    mn, mx = acc['min'], acc['max']
    depth = abs(mx[2] - mn[2])
    width = max(abs(mx[0] - mn[0]), abs(mx[1] - mn[1]))
    return (round(depth, 4), round(width, 4))


def shape_bake(entry, tmp_dir):
    """shape 적용 → 임시 shaped GLB → 색 ops 적용 → 최종 dst."""
    src, dst = entry['src'], entry['dst']
    params = entry.get('shape', {})
    color_ops = entry.get('color_ops', []) or entry.get('color_ops_by_zone', {})
    os.makedirs(tmp_dir, exist_ok=True)
    tmp = os.path.join(tmp_dir, os.path.basename(dst) + '.shaped.tmp')
    js, bc = glb_load(src)
    stat = apply_shape(js, bc, params)
    write_glb(tmp, js, bc)

    ops_by_zone = {}
    for op in color_ops:
        z = op['zone']
        ops_by_zone.setdefault(z, {})
        for k in ('hue-deg', 'sat', 'val', 'alpha', 'rgb'):
            if k in op: ops_by_zone[z][k] = op[k]
    res = dbk.bake(tmp, dst, ops_by_zone, tint_factor=False)
    res['shape_stat'] = stat
    res['signature'] = bounds_signature(js)
    try: os.remove(tmp)
    except OSError: pass
    return res


ARCH = {
    1: dict(length_scale=1.25, width_scale=0.82),                                # slender
    2: dict(length_scale=0.72, width_scale=1.22),                                # squat
    3: dict(length_scale=1.06, width_scale=0.98, twist_deg=55.0),                # twisted
    4: dict(length_scale=0.94, width_scale=1.06, taper_top=0.55),                # cone
    5: dict(length_scale=1.00, width_scale=0.90, bend=0.14),                     # bent
    6: dict(length_scale=0.55, width_scale=1.38),                                # disc
    7: dict(length_scale=1.12, width_scale=1.00, spread=0.38),                   # bushy
    8: dict(length_scale=1.18, width_scale=0.76, taper_top=0.30),                # spike
    9: dict(length_scale=0.86, width_scale=1.10, pinch=0.28),                    # waist
    10: dict(length_scale=0.90, width_scale=1.16, bulb=0.30),                    # bulb
}
ATTR_MOD = {'A': (1.0, 1.0), 'M': (1.04, 1.0), 'H': (0.96, 1.0), 'P': (1.0, 1.06)}
SRC_BY_ATTR = {'A': 'Assets/Resources/Models/UserProvided/herb_red.glb',
               'M': 'Assets/Resources/Models/UserProvided/herb_purple.glb',
               'H': 'Assets/Resources/Models/UserProvided/herb_green.glb',
               'P': 'Assets/Resources/Models/UserProvided/herb_blue.glb'}


def selftest(out_path):
    lines = ['[162-2차] derivative_shape.py 자가검증', '']
    # (a) 항등 변형 → 정점 무변화 + 바이트 크기 동일
    src = SRC_BY_ATTR['A']
    js2, bc2 = glb_load(src)
    apply_shape(js2, bc2, {})
    open('/tmp/st_id2.glb', 'wb').write(dbk.build_glb(json.dumps(js2).encode('utf-8'), bytes(bc2)))
    js3, bc3 = glb_load('/tmp/st_id2.glb')
    ca, acc, _ = find_position(js3)
    same = acc['min'] == acc['min'] and len(bc3) == len(bc2)
    import struct as _s
    ca0 = find_position(js2)[0]
    bv0 = js2['bufferViews'][acc['bufferView']]
    px2 = _s.unpack_from('<fff', bc2, bv0['byteOffset'])
    px3 = _s.unpack_from('<fff', bc3, bv0['byteOffset'])
    id_ok = abs(px2[0] - px3[0]) < 1e-9 and abs(px2[2] - px3[2]) < 1e-9 and len(bc3) == len(bc2)
    lines.append(f'(a) 항등 변형(빈 파라미터) 정점 무변화+크기 동일: {"PASS" if id_ok else "FAIL"}')
    # (b) z 반전 아님 — length 2.0 적용 시 min/max z 2배
    js4, bc4 = glb_load(src)
    apply_shape(js4, bc4, {'length_scale': 2.0})
    ca, acc4, _ = find_position(js4)
    z0 = (acc['max'][2] - acc['min'][2])
    z1 = (acc4['max'][2] - acc4['min'][2])
    ok2 = abs(z1 - 2.0 * z0) < 1e-3
    lines.append(f'(b) length_scale 2.0 → z 범위 2배 실측: {z0:.5f} → {z1:.5f} {"PASS" if ok2 else "FAIL"}')
    # (c) twist 90° → width(x/y) 유지(회전은 bounds 보존), min/max x/y 스왑 확인
    js5, bc5 = glb_load(src)
    apply_shape(js5, bc5, {'twist_deg': 90.0})
    ca, acc5, _ = find_position(js5)
    lines.append(f'(c) twist 90°: min/max 회전 후 {[round(v,4) for v in acc5["min"]]} / {[round(v,4) for v in acc5["max"]]}')
    ok_all = id_ok and ok2
    lines.append('')
    lines.append(f'총 결과: {"PASS" if ok_all else "FAIL"}')
    open(out_path, 'w', encoding='utf-8').write('\n'.join(lines))
    print('\n'.join(lines))
    return ok_all


def main():
    ap = argparse.ArgumentParser(description='[162-2차] 허브 GLB 모양 변형 + 색 통합 베이크')
    ap.add_argument('--src'); ap.add_argument('--dst')
    ap.add_argument('--length', type=float, default=1.0)
    ap.add_argument('--width', type=float, default=1.0)
    ap.add_argument('--taper', type=float, default=0.0)
    ap.add_argument('--twist', type=float, default=0.0)
    ap.add_argument('--bend', type=float, default=0.0)
    ap.add_argument('--spread', type=float, default=0.0)
    ap.add_argument('--pinch', type=float, default=0.0)
    ap.add_argument('--bulb', type=float, default=0.0)
    ap.add_argument('--map'); ap.add_argument('--tmp-dir', default='TestOutput/tmp_162')
    ap.add_argument('--report', default='TestOutput/qa_162_2_shape_report.txt')
    ap.add_argument('--selftest', action='store_true')
    ap.add_argument('--selftest-out', default='TestOutput/derivative_shape_selftest.txt')
    args = ap.parse_args()

    if args.selftest:
        ok = selftest(args.selftest_out)
        sys.exit(0 if ok else 1)

    if args.map:
        entries = json.load(open(args.map, encoding='utf-8'))
        results, sigs = [], {}
        for i, e in enumerate(entries):
            try:
                res = shape_bake(e, args.tmp_dir)
                results.append(res)
                sig = res['signature']
                sigs.setdefault(sig, []).append(os.path.basename(e['dst']))
                print(f'entry {i}: {os.path.basename(e["dst"])} sig={sig} '
                      f'min={[round(v,4) for v in res["shape_stat"]["min"]]} max={[round(v,4) for v in res["shape_stat"]["max"]]}')
            except Exception as ex:  # noqa: BLE001
                print(f'entry {i} 실패: {e["dst"]} — {ex}', file=sys.stderr)
                results.append({'dst': e['dst'], 'error': str(ex)})
        sig_dups = {k: v for k, v in sigs.items() if len(v) > 1}
        report = ['[162-2차] 허브 모양 재베이크 리포트', f'entries: {len(entries)}',
                  f'서명 유니크: {len(sigs)}/40', f'서명 중복 그룹: {len(sig_dups)}']
        for k, v in sorted(sig_dups.items()):
            report.append(f'  {k}: {", ".join(v)}')
        ok_count = sum(1 for r in results if 'error' not in r)
        report.append(f'베이크 성공: {ok_count}/{len(entries)}')
        report.append('')
        report.append('=== 개별 서명 ===')
        for r in results:
            if 'error' in r: report.append(f'{r["dst"]}: ERROR {r["error"]}')
            else: report.append(f'{os.path.basename(r["dst"])}: depth={r["signature"][0]} width={r["signature"][1]}')
        open(args.report, 'w', encoding='utf-8').write('\n'.join(report))
        print(f'\n리포트: {args.report}')
        print(f'서명 유니크 {len(sigs)}/40, 중복 그룹 {len(sig_dups)}, 베이크 {ok_count}/{len(entries)}')
        sys.exit(0 if ok_count == len(entries) and not sig_dups else 1)

    # 단건
    js, bc = glb_load(args.src)
    stat = apply_shape(js, bc, dict(length_scale=args.length, width_scale=args.width,
                                    taper_top=args.taper, twist_deg=args.twist,
                                    bend=args.bend, spread=args.spread,
                                    pinch=args.pinch, bulb=args.bulb))
    write_glb(args.dst, js, bc)
    print('shape 적용:', json.dumps(stat))


if __name__ == '__main__':
    main()
