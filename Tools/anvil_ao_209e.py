#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""[209차-E] Polyfork anvil-1286cb.glb 정점AO 수술 — 어댑터 드라이버(단일 파일).

- 핵심: vertex_ao_bake.bake_file(경로) 1회 호출 — COLOR_0 f32 VEC3 곱셈 전용
  (BIN 청크 길이 불변 → GLB 바이트 델타는 JSON 헤더 패딩만, BIN 길이 델타=0 계약).
- 모듈 상수: AO_RADIUS_M=0.05 · AO_FLOOR=0.60 (vertex_ao_bake 그대로).
- 백업(진실원): Tools/ao_prebake_backup209/anvil-1286cb.glb (사전 절차에서 생성·md5 검증).
- 매니페스트: Tools/polyfork_ao_manifest_209e.json (기존 매니페스트 무접촉 — 별도 파일).
- 검증: COLOR_0 실측 min/max 전후 · BIN 길이 델타=0 · 백업 md5 일치 · 재파싱.
"""
import hashlib
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vertex_ao_bake import parse_glb, bake_file, read_accessor  # noqa: E402

PROJ = "/mnt/c/Unity/code"
TARGET = os.path.join(PROJ, "Assets/Resources/Models/Polyfork/anvil-1286cb.glb")
BACKUP = os.path.join(PROJ, "Tools/ao_prebake_backup209/anvil-1286cb.glb")
MANIFEST = os.path.join(PROJ, "Tools/polyfork_ao_manifest_209e.json")


def md5_of(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def color0_actual(j, b):
    """COLOR_0 accessor 실측 (min, max, count) — 값 데이터에서 직접 산출."""
    out = []
    for mesh in j.get("meshes", []):
        for prim in mesh.get("primitives", []):
            ca = prim.get("attributes", {}).get("COLOR_0")
            if ca is None:
                continue
            acc = j["accessors"][ca]
            if acc["type"] not in ("VEC3", "VEC4") or acc["componentType"] != 5126:
                out.append({"type": acc["type"], "ct": acc["componentType"], "skip": True})
                continue
            vals, _, _, _, ncomp = read_accessor(j, b, acc)
            out.append({
                "count": int(vals.shape[0]), "ncomp": int(ncomp),
                "min": [round(float(x), 5) for x in vals.min(axis=0)],
                "max": [round(float(x), 5) for x in vals.max(axis=0)],
                "mean": [round(float(x), 5) for x in vals.mean(axis=0)],
            })
    return out


def main():
    try:
        assert os.path.exists(BACKUP), f"백업 부재: {BACKUP}"
        md5_t, md5_b = md5_of(TARGET), md5_of(BACKUP)
        assert md5_t == md5_b, f"백업 무결성 불일치: target={md5_t} backup={md5_b}"

        j0, b0 = parse_glb(TARGET)
        assert not j0.get("animations") and not j0.get("skins"), "rigged — 계약 위반"
        c0_before = color0_actual(j0, b0)
        assert c0_before, "COLOR_0 부재 — bake_file이 skip할 대상"
        bin0 = len(b0)

        r = bake_file(TARGET)
        if "verts" not in r:
            print(f"ERR: bake_file skipped — {r}")
            sys.exit(1)

        j2, b2 = parse_glb(TARGET)
        c0_after = color0_actual(j2, b2)
        bin_delta = len(b2) - bin0
        assert bin_delta == 0, f"BIN 길이 변동: {bin0} -> {len(b2)} (델타 {bin_delta})"
        assert md5_of(TARGET) != md5_t, "파일 미변경 — 수술 미적용?"

        result = {
            "bake": r,
            "color0_before": c0_before,
            "color0_after": c0_after,
            "bin_len_before": bin0,
            "bin_len_after": len(b2),
            "bin_delta": bin_delta,
            "md5_before": md5_t,
            "md5_after": md5_of(TARGET),
            "md5_backup": md5_b,
        }
        m = json.load(open(MANIFEST, encoding="utf-8"))
        import time
        m["bake"] = result
        m["bake_ts"] = time.strftime("%Y-%m-%dT%H:%M:%S")
        json.dump(m, open(MANIFEST, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        print("OK:", json.dumps(result, ensure_ascii=False, indent=1))
    except Exception as e:  # noqa: BLE001
        print(f"ERR: {repr(e)[:400]}")
        sys.exit(1)


if __name__ == "__main__":
    main()
