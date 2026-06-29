#!/bin/bash
set -euo pipefail

# Configuration
USERPROVIDED="/mnt/c/Unity/code/Assets/Resources/Models/UserProvided"
STATE_FILE="/tmp/.hermes_glb_state.txt"
ARCHIVE_DIR="/mnt/c/Unity/code/Assets/Models/UserProvided_Archive"
COMPILE_LOG="/mnt/c/tmp/compile.log"
SWAP_LOG="/mnt/c/tmp/swap.log"
UNITY_EXE="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH_WSL="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:\Unity\code"
LOG_DIR="/mnt/c/tmp"

# Ensure directories exist
mkdir -p "$ARCHIVE_DIR" "$LOG_DIR"

# Function to get allowed GLB basenames (lowercase, no extension) from ModelMapping.cs
get_allowed_basenames() {
    grep -o '\"[^\"]*\"\s*=>' "/mnt/c/Unity/code/Assets/Editor/ModelMapping.cs" | \
    sed 's/\"//g' | sed 's/=>//g' | tr -d ' ' | tr '[:upper:]' '[:lower:]' | sort | uniq
}

# Function to get processed basenames from state file (one per line)
get_processed_basenames() {
    if [[ -f "$STATE_FILE" ]]; then
        while IFS= read -r line; do
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
    printf '%s\n' "${basenames[@]}" > "$STATE_FILE"
}

# Function to run Unity batchmode command
run_unity_batchmode() {
    local method_name="$1"
    local log_file="$2"
    local bat_file="$LOG_DIR/unity_command.bat"
    
    # Convert paths to Windows format
    local unity_exe_win=$(wslpath -w "$UNITY_EXE")
    local log_file_win=$(wslpath -w "$log_file")
    local bat_file_win=$(wslpath -w "$bat_file")
    
    # Create the bat file using printf for reliable content
    printf '@echo off\r\n"%s" -quit -batchmode -projectPath "%s" -executeMethod %s -logFile "%s"\r\n' \
        "$unity_exe_win" "$PROJECT_PATH_WIN" "$method_name" "$log_file_win" > "$bat_file"
    
    # Run the bat file
    cmd.exe /c "$bat_file_win"
    local exit_code=$?
    
    # Clean up bat file
    rm -f "$bat_file"
    
    return $exit_code
}

# Function to kill Unity processes and remove lockfiles
unity_cleanup() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Killing Unity processes..."
    powershell.exe -NoProfile "Get-Process | Where-Object { \$_.ProcessName -like '*Unity*' } | Stop-Process -Force" 2>/dev/null || true
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
    filename=$(basename "$file_path")
    basename_no_ext="${filename%.*}"
    basename_lower=$(echo "$basename_no_ext" | tr '[:upper:]' '[:lower:]' | tr ' ' '_')
    
    if [[ " ${allowed_basenames[*]} " == *" $basename_lower "* ]] && [[ -z "${processed_set[$basename_lower]:-}" ]]; then
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

# Process each new file sequentially
for file_path in "${new_files[@]}"; do
    filename=$(basename "$file_path")
    basename_no_ext="${filename%.*}"
    basename_lower=$(echo "$basename_no_ext" | tr '[:upper:]' '[:lower:]' | tr ' ' '_')
    
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Processing $filename..."
    
    # Step 1: Cleanup Unity processes and lockfiles
    unity_cleanup
    
    # Step 2: Run compile test
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Running compile test..."
    if run_unity_batchmode "ProjectName.Core.CompileTestHelper.DoNothing" "$COMPILE_LOG"; then
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Compile test succeeded (exit code 0)."
        compile_success=true
    else
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Compile test failed. Checking log for errors..."
        if grep -i "error cs" "$COMPILE_LOG" >/dev/null 2>&1; then
            echo "[$(date '+%Y-%m-%d %H:%M:%S')] Compiler errors found in $COMPILE_LOG. Skipping swap."
            compile_success=false
        else
            echo "[$(date '+%Y-%m-%d %H:%M:%S')] No compiler errors found in log, but Unity exited with non-zero. Treating as failure."
            compile_success=false
        fi
    fi
    
    if [[ "$compile_success" != "true" ]]; then
        # Do not update state file, so file will be retried next cycle
        continue
    fi
    
    # Step 3: Copy GLB to archive (lowercase normalization, deduplication)
    archive_file="$ARCHIVE_DIR/$(echo "$filename" | tr '[:upper:]' '[:lower:]')"
    if [[ -f "$archive_file" ]]; then
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Archive already contains lowercase version of $filename. Skipping copy."
    else
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Copying $filename to archive as lowercase."
        cp "$file_path" "$archive_file"
    fi
    
    # Step 4: Run swap operation
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Running model swap..."
    if run_unity_batchmode "ModelSwapper.SwapAndSave" "$SWAP_LOG"; then
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Swap command executed successfully (exit code 0)."
        swap_success=true
    else
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Swap command failed. Checking log..."
        if grep -i "error cs" "$SWAP_LOG" >/dev/null 2>&1; then
            echo "[$(date '+%Y-%m-%d %H:%M:%S')] Compiler errors found in $SWAP_LOG. Swap failed."
            swap_success=false
        else
            # Check for success indicators in log
            if grep -i "교체 완료\|saved\|Swap successful" "$SWAP_LOG" >/dev/null 2>&1; then
                echo "[$(date '+%Y-%m-%d %H:%M:%S')] Swap succeeded based on log indicators."
                swap_success=true
            else
                echo "[$(date '+%Y-%m-%d %H:%M:%S')] No clear success indicators in swap log. Assuming failure."
                swap_success=false
            fi
        fi
    fi
    
    if [[ "$swap_success" != "true" ]]; then
        continue
    fi
    
    # Step 5: Update state file with current basename
    processed_basenames+=("$basename_lower")
    update_state_file "${processed_basenames[@]}"
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Updated state file with $basename_lower"
    
    # Step 6: Restart Unity Editor
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Restarting Unity Editor..."
    unity_exe_win=$(wslpath -w "$UNITY_EXE")
    project_path_win=$(wslpath -w "$PROJECT_PATH_WSL")
    cmd.exe /c start "" ""$unity_exe_win"" -projectPath ""$project_path_win""
    sleep 5
    
    # Step 7: Send Telegram notification (placeholder)
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] [Unity] GLB file '$filename' swapped successfully."
done

echo "[$(date '+%Y-%m-%d %H:%M:%S')] GLB watcher finished."
