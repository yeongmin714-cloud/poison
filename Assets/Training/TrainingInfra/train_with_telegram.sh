#!/bin/bash
# train_with_telegram.sh — Run training and send Telegram notification on completion
# Usage: ./train_with_telegram.sh <avatar_type> <policy_type> <epochs> [extra_args...]

AVATAR=$1
POLICY=$2
EPOCHS=$3
shift 3

LOG_FILE="$HOME/train_${AVATAR}_${POLICY}.log"
ONNX_PATH="Assets/Resources/NeuralModels/${POLICY}_${AVATAR}_base.onnx"
START_TIME=$(date +%s)

echo "[$(date)] Starting training: $AVATAR $POLICY $EPOCHS epochs" | tee -a "$LOG_FILE"

cd /mnt/c/Unity/code
source ~/torch_venv/bin/activate
python Assets/Training/TrainingInfra/train_torch.py \
  --avatar_type "$AVATAR" \
  --policy_type "$POLICY" \
  --epochs "$EPOCHS" \
  "$@" >> "$LOG_FILE" 2>&1

EXIT_CODE=$?
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))
HOURS=$((DURATION / 3600))
MINUTES=$(( (DURATION % 3600) / 60 ))
SECONDS=$((DURATION % 60))

# Check ONNX file
if [ -f "$ONNX_PATH" ]; then
    SIZE=$(stat -c%s "$ONNX_PATH" 2>/dev/null || stat -f%z "$ONNX_PATH" 2>/dev/null)
    SIZE_KB=$((SIZE / 1024))
    RESULT="✅ 성공"
else
    RESULT="❌ 실패"
    SIZE_KB="N/A"
fi

# Send Telegram notification via hermes CLI
MESSAGE="[Phase 68 학습 완료]
${RESULT} | ${AVATAR} ${POLICY}
⏱ ${HOURS}h ${MINUTES}m ${SECONDS}s
📦 ${ONNX_PATH} (${SIZE_KB}KB)
로그: ~/train_${AVATAR}_${POLICY}.log"

echo "$MESSAGE" | hermes send -t telegram -f -

echo "[$(date)] Training complete: $AVATAR $POLICY (exit=$EXIT_CODE)" | tee -a "$LOG_FILE"
exit $EXIT_CODE