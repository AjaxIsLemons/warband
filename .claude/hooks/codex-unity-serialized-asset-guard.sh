#!/usr/bin/env bash
# Block Codex apply_patch edits to Unity-managed serialized files.
set -euo pipefail

if [ "${DISABLE_WARBAND_UNITY_HOOKS:-}" = "1" ]; then
    exit 0
fi

ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [ -z "$ROOT" ] || [ ! -f "$ROOT/CLAUDE.md" ] || ! grep -qi '^# warband' "$ROOT/CLAUDE.md"; then
    exit 0
fi

INPUT="$(cat)"

block() {
    local path="$1"
    local reason="$2"
    jq -n --arg path "$path" --arg reason "$reason" '{
      hookSpecificOutput: {
        hookEventName: "PreToolUse",
        permissionDecision: "deny",
        permissionDecisionReason: ($path + ": " + $reason + " Use Unity MCP editor C# or an Editor script and let Unity save the asset.")
      }
    }'
    exit 0
}

check_path() {
    local path="$1"
    [ -z "$path" ] && return
    [ "$path" = "/dev/null" ] && return
    path="${path#./}"
    path="${path#\"}"
    path="${path%\"}"

    case "$path" in
        *.meta)
            block "$path" ".meta GUID/reference data is Unity-managed."
            ;;
        *.unity|*.prefab)
            block "$path" "scene and prefab YAML is Unity-managed."
            ;;
        *.asset)
            case "$path" in
                */Assets/Scripts/*|*/Assets/Editor/*|*/Assets/Plugins/*|Assets/Scripts/*|Assets/Editor/*|Assets/Plugins/*)
                    ;;
                *)
                    block "$path" "serialized .asset data is Unity-managed."
                    ;;
            esac
            ;;
    esac
}

# Support file-oriented editor tools if Codex exposes one in addition to apply_patch.
FILE_PATH="$(printf '%s' "$INPUT" | jq -r '.tool_input.file_path // empty')"
check_path "$FILE_PATH"

# apply_patch carries target paths in patch headers. Inspect headers only so source text that merely
# mentions a serialized extension does not cause a false block.
PATCH="$(printf '%s' "$INPUT" | jq -r '.tool_input.command // .tool_input.patch // .tool_input.input // empty')"
if [ -z "$PATCH" ]; then
    exit 0
fi

while IFS= read -r path; do
    check_path "$path"
done < <(
    printf '%s\n' "$PATCH" | sed -nE \
        -e 's#^\*\*\* (Update|Add|Delete) File: (.+)$#\2#p' \
        -e 's#^\*\*\* Move to: (.+)$#\1#p' \
        -e 's#^\+\+\+ (a/|b/)?(.+)$#\2#p'
)
