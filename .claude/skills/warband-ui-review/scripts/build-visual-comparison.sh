#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 <approved-reference.png> <unity-capture.png> <comparison-output.png>" >&2
}

if [[ $# -ne 3 ]]; then
  usage
  exit 2
fi

approved=$1
actual=$2
output=$3

for input in "$approved" "$actual"; do
  if [[ ! -f "$input" ]]; then
    echo "Missing input: $input" >&2
    exit 1
  fi
done

if ! command -v magick >/dev/null 2>&1 || ! command -v identify >/dev/null 2>&1; then
  echo "ImageMagick commands 'magick' and 'identify' are required." >&2
  exit 1
fi

approved_size=$(identify -format '%wx%h' "$approved")
actual_size=$(identify -format '%wx%h' "$actual")
if [[ "$approved_size" != "$actual_size" ]]; then
  echo "Dimension mismatch: approved=$approved_size unity=$actual_size" >&2
  echo "Capture the same physical viewport and crop before comparing." >&2
  exit 1
fi

output_dir=$(dirname "$output")
mkdir -p "$output_dir"

comparison_tmp=$(mktemp -d)
trap 'rm -rf "$comparison_tmp"' EXIT

magick "$approved" "$actual" \
  -define compose:args=50 -compose blend -composite \
  "$comparison_tmp/overlay.png"

magick "$approved" "$actual" \
  -compose difference -composite -auto-level \
  "$comparison_tmp/difference.png"

magick montage \
  \( "$approved" -set label "APPROVED REFERENCE ($approved_size)" \) \
  \( "$actual" -set label "UNITY CAPTURE ($actual_size)" \) \
  \( "$comparison_tmp/overlay.png" -set label "50% OVERLAY" \) \
  \( "$comparison_tmp/difference.png" -set label "ABSOLUTE DIFFERENCE (AUTO-LEVEL)" \) \
  -tile 2x2 -geometry +16+28 -background '#111111' -fill '#f2eadb' \
  "$output"

echo "Wrote comparison: $output"
