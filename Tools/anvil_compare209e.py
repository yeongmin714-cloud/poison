#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""[209차-E] Polyfork anvil 2패널 비교 PNG 합성 + 객관 통계(wood_sword_compare209c 선례).

- 패널: 원본(before) | AO 수술(after) — 각 256px, 가로 나열 + 라벨 스트립.
- 통계: 패널별 평균 휘도(Rec.601)/명암(표준편차) 전체 + 중앙 1/3 avgRGB.
- 출력: TestOutput/icon_compare209/polyfork_anvil_compare209e.png
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

PROJ = "/mnt/c/Unity/code"
OUT_DIR = os.path.join(PROJ, "TestOutput/icon_compare209")
PANELS = [
    ("polyfork_anvil_before.png", "원본 (AO 수술 전)"),
    ("polyfork_anvil_full.png", "AO 수술 (COLOR_0 × 정점AO)"),
]
OUT_PNG = os.path.join(OUT_DIR, "polyfork_anvil_compare209e.png")
STATS_JSON = os.path.join(OUT_DIR, "stats_209e.json")

PAD = 14
LABEL_H = 34
BG = (16, 16, 22)
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
