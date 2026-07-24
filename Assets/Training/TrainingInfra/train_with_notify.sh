#!/bin/bash
# train_with_notify.sh — Run training and send Telegram notification on completion
# Usage: ./train_with_notify.sh <avatar_type> <policy_type> <epochs> [extra_args...]

AVATAR=$1
POLICY=$2
EPOCHS=$3
shift 3

LOG_FILE="$HOME/train_${AVATAR}_${POLICY}.log"
ONNX_FILE="Assets/Resources/NeuralModels/${POLICY}_${AVATAR}_base.onnx"
START_TIME=$(date +%s)

echo "[$(date)] Starting training: $AVATAR $POLICY $EPOCHS epochs" | tee -a "$LOG_FILE"

# Source venv and run training
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
if [ -f "$ONNX_FILE" ]; then
    SIZE=$(stat -c%s "$ONNX_FILE")
    SIZE_KB=$((SIZE / 1024))
    RESULT="✅ 성공"
else
    RESULT="❌ 실패"
    SIZE_KB="N/A"
fi

# Send Telegram notification via bot API
MESSAGE="[Phase 68 학습 완료]
${RESULT} | ${AVATAR} ${POLICY}
⏱ ${HOURS}h ${MINUTES}m ${SECONDS}s
📦 ${ONNX_FILE} (${SIZE_KB}KB)
📋 로그: ${LOG_FILE}"

# Use Telegram bot API with environment variables
# Bot token and chat ID from hermes config
if [ -n "$HERMES_TELEGRAM_BOT_TOKEN" ] && [ -n "$HERMES_TELEGRAM_CHAT_ID" ]; then
    curl -s -X POST "https://api.telegram.org/bot${HERMES_TELEGRAM_BOT_TOKEN}/sendMessage" \
        -d "chat_id=${HERMES_TELEGRAM_CHAT_ID}" \
        -d "text=${MESSAGE}" \
        -d "parse_mode=HTML" > /dev/null 2>&1
fi

echo "[$(date)] Training complete: $AVATAR $POLICY (exit=$EXIT_CODE, duration=${HOURS}h ${MINUTES}m)" | tee -a "$LOG_FILE"
exit $EXIT_CODE