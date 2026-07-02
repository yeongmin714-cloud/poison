#!/bin/bash
set -x
set -euo pipefail

# Configuration
USERPROVIDED="/mnt/c/Unity/code/Assets/Resources/Models/UserProvided"
STATE_FILE="/tmp/.hermes_glb_state.txt"  # plain text, one basename per line
ARCHIVE_DIR="/mnt/c/Unity/code/Assets/Models/UserProvided_Archive"
COMPILE_LOG="/mnt/c/tmp/compile.log"
SWAP_LOG="/mnt/c/tmp/swap.log"
UNITY_EXE=/mnt/c/Program\ Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe
PROJECT_PATH_WSL="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:\\Unity\\code"
PROJECT_PATH="$PROJECT_PATH_WIN"
LOG_DIR="/mnt/c/tmp"

# Ensure directories exist
mkdir -p "$ARCHIVE_DIR" "$LOG_DIR"
SUFFIXES=("_rigged" "_tier1" "_tier2" "_tier3" "_tier4" "_tier5")

# Function to get allowed GLB basenames (lowercase, no extension) from ModelMapping.cs
get_allowed_basenames() {
    awk -F'"' '/{/{print $2}' "/mnt/c/Unity/code/Assets/Editor/ModelMapping.cs" | tr -d ' ' | tr '[:upper:]' '[:lower:]' | sort | uniq | grep -v '^_.*'
}
# Function to get processed basenames from state file (one per line)
get_processed_basenames() {
    if [[ -f "$STATE_FILE" ]]; then
        # Read each line, trim whitespace, ignore empty lines
        while IFS= read -r line; do
            # Trim leading and trailing whitespace
            line="$(echo "$line" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')"
            if [[ -n "$line" ]]; then
                echo "$line"
            fi
        done < "$STATE_FILE"
    fi
}

# Function to update state file with processed basenames (one per line)
update_state_file() {
    local basenames=("$@")
    # Write each basename on a new line
    printf '%s
' "${basenames[@]}" > "$STATE_FILE"
}


run_unitybatchmode() {
    # Function to run Unity batchmode command
    local method_name="$1"
    local log_file="$2"
    local bat_file="$LOG_DIR/unity_command.bat"

    # Create the bat file using printf for reliable content
    printf '@echo off\r\n"%s" -quit -batchmode -projectPath "%s" -executeMethod %s -logFile "%s"\r\n' \
        "$(wslpath -w "$UNITY_EXE")" "$PROJECT_PATH_WIN" "$method_name" "$(wslpath -w "$log_file")" > "$bat_file"

    # Convert bat file path to Windows for cmd.exe
    local bat_file_win=$(wslpath -w "$bat_file")

    # Run the bat file
    cmd.exe /c "$bat_file_win"
    local exit_code=$?

    # Clean up bat file
    rm -f "$bat_file"

    return $exit_code
}
unity_cleanup() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Killing Unity processes..."
    powershell.exe -NoProfile "Get-Process | Where-Object { $_.ProcessName -like '*Unity*' } | Stop-Process -Force" 2>/dev/null || true
    powershell.exe -Command "Get-Process -Name UnityHub,UnityPackageManager,UnityCrashHandler64,'Unity.Licensing.Client' -ErrorAction SilentlyContinue | Stop-Process -Force" 2>/dev/null || true
    find /mnt/c/Unity/code/Library -name "*-lock" -delete 2>/dev/null || true
    sleep 5
}

# Main processing
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Starting GLB watcher..."

# Get current GLB files (top-level only, exclude processed directory)
mapfile -t current_files < <(find "$USERPROVIDED" -maxdepth 1 -name "*.glb" -type f | sort)
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Found ${#current_files[@]} GLB files in UserProvided."

# Get allowed basenames from ModelMapping.cs
mapfile -t allowed_basenames < <(get_allowed_basenames)
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Allowed basenames: ${#allowed_basenames[@]}"

# Get processed basenames from state file
mapfile -t processed_basenames < <(get_processed_basenames)
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Already processed: ${#processed_basenames[@]}"

# Determine new files to process
declare -A processed_set
for bn in "${processed_basenames[@]}"; do
    processed_set["$bn"]=1
done

new_files=()
for file_path in "${current_files[@]}"; do
    # Extract basename without extension, lowercase, spaces to underscores
    filename=$(basename "$file_path")
    basename_no_ext="${filename%.*}"
    basename_lower=$(echo "$basename_no_ext" | tr '[:upper:]' '[:lower:]' | tr ' ' '_')
    # Handle suffix stripping for matching allowed baselines
    matched_basename="$basename_lower"
    matched=false
    # First, try exact match
    if [[ " ${allowed_basenames[*]} " == *" $matched_basename "* ]]; then
        matched=true
    else
        # Try stripping known suffixes
        for suffix in "${SUFFIXES[@]}"; do
            if [[ "$basename_lower" == *"$suffix" ]]; then
                stripped="${basename_lower%"$suffix"}"
                if [[ " ${allowed_basenames[*]} " == *" $stripped "* ]]; then
                    matched_basename="$stripped"
                    matched=true
                    break
                fi
            fi
        done
    fi
    # If not matched via suffix, we keep the original basename_lower for the check (will fail)
    if [[ -z "$matched" ]]; then
        matched_basename="$basename_lower"
    fi
    # Check if allowed and not processed
    if [[ " ${allowed_basenames[*]} " == *" $matched_basename "* ]] && [[ -z "${processed_set[$matched_basename]:-}" ]]; then
        new_files+=("$file_path")
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] New file to process: $filename (basename: $basename_lower)"
    else
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Skipping $filename (basename: $basename_lower) - not allowed or already processed"
    fi
done

if [[ ${#new_files[@]} -eq 0 ]]; then
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] No new GLB files to process. Exiting."
    exit 0
fi

# Kill Unity processes and clear lockfiles before swap
unity_cleanup

# Run compile test
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Running compile test..."
if ! run_unitybatchmode "ProjectName.Core.CompileTestHelper.DoNothing" "$COMPILE_LOG"; then
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Compile test failed to launch Unity batchmode."
    exit 1
fi

# Check compile log for success
if grep -i "error CS" "$COMPILE_LOG"; then
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Compile test failed. Checking log for errors...]\n"
    # Show first 10 error lines
    grep -i "error CS" "$COMPILE_LOG" | head -10 || true
    exit 1
fi
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Compile test passed."

# Run swap
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Running model swap..."
if ! run_unitybatchmode "ModelSwapper.SwapAndSave" "$SWAP_LOG"; then
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Swap failed to launch Unity batchmode."
    exit 1
fi

# Check swap log for success
if grep -i "error CS" "$SWAP_LOG"; then
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Swap failed. Checking log for errors...]\n"
    grep -i "error CS" "$SWAP_LOG" | head -10 || true
    exit 1
fi
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Swap passed."

# Update state file with all currently processed basenames (allowed basenames of current files)
current_basenames=()
for file_path in "${current_files[@]}"; do
    filename=$(basename "$file_path")
    basename_no_ext="${filename%.*}"
    basename_lower=$(echo "$basename_no_ext" | tr '[:upper:]' '[:lower:]' | tr ' ' '_')
    if [[ " ${allowed_basenames[*]} " == *" $basename_lower "* ]]; then
        current_basenames+=("$basename_lower")
    fi
done
update_state_file "${current_basenames[@]}"
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Updated state file with ${#current_basenames[@]} processed basenames."

# Restart Unity
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Restarting Unity Editor..."
cmd.exe /c start "" "$UNITY_EXE" -projectPath "$PROJECT_PATH_WIN"
echo "[$(date '+%Y-%m-%d %H:%M:%S')] Unity restart initiated."

exit 0
