#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""[199차 옵션1] wood_bow/stone_bow 임베드 JPEG 4096²(13MB×2) → 1024 재임베드 감량.

- 시각 동등 근거: 아이콘 렌더 1024RT·월드 노출 화면 크기에서 4096-1024 불가시(197차 파이프라인).
- 절차(192차 교체 레시피): 이미지 뷰만 대체 바이트로 교체 — relayout_bin이 bufferView byteOffset/
  byteLength 재계산, 나머지 뷰 원 바이트 보존. mimeType(image/jpeg) 유지.
- 백업: Tools/bow_prebake_backup199/ (Assets 밖 — 임포트 무관·롤백 가능, 최초 1회만).
- 검증: 재파싱 후 전체 이미지 디코딩(PIL load) + 목표 해상도 1024 확인.
"""
import io
import json
import os
import shutil
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vertex_ao_bake import parse_glb, repack_glb  # noqa: E402
from PIL import Image  # noqa: E402

PROJ = "/mnt/c/Unity/code"
BOWS = ["wood_bow", "stone_bow"]
GLB_DIR = os.path.join(PROJ, "Assets/Resources/Models/UserProvided")
BACKUP_DIR = os.path.join(PROJ, "Tools/bow_prebake_backup199")
TARGET = 1024
JPEG_Q = 88


def slim(path, fid):
    j, old_bin = parse_glb(path)
    replacements = {}
    report = []
    for img_idx, img in enumerate(j.get("images", [])):
        bv = j["bufferViews"][img["bufferView"]]
        off = bv.get("byteOffset", 0)
        blob = bytes(old_bin[off:off + bv["byteLength"]])
        mime = img.get("mimeType", "")
        if mime != "image/jpeg":
            report.append(f"img{img_idx}:skip({mime})")
            continue
        im = Image.open(io.BytesIO(blob))
        im.load()
        w, h = im.size
        if max(w, h) <= TARGET:
            report.append(f"img{img_idx}:skip({w}x{h}<=target)")
            continue
        small = im.convert("RGB").resize((TARGET, TARGET), Image.LANCZOS)
        buf = io.BytesIO()
        small.save(buf, format="JPEG", quality=JPEG_Q, optimize=True)
        new_blob = buf.getvalue()
        replacements[img["bufferView"]] = new_blob
        report.append(f"img{img_idx}:{w}x{h} {len(blob)}B->{len(new_blob)}B")
    if not replacements:
        raise RuntimeError(f"{fid}: 감량 대상 이미지 없음")
    new_bin = relayout_bin_local(j, old_bin, replacements)
    return j, new_bin, report, old_bin


def relayout_bin_local(j, old_bin, replacements):
    """floor_quality_bake.relayout_bin와 동일 — 로컬 복제(Tools 간 import 순환 방지)."""
    def _align4(n):
        return (n + 3) // 4 * 4
    new_bin = bytearray()
    for i, bv in enumerate(j["bufferViews"]):
        old_off = bv.get("byteOffset", 0)
        old_len = bv["byteLength"]
        payload = replacements.get(i)
        if payload is None:
            payload = old_bin[old_off:old_off + old_len]
        new_off = _align4(len(new_bin))
        new_bin += b"\0" * (new_off - len(new_bin))
        new_bin += payload
        bv["byteOffset"] = new_off
        bv["byteLength"] = len(payload)
    return new_bin


def main(apply_mode):
    os.makedirs(BACKUP_DIR, exist_ok=True)
    n_done = 0
    for fid in BOWS:
        path = os.path.join(GLB_DIR, fid + ".glb")
        try:
            if not apply_mode:
                j, old_bin = parse_glb(path)
                for img_idx, img in enumerate(j.get("images", [])):
                    bv = j["bufferViews"][img["bufferView"]]
                    blob = bytes(old_bin[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]])
                    if blob[:2] == b"\xff\xd8":
                        w, h = Image.open(io.BytesIO(blob)).size
                        print(f"DRY {fid}: img{img_idx} {w}x{h} {len(blob)}B mime={img.get('mimeType')}")
                continue
            bak = os.path.join(BACKUP_DIR, fid + ".glb")
            if not os.path.exists(bak):
                shutil.copy2(path, bak)   # 최초 1회 — 원본 보존
            new_j, new_bin, report, _ = slim(path, fid)
            tmp = path + ".slimtmp"
            open(tmp, "wb").write(repack_glb(new_j, new_bin))
            os.replace(tmp, path)
            # 검증 — 재파싱 + 전체 디코딩 + 해상도
            j2, b2 = parse_glb(path)
            for img2 in j2["images"]:
                bv2 = j2["bufferViews"][img2["bufferView"]]
                blob2 = bytes(b2[bv2["byteOffset"]:bv2["byteOffset"] + bv2["byteLength"]])
                assert len(blob2) == bv2["byteLength"], f"{fid}: 이미지 슬라이스 단축"
                im2 = Image.open(io.BytesIO(blob2))
                im2.load()
                if img2.get("mimeType") == "image/jpeg":
                    assert max(im2.size) == TARGET, f"{fid}: 재임베드 해상도 {im2.size}"
            print(f"OK {fid}: {' | '.join(report)} file={os.path.getsize(path)}B backup={os.path.getsize(bak)}B")
            n_done += 1
        except Exception as e:  # noqa: BLE001
            print(f"ERR {fid}: {repr(e)[:180]}")
    print(f"=== bows processed {n_done}/{len(BOWS)}, apply={apply_mode}")


if __name__ == "__main__":
    main(apply_mode="--apply" in sys.argv)
