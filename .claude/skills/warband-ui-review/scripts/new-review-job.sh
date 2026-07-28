#!/usr/bin/env bash
set -euo pipefail

job="${1:-}"
repo_root="${2:-.}"

if [[ ! "$job" =~ ^[a-z0-9][a-z0-9-]{0,62}$ ]]; then
    echo "usage: $0 <lowercase-hyphenated-job> [repo-root]" >&2
    exit 2
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
template="$script_dir/../assets/review-template.md"
review_root="$repo_root/docs/ui-reviews"
inbox_dir="$review_root/inbox/$job"
outbox_dir="$review_root/outbox/$job"
review_file="$outbox_dir/review.md"

mkdir -p "$inbox_dir" "$outbox_dir/samples" "$outbox_dir/work" "$outbox_dir/implementation"

if [[ ! -e "$review_file" ]]; then
    created_date="$(date +%F)"
    sed \
        -e "s/{{JOB}}/$job/g" \
        -e "s/{{DATE}}/$created_date/g" \
        "$template" > "$review_file"
fi

printf 'Inbox: %s\n' "$inbox_dir"
printf 'Outbox: %s\n' "$outbox_dir"
printf 'Review: %s\n' "$review_file"
