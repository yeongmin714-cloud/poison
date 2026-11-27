#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""[209차-C] wood_sword COLOR_0 정점AO 수술 — dish_quality_bake199 선례의 1종 적응판.

- 기법(174/192/199차 승계): COLOR_0 VEC3 f32 주입(BIN 성장 허용) = 정점AO(절대반경 5cm·밀도정규화)
  × 에지다크닝(위치그룹 면법선 상호편차>55° → ×0.84). 스펙클 없음. 팔레트PNG 바이트 무수정 계약(dish 선례).
- wood_sword 실측: 1 prim(POSITION/NORMAL/TEXCOORD_0, COLOR_0 부재), 4044 verts, diag≈1.50m, non-rigged.
  R = min(0.05, max(diag*0.5,1e-3)) = 5cm — 크레비스 폭 일정 철학(소품 크기 무관) + 소형 아이템 과음영은
  AO_FLOOR=0.60 하한이 방어. dish 검증 레시피(55°/0.84) 유지.
- 백업(진실원): Tools/ao_prebake_backup208/wood_sword.glb (Assets 밖, 최초 1회) + md5 기록.
- 매니페스트: Tools/wood_sword_ao_manifest_209c.json (기존 매니페스트 무접촉).
- 재수술 가드: COLOR_0 존재 시 에러(백업 원본만 1회 수술 — 192/199차 동일).
"""
import hashlib
import io
import json
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vertex_ao_bake import parse_glb, repack_glb, read_accessor, compute_ao  # noqa: E402
from PIL import Image  # noqa: E402
import numpy as np  # noqa: E402

PROJ = "/mnt/c/Unity/code"
TARGET_PATH = os.path.join(PROJ, "Assets/Resources/Models/UserProvided/wood_sword.glb")
TARGET_ID = "wood_sword"
BACKUP_DIR = os.path.join(PROJ, "Tools/ao_prebake_backup208")
MANIFEST = os.path.join(PROJ, "Tools/wood_sword_ao_manifest_209c.json")

EDGE_ANGLE_DEG = 55.0
EDGE_MULT = 0.84


def md5_of(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def _align4(n):
    return (n + 3) // 4 * 4


def _face_normals(P, F):
    a, b, c = P[F[:, 0]], P[F[:, 1]], P[F[:, 2]]
    n = np.cross(b - a, c - a)
    ln = np.linalg.norm(n, axis=1, keepdims=True)
    ln[ln < 1e-12] = 1.0
    return n / ln


def crease_verts(P_all, prims_faces):
    """dish_quality_bake199.crease_verts와 동일 — 위치 1e-4 그룹 면쌍 최대 각도>문턱 → crease."""
    total = len(P_all)
    faces_global = []
    voff = 0
    for F, nv in prims_faces:
        faces_global.append(F + voff)
        voff += nv
    Fg = np.vstack(faces_global)
    fn = _face_normals(P_all, Fg)
    v2faces = [[] for _ in range(total)]
    for fi, tri in enumerate(Fg):
        for v in tri:
            v2faces[v].append(fi)
    groups = {}
    for v in range(total):
        key = (round(float(P_all[v, 0]), 4), round(float(P_all[v, 1]), 4), round(float(P_all[v, 2]), 4))
        groups.setdefault(key, []).append(v)
    mask = np.zeros(total, dtype=bool)
    cos_thr = np.cos(np.radians(EDGE_ANGLE_DEG))
    for key, verts in groups.items():
        fset = set()
        for v in verts:
            fset.update(v2faces[v])
        if len(fset) < 2:
            continue
        fids = list(fset)
        fns = fn[fids]
        dots = fns @ fns.T
        np.fill_diagonal(dots, 1.0)
        if dots.min() < cos_thr:
            for v in verts:
                mask[v] = True
    return mask


def bake_wood_sword(path, fid):
    j, old_bin = parse_glb(path)
    assert not j.get("animations") and not j.get("skins"), f"{fid}: rigged — 계약 위반"
    all_prims = [p for m in j.get("meshes", []) for p in m.get("primitives", [])]
    # dish 199차 동일 가드 — NORMAL 보유 프리미티브(시각 메시)만 수술 대상
    prims = [p for p in all_prims if "NORMAL" in p["attributes"]]
    assert prims, f"{fid}: NORMAL 보유 프리미티브 없음"
    skipped = len(all_prims) - len(prims)

    P_all, faces = [], []
    for prim in prims:
        P, _, _, _, _ = read_accessor(j, old_bin, j["accessors"][prim["attributes"]["POSITION"]])
        F, _, _, _, _ = read_accessor(j, old_bin, j["accessors"][prim["indices"]])
        F = F.astype(np.int64).reshape(-1, 3)
        P_all.append(P)
        faces.append((F, len(P)))
    P = np.vstack(P_all)

    diag = float(np.linalg.norm(P.max(axis=0) - P.min(axis=0)))
    area = 0.0
    voff = 0
    for (F, nv) in faces:
        a, b, c = P[voff + F[:, 0]], P[voff + F[:, 1]], P[voff + F[:, 2]]
        area += float(0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1).sum())
        voff += nv
    spacing = max(np.sqrt(max(area, 1e-9) / max(len(P), 1)), 1e-5)
    R = min(0.05, max(diag * 0.5, 1e-3))   # AO_RADIUS_M=5cm 절대 반경 — 크레비스 스케일(소형 아이템 적합)
    ao = compute_ao(P, np.vstack([read_accessor(j, old_bin, j["accessors"][p["attributes"]["NORMAL"]])[0]
                                  for p in prims]), R, spacing)
    crease = crease_verts(P, faces)

    # COLOR_0 주입 — AO×에지다크닝(스펙클 없음)
    color_blocks = []
    voff = 0
    for (F, nv) in faces:
        vals = np.ones((nv, 3), dtype=np.float32)
        vals *= ao[voff:voff + nv].astype(np.float32)[:, None]
        vals[crease[voff:voff + nv]] *= EDGE_MULT
        vals = np.clip(vals, 0.0, 1.0)
        color_blocks.append(vals)
        voff += nv

    # BIN 재배치(이미지 교체 없음 — 팔레트 PNG 바이트 무수정)
    new_bin = bytearray()
    for bv in j["bufferViews"]:
        old_off = bv.get("byteOffset", 0)
        payload = old_bin[old_off:old_off + bv["byteLength"]]
        new_off = _align4(len(new_bin))
        new_bin += b"\0" * (new_off - len(new_bin))
        new_bin += payload
        bv["byteOffset"] = new_off
        # byteLength 불변 — 교체 없음

    # COLOR_0 뷰/액세서 추가(192/199차 동일)
    for bi, vals in enumerate(color_blocks):
        prim = prims[bi]
        if "COLOR_0" in prim["attributes"]:
            raise RuntimeError(f"{fid}: COLOR_0 이미 존재 — 재수술 금지(백업 원본만 1회 수술 계약)")
        pad = _align4(len(new_bin)) - len(new_bin)
        new_bin += b"\0" * pad
        off = len(new_bin)
        payload = vals.astype(np.float32).tobytes()
        new_bin += payload
        j["bufferViews"].append({"byteOffset": off, "byteLength": len(payload)})
        j["accessors"].append({
            "bufferView": len(j["bufferViews"]) - 1,
            "byteOffset": 0,
            "componentType": 5126,
            "count": int(vals.shape[0]),
            "type": "VEC3",
            "min": [float(x) for x in vals.min(axis=0)],
            "max": [float(x) for x in vals.max(axis=0)],
        })
        prim["attributes"]["COLOR_0"] = len(j["accessors"]) - 1

    return j, new_bin, {
        "verts": int(len(P)),
        "prims": len(prims),
        "prims_skipped_collision": skipped,
        "edges": int(crease.sum()),
        "radius": round(float(R), 4),
        "ao_min": round(float(ao.min()), 3),
        "ao_p05": round(float(np.quantile(ao, 0.05)), 3),
        "ao_p50": round(float(np.median(ao)), 3),
        "edge_deg": EDGE_ANGLE_DEG,
        "edge_mult": EDGE_MULT,
        "bin_len": len(old_bin),
        "bin_len_new": len(new_bin),
    }


def main(apply_mode):
    manifest = {}
    if os.path.exists(MANIFEST):
        manifest = json.load(open(MANIFEST, encoding="utf-8"))
    key = f"UserProvided/{TARGET_ID}"
    try:
        if not apply_mode:
            _, _, r = bake_wood_sword(TARGET_PATH, TARGET_ID)
            r["dry"] = True
            print(f"DRY {key}: {r}")
            return
        os.makedirs(BACKUP_DIR, exist_ok=True)
        bak = os.path.join(BACKUP_DIR, TARGET_ID + ".glb")
        if not os.path.exists(bak):
            shutil.copy2(TARGET_PATH, bak)   # 최초 1회 — 원본 보존(진실원)
        md5_orig = md5_of(TARGET_PATH)
        md5_bak = md5_of(bak)
        assert md5_bak == md5_orig, "백업 무결성 — 원본과 md5 불일치"

        # 원본 이미지 바이트 보존 대조용 스냅(팔레트PNG 무수정 계약)
        j0, b0 = parse_glb(bak)
        img_ref = []
        for img in j0.get("images", []):
            bv = j0["bufferViews"][img["bufferView"]]
            img_ref.append(bytes(b0[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]))

        new_j, new_bin, r = bake_wood_sword(TARGET_PATH, TARGET_ID)
        tmp = TARGET_PATH + ".aotmp"
        open(tmp, "wb").write(repack_glb(new_j, new_bin))
        os.replace(tmp, TARGET_PATH)

        # 재파싱 검증 — COLOR_0 전 프리미티브 존재 + 이미지 전체 디코딩 + 팔레트 무수정(백업과 바이트 동일)
        j2, b2 = parse_glb(TARGET_PATH)
        vis = [p for m in j2["meshes"] for p in m["primitives"] if "NORMAL" in p["attributes"]]
        has_c0 = all("COLOR_0" in p["attributes"] for p in vis)
        assert has_c0, f"{TARGET_ID}: 재파싱 COLOR_0 부재"
        for i, img2 in enumerate(j2.get("images", [])):
            bv2 = j2["bufferViews"][img2["bufferView"]]
            blob2 = bytes(b2[bv2["byteOffset"]:bv2["byteOffset"] + bv2["byteLength"]])
            Image.open(io.BytesIO(blob2)).load()
            assert blob2 == img_ref[i], f"{TARGET_ID}: 팔레트 PNG 변경됨 — 무수정 계약 위반"

        md5_new = md5_of(TARGET_PATH)
        r.update({
            "md5_before": md5_orig,
            "md5_after": md5_new,
            "md5_backup": md5_bak,
            "backup": os.path.relpath(bak, PROJ),
            "color0_count": int(sum(1 for m in j2["meshes"] for p in m["primitives"] if "COLOR_0" in p["attributes"])),
            "images_verified": len(img_ref),
        })
        manifest[key] = r
        json.dump(manifest, open(MANIFEST, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        print(f"OK {key}: {json.dumps(r, ensure_ascii=False)}")
    except Exception as e:  # noqa: BLE001
        print(f"ERR {key}: {repr(e)[:300]}")
        sys.exit(1)


if __name__ == "__main__":
    main(apply_mode="--apply" in sys.argv)
