#!/usr/bin/env bash
set -euo pipefail

# === Configuration ==
UNITY_EDITOR="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
STATE_FILE="/tmp/.hermes_glb_state.txt"
MODEL_MAPPING_CS="/mnt/c/Unity/code/Assets/Editor/ModelMapping.cs"
COMPILE_LOG="/mnt/c/tmp/compile.log"
SWAP_LOG="/mnt/c/tmp/swap.log"

# Function to log messages with timestamp
log() {
    # Get timestamp, if date fails use a placeholder
    local timestamp
    timestamp=$(date '+%Y-%m-%d %H:%M:%S' 2>/dev/null) || timestamp="0000-00-00 00:00:00"
    echo "[$timestamp] $1"
}

# Unity cleanup function (improved)
unity_cleanup() {
    log "Killing Unity processes..."
    # Kill main Unity processes using taskkill
    /mnt/c/Windows/System32/taskkill.exe /f /im Unity.exe >/dev/null 2>&1 || true
    /mnt/c/Windows/System32/taskkill.exe /f /im UnityHub.exe >/dev/null 2>&1 || true
    /mnt/c/Windows/System32/taskkill.exe /f /im UnityPackageManager.exe >/dev/null 2>&1 || true
    /mnt/c/Windows/System32/taskkill.exe /f /im UnityCrashHandler64.exe >/dev/null 2>&1 || true
    /mnt/c/Windows/System32/taskkill.exe /f /im "Unity.Licensing.Client.exe" >/dev/null 2>&1 || true
    # Remove lock files
    find /mnt/c/Unity/code/Library -name "*-lock" -delete 2>/dev/null || true
    rm -f /mnt/c/Unity/code/Library/ilpp.pid 2>/dev/null || true
    # Short wait
    sleep 2
}

# Function to get allowed basenames from ModelMapping.cs
# Fixed version as suggested in the skill
# Function to get allowed GLB basenames (lowercase, no extension) from ModelMapping.cs
get_allowed_basenames() {
    sed -n 's/.*{"\([^"]*\)",.*//p' "/mnt/c/Unity/code/Assets/Editor/ModelMapping.cs" |     tr '[:upper:]' '[:lower:]' |     sort | uniq
}
update_state_file() {
    local basenames=("$@")
    # Write each basename on a new line
    printf '%s\n' "${basenames[@]}" > "$STATE_FILE"
}

# Function to check if a basename is already processed
is_processed() {
    local basename="$1"
    if [[ -f "$STATE_FILE" ]]; then
        grep -q "^${basename}$" "$STATE_FILE"
        return $?
    else
        return 1
    fi
}

# Function to add a basename to the state file
add_to_state() {
    local basename="$1"
    echo "$basename" >> "$STATE_FILE" || true
}

# Main logic
main() {
    log "=== GLB Watcher Fixed Script Started ==="
    
    # Step 1: Clean up any existing Unity processes
    unity_cleanup || true
    
    # Step 2: Get list of GLB files in UserProvided
    user_provided_dir="/mnt/c/Unity/code/Assets/Resources/Models/UserProvided"
    mapfile -t glb_files < <(find "$user_provided_dir" -maxdepth 1 -name '*.glb' -type f)
    log "Found ${#glb_files[@]} GLB file(s) in UserProvided."
    
    if [[ ${#glb_files[@]} -eq 0 ]]; then
        log "No GLB files to process. Exiting."
        exit 0
    fi
    
    # Convert paths to Windows format for Unity
    PROJECT_PATH_WIN=$(wslpath -w "$PROJECT_PATH")
    COMPILE_LOG_WIN=$(wslpath -w "$COMPILE_LOG")
    SWAP_LOG_WIN=$(wslpath -w "$SWAP_LOG")
    
    # Step 3: Get allowed basenames
    mapfile -t allowed_basenames < <(get_allowed_basenames)
    log "Allowed basenames: ${#allowed_basenames[@]}"
    
    # Step 4: Process each GLB file
    processed_count=0
    for glb_path in "${glb_files[@]}"; do
        # Get basename without extension
        filename=$(basename "$glb_path")
        basename="${filename%.glb}"
        basename_lower=$(echo "$basename" | tr '[:upper:]' '[:lower:]')
        
        log "Processing $filename (basename: $basename)"
        
        # Skip if not allowed
        if [[ ! " ${allowed_basenames[*]} " =~ " ${basename_lower} " ]]; then
            log "  -> Skipped: not in allowed list"
            continue
        fi
        
        # Skip if already processed
        if is_processed "$basename_lower"; then
            log "  -> Skipped: already processed"
            continue
        fi
        
        # Step 5: Run compile test in batchmode
        log "  -> Running compile test..."
        "$UNITY_EDITOR" -quit -batchmode -projectPath "$PROJECT_PATH_WIN" -executeMethod TestCompile.CompileTest -logFile "$COMPILE_LOG_WIN" || true
        compile_exit=$?
        
        # Check compile log for errors
        if grep -i "error cs" "$COMPILE_LOG" >/dev/null 2>&1; then
            log "  -> Compiler errors found in $COMPILE_LOG. Skipping swap."
            # Continue to next file
            continue
        fi
        
        # If we get here, compile test passed (no error CS)
        log "  -> Compile test passed (no error CS)."
        
        # Step 6: Run the swap
        log "  -> Running ModelSwapper.SwapAndSave..."
        "$UNITY_EDITOR" -quit -batchmode -projectPath "$PROJECT_PATH_WIN" -executeMethod ModelSwapper.SwapAndSave -logFile "$SWAP_LOG_WIN" || true
        swap_exit=$?
        
        # Check swap log for success
        if grep -qi "success" "$SWAP_LOG" || grep -qi "completed" "$SWAP_LOG"; then
            log "  -> Swap successful."
            # Add to state file
            add_to_state "$basename_lower"
            ((processed_count++))
        else
            log "  -> Swap failed or not confirmed in log."
            # Optionally, check the swap log for errors
            # We'll just continue to next file
        fi
    done
    
    # Step 7: Restart Unity Editor (optional, but recommended)
    log "Restarting Unity Editor..."
    # Start Unity in the background (no wait)
    "$UNITY_EDITOR" -projectPath "$PROJECT_PATH_WIN" &>/dev/null &
    
    log "=== GLB Watcher Finished: Processed $processed_count new file(s) ==="
    exit 0
}

# Run main
main "$@"