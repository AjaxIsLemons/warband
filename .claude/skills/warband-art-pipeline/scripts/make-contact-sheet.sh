#!/usr/bin/env bash
set -euo pipefail

output="${1:-}"
shift || true

if [[ -z "$output" || "$#" -eq 0 ]]; then
    echo "usage: $0 <output.png> <image> [image ...]" >&2
    exit 2
fi

if ! command -v magick >/dev/null 2>&1; then
    echo "ImageMagick 'magick' is required" >&2
    exit 1
fi

for image in "$@"; do
    if [[ ! -f "$image" ]]; then
        echo "missing image: $image" >&2
        exit 1
    fi
done

mkdir -p "$(dirname -- "$output")"

magick montage "$@" \
    -auto-orient \
    -thumbnail '512x512>' \
    -background '#070B11' \
    -fill '#ECE8DF' \
    -stroke none \
    -font /usr/share/fonts/noto/NotoSans-Regular.ttf \
    -pointsize 18 \
    -set label '%t' \
    -tile '3x' \
    -geometry '512x512+24+48' \
    "$output"

printf 'Contact sheet: %s\n' "$output"
