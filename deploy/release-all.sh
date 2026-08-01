#!/usr/bin/env bash
# One command from homeserv: test → sync → build in the open Windows Editor → publish.
set -euo pipefail

WIN_SSH=${WIN_SSH:-jwjwi@192.168.1.102}
WIN_KEY=${WIN_KEY:-$HOME/.ssh/homeserv_to_windows}
SYNC_TIMEOUT=${SYNC_TIMEOUT:-180}
UNITY_BUILD_TIMEOUT=${UNITY_BUILD_TIMEOUT:-1800}
UNITY_STATUS_WIN=${UNITY_STATUS_WIN:-C:\\Users\\jwjwi\\warband-builds\\build-status.json}
REQUEST_ID="release-$(date -u +%Y%m%dT%H%M%SZ)-$$"
LOCK_OWNER="release:$$"
LOCK_HELD=0
BUILD_ACTIVE=0

cleanup() {
	if [[ "$LOCK_HELD" == 1 ]]; then
		if [[ "$BUILD_ACTIVE" == 1 ]]; then
			# The Editor build is asynchronous and cannot be safely cancelled by killing this
			# shell. Refresh once instead of immediately exposing the Editor to another agent.
			agent-lock acquire unity-warband --owner "$LOCK_OWNER" \
				--note "make release exited while Unity may still be building" >/dev/null || true
			echo "Unity may still be building; its lease was left to expire naturally." >&2
		else
			agent-lock release unity-warband --owner "$LOCK_OWNER" >/dev/null || true
		fi
	fi
}
trap cleanup EXIT

json_value() {
	python3 -c 'import json,sys; print(json.loads(sys.argv[1]).get(sys.argv[2], ""))' "$1" "$2"
}

echo ">> [1/6] testing sim + run"
make --no-print-directory test

echo ">> [2/6] rebuilding Unity plugin DLLs"
make --no-print-directory unity-sim

echo ">> [3/6] waiting for Windows Syncthing peer"
sync_deadline=$((SECONDS + SYNC_TIMEOUT))
synced=0
while (( SECONDS < sync_deadline )); do
	completion_json=$(make --no-print-directory sync-status)
	completion=$(json_value "$completion_json" completion)
	if python3 -c 'import sys; raise SystemExit(0 if float(sys.argv[1]) >= 100 else 1)' "$completion"; then
		echo ">> Windows peer is synced (${completion}%)."
		synced=1
		break
	fi
	echo ">> sync ${completion:-unknown}%..."
	sleep 3
done
if [[ "$synced" != 1 ]]; then
	echo "Windows peer did not reach 100% sync within ${SYNC_TIMEOUT}s." >&2
	exit 1
fi

echo ">> [4/6] acquiring shared Unity lease + queueing Windows build"
agent-lock acquire unity-warband --owner "$LOCK_OWNER" \
	--note "make release: build and publish $REQUEST_ID"
LOCK_HELD=1
WIN_SSH="$WIN_SSH" WIN_KEY="$WIN_KEY" \
	python3 deploy/unity-mcp-build.py queue --request-id "$REQUEST_ID"
BUILD_ACTIVE=1

echo ">> waiting for Unity build $REQUEST_ID"
build_deadline=$((SECONDS + UNITY_BUILD_TIMEOUT))
next_refresh=$((SECONDS + 120))
version=""
while (( SECONDS < build_deadline )); do
	if (( SECONDS >= next_refresh )); then
		agent-lock acquire unity-warband --owner "$LOCK_OWNER" \
			--note "make release: waiting for $REQUEST_ID" >/dev/null
		next_refresh=$((SECONDS + 120))
	fi

	if ! status_json=$(ssh -T -i "$WIN_KEY" -o ConnectTimeout=5 "$WIN_SSH" \
		"if exist $UNITY_STATUS_WIN (type $UNITY_STATUS_WIN) else (echo {})"); then
		echo ">> transient SSH failure while polling Unity; retrying..."
		sleep 5
		continue
	fi
	status_request=$(json_value "$status_json" requestId)
	if [[ "$status_request" != "$REQUEST_ID" ]]; then
		sleep 5
		continue
	fi

	state=$(json_value "$status_json" state)
	case "$state" in
		queued|running)
			echo ">> Unity build: $state"
			;;
		succeeded)
			version=$(json_value "$status_json" version)
			[[ -n "$version" ]] || {
				echo "Unity reported success without a release version." >&2
				exit 1
			}
			echo ">> Unity build succeeded: v$version"
			break
			;;
		failed)
			BUILD_ACTIVE=0
			echo "Unity build failed:" >&2
			json_value "$status_json" message >&2
			exit 1
			;;
	esac
	sleep 5
done
if [[ -z "$version" ]]; then
	echo "Unity build did not finish within ${UNITY_BUILD_TIMEOUT}s." >&2
	exit 1
fi

BUILD_ACTIVE=0
agent-lock release unity-warband --owner "$LOCK_OWNER"
LOCK_HELD=0

echo ">> [5/6] publishing v$version for existing launchers"
make --no-print-directory ship EXPECTED_VERSION="$version"

echo ">> [6/6] published release status"
make --no-print-directory release-status
