#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""derivative_bake.py — [Phase 0] 포이즌 파생 GLB 베이크 도구 (Python3 stdlib 전용).

원본 GLB(src)를 무손상으로 두고, COLOR_0 정점컬러 및/또는 머티리얼 baseColorFactor에
hue(도 단위 회전) / sat·val(승수) 재매핑을 적용한 파생본을 새 파일(dst)로만 생성한다.

- BIN 청크에서 재작성되는 바이트는 대상 COLOR_0 accessor 영역뿐이다
  (텍스처/버퍼의 나머지 원본 바이트는 그대로 유지 — 베이크 후 바이트 범위 검증 포함).
- 머티리얼 baseColorFactor: --alpha 지정 시 알파 성분만 변경, --tint-factor(또는 해당 존에
  COLOR_0가 없고 팩터가 유채색인 경우) 지정 시 RGB에 hue/sat/val 재매핑.
- 항등 연산(hue 0 / sat 1 / val 1)은 정점 바이트를 건드리지 않는다 → 출력이 src와 바이트 동일.
- 정점컬러 지원: 5126 float / 5121 ubyte / 5123 ushort (normalized 여부 무관),
  VEC3/VEC4(VEC4는 알파 채널 보존). signed byte/short는 normalized만 지원.

CLI:
  단일:
    python3 Tools/derivative_bake.py --src <glb> --dst <glb> \
        --hue-deg <deg> --sat <mult> --val <mult> \
        (--zone <idx[,idx...]> | --zone auto --base-hue <deg> [--hue-threshold <deg>] [--exclude-zone <idx,...>]) \
        [--alpha <a>] [--tint-factor]
  배치 (--map):
    python3 Tools/derivative_bake.py --map ops.json
      ops.json = [{"src":..., "dst":..., "ops":[{"zone":1,"hue-deg":...,"sat":...,"val":...,"alpha":...}]}]
      (경로는 절대경로 또는 CWD 기준 상대경로)
  자가검증:
    python3 Tools/derivative_bake.py --selftest [--selftest-out TestOutput/derivative_selftest.txt]

의존성: Python3 표준 라이브러리만 사용 (pip 설치 금지). GLB 파싱은 Tools/derivative_probe.py 재사용.
"""
import argparse
import colorsys
import hashlib
import json
import math
import os
import shutil
import struct
import sys
import tempfile
from datetime import datetime, timezone

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derivative_probe as dp  # noqa: E402

GLB_MAGIC = dp.GLB_MAGIC
CHUNK_JSON = dp.CHUNK_JSON
CHUNK_BIN = dp.CHUNK_BIN


# ---------------------------------------------------------------- 유틸

def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_file(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def pad4(data, fill):
    n = (4 - len(data) % 4) % 4
    return data + fill * n


def build_glb(json_bytes, bin_bytes):
    """JSON/BIN 청크로 GLB 파일 조립 (JSON은 공백 0x20, BIN은 0x00 패딩)."""
    json_bytes = pad4(json_bytes, b" ")
    bin_bytes = pad4(bin_bytes, b"\x00")
    total = 12 + 8 + len(json_bytes) + 8 + len(bin_bytes)
    out = bytearray()
    out += struct.pack("<III", GLB_MAGIC, 2, total)
    out += struct.pack("<II", len(json_bytes), CHUNK_JSON)
    out += json_bytes
    out += struct.pack("<II", len(bin_bytes), CHUNK_BIN)
    out += bin_bytes
    return bytes(out)


def extract_chunks(data):
    """파일 바이트에서 (ctype, chunk_bytes) 목록 추출."""
    chunks = []
    off = 12
    while off + 8 <= len(data):
        clen, ctype = struct.unpack_from("<II", data, off)
        chunks.append((ctype, data[off + 8: off + 8 + clen]))
        off += 8 + clen
    return chunks


def diff_ranges(a, b):
    """두 바이트열의 서로 다른 구간 목록 [(start, end), ...]."""
    ranges = []
    n = min(len(a), len(b))
    i = 0
    while i < n:
        if a[i] != b[i]:
            j = i
            while j < n and a[j] != b[j]:
                j += 1
            ranges.append((i, j))
            i = j
        else:
            i += 1
    if len(a) != len(b):
        ranges.append((n, max(len(a), len(b))))
    return ranges


def hue_distance(a, b):
    """원형 hue 거리(도, 0..180)."""
    d = abs(a - b) % 360.0
    return min(d, 360.0 - d)


def shortest_deg(a, b):
    """a→b 최단 부호 있는 회전(도)."""
    d = (b - a) % 360.0
    if d > 180.0:
        d -= 360.0
    return d


# ---------------------------------------------------------------- HSV 변환

def transform_rgb(rgb, hue_deg, sat_m, val_m):
    """RGB에 hue 회전(도)/sat·val 승수 적용. 항등 연산이면 입력을 비트 그대로 반환."""
    if hue_deg == 0.0 and sat_m == 1.0 and val_m == 1.0:
        return (rgb[0], rgb[1], rgb[2])
    h, s, v = colorsys.rgb_to_hsv(rgb[0], rgb[1], rgb[2])
    if hue_deg != 0.0:
        h = (h + hue_deg / 360.0) % 1.0
    if sat_m != 1.0:
        s = min(1.0, max(0.0, s * sat_m))
    if val_m != 1.0:
        v = min(1.0, max(0.0, v * val_m))
    r, g, b = colorsys.hsv_to_rgb(h, s, v)
    return (r, g, b)


def raw_to_float(raw, comp_type, normalized):
    """액세서 원시값 → 0..1 float."""
    if comp_type == 5126:
        return raw
    if comp_type == 5121:
        return raw / 255.0
    if comp_type == 5123:
        return raw / 65535.0
    if comp_type == 5120:
        if not normalized:
            raise ValueError("정규화되지 않은 signed byte COLOR_0는 미지원")
        return max(raw, -127) / 127.0
    if comp_type == 5122:
        if not normalized:
            raise ValueError("정규화되지 않은 signed short COLOR_0는 미지원")
        return max(raw, -32767) / 32767.0
    raise ValueError("미지원 componentType: %d" % comp_type)


def float_to_raw(val, comp_type, normalized):
    """0..1 float → 액세서 원시값 (반올림 + 클램프)."""
    if comp_type == 5126:
        return val
    if comp_type == 5121:
        q = int(math.floor(val * 255.0 + 0.5))
        return max(0, min(255, q))
    if comp_type == 5123:
        q = int(math.floor(val * 65535.0 + 0.5))
        return max(0, min(65535, q))
    if comp_type == 5120:
        scale = 127.0 if val >= 0 else 128.0
        q = int(math.floor(val * scale + (0.5 if val >= 0 else -0.5)))
        return max(-128, min(127, q))
    if comp_type == 5122:
        scale = 32767.0 if val >= 0 else 32768.0
        q = int(math.floor(val * scale + (0.5 if val >= 0 else -0.5)))
        return max(-32768, min(32767, q))
    raise ValueError("미지원 componentType: %d" % comp_type)


def rewrite_accessor(bin_arr, js, acc_idx, op):
    """COLOR_0 accessor 값에 HSV 연산 적용 (bin_arr를 직접 수정).

    항등 연산이면 바이트를 건드리지 않는다. 반환: 변경 정보 딕셔너리.
    """
    acc = js["accessors"][acc_idx]
    acc_type = acc["type"]
    if acc_type not in ("VEC3", "VEC4"):
        raise ValueError("COLOR_0 타입은 VEC3/VEC4만 지원: %s" % acc_type)
    comp_type = acc["componentType"]
    ncomp = dp.TYPE_NCOMP[acc_type]
    normalized = bool(acc.get("normalized", False))
    if acc.get("sparse"):
        raise ValueError("sparse accessor 미지원: accessor %d" % acc_idx)
    bv = js["bufferViews"][acc["bufferView"]]
    start = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = bv.get("byteStride") or ncomp * dp.COMPONENT_SIZES[comp_type]
    count = acc["count"]
    fmt = dp.COMPONENT_FMTS[comp_type]
    hue_deg = float(op.get("hue-deg", 0.0))
    sat_m = float(op.get("sat", 1.0))
    val_m = float(op.get("val", 1.0))
    identity = (hue_deg == 0.0 and sat_m == 1.0 and val_m == 1.0)
    region_end = start + (count - 1) * stride + ncomp * dp.COMPONENT_SIZES[comp_type]
    info = {
        "accessor": acc_idx,
        "componentType": comp_type,
        "type": acc_type,
        "normalized": normalized,
        "count": count,
        "stride": stride,
        "byteStart": start,
        "byteEnd": region_end,
        "identity": identity,
        "changed": False,
    }
    if identity:
        return info  # 수학적으로 항등 → 바이트 무손상
    pack_fmt = "<" + fmt * ncomp
    new_vals = []
    for i in range(count):
        off = start + i * stride
        raw = struct.unpack_from(pack_fmt, bin_arr, off)
        fl = [raw_to_float(r, comp_type, normalized) for r in raw]
        nr, ng, nb = transform_rgb((fl[0], fl[1], fl[2]), hue_deg, sat_m, val_m)
        out = [nr, ng, nb] + list(fl[3:])  # VEC4 알파 채널 보존
        raws = [float_to_raw(x, comp_type, normalized) for x in out]
        struct.pack_into(pack_fmt, bin_arr, off, *raws)
        new_vals.append(out)
    if "min" in acc or "max" in acc:
        mins = [min(v[k] for v in new_vals) for k in range(ncomp)]
        maxs = [max(v[k] for v in new_vals) for k in range(ncomp)]
        if "min" in acc:
            acc["min"] = mins
        if "max" in acc:
            acc["max"] = maxs
    info["changed"] = True
    return info


def apply_material_ops(js, ops_by_zone, zones_with_vc, tint_factor):
    """머티리얼 baseColorFactor에 alpha(알파 지정) 및 선택적 RGB 재매핑 적용."""
    changes = []
    mats = js.get("materials", [])
    for zone in sorted(ops_by_zone):
        op = ops_by_zone[zone]
        if zone >= len(mats):
            changes.append({"zone": zone, "error": "materialIndex 범위 초과"})
            continue
        m = mats[zone]
        pbr = m.get("pbrMetallicRoughness")
        if pbr is None:
            changes.append({"zone": zone, "note": "pbrMetallicRoughness 없음 — 팩터 연산 불가"})
            continue
        bcf = pbr.get("baseColorFactor")
        if bcf is None:
            bcf = [1.0, 1.0, 1.0, 1.0]  # glTF 기본값 — 부재여도 동일 의미, 지정 시에만 삽입
            pbr["baseColorFactor"] = bcf
            changes.append({"zone": zone, "note": "baseColorFactor 기본값 초기화"})
        alpha = op.get("alpha")
        if alpha is not None:
            old = bcf[3]
            new = float(alpha)
            if old != new:
                bcf[3] = new
                changes.append({"zone": zone, "field": "baseColorFactor[3](alpha)",
                                "old": old, "new": new})
        hue_deg = float(op.get("hue-deg", 0.0))
        sat_m = float(op.get("sat", 1.0))
        val_m = float(op.get("val", 1.0))
        abs_rgb = op.get("rgb")  # 절대 지정 [r,g,b] — 텍스처 곱연산용 보정 틴트
        apply_rgb = tint_factor or (zone not in zones_with_vc) or abs_rgb is not None
        if apply_rgb:
            if abs_rgb is not None:
                nr, ng, nb = (float(abs_rgb[0]), float(abs_rgb[1]), float(abs_rgb[2]))
                r, g, b = bcf[0], bcf[1], bcf[2]
            else:
                r, g, b = bcf[0], bcf[1], bcf[2]
                nr, ng, nb = transform_rgb((r, g, b), hue_deg, sat_m, val_m)
            if (nr, ng, nb) != (r, g, b):
                bcf[0], bcf[1], bcf[2] = nr, ng, nb
                changes.append({"zone": zone, "field": "baseColorFactor.rgb",
                                "old": [r, g, b], "new": [nr, ng, nb]})
    return changes


# ---------------------------------------------------------------- 존 선택

def select_zones_auto(src, base_hue, threshold, exclude):
    """--zone auto: base-hue와 hue 거리가 threshold 이내인 유채색 존만 선택."""
    rep = dp.probe_glb(src)
    selected = []
    details = []
    for z in rep["zones"]:
        zi = z["materialIndex"]
        if zi in exclude:
            details.append({"zone": zi, "selected": False, "reason": "exclude 지정"})
            continue
        hue = None
        kind = None
        vc = z.get("vertex_color")
        if vc and vc["sat_mean"] and vc["sat_mean"] > 0.01 and vc["hue_mean_circular"] is not None:
            hue = vc["hue_mean_circular"]
            kind = "vertex_color"
        elif z.get("baseColor_hsv") and z["baseColor_hsv"]["s"] > 0.01:
            hue = z["baseColor_hsv"]["h_deg"]
            kind = "baseColorFactor"
        if hue is None:
            details.append({"zone": zi, "selected": False,
                            "reason": "무채색/텍스처 기반 — hue 기준 없음"})
            continue
        d = hue_distance(hue, base_hue)
        ok = d <= threshold
        details.append({"zone": zi, "selected": ok, "hue": hue, "distance_deg": d, "source": kind})
        if ok:
            selected.append(zi)
    return selected, details


# ---------------------------------------------------------------- 베이크 본체

def bake(src, dst, ops_by_zone, tint_factor=False):
    """src를 파싱해 ops_by_zone(= {zone: {"hue-deg","sat","val","alpha"?}}) 대로 재매핑 후 dst에 기록.

    src는 절대 쓰지 않는다(무손상). 반환: 요약 딕셔너리.
    """
    if os.path.abspath(src) == os.path.abspath(dst):
        raise ValueError("dst는 src와 달라야 합니다: %s" % dst)
    with open(src, "rb") as f:
        src_bytes = f.read()
    src_sha = sha256_bytes(src_bytes)
    js, bin_data, meta = dp.parse_glb(src)
    chunks = extract_chunks(src_bytes)
    if len(chunks) > 2:
        raise ValueError("JSON/BIN 외 추가 청크가 있는 GLB는 미지원: %d개" % len(chunks))
    json_chunk = next((c for t, c in chunks if t == CHUNK_JSON), None)
    bin_chunk = next((c for t, c in chunks if t == CHUNK_BIN), b"")

    # 적용 대상 accessor 수집 (존 → COLOR_0 accessor)
    acc_ops = {}
    acc_zones = {}
    zones_with_vc = set()
    for mesh in js.get("meshes", []):
        for prim in mesh.get("primitives", []):
            mat_idx = prim.get("material")
            attrs = prim.get("attributes", {}) or {}
            c0 = attrs.get("COLOR_0")
            if mat_idx in ops_by_zone:
                if c0 is None:
                    continue
                zones_with_vc.add(mat_idx)
                op = ops_by_zone[mat_idx]
                if c0 in acc_ops and acc_ops[c0] != op:
                    raise ValueError(
                        "accessor %d가 서로 다른 연산을 가진 존들과 공유됨: %s vs %s"
                        % (c0, acc_zones.get(c0), mat_idx))
                acc_ops[c0] = op
                acc_zones[c0] = mat_idx

    js_snapshot = json.dumps(js, sort_keys=True)
    bin_arr = bytearray(bin_data)
    accessor_changes = []
    for acc_idx in sorted(acc_ops):
        info = rewrite_accessor(bin_arr, js, acc_idx, acc_ops[acc_idx])
        info["zone"] = acc_zones[acc_idx]
        accessor_changes.append(info)
    material_changes = apply_material_ops(js, ops_by_zone, zones_with_vc, tint_factor)
    json_dirty = json.dumps(js, sort_keys=True) != js_snapshot
    bin_dirty = any(c["changed"] for c in accessor_changes)

    if json_dirty:
        new_json_bytes = json.dumps(js, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    else:
        new_json_bytes = json_chunk  # 원본 청크 바이트 그대로 (패딩 포함)
    new_bin_bytes = bytes(bin_arr) if bin_dirty else bin_chunk

    # BIN 무결성: 변경 바이트는 COLOR_0 accessor 영역 내에만 존재해야 한다
    bin_diff_ok = True
    bin_diff_ranges = []
    if bin_dirty:
        regions = [(c["byteStart"], c["byteEnd"]) for c in accessor_changes if c["changed"]]
        bin_diff_ranges = diff_ranges(bin_data, new_bin_bytes)
        for (s, e) in bin_diff_ranges:
            if not any(rs <= s and e <= re for (rs, re) in regions):
                bin_diff_ok = False

    out_bytes = build_glb(new_json_bytes, new_bin_bytes)
    dst_dir = os.path.dirname(os.path.abspath(dst))
    if dst_dir and not os.path.isdir(dst_dir):
        os.makedirs(dst_dir, exist_ok=True)
    with open(dst, "wb") as f:
        f.write(out_bytes)

    src_sha_after = sha256_file(src)
    dst_probe = dp.probe_glb(dst)
    zone_stats = {}
    for z in dst_probe["zones"]:
        vc = z.get("vertex_color")
        if vc:
            zone_stats[str(z["materialIndex"])] = {
                "color_source": "vertex_color",
                "hue": vc["hue_mean_circular"],
                "sat": vc["sat_mean"],
                "val": vc["val_mean"],
            }
        else:
            zone_stats[str(z["materialIndex"])] = {
                "color_source": z.get("color_source"),
                "alpha": z.get("alpha"),
            }

    return {
        "src": os.path.abspath(src),
        "dst": os.path.abspath(dst),
        "src_sha256": src_sha,
        "src_sha256_after": src_sha_after,
        "src_untouched": src_sha == src_sha_after,
        "dst_sha256": sha256_bytes(out_bytes),
        "src_size": len(src_bytes),
        "dst_size": len(out_bytes),
        "byte_identical": out_bytes == src_bytes,
        "json_dirty": json_dirty,
        "bin_dirty": bin_dirty,
        "bin_diff_ranges": bin_diff_ranges,
        "bin_diff_confined_to_color0": bin_diff_ok,
        "accessor_changes": accessor_changes,
        "material_changes": material_changes,
        "ops_by_zone": {str(k): v for k, v in sorted(ops_by_zone.items())},
        "dst_zone_stats": zone_stats,
    }


def print_summary(s):
    print("-" * 70)
    print("bake: %s -> %s" % (s["src"], s["dst"]))
    print("  src sha256 %s (무손상=%s)" % (s["src_sha256"], s["src_untouched"]))
    print("  dst sha256 %s (size %d, byte_identical=%s)" % (
        s["dst_sha256"], s["dst_size"], s["byte_identical"]))
    print("  ops: %s" % json.dumps(s["ops_by_zone"], ensure_ascii=False))
    for c in s["accessor_changes"]:
        print("  acc %d (zone %s): %s ct=%d norm=%s count=%d region=[%d,%d) identity=%s changed=%s" % (
            c["accessor"], c["zone"], c["type"], c["componentType"], c["normalized"],
            c["count"], c["byteStart"], c["byteEnd"], c["identity"], c["changed"]))
    for c in s["material_changes"]:
        print("  material: %s" % json.dumps(c, ensure_ascii=False))
    print("  bin_diff_confined_to_color0: %s" % s["bin_diff_confined_to_color0"])
    for zi, st in s["dst_zone_stats"].items():
        print("  dst zone %s: %s" % (zi, json.dumps(st, ensure_ascii=False)))


# ---------------------------------------------------------------- 자가검증

def write_synthetic_glb(path, colors):
    """자가검증용 최소 GLB 생성: COLOR_0 VEC3 float 정점컬러만 가진 단일 메시."""
    n = len(colors)
    bin_data = b"".join(struct.pack("<fff", *c) for c in colors)
    js = {
        "asset": {"version": "2.0", "generator": "derivative_bake selftest"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0, "name": "selftest"}],
        "meshes": [{"name": "selftest",
                    "primitives": [{"attributes": {"COLOR_0": 0}, "material": 0, "mode": 4}]}],
        "materials": [{"name": "mat0",
                       "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1]}}],
        "accessors": [{"componentType": 5126, "count": n, "type": "VEC3", "bufferView": 0}],
        "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": n * 12}],
        "buffers": [{"byteLength": n * 12}],
    }
    json_bytes = json.dumps(js, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    with open(path, "wb") as f:
        f.write(build_glb(json_bytes, bin_data))


def find_repo_asset(rel):
    """selftest용 저장소 애셋 경로 탐색 (스크립트 위치 기준 → CWD 기준)."""
    here = os.path.dirname(os.path.abspath(__file__))
    cands = [
        os.path.abspath(os.path.join(here, "..", rel)),
        os.path.abspath(os.path.join(os.getcwd(), rel)),
    ]
    for c in cands:
        if os.path.isfile(c):
            return c
    return None


def run_selftest(out_path):
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")
    lines = []
    lines.append("=" * 70)
    lines.append("[Phase 0] Tools/derivative_bake.py 자가검증")
    lines.append("생성 시각: %s" % stamp)
    lines.append("Python: %s" % sys.version.split()[0])
    lines.append("")
    ok_all = True
    tmpdir = tempfile.mkdtemp(prefix="derivative_bake_selftest_")
    try:
        # ---- (a) 항등 변환 → 바이트 동일
        lines.append("(a) 항등 변환(hue 0 / sat 1 / val 1) → 출력이 src와 바이트 동일")
        src = find_repo_asset(os.path.join("Assets", "Resources", "Models", "Polyfork",
                                           "potion-bottle-099ad7.glb"))
        if src is None:
            lines.append("    FAIL: potion-bottle-099ad7.glb를 찾을 수 없음")
            ok_all = False
        else:
            dst = os.path.join(tmpdir, "identity_out.glb")
            summary = bake(src, dst, {0: {"hue-deg": 0.0, "sat": 1.0, "val": 1.0}})
            same = summary["byte_identical"]
            ok_all = ok_all and same
            lines.append("    src: %s" % src)
            lines.append("    src sha256: %s" % summary["src_sha256"])
            lines.append("    dst sha256: %s" % summary["dst_sha256"])
            lines.append("    size: %d bytes, byte_identical=%s" % (summary["dst_size"], same))
            lines.append("    결과: %s" % ("PASS" if same else "FAIL"))

        # ---- (b) 합성 정점컬러 hue 120° 회전 HSV 검증
        lines.append("")
        lines.append("(b) 합성 정점컬러 버퍼 hue 120° 회전 → HSV 검증")
        colors = [
            ("red", (1.0, 0.0, 0.0)),
            ("green", (0.0, 1.0, 0.0)),
            ("blue", (0.0, 0.0, 1.0)),
            ("yellow", (1.0, 1.0, 0.0)),
            ("cyan", (0.0, 1.0, 1.0)),
            ("magenta", (1.0, 0.0, 1.0)),
            ("white", (1.0, 1.0, 1.0)),
            ("gray", (0.5, 0.5, 0.5)),
        ]
        expected_map = {
            "red": "green", "green": "blue", "blue": "red",
            "yellow": "cyan", "cyan": "magenta", "magenta": "yellow",
            "white": "white", "gray": "gray",
        }
        named = dict(colors)
        syn = os.path.join(tmpdir, "synthetic.glb")
        write_synthetic_glb(syn, [c for _, c in colors])
        out = os.path.join(tmpdir, "synthetic_hue120.glb")
        summary = bake(syn, out, {0: {"hue-deg": 120.0, "sat": 1.0, "val": 1.0}})
        js2, bin2, _ = dp.parse_glb(out)
        vals, acc_info = dp.read_accessor(js2, bin2, js2["meshes"][0]["primitives"][0]["attributes"]["COLOR_0"])
        ok_b = True
        for i, (name, rgb) in enumerate(colors):
            exp = named[expected_map[name]]
            got = tuple(vals[i][:3])
            match = all(abs(got[k] - exp[k]) <= 1e-6 for k in range(3))
            if not match:
                ok_b = False
            lines.append("    %s %s → %s (기대 %s) %s" % (
                name, rgb, tuple(round(x, 6) for x in got), exp, "OK" if match else "FAIL"))
        for i, (name, rgb) in enumerate(colors):
            h0, s0, v0 = colorsys.rgb_to_hsv(*rgb)
            h1, s1, v1 = colorsys.rgb_to_hsv(*vals[i][:3])
            if s0 > 0.01:
                dh = ((h1 - h0) * 360.0 + 180.0) % 360.0 - 180.0
                if abs(dh - 120.0) > 0.01:
                    ok_b = False
                    lines.append("    hue 검증 FAIL: %s Δhue=%.4f" % (name, dh))
        lines.append("    bin_diff_confined_to_color0=%s json_dirty=%s" % (
            summary["bin_diff_confined_to_color0"], summary["json_dirty"]))
        lines.append("    결과: %s (8/8 정점 비교)" % ("PASS" if ok_b else "FAIL"))
        ok_all = ok_all and ok_b
    finally:
        shutil.rmtree(tmpdir, ignore_errors=True)

    lines.append("")
    lines.append("총 결과: %s" % ("PASS (2/2)" if ok_all else "FAIL"))
    lines.append("=" * 70)
    text = "\n".join(lines)
    out_dir = os.path.dirname(os.path.abspath(out_path))
    if out_dir and not os.path.isdir(out_dir):
        os.makedirs(out_dir, exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(text + "\n")
    print(text)
    print("selftest written: %s" % out_path)
    return ok_all


# ---------------------------------------------------------------- CLI

def parse_zone_arg(values):
    joined = ",".join(values or []).strip()
    if joined.lower() == "auto":
        return "auto"
    zones = []
    for part in joined.split(","):
        part = part.strip()
        if part:
            zones.append(int(part))
    return zones


def resolve_path(p):
    if p is None:
        return None
    if os.path.isabs(p):
        return p
    return os.path.abspath(os.path.join(os.getcwd(), p))


def main():
    ap = argparse.ArgumentParser(description="파생 GLB 베이크 (stdlib 전용)")
    ap.add_argument("--src", default=None)
    ap.add_argument("--dst", default=None)
    ap.add_argument("--hue-deg", type=float, default=0.0)
    ap.add_argument("--sat", type=float, default=1.0)
    ap.add_argument("--val", type=float, default=1.0)
    ap.add_argument("--zone", nargs="*", default=None,
                    help="머티리얼 인덱스(쉼표/공백 구분) 또는 'auto'")
    ap.add_argument("--base-hue", type=float, default=None)
    ap.add_argument("--hue-threshold", type=float, default=45.0)
    ap.add_argument("--exclude-zone", default="")
    ap.add_argument("--alpha", type=float, default=None)
    ap.add_argument("--tint-factor", action="store_true")
    ap.add_argument("--map", default=None)
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--selftest-out", default="TestOutput/derivative_selftest.txt")
    args = ap.parse_args()

    if args.selftest:
        ok = run_selftest(args.selftest_out)
        sys.exit(0 if ok else 1)

    def build_ops_from_cli(src):
        zones = parse_zone_arg(args.zone)
        if zones == "auto":
            if args.base_hue is None:
                raise SystemExit("--zone auto에는 --base-hue가 필요합니다")
            exclude = {int(x) for x in args.exclude_zone.split(",") if x.strip()}
            selected, details = select_zones_auto(src, args.base_hue, args.hue_threshold, exclude)
            print("auto zone 선택 (base-hue=%.4f, threshold=%.1f): %s" % (
                args.base_hue, args.hue_threshold, selected))
            for d in details:
                print("  %s" % json.dumps(d, ensure_ascii=False))
            if not selected:
                raise SystemExit("auto 선택 결과 존 없음")
            zones = selected
        elif not zones:
            raise SystemExit("--zone이 필요합니다 (인덱스 목록 또는 'auto')")
        op = {"hue-deg": args.hue_deg, "sat": args.sat, "val": args.val}
        if args.alpha is not None:
            op["alpha"] = args.alpha
        return {z: dict(op) for z in zones}

    failures = 0
    if args.map:
        map_path = resolve_path(args.map)
        with open(map_path, "r", encoding="utf-8") as f:
            entries = json.load(f)
        if not isinstance(entries, list):
            raise SystemExit("--map JSON은 배열이어야 합니다")
        for i, entry in enumerate(entries):
            src = resolve_path(entry.get("src") or args.src)
            dst = resolve_path(entry.get("dst") or args.dst)
            if not src or not dst:
                print("entry %d: src/dst 누락" % i, file=sys.stderr)
                failures += 1
                continue
            ops_by_zone = {}
            tint = args.tint_factor
            for op in entry.get("ops", []):
                zspec = op.get("zone")
                if zspec == "auto":
                    base = op.get("base-hue", args.base_hue)
                    if base is None:
                        raise SystemExit("auto 존에는 base-hue가 필요합니다")
                    exclude = {int(x) for x in str(op.get("exclude-zone", "")).split(",") if x.strip()}
                    selected, details = select_zones_auto(
                        src, float(base), float(op.get("hue-threshold", args.hue_threshold)), exclude)
                    print("entry %d auto zone 선택 (base-hue=%.4f): %s" % (i, float(base), selected))
                    for d in details:
                        print("  %s" % json.dumps(d, ensure_ascii=False))
                    zones = selected
                else:
                    zones = [int(zspec)]
                o = {k: op[k] for k in ("hue-deg", "sat", "val", "alpha", "rgb") if k in op}
                for k in ("hue-deg", "sat", "val"):
                    o.setdefault(k, {"hue-deg": 0.0, "sat": 1.0, "val": 1.0}[k])
                for z in zones:
                    ops_by_zone[z] = dict(o)
                if op.get("tint-factor"):
                    tint = True
            if not ops_by_zone:
                print("entry %d: ops 없음" % i, file=sys.stderr)
                failures += 1
                continue
            try:
                summary = bake(src, dst, ops_by_zone, tint_factor=tint)
                print_summary(summary)
                print("BAKE-SUMMARY-JSON: %s" % json.dumps(summary, ensure_ascii=False))
            except Exception as e:  # noqa: BLE001
                print("entry %d 베이크 실패: %s" % (i, e), file=sys.stderr)
                failures += 1
        sys.exit(1 if failures else 0)

    # 단일 모드
    if not args.src or not args.dst:
        ap.error("--src와 --dst가 필요합니다 (또는 --map / --selftest)")
    src = resolve_path(args.src)
    dst = resolve_path(args.dst)
    ops_by_zone = build_ops_from_cli(src)
    summary = bake(src, dst, ops_by_zone, tint_factor=args.tint_factor)
    print_summary(summary)
    print("BAKE-SUMMARY-JSON: %s" % json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
