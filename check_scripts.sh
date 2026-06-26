#!/bin/bash
set -euo pipefail

PROJECT_ROOT="/mnt/c/Unity/code"
SCRIPTS_DIR="${PROJECT_ROOT}/Assets/Scripts"

if [[ ! -d "$SCRIPTS_DIR" ]]; then
    echo "Directory $SCRIPTS_DIR not found"
    exit 1
fi

total=0
passed=0
issues=0

echo "Checking C# scripts in $SCRIPTS_DIR"
echo "----------------------------------------"

while IFS= read -r -d '' csfile; do
    total=$((total + 1))
    relpath="${csfile#$PROJECT_ROOT/}"
    issue_found=false
    issues_list=""

    # Check for syntax error pattern
    if grep -q "error CS" "$csfile" 2>/dev/null; then
        issue_found=true
        issues_list="${issues_list}Syntax error suspected (error CS); "
    fi

    # Check for MonoBehaviour without lifecycle methods
    if grep -q "class.*:.*MonoBehaviour" "$csfile" 2>/dev/null; then
        # Check for any of the four methods
        if ! grep -qE "(void Start\\(\)|void Update\\(\)|void Awake\\(\)|void OnEnable\\(\))" "$csfile" 2>/dev/null; then
            issue_found=true
            issues_list="${issues_list}Missing lifecycle methods (Start/Update/Awake/OnEnable); "
        fi
    fi

    if $issue_found; then
        issues=$((issues + 1))
        # Trim trailing space and semicolon
        issues_list="${issues_list%, }"
        echo "[ISSUE] $relpath: $issues_list"
    else
        passed=$((passed + 1))
        echo "[OK] $relpath: ✅"
    fi
done < <(find "$SCRIPTS_DIR" -name "*.cs" -print0 2>/dev/null)

echo "----------------------------------------"
echo "Summary:"
echo "  Total files checked: $total"
echo "  Passed: $passed"
echo "  Issues: $issues"