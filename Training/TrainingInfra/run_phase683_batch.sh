#!/bin/bash
# Phase 68.3 Batch Runner - Quick mode for remaining models
# Usage: ./run_phase683_batch.sh <avatar_type> <policy_type>
# Runs BC + KD in quick mode (no Telegram - just runs)

AVATAR=$1
POLICY=$2
cd /mnt/c/Unity/code
source ~/torch_venv/bin/activate

LOG_DIR=~/phase683_logs
mkdir -p $LOG_DIR
LOG_FILE="$LOG_DIR/${AVATAR}_${POLICY}.log"

echo "[$(date)] Starting Phase 68.3: $AVATAR/$POLICY" > $LOG_FILE

# BC + KD (skip ensemble/lod/quant - will do those in final batch)
python Assets/Training/TrainingInfra/phase683_advanced.py \
  --avatar_type $AVATAR --policy_type $POLICY \
  --quick \
  --skip_ensemble --skip_lod --skip_quant \
  --verbose \
  >> $LOG_FILE 2>&1

EXIT_CODE=$?
echo "[$(date)] $AVATAR/$POLICY complete (exit=$EXIT_CODE)" >> $LOG_FILE

exit $EXIT_CODE