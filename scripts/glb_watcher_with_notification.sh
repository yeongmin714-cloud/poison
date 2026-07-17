#!/usr/bin/env bash
set -euo pipefail

# === Configuration ==
# Path to the corrected GLB watcher script
GLB_WATCHER_SCRIPT="/mnt/c/Unity/code/glb_watcher_fixed.sh"
# State files used by the GLB watcher script
STATE_FILE="/tmp/.hermes_glb_state.txt"
JSON_STATE_FILE="/tmp/.hermes_glb_state.json"
# Log file for this wrapper (optional)
LOG_FILE="/tmp/glb_watcher_wrapper.log"
# Telegram notification script from the telegram-cli skill
TELEGRAM_SEND_SCRIPT="$HOME/.hermes/scripts/telegram-send.sh"

# Function to log messages with timestamp
log() {
    # Get timestamp, if date fails use a placeholder
    local timestamp
    timestamp=$(date '+%Y-%m-%d %H:%M:%S' 2>/dev/null) || timestamp="0000-00-00 00:00:00"
    echo "[$timestamp] $1" | tee -a "$LOG_FILE"
}

# Function to send a Telegram notification
send_telegram_notification() {
    local message="$1"
    # Check if the telegram-send script exists and is executable
    if [[ -x "$TELEGRAM_SEND_SCRIPT" ]]; then
        # The script expects token and chat ID as arguments, or they can be set via environment variables.
        # We assume the script is configured with the token and chat ID either in the script itself or via env.
        timeout 10 "$TELEGRAM_SEND_SCRIPT" "$message"
        log "Sent Telegram notification: $message"
    else
            log "WARNING: Telegram send script not found or not executable at $TELEGRAM_SEND_SCRIPT"
            log "INFO: Telegram notifications disabled."
    fi
}

# Function to synchronize state files using the state manager script
sync_state_files() {
    local state_manager_script="$SCRIPT_DIR/glb_state_manager.sh"
    if [[ -f "$state_manager_script" ]]; then
        log "Running state manager script to synchronize state files..."
        "$state_manager_script" update
        log "State file synchronization completed."
    else
        log "WARNING: State manager script not found at $state_manager_script. Skipping synchronization."
    fi
}

# Main script
main() {
    log "=== GLB Watcher Wrapper Started ==="
    local inner_exit=0

    # Ensure the log file directory exists
    mkdir -p "$(dirname "$LOG_FILE")"

    # Determine the script directory for locating the state manager script
    SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

    # Step 1: Get initial state file count from JSON state file
    initial_count=0
    if [[ -f "$JSON_STATE_FILE" ]]; then
        # Use Python to safely get the length of the JSON array or object
        initial_count=$(python3 -c "
import json, sys
try:
    with open(\"$JSON_STATE_FILE\") as f:
        data = json.load(f)
    if isinstance(data, list):
        print(len(data))
    elif isinstance(data, dict):
        print(len(data))
    else:
        print(0)
except Exception as e:
    print(0)
") 2>/dev/null || echo 0
    fi
    log "Initial state file count (JSON): $initial_count"

    # Step 2: Run the corrected GLB watcher script
    log "Running GLB watcher script: $GLB_WATCHER_SCRIPT"
    log "PATH for GLB watcher script: $PATH"
    if "$GLB_WATCHER_SCRIPT"; then
        inner_exit=0
        log "GLB watcher script completed successfully."
    else
        inner_exit=$?
        log "ERROR: GLB watcher script exited with non-zero status."
    fi

    # Step 3: Synchronize state files after the GLB watcher script runs
    sync_state_files

    # Step 4: Get final state file count from JSON state file
    final_count=0
    if [[ -f "$JSON_STATE_FILE" ]]; then
        final_count=$(python3 -c "
import json, sys
try:
    with open(\"$JSON_STATE_FILE\") as f:
        data = json.load(f)
    if isinstance(data, list):
        print(len(data))
    elif isinstance(data, dict):
        print(len(data))
    else:
        print(0)
except Exception as e:
    print(0)
") 2>/dev/null || echo 0
    fi
    log "Final state file count (JSON): $final_count"

    # Step 5: Determine if any new files were processed
    if [[ "$final_count" -gt "$initial_count" ]]; then
        new_count=$((final_count - initial_count))
        log "Detected $new_count new GLB file(s) processed."
        # Send Telegram notification
        message="🎮 GLB Watcher: Successfully processed $new_count new GLB file(s)."
        send_telegram_notification "$message"
    else
        log "No new GLB files were processed."
    fi

    log "=== GLB Wrapper Finished ==="
    exit $inner_exit
}

# Run the main function
main "$@"
