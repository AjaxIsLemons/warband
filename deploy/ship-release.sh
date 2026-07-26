#!/usr/bin/env bash
# Publish a manifest-verified warband client release. Copied in shape from Shoota's
# deploy/ship-release.sh (same infra, same laws) and cut down: warband has no server, so this is
# pull → verify → zip → publish, nothing to restart.
#
# The Unity side (`Warband/Build Windows Client`) writes release.json LAST and only on success, so
# "is there a publishable build" is answerable without trusting a synced ProjectSettings.asset —
# and a failed build physically cannot be shipped.
#
# Publishing uses same-filesystem renames throughout, so a launcher polling mid-publish never sees a
# partially copied zip or a manifest pointing at one.
set -euo pipefail

MODE=${1:-ship}
WIN_SSH=${WIN_SSH:-jwjwi@192.168.1.102}
WIN_KEY=${WIN_KEY:-$HOME/.ssh/homeserv_to_windows}
# OUTSIDE the Syncthing tree on purpose — a player build inside the synced repo would sync back to
# homeserv and show up in git status.
WIN_BUILDS=${WIN_BUILDS:-C:/Users/jwjwi/warband-builds}
RELEASES_DIR=${RELEASES_DIR:-/srv/warband-releases}
EXPECTED_VERSION=${EXPECTED_VERSION:-}

case "$MODE" in
	ship|preflight|status) ;;
	*) echo "unknown mode '$MODE' (expected ship, preflight, or status)" >&2; exit 2 ;;
esac

json_field() {
	python3 - "$1" "$2" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as handle:
    value = json.load(handle)
for part in sys.argv[2].split("."):
    value = value[part]
print(str(value).lower() if isinstance(value, bool) else value)
PY
}

if [[ "$MODE" == status ]]; then
	manifest="$RELEASES_DIR/warband-latest-win64.json"
	if [[ ! -f "$manifest" ]]; then
		echo "No release published yet (looked for $manifest)."
		exit 1
	fi
	printf '\n%-20s %s\n' "Published:" "v$(json_field "$manifest" version)"
	printf '%-20s %s\n' "Content version:" "$(json_field "$manifest" contentVersion)"
	printf '%-20s %s\n' "Size:" "$(( $(json_field "$manifest" size) / 1024 / 1024 ))MB"
	printf '%-20s %s\n' "Published at:" "$(json_field "$manifest" publishedAt)"
	printf '%-20s %s\n' "SHA-256:" "$(json_field "$manifest" sha256)"
	exit 0
fi

command -v zip >/dev/null || { echo "zip is not installed on homeserv" >&2; exit 1; }

stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT

echo ">> pulling release manifest + client from $WIN_SSH:$WIN_BUILDS ..."
if ! scp -q -i "$WIN_KEY" -r "$WIN_SSH:$WIN_BUILDS/release.json" "$WIN_SSH:$WIN_BUILDS/WindowsClient" "$stage/"; then
	echo "scp failed — run Unity menu 'Warband/Build Windows Client' on the Windows box first." >&2
	exit 1
fi

manifest="$stage/release.json"
[[ -f "$manifest" ]] || { echo "staged build has no release.json — the build did not succeed" >&2; exit 1; }

version=$(json_field "$manifest" version)
content_version=$(json_field "$manifest" contentVersion)
result=$(json_field "$manifest" client.result)
errors=$(json_field "$manifest" client.errors)
commit=$(json_field "$manifest" commit)
dirty=$(json_field "$manifest" dirty)
exe=$(json_field "$manifest" exe)

[[ -n "$version" ]] || { echo "release manifest has no version" >&2; exit 1; }
[[ "$result" == Succeeded && "$errors" == 0 ]] || {
	echo "manifest is not publishable (result=$result, errors=$errors)" >&2; exit 1; }
[[ -f "$stage/WindowsClient/$exe" ]] || {
	echo "staged client is missing $exe" >&2; exit 1; }
if [[ -n "$EXPECTED_VERSION" && "$version" != "$EXPECTED_VERSION" ]]; then
	echo "refusing stale/wrong build: expected v$EXPECTED_VERSION, Windows manifest is v$version" >&2
	exit 1
fi

# The content fingerprint the client will run must match what homeserv builds, or a save made on one
# refuses to load on the other and the symptom looks like save corruption (ADR 0008).
local_content=""
if command -v dotnet >/dev/null && [[ -d sim/Warband.Sweep ]]; then
	local_content=$(dotnet run --project sim/Warband.Sweep -c Release -- --version 2>/dev/null | tail -1 || true)
fi
if [[ -n "$local_content" && "$local_content" != "$content_version" ]]; then
	echo "WARNING: build content version ($content_version) != homeserv content version ($local_content)."
	echo "         The Windows DLLs are probably stale — check 'make sync-status' and rebuild." >&2
	[[ "$MODE" == preflight ]] || exit 1
fi

echo ">> ready: v$version (content $content_version, $commit, dirty=$dirty)"

if [[ "$MODE" == preflight ]]; then
	echo ">> PREFLIGHT OK v$version — manifest and $exe staged; nothing published."
	exit 0
fi

zip_path="$stage/warband-$version-win64.zip"
( cd "$stage/WindowsClient" && zip -qr "$zip_path" . )

mkdir -p "$RELEASES_DIR"
versioned="$RELEASES_DIR/warband-$version-win64.zip"
latest="$RELEASES_DIR/warband-latest-win64.zip"
launcher_manifest="$RELEASES_DIR/warband-latest-win64.json"

cp "$zip_path" "$versioned.tmp" && mv -f "$versioned.tmp" "$versioned"
cp "$zip_path" "$latest.tmp"    && mv -f "$latest.tmp" "$latest"

sha=$(sha256sum "$latest" | awk '{print $1}')
size=$(stat -c%s "$latest")
published=$(date -u +%Y-%m-%dT%H:%M:%SZ)
python3 - "$launcher_manifest.tmp" <<PY
import json, sys
json.dump({
    "version": "$version",
    "contentVersion": "$content_version",
    "channel": "win64",
    "file": "warband-latest-win64.zip",
    "sha256": "$sha",
    "size": $size,
    "exe": "$exe",
    "commit": "$commit",
    "publishedAt": "$published",
}, open(sys.argv[1], "w"), indent=2)
PY
mv -f "$launcher_manifest.tmp" "$launcher_manifest"
cp "$manifest" "$RELEASES_DIR/warband-$version-build.json.tmp"
mv -f "$RELEASES_DIR/warband-$version-build.json.tmp" "$RELEASES_DIR/warband-$version-build.json"

printf '\nSHIPPED v%s (content %s, %sMB)\n' "$version" "$content_version" "$(( size / 1024 / 1024 ))"
echo "  $latest"
echo "  $launcher_manifest"

# "SHIPPED" must mean the LAUNCHER can see it, not merely that files landed in a directory. On
# 2026-07-26 a release was published into a staging dir while RELEASES_DIR on the site pointed
# elsewhere; every local check passed and the launcher still died with "manifest returned HTTP 404".
# A file on disk that the site does not serve is not a release.
manifest_url=${MANIFEST_URL:-https://warband.inhouseboyz.com/releases/warband-latest-win64.json}
served=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 10 "$manifest_url" 2>/dev/null || echo 000)
if [[ "$served" == 200 ]]; then
	served_version=$(curl -sS --max-time 10 "$manifest_url" 2>/dev/null \
		| python3 -c 'import json,sys; print(json.load(sys.stdin).get("version",""))' 2>/dev/null || true)
	if [[ "$served_version" == "$version" ]]; then
		echo "  site serves v$served_version — the launcher will see this release."
	else
		echo "!! the site serves v$served_version, not the v$version just published." >&2
		echo "   WARBAND_RELEASES_DIR in ~/.config/warband-site/env probably != $RELEASES_DIR." >&2
		exit 1
	fi
else
	echo "!! published, but $manifest_url returns HTTP $served — the launcher cannot see it." >&2
	echo "   Check: systemctl --user status warband-site, and WARBAND_RELEASES_DIR vs $RELEASES_DIR." >&2
	exit 1
fi
