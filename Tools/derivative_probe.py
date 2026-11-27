#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""파생 GLB 프로브 — [Phase 0] 포이즌 파생 베이크 사전 정밀 조사 도구.

GLB(바이너리 glTF 2.0)를 Python3 표준 라이브러리만으로 파싱하여 다음을 리포트한다.
  - 머티리얼 목록: name, baseColorFactor RGB(+HSV), alphaMode, alpha(baseColorFactor[3]), alphaCutoff, 텍스처 사용 여부
  - 각 mesh primitive: materialIndex, COLOR_0 액세서 존재/컴포넌트타입(5126 float / 5121 ubyte / 5123 ushort /
    5125 uint / 5120 byte), normalized 여부, VEC3/VEC4, count/byteStride/byteOffset
  - 머티리얼 존(=materialIndex)별 정점컬러 HSV 통계(min/max/mean, 원형 평균 hue 포함)
  - 색 판정: 정점컬러(COLOR_0) 기반인지 머티리얼 baseColorFactor 기반인지 텍스처 기반인지

사용법:
  python3 Tools/derivative_probe.py --src <glb> [<glb> ...] [--out TestOutput/derivative_probe.json]

의존성: Python3 표준 라이브러리만 사용 (pip 설치 금지).
"""
import argparse
import colorsys
import json
import math
import os
import struct
import sys

GLB_MAGIC = 0x46546C67      # "glTF"
CHUNK_JSON = 0x4E4F534A     # "JSON"
CHUNK_BIN = 0x004E4942      # "BIN\0"

COMPONENT_TYPES = {
    5120: "byte",
    5121: "ubyte",
    5122: "short",
    5123: "ushort",
    5125: "uint",
    5126: "float",
}
# struct 포맷 문자 (리틀 엔디안)
COMPONENT_FMTS = {
    5120: "b",
    5121: "B",
    5122: "h",
    5123: "H",
    5125: "I",
    5126: "f",
}
COMPONENT_SIZES = {
    5120: 1,
    5121: 1,
    5122: 2,
    5123: 2,
    5125: 4,
    5126: 4,
}
TYPE_NCOMP = {
    "SCALAR": 1,
    "VEC2": 2,
    "VEC3": 3,
    "VEC4": 4,
}


class GlbError(Exception):
    pass


def parse_glb(path):
    """GLB 파일을 파싱해 (json_obj, bin_bytes, meta)를 반환.

    meta: {"file_size", "json_chunk_length", "bin_chunk_length", "chunks"}
    멀티 청크(GLB 확장)도 순회하며 JSON/BIN 청크를 찾는다.
    """
    with open(path, "rb") as f:
        data = f.read()
    if len(data) < 12:
        raise GlbError("파일이 너무 짧음: %s (%d bytes)" % (path, len(data)))
    magic, version, total_len = struct.unpack_from("<III", data, 0)
    if magic != GLB_MAGIC:
        raise GlbError("GLB magic 불일치: 0x%08X (기대 0x46546C67): %s" % (magic, path))
    js = None
    bin_data = b""
    chunks = []
    off = 12
    while off + 8 <= len(data):
        clen, ctype = struct.unpack_from("<II", data, off)
        body = data[off + 8: off + 8 + clen]
        if len(body) < clen:
            raise GlbError("청크 길이 초과: offset=%d clen=%d file=%s" % (off, clen, path))
        chunks.append({"type": ctype, "length": clen})
        if ctype == CHUNK_JSON:
            js = json.loads(body.decode("utf-8"))
        elif ctype == CHUNK_BIN:
            bin_data = body
        off += 8 + clen
    if js is None:
        raise GlbError("JSON 청크 없음: %s" % path)
    meta = {
        "file_size": len(data),
        "header_total_length": total_len,
        "json_chunk_length": next((c["length"] for c in chunks if c["type"] == CHUNK_JSON), None),
        "bin_chunk_length": next((c["length"] for c in chunks if c["type"] == CHUNK_BIN), 0),
        "chunks": chunks,
    }
    return js, bin_data, meta


def read_accessor(js, bin_data, acc_idx):
    """액세서의 정규화된 값 리스트를 읽는다. 반환: (values, info)

    values: 정점별 시퀀스 (튜플). normalized 정수형은 [0,1]로 정규화.
    info: {"componentType", "componentTypeName", "type", "normalized", "count",
           "byteStride", "byteOffset", "bufferView", "tight"}
    """
    acc = js["accessors"][acc_idx]
    comp_type = acc["componentType"]
    acc_type = acc["type"]
    ncomp = TYPE_NCOMP[acc_type]
    fmt = COMPONENT_FMTS[comp_type]
    comp_size = COMPONENT_SIZES[comp_type]
    normalized = bool(acc.get("normalized", False))
    count = acc["count"]
    if acc.get("sparse"):
        raise GlbError("sparse 액세서 미지원: accessor %d" % acc_idx)
    bv = js["bufferViews"][acc["bufferView"]]
    view_off = bv.get("byteOffset", 0)
    acc_off = acc.get("byteOffset", 0)
    start = view_off + acc_off
    stride = bv.get("byteStride") or ncomp * comp_size
    tight = stride == ncomp * comp_size
    vals = []
    for i in range(count):
        base = start + i * stride
        raw = struct.unpack_from("<" + fmt * ncomp, bin_data, base)
        if normalized and comp_type in (5120, 5121, 5122, 5123):
            denom = 127.0 if comp_type == 5120 else (255.0 if comp_type == 5121 else (32767.0 if comp_type == 5122 else 65535.0))
            raw = tuple(v / denom for v in raw)
        vals.append(raw)
    info = {
        "accessor_index": acc_idx,
        "componentType": comp_type,
        "componentTypeName": COMPONENT_TYPES.get(comp_type, str(comp_type)),
        "type": acc_type,
        "normalized": normalized,
        "count": count,
        "byteStride": stride,
        "byteOffset": start,
        "bufferView": acc["bufferView"],
        "tight": tight,
    }
    return vals, info


def rgb_to_hsv_deg(r, g, b):
    """RGB(0..1) → (hue 도, sat, val)."""
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    return h * 360.0, s, v


def hsv_stats(rgb_list):
    """RGB 값 리스트 → HSV 통계 (min/max/mean, 원형 평균 hue, 채도 0 제외 hue 통계).

    채도 0(무채색) 픽셀은 hue가 정의되지 않으므로 hue 통계에서 제외하고 별도 집계한다.
    """
    hues = []
    sats = []
    vals = []
    achromatic = 0
    for r, g, b in rgb_list:
        h, s, v = rgb_to_hsv_deg(r, g, b)
        sats.append(s)
        vals.append(v)
        if s < 1e-6 or v < 1e-6:
            achromatic += 1
        else:
            hues.append(h)
    stats = {
        "count": len(rgb_list),
        "achromatic_count": achromatic,
        "sat_min": min(sats) if sats else None,
        "sat_max": max(sats) if sats else None,
        "sat_mean": (sum(sats) / len(sats)) if sats else None,
        "val_min": min(vals) if vals else None,
        "val_max": max(vals) if vals else None,
        "val_mean": (sum(vals) / len(vals)) if vals else None,
    }
    if hues:
        rad = [math.radians(h) for h in hues]
        cx = sum(math.cos(a) for a in rad) / len(rad)
        cy = sum(math.sin(a) for a in rad) / len(rad)
        circ_mean = math.degrees(math.atan2(cy, cx)) % 360.0
        stats["hue_min"] = min(hues)
        stats["hue_max"] = max(hues)
        stats["hue_mean_arithmetic"] = sum(hues) / len(hues)
        stats["hue_mean_circular"] = circ_mean
        stats["hue_r_max"] = max(min(abs(h - circ_mean), 360.0 - abs(h - circ_mean)) for h in hues)
    else:
        stats["hue_min"] = None
        stats["hue_max"] = None
        stats["hue_mean_arithmetic"] = None
        stats["hue_mean_circular"] = None
        stats["hue_r_max"] = None
    return stats


def material_info(js, mat_idx):
    mats = js.get("materials", [])
    if mat_idx is None or mat_idx >= len(mats):
        return None
    m = mats[mat_idx]
    pbr = m.get("pbrMetallicRoughness", {}) or {}
    bcf = pbr.get("baseColorFactor")
    tex = pbr.get("baseColorTexture")
    rgb = hsv = None
    if bcf is not None:
        r, g, b = bcf[0], bcf[1], bcf[2]
        h, s, v = rgb_to_hsv_deg(r, g, b)
        rgb = [r, g, b]
        hsv = {"h_deg": h, "s": s, "v": v}
    return {
        "index": mat_idx,
        "name": m.get("name"),
        "baseColorFactor": bcf,
        "baseColor_rgb": rgb,
        "baseColor_hsv": hsv,
        "alpha": bcf[3] if bcf is not None and len(bcf) > 3 else None,
        "alphaMode": m.get("alphaMode"),
        "alphaCutoff": m.get("alphaCutoff"),
        "doubleSided": m.get("doubleSided"),
        "baseColorTexture": tex,
        "hasTexture": tex is not None,
    }


def probe_glb(path):
    """GLB 1개를 프로브해 리포트 딕셔너리를 생성."""
    js, bin_data, meta = parse_glb(path)
    mats = js.get("materials", [])
    mat_reports = [material_info(js, i) for i in range(len(mats))]

    # materialIndex → 정점컬러 RGB 수집
    zone_colors = {}
    prim_reports = []
    for mesh_i, mesh in enumerate(js.get("meshes", [])):
        for prim_i, prim in enumerate(mesh.get("primitives", [])):
            attrs = prim.get("attributes", {}) or {}
            mat_idx = prim.get("material")
            pr = {
                "mesh_index": mesh_i,
                "mesh_name": mesh.get("name"),
                "primitive_index": prim_i,
                "materialIndex": mat_idx,
                "mode": prim.get("mode", 4),
                "attributes": sorted(attrs.keys()),
            }
            c0 = attrs.get("COLOR_0")
            if c0 is not None:
                vals, info = read_accessor(js, bin_data, c0)
                rgbs = [v[:3] for v in vals]
                stats = hsv_stats(rgbs)
                rgb_min = [min(v[k] for v in rgbs) for k in range(3)]
                rgb_max = [max(v[k] for v in rgbs) for k in range(3)]
                rgb_mean = [sum(v[k] for v in rgbs) / len(rgbs) for k in range(3)]
                pr["color_0"] = dict(info)
                pr["color_0_rgba_min"] = rgb_min
                pr["color_0_rgba_max"] = rgb_max
                pr["color_0_rgba_mean"] = rgb_mean
                pr["color_0_hsv_stats"] = stats
                if mat_idx is not None:
                    zc = zone_colors.setdefault(mat_idx, [])
                    zc.extend(rgbs)
            else:
                pr["color_0"] = None
            prim_reports.append(pr)

    # 존(머티리얼)별 판정
    zone_reports = []
    for i, m in enumerate(mat_reports):
        zc = zone_colors.get(i)
        entry = {
            "materialIndex": i,
            "material_name": m["name"] if m else None,
            "baseColorFactor": m["baseColorFactor"] if m else None,
            "alpha": m["alpha"] if m else None,
            "alphaMode": m["alphaMode"] if m else None,
            "hasTexture": m["hasTexture"] if m else None,
        }
        if zc:
            entry["vertex_color"] = hsv_stats(zc)
            entry["vertex_color_rgba_mean"] = [
                sum(c[k] for c in zc) / len(zc) for k in range(3)
            ]
            entry["color_source"] = "vertex_color"
        elif m is not None and m["baseColorFactor"] is not None:
            entry["vertex_color"] = None
            entry["color_source"] = "material_baseColorFactor"
            entry["baseColor_hsv"] = m["baseColor_hsv"]
        elif m is not None and m["hasTexture"]:
            entry["vertex_color"] = None
            entry["color_source"] = "texture"
        else:
            entry["vertex_color"] = None
            entry["color_source"] = "none"
        zone_reports.append(entry)

    return {
        "path": os.path.abspath(path),
        "file": os.path.basename(path),
        "meta": meta,
        "asset": js.get("asset", {}),
        "materials": mat_reports,
        "primitives": prim_reports,
        "zones": zone_reports,
        "mesh_count": len(js.get("meshes", [])),
        "material_count": len(mats),
        "image_count": len(js.get("images", [])),
        "texture_count": len(js.get("textures", [])),
        "node_names": [(n.get("name"), n.get("mesh")) for n in js.get("nodes", [])],
    }


def format_report(rep):
    """stdout용 사람 판독 리포트 문자열."""
    lines = []
    lines.append("=" * 70)
    lines.append("%s  filelen=%d binlen=%d" % (
        rep["path"], rep["meta"]["file_size"], rep["meta"]["bin_chunk_length"]))
    lines.append("materials:")
    for m in rep["materials"]:
        if m is None:
            continue
        bcf = m["baseColorFactor"]
        hsv = m["baseColor_hsv"]
        hsv_str = ""
        if hsv:
            hsv_str = " hsv=(%.2f, %.4f, %.4f)" % (hsv["h_deg"], hsv["s"], hsv["v"])
        lines.append("  mat %d name=%s baseColorFactor=%s alphaMode=%s alpha=%s%s tex=%s" % (
            m["index"], m["name"], bcf, m["alphaMode"], m["alpha"], hsv_str,
            "yes" if m["hasTexture"] else "no"))
    lines.append("primitives:")
    for pr in rep["primitives"]:
        lines.append("  mesh %d(%s) prim %d mat=%s attrs=%s" % (
            pr["mesh_index"], pr["mesh_name"], pr["primitive_index"],
            pr["materialIndex"], pr["attributes"]))
        c0 = pr.get("color_0")
        if c0:
            lines.append("    COLOR_0 acc=%d type=%s ct=%d(%s) norm=%s count=%d stride=%d start=%d" % (
                c0["accessor_index"], c0["type"], c0["componentType"], c0["componentTypeName"],
                c0["normalized"], c0["count"], c0["byteStride"], c0["byteOffset"]))
            st = pr["color_0_hsv_stats"]
            lines.append("    hsv mean=(%.2f, %.4f, %.4f) hue_min=%.2f hue_max=%.2f achromatic=%d/%d" % (
                st["hue_mean_circular"] or 0.0, st["sat_mean"] or 0.0, st["val_mean"] or 0.0,
                st["hue_min"] if st["hue_min"] is not None else -1.0,
                st["hue_max"] if st["hue_max"] is not None else -1.0,
                st["achromatic_count"], st["count"]))
            lines.append("    rgba mean=%s" % ["%.4f" % x for x in pr["color_0_rgba_mean"]])
    lines.append("zones (판정):")
    for z in rep["zones"]:
        vc = z.get("vertex_color")
        if vc:
            lines.append("  zone %d src=%s hsv_mean=(%.2f, %.4f, %.4f) hue_r_max=%.2f" % (
                z["materialIndex"], z["color_source"],
                vc["hue_mean_circular"] or 0.0, vc["sat_mean"] or 0.0, vc["val_mean"] or 0.0,
                vc["hue_r_max"] or 0.0))
        else:
            lines.append("  zone %d src=%s alpha=%s" % (
                z["materialIndex"], z["color_source"], z["alpha"]))
    lines.append("nodes: %s" % rep["node_names"])
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser(description="GLB 파생 프로브 (stdlib 전용)")
    ap.add_argument("--src", nargs="+", required=True, help="프로브할 GLB 경로(복수 가능)")
    ap.add_argument("--out", default=None, help="JSON 리포트 출력 경로 (예: TestOutput/derivative_probe.json)")
    args = ap.parse_args()

    reports = []
    failed = []
    for src in args.src:
        try:
            rep = probe_glb(src)
        except GlbError as e:
            print("PROBE FAIL: %s" % e, file=sys.stderr)
            failed.append(src)
            continue
        print(format_report(rep))
        reports.append(rep)

    if args.out and reports:
        out_dir = os.path.dirname(os.path.abspath(args.out))
        if out_dir and not os.path.isdir(out_dir):
            os.makedirs(out_dir, exist_ok=True)
        payload = {"generated_by": "Tools/derivative_probe.py", "reports": reports}
        with open(args.out, "w", encoding="utf-8") as f:
            json.dump(payload, f, ensure_ascii=False, indent=1)
        print("probe JSON written: %s (%d files)" % (args.out, len(reports)))
    if failed:
        print("failed files: %s" % failed, file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
