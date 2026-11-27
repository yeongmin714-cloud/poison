#!/usr/bin/env python3
"""[174차 P1] 정점 AO 베이커 — 저폴리 GLB 고품질화(파생 파이프라인 확장).

원리: 포인트클라우드 오클루전 근사 — 정점 i 주변 반경 R 내 이웃 정점 j가
i의 법선 반구에 있으면 가려짐 항(콘트라스트 가중) 누적 → AO = 1 - occlusion.
COLOR_0 rgb에 곱셈 베이크(crevices 자동 음영 — 저폴리 "가짜 고해상도").

계약:
- BIN 청크 길이 불변(COLOR_0 f32 동일 크기 곱셈).
- rigged/animated(skins·animations 존재) 스킵 — 캐릭터 변형 오염 방지.
- COLOR_0 부재 프리미티브는 오클루더로만 참여(색 미변경).
- 원본 백업: Tools/ao_prebake_backup/<group>/<id>.glb (Assets 밖 — 임포트 무관).
- 매니페스트: Tools/ao_bake_manifest.json (계약 테스트 스팟 체크 소스).

사용: python3 vertex_ao_bake.py --sample   # 8종 대표 미리보기(tmp) — 적용 전 판정
      python3 vertex_ao_bake.py --apply   # 전수 적용
"""
import colorsys
import json
import os
import shutil
import struct
import sys

import numpy as np

PROJ = "/mnt/c/Unity/code"
# [174차 정책] Polyfork 월드 소품만 — 실내/던전/야외 배선 원본이 소비하는 경로.
# UserProvided 게임 아이템(물약 액체색·구슬 floor lift·음식 틴트 등 가독성 계약)은 스킵 — 핸드튜닝 보존.
TARGETS = {
    "Polyfork": os.path.join(PROJ, "Assets/Resources/Models/Polyfork"),
}
BACKUP_DIR = os.path.join(PROJ, "Tools/ao_prebake_backup")
MANIFEST = os.path.join(PROJ, "Tools/ao_bake_manifest.json")

AO_FLOOR = 0.60       # 최종 AO 하한 — 과음영 방지(vision 소형 소품 탁함 완화)
AO_RADIUS_M = 0.05    # 절대 월드 반경(5cm) — 크레비스 폭 일정, 소품 크기 무관
CHUNK = 512           # 정점 처리 청크


def parse_glb(path):
    data = open(path, "rb").read()
    magic, ver, length = struct.unpack("<III", data[:12])
    assert magic == 0x46546C67 and ver == 2, "not glb"
    clen, ctype = struct.unpack("<II", data[12:20])
    j = json.loads(data[20:20 + clen])
    off = 20 + clen
    blen, btype = struct.unpack("<II", data[off:off + 8])
    return j, bytearray(data[off + 8:off + 8 + blen])


def repack_glb(j, bin_bytes):
    jbin = json.dumps(j, separators=(",", ":")).encode()
    pad_j = (4 - len(jbin) % 4) % 4
    jbin += b" " * pad_j
    pad_b = (4 - len(bin_bytes) % 4) % 4
    bin_bytes = bytes(bin_bytes) + b"\0" * pad_b
    total = 12 + 8 + len(jbin) + 8 + len(bin_bytes)
    out = struct.pack("<III", 0x46546C67, 2, total)
    out += struct.pack("<II", len(jbin), 0x4E4F534A) + jbin
    out += struct.pack("<II", len(bin_bytes), 0x004E4942) + bin_bytes
    return out


def read_accessor(j, b, acc):
    comp_t, ncomp = acc["componentType"], {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[acc["type"]]
    fmt = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}
    dt = np.dtype(fmt[comp_t])
    if acc.get("normalized"):
        dt = dt.newbyteorder("<")
    bv = j["bufferViews"][acc["bufferView"]]
    base = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = bv.get("byteStride") or (dt.itemsize * ncomp)
    count = acc["count"]
    raw = b[base:base + count * stride]
    arr = np.zeros((count, ncomp), dtype=np.float64)
    for i in range(count):
        arr[i] = np.frombuffer(raw[i * stride:i * stride + dt.itemsize * ncomp], dtype=dt, count=ncomp)
        if acc.get("normalized") and comp_t in (5120, 5121):
            arr[i] = arr[i] / 255.0
    return arr, dt, stride, base, ncomp


def compute_ao(P, N, R, spacing):
    """포인트클라우드 오크루전 — solid-angle 근사(밀도 불변).

    contrib_j = max(0, N_j·dir(j→i)) × (1-d²/R²)² × s²/d²   (j별 상한 1)
    - facing≈0 평면(개방면)은 밝게 유지, 오버행/요철(수직 이웃)만 가림.
    - s²/d² = 로컬 정점 간격 기반 입체각 — 메시 밀도·크기 불변.
    """
    v_count = len(P)
    cell = max(R, 1e-6)
    lo = P.min(axis=0)
    cells = np.floor((P - lo) / cell).astype(np.int64)
    grid = {}
    for i in range(v_count):
        grid.setdefault(tuple(cells[i]), []).append(i)
    occ = np.zeros(v_count, dtype=np.float64)
    r2 = R * R
    s2 = spacing * spacing
    offs = [(dx, dy, dz) for dx in (-1, 0, 1) for dy in (-1, 0, 1) for dz in (-1, 0, 1)]
    for st in range(0, v_count, CHUNK):
        e = min(st + CHUNK, v_count)
        for i in range(st, e):
            ci = tuple(cells[i])
            cand = []
            for o in offs:
                k = (ci[0] + o[0], ci[1] + o[1], ci[2] + o[2])
                if k in grid:
                    cand.extend(grid[k])
            if not cand:
                continue
            J = np.array(cand, dtype=np.int64)
            D = P[J] - P[i]
            d2 = (D * D).sum(axis=1)
            m = (d2 > 1e-14) & (d2 < r2)
            if not m.any():
                continue
            Dm, d2m, Jm = D[m], d2[m], J[m]
            dist = np.sqrt(d2m)
            facing = (N[Jm] * -Dm / dist[:, None]).sum(axis=1)
            falloff = 1.0 - d2m / r2
            contrib = np.maximum(facing, 0.0) * (falloff * falloff) * (s2 / np.maximum(d2m, s2))
            occ[i] += np.minimum(contrib, 1.0).sum()
    ao = 1.0 - np.clip(occ, 0.0, 1.0)
    return AO_FLOOR + (1.0 - AO_FLOOR) * ao


def is_rigged(j):
    return bool(j.get("animations")) or bool(j.get("skins"))


def bake_file(path):
    j, b = parse_glb(path)
    if is_rigged(j):
        return {"skipped": "rigged"}
    prims = []
    for mesh in j.get("meshes", []):
        for prim in mesh.get("primitives", []):
            prims.append(prim)
    if not prims:
        return {"skipped": "no-prims"}

    # 전역 지오메트리 집계
    P_all, N_all, idx_all, prim_ranges = [], [], [], []
    vbase = 0
    for prim in prims:
        acc = j["accessors"][prim["attributes"]["POSITION"]]
        P, _, _, _, _ = read_accessor(j, b, acc)
        if "NORMAL" in prim["attributes"]:
            N, _, _, _, _ = read_accessor(j, b, j["accessors"][prim["attributes"]["NORMAL"]])
        else:
            N = np.zeros_like(P)
        P_all.append(P)
        N_all.append(N)
        prim_ranges.append((vbase, vbase + len(P), prim))
        vbase += len(P)
    P = np.vstack(P_all)
    N = np.vstack(N_all)

    # 법선 결측 보정: 면 평균
    missing = (np.abs(N).sum(axis=1) < 1e-9)
    if missing.any():
        # 면법선 누적(간이)
        acc_n = np.zeros_like(P)
        for prim in prims:
            if "NORMAL" in prim["attributes"]:
                continue
        for rng in prim_ranges:
            pass
        # 전역 인덱스로 누적
        for mesh in j.get("meshes", []):
            for prim in mesh.get("primitives", []):
                if "NORMAL" in prim["attributes"]:
                    continue
                ia = j["accessors"][prim["indices"]]
                F, _, _, _, _ = read_accessor(j, b, ia)
                F = F.astype(np.int64).reshape(-1, 3)
                # 이 프리미티브의 전역 오프셋 찾기
                for vb, ve, pr in prim_ranges:
                    if pr is prim:
                        Fg = F + vb
                        break
                for tri in Fg:
                    a, bb, c = P[tri[0]], P[tri[1]], P[tri[2]]
                    fn = np.cross(bb - a, c - a)
                    ln = np.linalg.norm(fn)
                    if ln > 1e-12:
                        acc_n[tri[0]] += fn
                        acc_n[tri[1]] += fn
                        acc_n[tri[2]] += fn
        ln = np.linalg.norm(acc_n, axis=1, keepdims=True)
        ln[ln < 1e-12] = 1.0
        N[missing] = acc_n[missing] / ln[missing]

    diag = float(np.linalg.norm(P.max(axis=0) - P.min(axis=0)))
    # 면적 기반 정점 간격 — solid-angle 정규화용
    area = 0.0
    for mesh in j.get("meshes", []):
        for prim in mesh.get("primitives", []):
            if "indices" not in prim:
                continue
            ia = j["accessors"][prim["indices"]]
            F, _, _, _, _ = read_accessor(j, b, ia)
            F = F.astype(np.int64).reshape(-1, 3)
            for vb, ve, pr in prim_ranges:
                if pr is prim:
                    Fg = F + vb
                    break
            a, bb, c = P[Fg[:, 0]], P[Fg[:, 1]], P[Fg[:, 2]]
            area += float(0.5 * np.linalg.norm(np.cross(bb - a, c - a), axis=1).sum())
    spacing = max(np.sqrt(max(area, 1e-9) / max(len(P), 1)), 1e-5)
    R = min(AO_RADIUS_M, max(diag * 0.5, 1e-3))  # 초소형 소품은 자기 크기 캡
    ao = compute_ao(P, N, R, spacing)

    # COLOR_0 곱셈 베이크
    changed_prims = 0
    for vb, ve, prim in prim_ranges:
        if "COLOR_0" not in prim["attributes"]:
            continue
        acc = j["accessors"][prim["attributes"]["COLOR_0"]]
        if acc["type"] not in ("VEC3", "VEC4") or acc["componentType"] != 5126:
            continue  # f32 VEC3/VEC4만(폴리포크 계약)
        _, dt, stride, base, ncomp = read_accessor(j, b, acc)
        sub_ao = ao[vb:ve]
        for i in range(ve - vb):
            o = base + i * stride
            vals = struct.unpack_from("<" + "f" * ncomp, b, o)
            m = float(sub_ao[i])
            new = [vals[0] * m, vals[1] * m, vals[2] * m] + list(vals[3:])
            struct.pack_into("<" + "f" * ncomp, b, o, *new)
        # accessor min/max 갱신
        mm = np.array([struct.unpack_from("<" + "f" * ncomp, b, base + i * stride) for i in range(ve - vb)])
        acc["min"] = [float(x) for x in mm.min(axis=0)]
        acc["max"] = [float(x) for x in mm.max(axis=0)]
        changed_prims += 1
    if changed_prims == 0:
        return {"skipped": "no-color0"}

    tmp = path + ".aotmp"
    open(tmp, "wb").write(repack_glb(j, b))
    os.replace(tmp, path)
    # 재파싱 검증
    j2, b2 = parse_glb(path)
    assert len(b2) == len(b), "BIN length drift"
    ao_p05 = float(np.quantile(ao, 0.05))
    return {"verts": int(vbase), "radius": round(R, 4), "ao_p05": round(ao_p05, 3),
            "ao_min": round(float(ao.min()), 3), "prims": changed_prims}


def collect_files():
    out = []
    for group, d in TARGETS.items():
        for f in sorted(os.listdir(d)):
            if f.endswith(".glb"):
                out.append((group, os.path.join(d, f), f[:-4]))
    return out


def run(apply_mode, only_ids=None):
    files = collect_files()
    manifest = {}
    if os.path.exists(MANIFEST):
        manifest = json.load(open(MANIFEST, encoding="utf-8"))
    n_done = n_skip = 0
    for group, path, fid in files:
        if only_ids is not None and fid not in only_ids:
            continue
        key = f"{group}/{fid}"
        if apply_mode:
            bak = os.path.join(BACKUP_DIR, group, fid + ".glb")
            if not os.path.exists(bak):
                os.makedirs(os.path.dirname(bak), exist_ok=True)
                shutil.copy2(path, bak)
        try:
            if apply_mode:
                r = bake_file(path)
            else:
                # 샘플 모드 — 원본 무접촉, tmp 사본 베이크
                os.makedirs("/tmp/sample_aobake", exist_ok=True)
                tmp = os.path.join("/tmp/sample_aobake", fid + ".glb")
                shutil.copy2(path, tmp)
                r = bake_file(tmp)
        except Exception as e:  # noqa: BLE001
            r = {"error": repr(e)[:120]}
        if apply_mode:
            manifest[key] = r
        tag = "OK" if "verts" in r else "SKIP"
        print(f"{tag} {key}: {r}")
        if "verts" in r:
            n_done += 1
        else:
            n_skip += 1
    if apply_mode:
        json.dump(manifest, open(MANIFEST, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"=== baked {n_done}, skipped {n_skip}, apply={apply_mode}")


if __name__ == "__main__":
    if "--sample" in sys.argv:
        sample = ["overflowing-treasure-chest-3d6ee0", "cemetery-lantern-post-fed352", "potion-bottle-099ad7",
                  "charred-meat-haunch-be1ea2", "knuckle-ring-529610", "prayer-bead-strand-e18d9a",
                  "coin-pouch-be4dd8", "looted-gilded-throne-008fc3", "anvil-1286cb", "campfire-parent-b1c0d5"]
        run(apply_mode=False, only_ids=set(sample))
    else:
        run(apply_mode=True)
