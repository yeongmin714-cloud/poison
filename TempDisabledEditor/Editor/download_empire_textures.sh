#!/bin/bash
# 황제국 돌 텍스처 다운로드 (Poly Haven)
mkdir -p /mnt/c/Unity/code/Assets/Resources/Textures/Empire
cd /mnt/c/Unity/code/Assets/Resources/Textures/Empire

# Poly Haven stone texture (1K resolution)
wget -q -O empire_stone_col.jpg "https://polyhaven.com/dl/stone_wall_02/Stone_Wall_02_col_1k.jpg" || echo "FAILED: stone color"
wget -q -O empire_stone_nrm.jpg "https://polyhaven.com/dl/stone_wall_02/Stone_Wall_02_nrm_1k.jpg" || echo "FAILED: stone normal"
wget -q -O empire_marble_col.jpg "https://polyhaven.com/dl/marble_01/Marble_01_col_1k.jpg" || echo "FAILED: marble color"

echo "✅ Empire textures downloaded"
