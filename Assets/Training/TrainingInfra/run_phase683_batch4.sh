#!/bin/bash
# Phase 68.3 Batch4 - Ensemble + INT8 + LOD for one model
# Usage: ./run_phase683_batch4.sh <avatar_type> <policy_type>

AVATAR=$1
POLICY=$2
cd /mnt/c/Unity/code
source ~/torch_venv/bin/activate

LOG_DIR=~/phase683_logs
mkdir -p $LOG_DIR
LOG_FILE="$LOG_DIR/batch4_${AVATAR}_${POLICY}.log"
OUTPUT_DIR="Assets/Resources/NeuralModels"

echo "[$(date)] Starting Batch4: $AVATAR/$POLICY" > $LOG_FILE

ENSEMBLE_PATH="$OUTPUT_DIR/ensemble_${POLICY}_${AVATAR}_base.onnx"
INT8_PATH="$OUTPUT_DIR/${POLICY}_${AVATAR}_base_int8.onnx"

# 1. Ensemble (skip if exists)
if [ -f "$ENSEMBLE_PATH" ]; then
  echo "[$(date)] Ensemble already exists, skipping" >> $LOG_FILE
else
  echo "[$(date)] Running Ensemble for $AVATAR/$POLICY..." >> $LOG_FILE
  python Assets/Training/TrainingInfra/phase683_advanced.py \
    --avatar_type $AVATAR --policy_type $POLICY \
    --skip_bc --skip_kd --skip_quant --skip_lod \
    --quick \
    --verbose \
    >> $LOG_FILE 2>&1
  echo "[$(date)] Ensemble exit=$?" >> $LOG_FILE
fi

# 2. INT8 Quantization (skip if exists)
if [ -f "$INT8_PATH" ]; then
  echo "[$(date)] INT8 already exists, skipping" >> $LOG_FILE
else
  BASE_MODEL="$OUTPUT_DIR/${POLICY}_${AVATAR}_base.onnx"
  if [ -f "$BASE_MODEL" ]; then
    echo "[$(date)] Running INT8 for $BASE_MODEL..." >> $LOG_FILE
    python -c "
import sys; sys.path.insert(0, 'Assets/Training/TrainingInfra')
from phase683_advanced import quantize_onnx_int8
quantize_onnx_int8('$BASE_MODEL', '$INT8_PATH', verbose=True)
" >> $LOG_FILE 2>&1
    echo "[$(date)] INT8 exit=$?" >> $LOG_FILE
  else
    echo "[$(date)] Base model not found: $BASE_MODEL" >> $LOG_FILE
  fi
fi

# 3. LOD Models (skip if LOD0 already exists)
LOD0_PATH="$OUTPUT_DIR/${POLICY}_${AVATAR}_LOD0.onnx"
if [ -f "$LOD0_PATH" ]; then
  echo "[$(date)] LOD models already exist, skipping" >> $LOG_FILE
else
  BASE_MODEL="$OUTPUT_DIR/${POLICY}_${AVATAR}_base.onnx"
  if [ -f "$BASE_MODEL" ]; then
    echo "[$(date)] Running LOD models for $AVATAR/$POLICY..." >> $LOG_FILE
    python -c "
import sys; sys.path.insert(0, 'Assets/Training/TrainingInfra')
from phase683_advanced import generate_lod_models
generate_lod_models('$AVATAR', '$POLICY', base_model_path='$BASE_MODEL', verbose=True)
" >> $LOG_FILE 2>&1
    echo "[$(date)] LOD exit=$?" >> $LOG_FILE
  else
    echo "[$(date)] Base model not found: $BASE_MODEL" >> $LOG_FILE
  fi
fi

echo "[$(date)] Batch4 complete: $AVATAR/$POLICY" >> $LOG_FILE