#!/usr/bin/env bash
set -euo pipefail

# === State Manager Script for GLB Watcher ===
# Synchronizes the plain text state file (.txt) and the JSON state file (.json)
# used by the GLB watcher wrapper and fixed scripts.

STATE_FILE="/tmp/.hermes_glb_state.txt"
JSON_STATE_FILE="/tmp/.hermes_glb_state.json"

usage() {
    echo "Usage: $0 update"
    echo "  update   Synchronize state files (ensure both contain the same set)."
    exit 1
}

sync_state_files() {
    # Collect basenames from both sources
    declare -A seen  # associative array for uniqueness
    local items=()

    # Read from text file (one per line)
    if [[ -f "$STATE_FILE" ]]; then
        while IFS= read -r line; do
            # Trim whitespace
            line="$(echo "$line" | xargs)"
            if [[ -n "$line" ]]; then
                # Convert to lowercase as used elsewhere
                key="${line,,}"
                if [[ -z "${seen[$key]:-}" ]]; then
                    seen["$key"]=1
                    items+=("$line")  # keep original case? we store lowercase later
                fi
            fi
        done < "$STATE_FILE"
    fi

    # Read from JSON file
    if [[ -f "$JSON_STATE_FILE" ]]; then
        # Use Python to parse JSON safely
        mapfile -t json_lines < <(python3 -c "
import json, sys
try:
    with open('$JSON_STATE_FILE') as f:
        data = json.load(f)
    if isinstance(data, list):
        for item in data:
            if isinstance(item, str):
                print(item.strip())
    elif isinstance(data, dict):
        # If it's an object, maybe we want keys? but we expect array.
        for key in data.keys():
            if isinstance(key, str):
                print(key.strip())
except Exception as e:
    pass
") 2>/dev/null || true

        for line in "${json_lines[@]}"; do
            line="$(echo "$line" | xargs)"
            if [[ -n "$line" ]]; then
                key="${line,,}"
                if [[ -z "${seen[$key]:-}" ]]; then
                    seen["$key"]=1
                    items+=("$line")
                fi
            fi
        done
    fi

    # Write text file (sorted, one per line, lowercase)
    printf '%s\n' "${items[@],,}" | sort -u > "$STATE_FILE"

    # Write JSON file
    {
        echo '['
        n=${#items[@]}
        for i in "${!items[@]}"; do
            val="$(echo "${items[i]}" | sed 's/"/\\\\\"/g')"  # escape quotes
            printf '  "%s"' "$val"
            if (( i < n-1 )); then
                echo ','
            else
                echo
            fi
        done
        echo ']'
    } > "$JSON_STATE_FILE"
}

main() {
    if [[ $# -ne 1 ]] || [[ "$1" != "update" ]]; then
        usage
    fi
    sync_state_files
}

main "$@"