#!/usr/bin/env bash
# PostToolUse: fast feedback on .cs edits.
#
# Runs scoped format + build + arch tests on the touched project/module only.
# Emits additional context back to Claude via stdout (JSON) so failures land in
# the conversation and Claude can react.

set -uo pipefail

input=$(cat)
file=$(printf '%s' "$input" | jq -r '.tool_input.file_path // .tool_input.path // empty')

# Only care about .cs files inside src/
if [[ -z "$file" || "$file" != *.cs ]]; then
  exit 0
fi

rel="${file#"$CLAUDE_PROJECT_DIR/"}"
if [[ "$rel" != src/* && "$rel" != tests/* ]]; then
  exit 0
fi

# Find the nearest .csproj walking up from the file
dir=$(dirname "$file")
csproj=""
while [[ "$dir" != "$CLAUDE_PROJECT_DIR" && "$dir" != "/" ]]; do
  found=$(find "$dir" -maxdepth 1 -name "*.csproj" -print -quit 2>/dev/null || true)
  if [[ -n "$found" ]]; then
    csproj="$found"
    break
  fi
  dir=$(dirname "$dir")
done

if [[ -z "$csproj" ]]; then
  exit 0
fi

msgs=()
fail=0

# 1. Format check (fast) — scoped to the project
fmt_out=$(cd "$CLAUDE_PROJECT_DIR" && dotnet format "$csproj" --verify-no-changes --verbosity quiet 2>&1 || true)
if [[ $? -ne 0 ]] && printf '%s' "$fmt_out" | grep -q "error\|needs formatting"; then
  msgs+=("Format drift in $(basename "$csproj"). Run: dotnet format $csproj")
  fail=1
fi

# 2. Build — scoped
build_out=$(cd "$CLAUDE_PROJECT_DIR" && dotnet build "$csproj" --nologo --verbosity quiet --no-restore 2>&1 || true)
if printf '%s' "$build_out" | grep -qE "error [A-Z]+[0-9]+:"; then
  errors=$(printf '%s' "$build_out" | grep -E "error [A-Z]+[0-9]+:" | head -n 5)
  msgs+=("Build failed in $(basename "$csproj"):\n$errors")
  fail=1
fi

# 3. Arch tests when domain or module-root files change
if [[ "$rel" == */Domain/* || "$rel" =~ src/Modules/[^/]+/[^/]+\.cs$ ]]; then
  arch_proj=$(find "$CLAUDE_PROJECT_DIR/tests" -name "*Architecture*.csproj" -print -quit 2>/dev/null || true)
  if [[ -n "$arch_proj" ]]; then
    arch_out=$(cd "$CLAUDE_PROJECT_DIR" && dotnet test "$arch_proj" --nologo --verbosity quiet --no-restore 2>&1 || true)
    if printf '%s' "$arch_out" | grep -qE "Failed!"; then
      failed=$(printf '%s' "$arch_out" | grep -E "Failed |^\s+X " | head -n 5)
      msgs+=("Architecture tests failed:\n$failed\nModule boundaries or domain purity rules are violated.")
      fail=1
    fi
  fi
fi

if [[ $fail -eq 1 ]]; then
  # Emit JSON so Claude sees the additional context
  joined=$(printf '%s\n---\n' "${msgs[@]}")
  jq -n --arg ctx "$joined" '{
    hookSpecificOutput: {
      hookEventName: "PostToolUse",
      additionalContext: $ctx
    }
  }'
  exit 0
fi

exit 0
