#!/usr/bin/env bash
# PreToolUse hook: block edits to protected paths and suspicious cross-module changes.
#
# Reads Claude's tool-call JSON from stdin, inspects file_path, exits:
#   0 = allow   1 = warn (shown to user, still allowed)   2 = block (feedback to Claude)
#
# Decision JSON on stdout is also supported; we use simple exit codes for clarity.

set -euo pipefail

input=$(cat)
tool=$(printf '%s' "$input" | jq -r '.tool_name // empty')
file=$(printf '%s' "$input" | jq -r '.tool_input.file_path // .tool_input.path // empty')

# Nothing to check
if [[ -z "$file" ]]; then
  exit 0
fi

# Normalize to project-relative path
rel="${file#"$CLAUDE_PROJECT_DIR/"}"

block() {
  # Exit code 2 feeds stderr back to Claude as a blocking error.
  echo "BLOCKED: $1" >&2
  echo "If this change is truly needed, ask the user to make it directly or to override." >&2
  exit 2
}

# --- Hard blocks --------------------------------------------------------------

# ADRs are human decisions
if [[ "$rel" == docs/adr/* ]]; then
  block "ADRs ($rel) are human-authored. Draft the content in chat; let the user commit it."
fi

# Production config
if [[ "$rel" == *appsettings.Production.json ]]; then
  block "Production config ($rel) must not be modified by the agent."
fi

# Central package/build config — route through ask
case "$rel" in
  *Directory.Packages.props|*Directory.Build.props|*global.json|*.sln)
    # Let ask-permission handle it; only block if running non-interactively
    # (the 'ask' permission in settings.json covers the interactive case)
    ;;
esac

# Migrations already committed — warn, don't block, but flag loudly
if [[ "$rel" == *Migrations/*.cs && -f "$file" ]]; then
  # If file exists, it's been created before. Editing applied migrations is dangerous.
  echo "⚠️  Editing existing migration $rel — this usually indicates a new migration is needed instead." >&2
  # Exit 1 would show as warning in some clients; use 0 to allow but with stderr visible.
fi

# --- Domain purity check ------------------------------------------------------

# Block obvious infrastructure imports being added to Domain files
if [[ "$rel" == */Domain/* && "$rel" == *.cs ]]; then
  content=""
  if [[ "$tool" == "Write" ]]; then
    content=$(printf '%s' "$input" | jq -r '.tool_input.content // empty')
  elif [[ "$tool" == "Edit" || "$tool" == "MultiEdit" ]]; then
    content=$(printf '%s' "$input" | jq -r '.tool_input.new_string // .tool_input.edits[]?.new_string // empty' 2>/dev/null || true)
  fi

  if [[ -n "$content" ]]; then
    forbidden='using (Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|Wolverine|Serilog|System\.Net\.Http|Microsoft\.Extensions\.(Caching|Configuration|Hosting|Logging))'
    if printf '%s' "$content" | grep -Eq "$forbidden"; then
      block "Domain file $rel must stay infrastructure-free. Detected a forbidden using directive. Move this to the module's Infrastructure or Application layer."
    fi
  fi
fi

# --- Cross-module reference check --------------------------------------------

# If editing a Module file, warn if new content references another module directly (not via Contracts)
if [[ "$rel" =~ ^src/Modules/([^/]+)/ ]]; then
  this_module="${BASH_REMATCH[1]}"
  content=""
  if [[ "$tool" == "Write" ]]; then
    content=$(printf '%s' "$input" | jq -r '.tool_input.content // empty')
  elif [[ "$tool" == "Edit" || "$tool" == "MultiEdit" ]]; then
    content=$(printf '%s' "$input" | jq -r '.tool_input.new_string // .tool_input.edits[]?.new_string // empty' 2>/dev/null || true)
  fi

  if [[ -n "$content" ]]; then
    # Look for `using Modulith.Modules.<Other>.` not ending in .Contracts
    other=$(printf '%s' "$content" \
      | grep -Eo 'using Modulith\.Modules\.[A-Za-z0-9_]+\.[A-Za-z0-9_.]+' \
      | grep -v "Modulith\.Modules\.${this_module}\." \
      | grep -v '\.Contracts' \
      | head -n 1 || true)
    if [[ -n "$other" ]]; then
      block "Cross-module reference detected in $rel: '$other'. Modules may only reference each other's Contracts project."
    fi
  fi
fi

exit 0
