#!/usr/bin/env bash
# Reads project planning files and runs the WhatIsNext mission.
# Usage: ./what-is-next.sh [repo-root]

set -euo pipefail

REPO="${1:-$(git -C "$(dirname "$0")" rev-parse --show-toplevel)}"
DOCS="$REPO/docs"
MEMORY="$HOME/.claude/projects/-Users-ameerdeen-progs-mission-control-language/memory"

# Collect the most relevant planning files
context=""

append() {
  local label="$1" file="$2"
  if [[ -f "$file" ]]; then
    context+="### $label\n\n$(cat "$file")\n\n---\n\n"
  fi
}

append "plan.md"           "$DOCS/plan.md"
append "MEMORY.md (index)" "$MEMORY/MEMORY.md"

# Last 3 phase files by filename (highest phase numbers)
while IFS= read -r f; do
  append "$(basename "$f")" "$f"
done < <(ls "$DOCS/phases"/phase-3*.md "$DOCS/phases"/phase-32*.md "$DOCS/phases"/phase-33*.md 2>/dev/null | sort -V | tail -3)

# Run the mission
cd "$(dirname "$0")"
forge run mission.mcl --var "context=$context"
