#!/usr/bin/env bash
# SessionStart: inject compact project orientation so every session starts aligned.
#
# Outputs JSON with additionalContext that Claude reads as part of the session.

set -uo pipefail

ctx_parts=()

# Root CLAUDE.md summary (first ~40 lines — keep it tight)
if [[ -f "$CLAUDE_PROJECT_DIR/CLAUDE.md" ]]; then
  summary=$(head -n 40 "$CLAUDE_PROJECT_DIR/CLAUDE.md")
  ctx_parts+=("=== Project overview (CLAUDE.md head) ===
$summary")
fi

# Current git branch and recent changes
if command -v git >/dev/null 2>&1 && git -C "$CLAUDE_PROJECT_DIR" rev-parse 2>/dev/null; then
  branch=$(git -C "$CLAUDE_PROJECT_DIR" branch --show-current 2>/dev/null || echo "?")
  recent=$(git -C "$CLAUDE_PROJECT_DIR" log --oneline -5 2>/dev/null || true)
  status=$(git -C "$CLAUDE_PROJECT_DIR" status --short 2>/dev/null | head -n 20 || true)
  ctx_parts+=("=== Git ===
Branch: $branch
Recent commits:
$recent
Uncommitted:
${status:-<clean>}")
fi

# List modules so Claude knows the landscape
if [[ -d "$CLAUDE_PROJECT_DIR/src/Modules" ]]; then
  modules=$(find "$CLAUDE_PROJECT_DIR/src/Modules" -maxdepth 1 -mindepth 1 -type d -exec basename {} \; 2>/dev/null | sort | paste -sd ', ' -)
  ctx_parts+=("=== Modules present ===
$modules")
fi

# Reminder of non-negotiable rules
ctx_parts+=("=== Guardrails (enforced by hooks) ===
- Domain folders must stay free of EF/ASP.NET/Wolverine/Serilog usings.
- Cross-module references must go through the other module's Contracts project.
- ADRs under docs/adr/ are human-authored — draft in chat only.
- Directory.Packages.props, Directory.Build.props, global.json require user confirmation.
- Architecture tests run after domain/module-root edits and must stay green before Stop.")

if [[ ${#ctx_parts[@]} -eq 0 ]]; then
  exit 0
fi

joined=$(printf '%s\n\n' "${ctx_parts[@]}")
jq -n --arg ctx "$joined" '{
  hookSpecificOutput: {
    hookEventName: "SessionStart",
    additionalContext: $ctx
  }
}'
