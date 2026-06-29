#!/bin/bash
set -euo pipefail

# Configuration
USERPROVIDED="/mnt/c/Unity/code/Assets/Resources/Models/UserProvided"
STATE_FILE="/tmp/.hermes_glb_state.txt"  # Changed to plain text, one basename per line
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
    printf '%s\n' "${basenames[@]}" > "$STATE_FILE"
}

# Function to run Unity batchmode command
run_unity_batchmode() {
    local method_name="$1"
    local log_file="$2"
    local bat_file="$LOG_DIR/unity_command.bat"
    
    # Create the bat file using printf for reliable content
    printf '@echo off\r\n"%s" -quit -batchmode -projectPath "%s" -executeMethod %s -logFile "%s"\r\n' \
        "$UNITY_EXE" "$PROJECT_PATH_WIN" "$method_name" "$log_file" > "$bat_file"
    
    # Convert bat file path to Windows for cmd.exe
    local bat_file_win=$(wslpath -w "$bat_file")
    
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
    # Extract basename without extension, lowercase, spaces to underscores
    filename=$(basename "$file_path")
    basename_no_ext="${filename%.*}"
    basename_lower=$(echo "$basename_no_ext" | tr '[:upper:]' '[:lower:]' | tr ' ' '_')
    
    # Check if allowed and not processed
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
    
    fi
    
    # Step 3: Copy GLB to archive (lowercase normalization, deduplication)
    archive_file="$ARCHIVE_DIR/$(echo "$filename" | tr '[:upper:]' '[:lower:]')' '$' ']'\n' | sed '$' //" | sed 's/\\/\\\\/g' | sed "s/\"/\\\"/g" | awk '{printf "%s\"%s\"", sep, $0; sep=", "} END {if (NR>0) printf "];"\n else printf "[];\n"}'  # This is too complex.

Given the time, let's stick with the plain text state file and update the script accordingly.

We'll write the script now.

We'll also fix the issue where the archive copy path was malformed (we saw a typo: archive_file="$ARCHIVE_DIR/$(echo "$filename" | tr '[:upper:]' '[:lower:]')" but in the output we saw something else? Actually in the previous run, the archive copy step was skipped because the archive already contained the lowercase version. That's fine.

Let's create the final script.

We'll also add a check to ensure the swap succeeded by looking for clear success indicators in the log, and only then update the state file.

We'll also ensure that after a successful swap, we update the state file with the new basename.

We'll also restart Unity only after a successful swap.

Let's write the script.
<tool_call>
<function=terminal>
<parameter=command>
cat > /mnt/c/Unity/code/glb_watcher_fixed.sh << 'END_OF_SCRIPT'
#!/bin/bash
set -euo pipefail

# Configuration
USERPROVIDED="/mnt/c/Unity/code/Assets/Resources/Models/UserProvided"
STATE_FILE="/tmp/.hermes_glb_state.txt"  # Plain text, one basename per line
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
    printf '%s\n' "${basenames[@]}" > "$STATE_FILE"
}

# Function to run Unity batchmode command
run_unity_batchmode() {
    local method_name="$1"
    local log_file="$2"
    local bat_file="$LOG_DIR/unity_command.bat"
    
    # Create the bat file using printf for reliable content
    printf '@echo off\r\n"%s" -quit -batchmode -projectPath "%s" -executeMethod %s -logFile "%s"\r\n' \
        "$UNITY_EXE" "$PROJECT_PATH_WIN" "$method_name" "$log_file" > "$bat_file"
    
    # Convert bat file path to Windows for cmd.exe
    local bat_file_win
