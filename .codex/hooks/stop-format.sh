#!/usr/bin/env bash
# Stop hook (runs before stop-gate.sh): auto-fix formatting and analyzer issues
# on projects changed in this session so the workspace lands tidy.
#
# Scope: projects containing changed .cs files (per git). Keeps this fast.
# Never edits files outside src/ or tests/.

set -uo pipefail

# Drain stdin
cat >/dev/null

# Avoid re-entry loops
if [[ "${CLAUDE_STOP_FORMAT_RAN:-0}" == "1" ]]; then
  exit 0
fi
export CLAUDE_STOP_FORMAT_RAN=1

cd "$CLAUDE_PROJECT_DIR" || exit 0

# --- Find changed .cs files (staged, unstaged, untracked) --------------------
changed=$(
  {
    git diff --name-only HEAD 2>/dev/null
    git diff --name-only --cached 2>/dev/null
    git ls-files --others --exclude-standard 2>/dev/null
  } | grep -E '\.cs$' | grep -E '^(src|tests)/' | sort -u
)

if [[ -z "$changed" ]]; then
  exit 0
fi

# --- Derive affected .csproj files -------------------------------------------
declare -A projects
while IFS= read -r file; do
  dir=$(dirname "$file")
  while [[ "$dir" != "." && "$dir" != "/" ]]; do
    found=$(find "$dir" -maxdepth 1 -name "*.csproj" -print -quit 2>/dev/null || true)
    if [[ -n "$found" ]]; then
      projects["$found"]=1
      break
    fi
    dir=$(dirname "$dir")
  done
done <<< "$changed"

if [[ ${#projects[@]} -eq 0 ]]; then
  exit 0
fi

# --- Run dotnet format (fix, not verify) on each project ---------------------
fixed_any=0
failures=()

for proj in "${!projects[@]}"; do
  # Whitespace + style + analyzer fixes in one command (dotnet format default)
  out=$(dotnet format "$proj" --verbosity quiet 2>&1)
  rc=$?
  if [[ $rc -ne 0 ]]; then
    failures+=("$(basename "$proj"): $out")
  fi
done

# --- Report back any files that changed --------------------------------------
post_change=$(git diff --name-only 2>/dev/null | grep -E '\.cs$' || true)

if [[ -n "$post_change" || ${#failures[@]} -gt 0 ]]; then
  msg=""
  if [[ -n "$post_change" ]]; then
    count=$(printf '%s\n' "$post_change" | wc -l | tr -d ' ')
    msg+="dotnet format auto-fixed $count file(s) across ${#projects[@]} project(s). Review the diff before committing."$'\n'
  fi
  if [[ ${#failures[@]} -gt 0 ]]; then
    msg+=$'\nFormat failures:\n'
    for f in "${failures[@]}"; do
      msg+="- $f"$'\n'
    done
  fi

  # additionalContext so Claude sees what happened; do NOT block
  jq -n --arg ctx "$msg" '{
    hookSpecificOutput: {
      hookEventName: "Stop",
      additionalContext: $ctx
    }
  }'
fi

exit 0
