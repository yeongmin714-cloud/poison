#!/bin/bash
set +e

UNITY_EXE="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
USER_PROVIDED_DIR="$PROJECT_PATH/Assets/Resources/Models/UserProvided"
STATE_FILE="/tmp/.hermes_glb_state.json"

# Get list of .glb files in UserProvided (top level only)
mapfile -t glb_files < <(find "$USER_PROVIDED_DIR" -maxdepth 1 -name "*.glb" -type f -basename | sort)

# Extract allowed basenames from ModelMapping.cs (lowercase, no extension, spaces->underscores)
allowed_basenames=$(grep -o '\"[^\"]*\"\\s*=>' "$PROJECT_PATH/Assets/Editor/ModelMapping.cs" | sed 's/\"//g' | sed 's/=>//g' | tr -d ' ' | tr '[:upper:]' '[:lower:]')

# Convert glb files to basenames for comparison
declare -a glb_basenames
declare -a glb_files_array
for f in "${glb_files[@]}"; do
    glb_files_array+=("$f")
    base=$(basename "$f" .glb)
    base_lower=$(echo "$base" | tr '[:upper:]' '[:lower:]' | tr ' ' '_')
    glb_basenames+=("$base_lower")
done

# Read state file
declare -a processed_basenames
if [ -f "$STATE_FILE" ]; then
    state_content=$(cat "$STATE_FILE" 2>/dev/null)
    if python3 -c "import json,sys; json.loads(sys.stdin.read())" <<< "$state_content" 2>/dev/null; then
        mapfile -t processed_basenames < <(python3 -c "import json,sys; print('\\n'.join(json.loads(sys.stdin.read())))" <<< "$state_content")
    fi
fi

# Find new files: in glb_files, basename in allowed_basenames, not in processed_basenames
new_files=()
for i in "${!glb_files_array[@]}"; do
    base="${glb_basenames[$i]}"
    if echo "$allowed_basenames" | grep -q "^$base$"; then
        skip=0
        for p in "${processed_basenames[@]}"; do
            if [ "$p" = "$base" ]; then
                skip=1
                break
            fi
        done
        if [ $skip -eq 0 ]; then
            new_files+=("${glb_files_array[$i]}")
        fi
    fi
done

if [ ${#new_files[@]} -eq 0 ]; then
    echo "[SILENT]"
    exit 0
fi

# Process first new file
file_to_process="${new_files[0]}"
base_name=$(basename "$file_to_process" .glb)
base_for_state=$(echo "$base_name" | tr '[:upper:]' '[:lower:]' | tr ' ' '_')

echo "Processing new GLB file: $file_to_process" >&2

# Kill Unity processes
echo "Killing Unity processes..." >&2
powershell.exe -NoProfile "Get-Process Unity -ErrorAction SilentlyContinue | Stop-Process -Force" 2>/dev/null || true
powershell.exe -NoProfile "Get-Process UnityHub,UnityPackageManager,UnityCrashHandler64,'Unity.Licensing.Client' -ErrorAction SilentlyContinue | Stop-Process -Force" 2>/dev/null || true
find "$PROJECT_PATH/Library" -name "*-lock" -delete 2>/dev/null || true
sleep 5

# Compile test
echo "Running compile test..." >&2
UNITY_EXE_WIN=$(wslpath -w "$UNITY_EXE")
PROJECT_PATH_WIN=$(wslpath -w "$PROJECT_PATH")
cmd.exe /c "\"\"$UNITY_EXE_WIN\"\" -quit -batchmode -projectPath \"\"$PROJECT_PATH_WIN\"\" -executeMethod TestCompile.CompileTest -logFile compile.log" 2>&1 >&2
if grep -i "error cs" "$PROJECT_PATH/compile.log" 2>/dev/null; then
    echo "Compile test failed. See compile.log for details." >&2
    exit 1
fi

# Swap operation
echo "Running swap operation..." >&2
cmd.exe /c "\"\"$UNITY_EXE_WIN\"\" -quit -batchmode -projectPath \"\"$PROJECT_PATH_WIN\"\" -executeMethod ModelSwapper.SwapAndSave -logFile swap.log" 2>&1 >&2

# Check swap.log
if grep -i "error cs" "$PROJECT_PATH/swap.log" 2>/dev/null; then
    echo "Swap operation failed with compiler errors. See swap.log." >&2
    exit 1
fi

if grep -q "씬 저장 완료!" "$PROJECT_PATH/swap.log" 2>/dev/null || grep -q "✅.*교체 완료" "$PROJECT_PATH/swap.log" 2>/dev/null; then
    echo "Swap operation successful." >&2

    # Update state with all currently scanned allowed basenames
    processed_in_this_run=()
    for i in "${!glb_files_array[@]}"; do
        base="${glb_basenames[$i]}"
        if echo "$allowed_basenames" | grep -q "^$base$"; then
            processed_in_this_run+=("$base")
        fi
    done

    printf '%s\n' "${processed_in_this_run[@]}" > /tmp/processed_list.txt
    python3 -c "import json,sys; data=[line.rstrip('\\n') for line in sys.stdin]; print(json.dumps(data))" < /tmp/processed_list.txt > "$STATE_FILE"

    # Restart Unity Editor
    echo "Restarting Unity Editor..." >&2
    cmd.exe /c start "" ""\"$UNITY_EXE_WIN\"\" -projectPath \"\"$PROJECT_PATH_WIN\"\"" 2>&1 >&2

    # Success message
    echo "[Unity] GLB file '$base_name' swapped successfully."
    exit 0
else
    echo "Swap operation did not show success indicators. See swap.log for details." >&2
    exit 1
fi
