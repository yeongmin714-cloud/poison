#!/bin/bash
# Phase 68.3 Batch Runner - Quick mode for remaining models
# Usage: ./run_phase683_batch.sh <avatar_type> <policy_type>
# Runs BC + KD + LOD + INT8 in quick mode

AVATAR=$1
POLICY=$2
cd /mnt/c/Unity/code
source ~/torch_venv/bin/activate

LOG_DIR=~/phase683_logs
mkdir -p $LOG_DIR
LOG_FILE="$LOG_DIR/${AVATAR}_${POLICY}.log"

echo "[$(date)] Starting Phase 68.3: $AVATAR/$POLICY" > $LOG_FILE

# BC + KD + Ensemble (skip quant/lod for now, will do batch at end)
python Assets/Training/TrainingInfra/phase683_advanced.py \
  --avatar_type $AVATAR --policy_type $POLICY \
  --quick \
  --skip_lod \
  --verbose \
  >> $LOG_FILE 2>&1

EXIT_CODE=$?
echo "[$(date)] $AVATAR/$POLICY complete (exit=$EXIT_CODE)" >> $LOG_FILE

# Notify Telegram
if [ $EXIT_CODE -eq 0 ]; then
  python3 -c "
import urllib.request, urllib.parse
msg = '✅ Phase 68.3 완료: $AVATAR/$POLICY'
url = 'https://api.telegram.org/bot'
# Try to get token
import os, re
try:
    with open(os.path.expanduser('~/.hermes/config.yaml')) as f:
        m = re.search(r'token:\s*\"([^\"]+)\"', f.read())
        if m:
            data = urllib.parse.urlencode({'chat_id': '6847418902', 'text': msg}).encode()
            urllib.request.urlopen(url + m.group(1) + '/sendMessage', data=data, timeout=10)
except: pass
"
else:
  python3 -c "
import urllib.request, urllib.parse, os, re
msg = '❌ Phase 68.3 실패: $AVATAR/$POLICY (exit=$EXIT_CODE)'
try:
    with open(os.path.expanduser('~/.hermes/config.yaml')) as f:
        m = re.search(r'token:\s*\"([^\"]+)\"', f.read())
        if m:
            data = urllib.parse.urlencode({'chat_id': '6847418902', 'text': msg}).encode()
            urllib.request.urlopen(url + m.group(1) + '/sendMessage', data=data, timeout=10)
except: pass
"
fi