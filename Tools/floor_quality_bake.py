#!/usr/bin/env python3
"""[192차] 던전 팩 바닥 타일 고품질화 — COLOR_0 주입 + 정점AO/에지다크닝 베이크 + 팔레트 스펙클.

배경: Floor_1_*A 6종은 albedo(팔레트 그라디언트 "Concrate")만 존재 — 노멀맵·오클루전·COLOR_0 전무.
벽(CreateWallSide)/돌판 경로(CreateStonePathMat)는 절차 줄눈+노멀 릴리프가 있어 바닥만 평면 도장처럼 보임.
팔레트 UV는 면단위 붕괴 방식이라 노멀맵 파생은 무효 → 프로젝트 선례(172차 에지다크닝·174차 정점AO)인
정점색 수술로 릴리프를 대체한다.

처리 3단:
  1) COLOR_0 f32 VEC3 신설 주입(BIN 재배치 — 성장 허용. 174차 BIN 불변 계약은 곱셈 그룹 전용이라 무충돌)
  2) 정점AO(174차 동일 공식 — compute_ao 재사용 R절대 5cm·s²/d²·AO_FLOOR 0.60) × 에지다크닝
     (위치그룹 면법선 최대 상호편차 > 55° → ×0.84 — 슬랩 이음매·모서리가 그루트처럼 읽힘)
  3) 임베디드 팔레트 PNG에 결정론 스펙클(저주파 64px ±3.5% + 고주파 256px ±1.2%)
     — 실내 절차타일 스펙클(±0.06) 수준 정합, 팔레트 그라디언트 무늬 보존

계약:
- 대상: Assets/Resources/Models/Dungeon/Floor_1_1A..6A.glb 6종만(벽·소품 무접촉).
- 원본 백업: Tools/ao_prebake_backup/DungeonFloor/<id>.glb (Assets 밖 — 임포트 무관).
- 매니페스트: Tools/ao_bake_manifest.json에 "DungeonFloor/<id>" 키 추가.
- rigged 스킵 계약 동일(바닥 타일은 비리깅 — 예외 없음 확인).

사용: python3 floor_quality_bake.py --dry    # 미적용 리포트
      python3 floor_quality_bake.py --apply  # 전수 적용
"""
import io
import json
import os
import shutil
import struct
import sys
import zlib

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vertex_ao_bake import parse_glb, repack_glb, read_accessor, compute_ao, AO_FLOOR, AO_RADIUS_M  # noqa: E402

PROJ = "/mnt/c/Unity/code"
DUNGEON_DIR = os.path.join(PROJ, "Assets/Resources/Models/Dungeon")
FLOOR_IDS = [f"Floor_1_{i}A" for i in range(1, 7)]   # [191차] 통일 계약 시리즈
BACKUP_DIR = os.path.join(PROJ, "Tools/ao_prebake_backup/DungeonFloor")
MANIFEST = os.path.join(PROJ, "Tools/ao_bake_manifest.json")

EDGE_ANGLE_DEG = 55.0   # 위치그룹 면법선 최대 상호편차 문턱 — 이 각 초과 굽은면만 어둡힘
EDGE_MULT = 0.84        # 굽은면 곱셈 계수 — 슬랩 테두리·이음매 그루트화
SPECKLE_LOW = 0.035     # 저주파(64px) 스펙클 진폭 — 실내 절차타일 0.06 정합
SPECKLE_FINE = 0.012    # 고주파(256px) 스펙클 진폭 — 잔결


def _align4(n):
    return (n + 3) // 4 * 4


def _face_normals(P, F):
    """(m,3) 삼각형 면법선 단위벡터."""
    a, b, c = P[F[:, 0]], P[F[:, 1]], P[F[:, 2]]
    n = np.cross(b - a, c - a)
    ln = np.linalg.norm(n, axis=1, keepdims=True)
    ln[ln < 1e-12] = 1.0
    return n / ln


def crease_verts(P_all, prims_faces):
    """P_all: (N,3) 전역 정점, prims_faces: [(m,3) 로컬 인덱스, ...] → crease 정점 전역 마스크.

    각 프리미티브의 로컬 인덱스에 전역 오프셋을 더해 전역 면 목록을 만들고,
    위치 1e-4 그룹 내 면쌍 최대 각도가 EDGE_ANGLE_DEG 초과면 그룹 전 정점 crease.
    """
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
        # 면쌍 최대 상호편차 — 정규화 내적 최소
        dots = fns @ fns.T
        np.fill_diagonal(dots, 1.0)
        max_dev_cos = dots.min()   # 가장 큰 각도 쌍의 코사인
        if max_dev_cos < cos_thr:
            for v in verts:
                mask[v] = True
    return mask


def speckle_png(png_bytes, seed):
    """팔레트 PNG에 결정론 스펙클 — 저주파+고주파 옥타브, 알파 보존."""
    im = Image.open(io.BytesIO(png_bytes))
    w, h = im.size
    rgba = im.convert("RGBA")
    arr = np.asarray(rgba, dtype=np.float32)

    rng = np.random.default_rng(seed)
    def octave(res, amp):
        small = rng.uniform(-1.0, 1.0, (res, res))
        g = Image.fromarray(((small + 1.0) * 127.5).astype(np.uint8), mode="L").resize((w, h), Image.BILINEAR)
        n2d = (np.asarray(g, dtype=np.float32) / 127.5 - 1.0) * amp
        return n2d[..., None]   # (h,w,1) — RGB 브로드캐스트

    noise = octave(64, SPECKLE_LOW) + octave(256, SPECKLE_FINE)
    arr[:, :, :3] = np.clip(arr[:, :, :3] + noise, 0.0, 255.0)
    out = Image.fromarray(arr.astype(np.uint8), mode="RGBA")
    buf = io.BytesIO()
    out.save(buf, format="PNG")
    return buf.getvalue()


def relayout_bin(j, old_bin, image_replacements):
    """버퍼뷰 전체 재배치 — 이미지 뷰는 대체 바이트로 교체(길이 변화 허용).

    image_replacements: {view_index: new_bytes}. 비이미지 뷰는 원 바이트 보존.
    모든 bufferView.byteOffset를 4정렬 간격으로 재계산해 재작성.
    """
    new_bin = bytearray()
    for i, bv in enumerate(j["bufferViews"]):
        old_off = bv.get("byteOffset", 0)
        old_len = bv["byteLength"]
        payload = image_replacements.get(i)
        if payload is None:
            payload = old_bin[old_off:old_off + old_len]
        new_off = _align4(len(new_bin))
        new_bin += b"\0" * (new_off - len(new_bin))
        new_bin += payload
        bv["byteOffset"] = new_off
        bv["byteLength"] = len(payload)   # [필수] 대체 바이트 길이 갱신 — 미갱신 시 잘린 PNG로 읽힘
    return new_bin


def bake_floor(path, fid):
    j, old_bin = parse_glb(path)
    assert not j.get("animations") and not j.get("skins"), f"{fid}: rigged — 계약 위반"

    prims = []
    for mesh in j.get("meshes", []):
        for prim in mesh.get("primitives", []):
            prims.append(prim)

    # ── 지오메트리 집계 ──
    P_all, N_all, faces = [], [], []
    for prim in prims:
        P, _, _, _, _ = read_accessor(j, old_bin, j["accessors"][prim["attributes"]["POSITION"]])
        N, _, _, _, _ = read_accessor(j, old_bin, j["accessors"][prim["attributes"]["NORMAL"]])
        F, _, _, _, _ = read_accessor(j, old_bin, j["accessors"][prim["indices"]])
        F = F.astype(np.int64).reshape(-1, 3)
        P_all.append(P)
        N_all.append(N)
        faces.append((F, len(P)))
    P = np.vstack(P_all)
    N = np.vstack(N_all)

    diag = float(np.linalg.norm(P.max(axis=0) - P.min(axis=0)))
    area = 0.0
    voff = 0
    for (F, nv) in faces:
        a, b, c = P[voff + F[:, 0]], P[voff + F[:, 1]], P[voff + F[:, 2]]
        area += float(0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1).sum())
        voff += nv
    spacing = max(np.sqrt(max(area, 1e-9) / max(len(P), 1)), 1e-5)
    R = min(AO_RADIUS_M, max(diag * 0.5, 1e-3))
    ao = compute_ao(P, N, R, spacing)

    # ── 에지다크닝 마스크 ──
    crease = crease_verts(P, faces)

    # ── COLOR_0 주입 데이터 ──
    color_blocks = []
    voff = 0
    for (F, nv) in faces:
        vals = np.ones((nv, 3), dtype=np.float32)
        ao_sub = ao[voff:voff + nv].astype(np.float32)
        cre_sub = crease[voff:voff + nv]
        vals *= ao_sub[:, None]
        vals[cre_sub] *= EDGE_MULT
        vals = np.clip(vals, 0.0, 1.0)
        color_blocks.append(vals)
        voff += nv

    # ── 이미지 스펙클 ──
    image_views = {j["images"][i]["bufferView"]: i for i in range(len(j["images"]))}
    replacements = {}
    speckled = []
    seed_base = zlib.crc32(fid.encode("utf-8"))
    for view_idx, img_idx in image_views.items():
        bv = j["bufferViews"][view_idx]
        old_off = bv.get("byteOffset", 0)
        png_bytes = old_bin[old_off:old_off + bv["byteLength"]]
        mime = j["images"][img_idx].get("mimeType", "")
        if mime != "image/png":
            speckled.append(f"img{img_idx}:skip({mime})")
            continue
        old_len = len(png_bytes)
        new_png = speckle_png(bytes(png_bytes), seed_base + img_idx * 1000)
        replacements[view_idx] = new_png
        speckled.append(f"img{img_idx}:{old_len}->{len(new_png)}B")

    # ── BIN 재배치(이미지 교체 포함) ──
    new_bin = relayout_bin(j, old_bin, replacements)

    # ── COLOR_0 뷰/액세서 추가 ──
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
        mn = vals.min(axis=0)
        mx = vals.max(axis=0)
        j["accessors"].append({
            "bufferView": len(j["bufferViews"]) - 1,
            "byteOffset": 0,
            "componentType": 5126,
            "count": int(vals.shape[0]),
            "type": "VEC3",
            "min": [float(x) for x in mn],
            "max": [float(x) for x in mx],
        })
        prim["attributes"]["COLOR_0"] = len(j["accessors"]) - 1

    return j, new_bin, {
        "verts": int(len(P)),
        "prims": len(prims),
        "edges": int(crease.sum()),
        "radius": round(float(R), 4),
        "ao_min": round(float(ao.min()), 3),
        "ao_p05": round(float(np.quantile(ao, 0.05)), 3),
        "edge_deg": EDGE_ANGLE_DEG,
        "edge_mult": EDGE_MULT,
        "speckle_low": SPECKLE_LOW,
        "speckle_fine": SPECKLE_FINE,
        "images": ", ".join(speckled),
        "bin_len": len(old_bin),
        "bin_len_new": len(new_bin),
    }


def write_floor(path, fid, j, new_bin):
    """변이된 j(주입 완료) + 재배치 BIN 직렬화 — 재파싱하면 주입 유실되므로 j를 직접 받는다."""
    tmp = path + ".fqtmp"
    open(tmp, "wb").write(repack_glb(j, new_bin))
    os.replace(tmp, path)


def main(apply_mode):
    manifest = {}
    if os.path.exists(MANIFEST):
        manifest = json.load(open(MANIFEST, encoding="utf-8"))
    n_done = 0
    for fid in FLOOR_IDS:
        path = os.path.join(DUNGEON_DIR, fid + ".glb")
        key = f"DungeonFloor/{fid}"
        try:
            if apply_mode:
                bak = os.path.join(BACKUP_DIR, fid + ".glb")
                if not os.path.exists(bak):
                    os.makedirs(os.path.dirname(bak), exist_ok=True)
                    shutil.copy2(path, bak)   # 최초 1회만 — 원본 보존
                new_j, new_bin, r = bake_floor(path, fid)
                write_floor(path, fid, new_j, new_bin)
                # 재파싱 검증 — COLOR_0 주입 + 이미지 전체 디코딩(잘림 즉시 검출)
                j2, b2 = parse_glb(path)
                has_c0 = all("COLOR_0" in p["attributes"] for m in j2["meshes"] for p in m["primitives"])
                assert has_c0, f"{fid}: 재파싱 COLOR_0 부재"
                for img2 in j2["images"]:
                    bv2 = j2["bufferViews"][img2["bufferView"]]
                    blob2 = bytes(b2[bv2["byteOffset"]:bv2["byteOffset"] + bv2["byteLength"]])
                    assert len(blob2) == bv2["byteLength"], f"{fid}: 이미지 슬라이스 단축"
                    Image.open(io.BytesIO(blob2)).load()   # 전체 디코딩 — truncated PNG 검출
                manifest[key] = r
                n_done += 1
                print(f"OK {key}: {r}")
            else:
                _, _, r = bake_floor(path, fid)   # [192차] 3-반환(j, bin, r) — j 폐기
                r["dry"] = True
                print(f"DRY {key}: {r}")
        except Exception as e:  # noqa: BLE001
            print(f"ERR {key}: {repr(e)[:160]}")
    if apply_mode:
        json.dump(manifest, open(MANIFEST, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"=== floors processed {n_done}/{len(FLOOR_IDS)}, apply={apply_mode}")


if __name__ == "__main__":
    main(apply_mode="--apply" in sys.argv)
