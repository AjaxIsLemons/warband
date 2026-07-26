#!/usr/bin/env bash
# Root half of the warband site setup: the releases directory and the Caddy vhost.
# Run once:  sudo bash deploy/setup-warband-site.sh
# Then the user half (no sudo):  bash deploy/install-warband-site.sh
#
# Deliberately small and idempotent. It does not touch DNS: dnsmasq already answers
# *.inhouseboyz.com -> the tailnet address, so a new subdomain needs no DNS work for devices on the
# tailnet (see ~/brain/server/homeserv.md).
set -euo pipefail

OWNER=${OWNER:-jake}
RELEASES_DIR=${RELEASES_DIR:-/srv/warband-releases}
SITE_PORT=${SITE_PORT:-8092}   # 8090 is taken by arena's vite preview (--strictPort)
HOSTNAME_=${HOSTNAME_:-warband.inhouseboyz.com}
CADDYFILE=${CADDYFILE:-/etc/caddy/Caddyfile}

[ "$(id -u)" = 0 ] || { echo "run this with sudo" >&2; exit 1; }

install -d -o "$OWNER" -g "$OWNER" "$RELEASES_DIR"
echo ">> $RELEASES_DIR ready (owner $OWNER)"

if grep -q "$HOSTNAME_" "$CADDYFILE" 2>/dev/null; then
	echo ">> $HOSTNAME_ already present in $CADDYFILE — leaving it alone."
	exit 0
fi

cat >> "$CADDYFILE" <<EOF

# warband — friend playtest downloads. Discord sign-in gates the LAUNCHER only; the release
# manifest and client zip under /releases/ are intentionally open, because the launcher is not a
# browser and carries no session. See site/main.go's package comment.
$HOSTNAME_ {
	reverse_proxy 127.0.0.1:$SITE_PORT
}
EOF
echo ">> appended a $HOSTNAME_ vhost to $CADDYFILE"

if caddy validate --config "$CADDYFILE" >/dev/null 2>&1; then
	systemctl reload caddy
	echo ">> caddy reloaded"
else
	echo ">> WARNING: caddy validate failed — NOT reloading. Fix $CADDYFILE, then: systemctl reload caddy" >&2
	exit 1
fi
