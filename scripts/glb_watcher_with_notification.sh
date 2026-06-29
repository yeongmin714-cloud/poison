#!/usr/bin/env bash
set -euo pipefail

# === Configuration ===
# Path to the corrected GLB watcher script
GLB_WATCHER_SCRIPT="/mnt/c/Unity/code/glb_watcher_corrected.sh"
# State file used by the GLB watcher script (plain text, one basename per line)
STATE_FILE="/tmp/.hermes_glb_state.txt"
# Log file for this wrapper (optional)
LOG_FILE="/tmp/glb_watcher_wrapper.log"
# Telegram notification script from the telegram-cli skill
TELEGRAM_SEND_SCRIPT="$HOME/.hermes/scripts/telegram-send.sh"

# Function to log messages with timestamp
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# Function to send a Telegram notification
send_telegram_notification() {
    local message="$1"
    # Check if the telegram-send script exists and is executable
    if [[ -x "$TELEGRAM_SEND_SCRIPT" ]]; then
        # The script expects token and chat ID as arguments, or they can be set via environment variables.
        # We assume the script is configured with the token and chat ID either in the script itself or via env.
        "$TELEGRAM_SEND_SCRIPT" "$message"
        log "Sent Telegram notification: $message"
    else
        log "WARNING: Telegram send script not found or not executable at $TELEGRAM_SEND_SCRIPT"
        # Fallback: try to use environment variables with curl if available
        if [[ -n "${TELEGRAM_TOKEN:-}" && -n "${TELEGRAM_CHAT_ID:-}" ]]; then
            curl -s -X POST "https://api.telegram.org/bot${TELEGRAM_TOKEN}/sendMessage" \
                -d "chat_id=${TELEGRAM_CHAT_ID}" \
                -d "text=$(python3 -c "import urllib.parse, sys; print(urllib.parse.quote(sys.argv[1]))" "$message")" \
                -H "Content-Type: application/x-www-form-urlencoded" >/dev/null && \
                log "Sent Telegram notification via curl fallback: $message" || \
                log "ERROR: Failed to send Telegram notification via curl"
        else
            log "ERROR: Telegram credentials not configured. Set TELEGRAM_TOKEN and TELEGRAM_CHAT_ID or ensure $TELEGRAM_SEND_SCRIPT is set up."
        fi
    fi
}

# Main script
main() {
    log "=== GLB Watcher Wrapper Started ==="

    # Ensure the log file directory exists
    mkdir -p "$(dirname "$LOG_FILE")"

    # Step 1: Get initial state file line count
    if [[ -f "$STATE_FILE" ]]; then
        initial_count=$(wc -l < "$STATE_FILE")
        # Remove leading/trailing whitespace
        initial_count=$(echo "$initial_count" | xargs)
    else
        initial_count=0
    fi
    log "Initial state file line count: $initial_count"

    # Step 2: Run the corrected GLB watcher script
    log "Running GLB watcher script: $GLB_WATCHER_SCRIPT"
    if "$GLB_WATCHER_SCRIPT"; then
        log "GLB watcher script completed successfully."
    else
        log "ERROR: GLB watcher script exited with non-zero status."
        # We still check the state file in case some files were processed before failure?
        # But per the corrected script, it only updates state on success, so we can skip notification.
    fi

    # Step 3: Get final state file line count
    if [[ -f "$STATE_FILE" ]]; then
        final_count=$(wc -l < "$STATE_FILE")
        final_count=$(echo "$final_count" | xargs)
    else
        final_count=0
    fi
    log "Final state file line count: $final_count"

    # Step 4: Determine if any new files were processed
    if [[ "$final_count" -gt "$initial_count" ]]; then
        new_count=$((final_count - initial_count))
        log "Detected $new new GLB file(s) processed."
        # Send Telegram notification
        message="🎮 GLB Watcher: Successfully processed $new new GLB file(s)."
        send_telegram_notification "$message"
    else
        log "No new GLB files were processed."
    fi

    log "=== GLB Wrapper Finished ==="
}

# Run the main function
main "$@"