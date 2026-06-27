#!/bin/bash
set +e

UNITY_EXE="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
USER_PROVIDED_DIR="$PROJECT_PATH/Assets/Resources/Models/UserProvided"
ARCHIVE_DIR="$PROJECT_PATH/Assets/Models/UserProvided_Archive"
MODEL_MAPPING_CS="$PROJECT_PATH/Assets/Editor/ModelMapping.cs"
STATE_FILE="/tmp/.hermes_glb_state.json"
COMPILE_LOG="/tmp/compile.log"
SWAP_LOG="/tmp/swap.log"
TELEGRAM_CHAT_ID="6847418902" # From the skill, but we won't actually send telegram in cron

# Convert WSL path to Windows path
wslpath_to_windows() {
    wslpath -w "$1"
}

# Kill Unity-related processes
kill_unity_processes() {
    powershell.exe -NoProfile "Get-Process | Where-Object { $_.ProcessName -like '*Unity*' } | Stop-Process -Force" 2>/dev/null || true
    powershell.exe -Command "Get-Process -Name UnityHub,UnityPackageManager,UnityCrashHandler64,'Unity.Licensing.Client' -ErrorAction SilentlyContinue | Stop-Process -Force" 2>/dev/null || true
    find "$PROJECT_PATH/Library" -name '*-lock' -delete 2>/dev/null || true
}

# Extract allowed GLB basenames from ModelMapping.cs (lowercase, no extension)
# Outputs each basename on a new line
get_allowed_basenames() {
    awk -F'"' '/=>/ {print tolower($2)}' "$MODEL_MAPPING_CS"
}

# Read state file (JSON array) into a bash array
load_state_array() {
    local -n arr=$1  # nameref to return array
    if [[ ! -f "$STATE_FILE" ]]; then
        arr=()
        return 0
    fi
    local content
    if ! content=$(cat "$STATE_FILE" 2>/dev/null); then
        arr=()
        return 0
    fi
    # Remove brackets
    content="${corenotice: The content was cut off due to length. Let's try a different approach. We'll write the script in parts or use the existing script from the skill.

Given the complexity, let's instead run the existing glb_watcher_fixed2.sh script that we saw in the Unity project directory. It seems to be a recent version.

We'll run that script and capture the output.

Let's run:

```bash
cd /mnt/c/Unity/code && bash glb_watcher_fixed2.sh
```

We'll set a timeout of 120 seconds.

Let's do it<tool_call>
<function=terminal>
<parameter=command>
cd /mnt/c/Unity/code && bash glb_watcher_fixed2.sh