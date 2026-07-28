#!/usr/bin/env bash
set -euo pipefail

repo_root="${1:-.}"
outbox_root="$repo_root/docs/art-reviews/outbox"

if [[ ! -d "$outbox_root" ]]; then
    exit 0
fi

while IFS= read -r -d '' job_file; do
    status="$(sed -n 's/^Status: *//p' "$job_file" | head -n 1 | sed 's/[[:space:]]*$//')"
    if [[ "$status" == "WAITING_FOR_CODEX" ]]; then
        job_name="$(basename -- "$(dirname -- "$job_file")")"
        asset_class="$(sed -n 's/^Asset class: *//p' "$job_file" | head -n 1 | sed 's/[[:space:]]*$//')"
        printf '%s\t%s\t%s\n' "$job_name" "$asset_class" "$job_file"
    fi
done < <(find "$outbox_root" -mindepth 2 -maxdepth 2 -type f -name job.md -print0 | sort -z)
