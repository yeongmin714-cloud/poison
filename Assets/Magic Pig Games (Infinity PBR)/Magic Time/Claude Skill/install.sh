#!/usr/bin/env bash
#
# Installs the Magic Time AI Agent Skill into a project's (or the user's global)
# .claude/skills/ folder so Claude Code / Cowork discover it.
#
# Usage:
#   bash install.sh            # install into the nearest Unity project (folder containing Assets/)
#   bash install.sh --global   # install into ~/.claude/skills/ (all projects)
#   bash install.sh /path/to/project   # install into a specific project root
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE="$SCRIPT_DIR/magic-time"
SKILL_NAME="magic-time"

if [[ ! -d "$SOURCE" ]]; then
  echo "error: cannot find the skill at: $SOURCE" >&2
  exit 1
fi

# Resolve the destination .claude/skills directory.
DEST_ROOT=""
if [[ "${1:-}" == "--global" ]]; then
  DEST_ROOT="$HOME"
elif [[ -n "${1:-}" ]]; then
  DEST_ROOT="$1"
else
  # Walk up from the current directory to find the Unity project root (has Assets/).
  dir="$PWD"
  while [[ "$dir" != "/" ]]; do
    if [[ -d "$dir/Assets" ]]; then DEST_ROOT="$dir"; break; fi
    dir="$(dirname "$dir")"
  done
  if [[ -z "$DEST_ROOT" ]]; then
    echo "Could not find a Unity project (no Assets/ folder above $PWD)." >&2
    echo "Run from inside your project, pass a path, or use --global." >&2
    exit 1
  fi
fi

DEST="$DEST_ROOT/.claude/skills/$SKILL_NAME"
mkdir -p "$(dirname "$DEST")"

if [[ -d "$DEST" ]]; then
  echo "Updating existing skill at: $DEST"
  rm -rf "$DEST"
else
  echo "Installing skill to: $DEST"
fi

# Copy everything except OS/Unity cruft.
cp -R "$SOURCE" "$DEST"
find "$DEST" \( -name ".DS_Store" -o -name "*.meta" \) -delete 2>/dev/null || true

echo
echo "✅ Installed the Magic Time skill."
echo "   Open Claude Code / Cowork in $DEST_ROOT and ask:"
echo "     \"Set up Magic Time and make this enemy move in slow motion.\""
