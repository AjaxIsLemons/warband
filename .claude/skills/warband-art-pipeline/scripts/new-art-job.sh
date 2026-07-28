#!/usr/bin/env bash
set -euo pipefail

job="${1:-}"
asset_class="${2:-}"
repo_root="${3:-.}"

if [[ ! "$job" =~ ^[a-z0-9][a-z0-9-]{0,62}$ ]]; then
    echo "usage: $0 <lowercase-hyphenated-job> <asset-class> [repo-root]" >&2
    exit 2
fi

case "$asset_class" in
    portrait|icon|weapon-art|vfx-concept|vfx-source|material-texture|environment-concept)
        ;;
    *)
        echo "unsupported asset class: $asset_class" >&2
        exit 2
        ;;
esac

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
template="$script_dir/../assets/job-template.md"
review_root="$repo_root/docs/art-reviews"
inbox_dir="$review_root/inbox/$job"
outbox_dir="$review_root/outbox/$job"
job_file="$outbox_dir/job.md"

mkdir -p \
    "$inbox_dir" \
    "$outbox_dir/prompts" \
    "$outbox_dir/candidates" \
    "$outbox_dir/proofs" \
    "$outbox_dir/processed" \
    "$outbox_dir/implementation"

if [[ ! -e "$job_file" ]]; then
    created_date="$(date +%F)"
    sed \
        -e "s/{{JOB}}/$job/g" \
        -e "s/{{CLASS}}/$asset_class/g" \
        -e "s/{{DATE}}/$created_date/g" \
        "$template" > "$job_file"
fi

printf 'Inbox: %s\n' "$inbox_dir"
printf 'Outbox: %s\n' "$outbox_dir"
printf 'Job: %s\n' "$job_file"
