#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""[199차 옵션2] dish_* 12종 COLOR_0 정점수술 — UV붕괴 팔레트형의 유일한 디테일 개선로.

- 기법(174/192차 승계): COLOR_0 VEC3 f32 주입(BIN 성장 허용) = 정점AO(절대반경 5cm·밀도정규화)
  × 에지다크닝(위치그룹 면법선 상호편차>55° → ×0.84). **스펙클 없음** — 음식에 석재 노이즈 부자연,
  팔레트 PNG는 바이트 무수정(계약). 틴트(_BaseColor MPB)와 곱연산 공존.
- 백업: Tools/dish_prebake_backup199/ (Assets 밖, 최초 1회) + 매니페스트 Tools/dish_bake_manifest_199.json.
- 재수술 가드: COLOR_0 존재 시 에러(백업 원본만 1회 수술 — 192차 동일).
"""
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
DISH_DIR = os.path.join(PROJ, "Assets/Resources/Models/UserProvided")
DISH_IDS = ["dish_braise", "dish_jelly", "dish_jeon", "dish_pickle", "dish_pie", "dish_ricebowl",
            "dish_roll", "dish_sashimi", "dish_steak", "dish_steam", "dish_stew", "dish_stirfry"]
BACKUP_DIR = os.path.join(PROJ, "Tools/dish_prebake_backup199")
MANIFEST = os.path.join(PROJ, "Tools/dish_bake_manifest_199.json")

EDGE_ANGLE_DEG = 55.0
EDGE_MULT = 0.84


def _align4(n):
    return (n + 3) // 4 * 4


def _face_normals(P, F):
    a, b, c = P[F[:, 0]], P[F[:, 1]], P[F[:, 2]]
    n = np.cross(b - a, c - a)
    ln = np.linalg.norm(n, axis=1, keepdims=True)
    ln[ln < 1e-12] = 1.0
    return n / ln


def crease_verts(P_all, prims_faces):
    """floor_quality_bake.crease_verts와 동일 — 위치 1e-4 그룹 면쌍 최대 각도>문턱 → crease."""
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


def bake_dish(path, fid):
    j, old_bin = parse_glb(path)
    assert not j.get("animations") and not j.get("skins"), f"{fid}: rigged — 계약 위반"
    all_prims = [p for m in j.get("meshes", []) for p in m.get("primitives", [])]
    # [199차 실측] dish GLB 2메시 구성: 시각 메시(NORMAL·TANGENT 보유) + 충돌 프록시(POSITION 전용,
    # OMI_physics_body — 렌더 비대상). 수술·AO 대상 = NORMAL 보유 프리미티브 한정.
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
    R = min(0.05, max(diag * 0.5, 1e-3))   # AO_RADIUS_M=5cm 절대 반경
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

    # COLOR_0 뷰/액세서 추가(192차 동일)
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
        "edge_deg": EDGE_ANGLE_DEG,
        "edge_mult": EDGE_MULT,
        "bin_len": len(old_bin),
        "bin_len_new": len(new_bin),
    }


def main(apply_mode):
    manifest = {}
    if os.path.exists(MANIFEST):
        manifest = json.load(open(MANIFEST, encoding="utf-8"))
    os.makedirs(BACKUP_DIR, exist_ok=True)
    n_done = 0
    for fid in DISH_IDS:
        path = os.path.join(DISH_DIR, fid + ".glb")
        key = f"UserProvided/{fid}"
        try:
            if not apply_mode:
                _, _, r = bake_dish(path, fid)
                r["dry"] = True
                print(f"DRY {key}: {r}")
                continue
            bak = os.path.join(BACKUP_DIR, fid + ".glb")
            if not os.path.exists(bak):
                shutil.copy2(path, bak)   # 최초 1회 — 원본 보존
            # 원본 이미지 바이트 보존 대조용 스냅
            j0, b0 = parse_glb(bak)
            img_ref = []
            for img in j0["images"]:
                bv = j0["bufferViews"][img["bufferView"]]
                img_ref.append(bytes(b0[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]))
            new_j, new_bin, r = bake_dish(path, fid)
            tmp = path + ".dtmp"
            open(tmp, "wb").write(repack_glb(new_j, new_bin))
            os.replace(tmp, path)
            # 재파싱 검증 — COLOR_0 전 프리미티브 + 이미지 전체 디코딩 + 팔레트 무수정(백업과 바이트 동일)
            j2, b2 = parse_glb(path)
            vis = [p for m in j2["meshes"] for p in m["primitives"] if "NORMAL" in p["attributes"]]
            has_c0 = all("COLOR_0" in p["attributes"] for p in vis)
            assert has_c0, f"{fid}: 재파싱 COLOR_0 부재"
            for i, img2 in enumerate(j2["images"]):
                bv2 = j2["bufferViews"][img2["bufferView"]]
                blob2 = bytes(b2[bv2["byteOffset"]:bv2["byteOffset"] + bv2["byteLength"]])
                Image.open(io.BytesIO(blob2)).load()
                assert blob2 == img_ref[i], f"{fid}: 팔레트 PNG 변경됨 — 무수정 계약 위반"
            manifest[key] = r
            n_done += 1
            print(f"OK {key}: {r}")
        except Exception as e:  # noqa: BLE001
            print(f"ERR {key}: {repr(e)[:180]}")
    if apply_mode:
        json.dump(manifest, open(MANIFEST, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"=== dishes processed {n_done}/{len(DISH_IDS)}, apply={apply_mode}")


if __name__ == "__main__":
    main(apply_mode="--apply" in sys.argv)
