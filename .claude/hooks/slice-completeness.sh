#!/usr/bin/env bash
# PostToolUse: when a slice file is created or edited, check its siblings exist.
#
# Your template convention:
#   src/Modules/<Module>/Features/<Feature>/
#     Request.cs       Response.cs    Command.cs
#     Handler.cs       Validator.cs   Endpoint.cs
#
# Missing pieces are reported back so Claude notices and fills them in.

set -uo pipefail

input=$(cat)
file=$(printf '%s' "$input" | jq -r '.tool_input.file_path // .tool_input.path // empty')

if [[ -z "$file" || "$file" != *.cs ]]; then
  exit 0
fi

# Match slice feature path — actual structure: src/Modules/<Module>/Modulith.Modules.<Module>/Features/<Feature>/
if [[ ! "$file" =~ src/Modules/[^/]+/[^/]+/Features/[^/]+/[^/]+\.cs$ ]]; then
  exit 0
fi

feature_dir=$(dirname "$file")
feature_name=$(basename "$feature_dir")

expected=(Request.cs Response.cs Command.cs Handler.cs Validator.cs Endpoint.cs)
missing=()

for f in "${expected[@]}"; do
  # Allow either exact name or prefixed variant (e.g. CreateUserCommand.cs)
  if ! find "$feature_dir" -maxdepth 1 -type f -iname "*${f%.cs}*.cs" | grep -q .; then
    missing+=("$f")
  fi
done

if [[ ${#missing[@]} -gt 0 ]]; then
  list=$(printf -- '- %s\n' "${missing[@]}")
  msg="Slice '$feature_name' appears incomplete. Missing expected files:
$list
Convention: each slice has Request, Response, Command, Handler, Validator, Endpoint."

  jq -n --arg ctx "$msg" '{
    hookSpecificOutput: {
      hookEventName: "PostToolUse",
      additionalContext: $ctx
    }
  }'
fi

exit 0
