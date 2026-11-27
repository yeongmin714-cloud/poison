#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
[196차 v2] 던전 바닥 타일 근접 코플래너 수술 — 스냅+선별 퇴화.

v1 교훈(실측): 근접 상향 면들은 0.4~1.3mm 높이 편차의 "두 번째 스톤 탑 층위"(스타일리즈드
불균일 스톤)였고, 무차별 퇴화는 가시면을 삭제해 구멍을 만들었다(vision 실측).

v2 로직:
  1) 원본 = 백업(DungeonFloorGrout196)이 있으면 그것(진실원), 없으면 현재 파일. 항상 원본→출력(멱등).
  2) 주면 = 최대 상향(n.y>0.99) 평면 그룹.
  3) 근접 삼각형(0.5mm < |d-main| < 2.0mm) 분류:
     a. 중심점이 주면 삼각형 XZ 내부 → 겹침 lip → 인덱스 퇴화(3개 동일 값)
     b. 그 외(주면과 XZ 분리 — 스톤 탑 노이즈) → 정점 y를 주면 높이로 스냅(면 보존, 코플래너 비겹침=무플리커)
  4) POSITION accessor min/max 재계산(스냅 반영). 인덱스·버퍼 레이아웃 무변경, COLOR_0 보존.
검증: 재파싱 — 근접 상향 그룹 잔존 0(퇴화+스냅 후), 퇴화/스냅 건수 리포트.
"""
import json
import struct
import shutil
import os
import collections
import numpy as np

GLB_DIR = "/mnt/c/Unity/code/Assets/Resources/Models/Dungeon"
BACKUP_DIR = "/mnt/c/Unity/code/Tools/ao_prebake_backup/DungeonFloorGrout196"
VARIANTS = [1, 2, 3, 4, 5, 6]
WIN_LO, WIN_HI = 0.0005, 0.002

def parse_glb(path):
    with open(path, "rb") as f:
        f.read(4)
        version, length = struct.unpack("<II", f.read(8))
        jlen, jtype = struct.unpack("<II", f.read(8))
        j = json.loads(f.read(jlen))
        blen, btype = struct.unpack("<II", f.read(8))
        assert btype == 0x004E4942
        bin_chunk = f.read(blen)
    return j, bin_chunk

def write_glb(path, j, bin_chunk):
    jb = json.dumps(j, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    if len(jb) % 4: jb += b" " * (4 - len(jb) % 4)
    if len(bin_chunk) % 4: bin_chunk += b"\x00" * (4 - len(bin_chunk) % 4)
    total = 12 + 8 + len(jb) + 8 + len(bin_chunk)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(jb), 0x4E4F534A))
        f.write(jb)
        f.write(struct.pack("<II", len(bin_chunk), 0x004E4942))
        f.write(bin_chunk)

def geo(j, bc):
    prim = j["meshes"][0]["primitives"][0]
    ap = j["accessors"][prim["attributes"]["POSITION"]]
    bp = j["bufferViews"][ap["bufferView"]]
    voff = bp.get("byteOffset", 0) + ap.get("byteOffset", 0)
    verts = np.frombuffer(bc, dtype=np.float32, count=ap["count"] * 3, offset=voff).reshape(-1, 3).copy()
    ia = j["accessors"][prim["indices"]]
    bi = j["bufferViews"][ia["bufferView"]]
    ioff = bi.get("byteOffset", 0) + ia.get("byteOffset", 0)
    idxs = np.frombuffer(bc, dtype=np.uint16, count=ia["count"], offset=ioff).copy()
    return prim, ap, bp, verts, ia, bi, idxs, voff, ioff

def point_in_tri2(px, pz, tri):
    ax, az = tri[0][0], tri[0][2]
    bx, bz = tri[1][0], tri[1][2]
    cx, cz = tri[2][0], tri[2][2]
    d1 = (px - bx) * (az - bz) - (ax - bx) * (pz - bz)
    d2 = (px - cx) * (bz - cz) - (bx - cx) * (pz - cz)
    d3 = (px - ax) * (cz - az) - (cx - ax) * (pz - az)
    has_neg = (d1 < 0) or (d2 < 0) or (d3 < 0)
    has_pos = (d1 > 0) or (d2 > 0) or (d3 > 0)
    return not (has_neg and has_pos)

def main():
    report = []
    for v in VARIANTS:
        name = f"Floor_1_{v}A.glb"
        cur = os.path.join(GLB_DIR, name)
        bak = os.path.join(BACKUP_DIR, name)
        src = bak if os.path.exists(bak) else cur
        j, bc = parse_glb(src)
        prim, ap, bp, verts, ia, bi, idxs, voff, ioff = geo(j, bc)

        # 주면 판정 + 주면 삼각형 목록
        planes = collections.defaultdict(list)
        for t in range(0, len(idxs) - 2, 3):
            if idxs[t] == idxs[t + 1] == idxs[t + 2]:
                continue
            a, b, c = verts[idxs[t]], verts[idxs[t + 1]], verts[idxs[t + 2]]
            n = np.cross(b - a, c - a)
            ln = np.linalg.norm(n)
            if ln < 1e-9: continue
            n = n / ln
            if n[1] <= 0.99: continue
            planes[round(-float(np.dot(n, a)), 4)].append(t)
        if not planes:
            report.append(f"{name}: 상향면 없음 — 스킵"); continue
        main_d = max(planes.items(), key=lambda kv: len(kv[1]))[0]
        main_tris = planes[main_d]
        main_y = -main_d

        # 근접 삼각형 분류
        buf = bytearray(bc)
        degenerate, snapped = [], 0
        snap_ys = {}
        for d, ts in planes.items():
            if d == main_d or not (WIN_LO < abs(d - main_d) < WIN_HI):
                continue
            for t in ts:
                tri = verts[[idxs[t], idxs[t + 1], idxs[t + 2]]]
                cx, cz = tri[:, 0].mean(), tri[:, 2].mean()
                overlapped = any(point_in_tri2(cx, cz, verts[[idxs[m], idxs[m + 1], idxs[m + 2]]]) for m in main_tris)
                if overlapped:
                    degenerate.append(t)
                else:
                    for vi in (idxs[t], idxs[t + 1], idxs[t + 2]):
                        vi = int(vi)
                        if verts[vi][1] > main_y + 0.0002:
                            snap_ys[vi] = main_y
        for t in degenerate:
            same = int(idxs[t])
            buf[ioff + t * 2: ioff + t * 2 + 6] = struct.pack("<HHH", same, same, same)
        for vi, ny in snap_ys.items():
            struct.pack_into("<f", buf, voff + vi * 12 + 4, ny)
            snapped += 1

        # POSITION accessor min/max 재계산
        new_verts = np.frombuffer(bytes(buf), dtype=np.float32, count=ap["count"] * 3, offset=voff).reshape(-1, 3)
        if "min" in ap and "max" in ap:
            ap["min"] = [float(x) for x in new_verts.min(axis=0)]
            ap["max"] = [float(x) for x in new_verts.max(axis=0)]

        write_glb(cur, j, bytes(buf))

        # 재검증
        j2, bc2 = parse_glb(cur)
        r2 = geo(j2, bc2)
        verts2, idxs2 = r2[3], r2[6]
        left = 0
        for t in range(0, len(idxs2) - 2, 3):
            if idxs2[t] == idxs2[t + 1] == idxs2[t + 2]: continue
            a, b, c = verts2[idxs2[t]], verts2[idxs2[t + 1]], verts2[idxs2[t + 2]]
            n = np.cross(b - a, c - a)
            ln = np.linalg.norm(n)
            if ln < 1e-9: continue
            n = n / ln
            if n[1] <= 0.99: continue
            d = -float(np.dot(n, a))
            if WIN_LO < abs(d - main_d) < WIN_HI: left += 1
        report.append(f"{name}: 퇴화 {len(degenerate)} / 스냅정점 {snapped} / 근접잔존 {left} {'OK' if left == 0 else '!!'}")

    print("\n".join(report))

if __name__ == "__main__":
    main()
