#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""[209차-C] wood_sword 3패널 비교 PNG 합성 + 객관 통계(facts.log 선례).

- 패널: 원본(구 파이프라인) | 신규 조명+후처리 | 신규+AO 수술 — 각 256px, 가로 나열.
- 통계: 패널별 평균 휘도(Rec.601)/명암(표준편차) 전체 + 중앙 1/3 avgRGB(193/194 프로브 패턴).
- 출력: TestOutput/icon_compare209/wood_sword_compare209.png
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

PROJ = "/mnt/c/Unity/code"
OUT_DIR = os.path.join(PROJ, "TestOutput/icon_compare209")
PANELS = [
    ("wood_sword_before.png", "원본 (구 파이프라인)"),
    ("wood_sword_after13.png", "신규 조명+후처리"),
    ("wood_sword_full.png", "신규 + AO 수술"),
]
OUT_PNG = os.path.join(OUT_DIR, "wood_sword_compare209.png")
STATS_JSON = os.path.join(OUT_DIR, "stats_209c.json")

PAD = 14          # 패널 간/외곽 패딩
LABEL_H = 34      # 라벨 스트립 높이
BG = (16, 16, 22)          # 아이콘 배경(0.06,0.06,0.09)과 톤 일치
FG = (230, 230, 235)
BORDER = (70, 70, 80)

FONT_PATH = "/mnt/c/Windows/Fonts/malgun.ttf"
FONT_BD = "/mnt/c/Windows/Fonts/malgunbd.ttf"


def luma(arr):
    return 0.299 * arr[..., 0] + 0.587 * arr[..., 1] + 0.114 * arr[..., 2]


def main():
    try:
        font = ImageFont.truetype(FONT_BD, 17)
    except OSError:
        font = ImageFont.truetype(FONT_PATH, 17)

    imgs, stats = [], []
    for fname, label in PANELS:
        im = Image.open(os.path.join(OUT_DIR, fname)).convert("RGB")
        arr = np.asarray(im, dtype=np.float64)
        L = luma(arr)
        h, w = arr.shape[:2]
        c = arr[h // 3:h * 2 // 3, w // 3:w * 2 // 3]
        stats.append({
            "panel": fname, "label": label,
            "luma_mean": round(float(L.mean()), 2),
            "luma_std": round(float(L.std()), 2),
            "center3_avgRGB": [round(float(x), 3) for x in c.reshape(-1, 3).mean(axis=0)],
        })
        imgs.append(im)

    pw, ph = imgs[0].size
    W = PAD + len(imgs) * (pw + PAD)
    H = PAD + ph + LABEL_H + PAD
    canvas = Image.new("RGB", (W, H), BG)
    draw = ImageDraw.Draw(canvas)
    for i, (im, (fname, label)) in enumerate(zip(imgs, PANELS)):
        x = PAD + i * (pw + PAD)
        canvas.paste(im, (x, PAD))
        draw.rectangle([x - 1, PAD - 1, x + pw, PAD + ph], outline=BORDER, width=1)
        tw = draw.textbbox((0, 0), label, font=font)[2]
        draw.text((x + (pw - tw) // 2, PAD + ph + 7), label, fill=FG, font=font)

    canvas.save(OUT_PNG)
    json.dump({"panels": stats, "out": OUT_PNG}, open(STATS_JSON, "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    for s in stats:
        print(f"{s['label']}: luma_mean={s['luma_mean']} luma_std={s['luma_std']} "
              f"center3_avgRGB={s['center3_avgRGB']}")
    print(f"saved: {OUT_PNG} ({canvas.size[0]}x{canvas.size[1]})")


if __name__ == "__main__":
    main()
