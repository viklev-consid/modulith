#!/usr/bin/env bash
# Stop hook: architecture tests must pass before the session can end.
#
# If they fail, we exit 2 which blocks the stop and feeds reason back to Claude.

set -uo pipefail

# Read input just to drain stdin (we don't use it)
cat >/dev/null

# Skip if we've already been invoked once in this stop cycle (avoid loops).
# Claude sets stop_hook_active in the payload; check env or use a marker.
if [[ "${CLAUDE_STOP_HOOK_ACTIVE:-0}" == "1" ]]; then
  exit 0
fi

arch_proj=$(find "$CLAUDE_PROJECT_DIR/tests" -name "*Architecture*.csproj" -print -quit 2>/dev/null || true)

if [[ -z "$arch_proj" ]]; then
  exit 0
fi

out=$(cd "$CLAUDE_PROJECT_DIR" && dotnet test "$arch_proj" --nologo --verbosity quiet 2>&1)
rc=$?

if [[ $rc -ne 0 ]]; then
  failed=$(printf '%s' "$out" | grep -E "Failed |^\s+X " | head -n 10)
  reason="Architecture tests must pass before the session ends. Failures:

$failed

Fix these before wrapping up."
  jq -n --arg r "$reason" '{
    decision: "block",
    reason: $r
  }'
  exit 0
fi

exit 0
