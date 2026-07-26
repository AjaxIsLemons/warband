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

# PUBLIC DNS MUST EXIST FIRST. This is not a nicety: Caddy asks Let's Encrypt for a cert the moment
# the vhost loads, LE resolves the name from the public internet, and an NXDOMAIN is a HARD failure
# that Caddy then backs off from for a long time. The site answers TLS handshakes with
# "tlsv1 alert internal error" until issuance eventually succeeds — which looks like a broken server,
# not a missing DNS record. This exact thing happened on 2026-07-26.
#
# Local resolution is NOT a valid check: dnsmasq answers *.inhouseboyz.com from split-DNS, so the
# name resolves here while being NXDOMAIN to the rest of the world. Query a public resolver.
echo ">> checking PUBLIC dns for $HOSTNAME_ (local split-DNS would lie here)..."
dns_status=$(curl -fsS -H 'accept: application/dns-json' \
	"https://1.1.1.1/dns-query?name=$HOSTNAME_&type=A" 2>/dev/null \
	| python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("Status",-1), len([a for a in d.get("Answer",[]) if a.get("type")==1]))' 2>/dev/null || echo "-1 0")
read -r rcode acount <<<"$dns_status"
if [ "$rcode" != 0 ] || [ "${acount:-0}" -lt 1 ]; then
	cat >&2 <<MSG
!! $HOSTNAME_ has no public A record (resolver status=$rcode, A records=${acount:-0}).
   Add the DNS record FIRST, wait for it to resolve publicly, then re-run this script.
   Adding the vhost now would make Caddy fail issuance and back off, and the site would serve
   "SSL protocol error" for a while even after DNS is fixed.
   Check with:
     curl -s -H 'accept: application/dns-json' \\
       'https://1.1.1.1/dns-query?name=$HOSTNAME_&type=A' | python3 -m json.tool
MSG
	exit 1
fi
echo ">> public dns OK"

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

# A reload only STARTS certificate issuance. Wait for it, because "reloaded" without a cert is the
# silent failure this script exists to prevent — the operator finds out by getting an SSL error in a
# browser and has no idea Caddy gave up minutes ago.
echo -n ">> waiting for a TLS certificate for $HOSTNAME_"
for _ in $(seq 1 30); do
	if curl -sS -o /dev/null --max-time 4 --resolve "$HOSTNAME_:443:127.0.0.1" \
		"https://$HOSTNAME_/healthz" 2>/dev/null; then
		echo " ✓"
		echo ">> $HOSTNAME_ is serving HTTPS."
		exit 0
	fi
	echo -n "."
	sleep 2
done
echo " ✗"
cat >&2 <<MSG
!! No certificate after 60s. The vhost is loaded but TLS is not working yet.
   The ACME failure, verbatim:
MSG
journalctl -u caddy --since "-3min" --no-pager 2>/dev/null \
	| grep -i "$HOSTNAME_" | grep -iE "challenge failed|could not get certificate|problem" \
	| tail -3 | cut -c1-240 >&2
cat >&2 <<MSG
   Most likely causes: public DNS only just became correct (Caddy backs off after a hard failure —
   re-run 'systemctl reload caddy' to retry immediately), or port 80/443 is not reaching this box
   from the internet, which HTTP-01 and TLS-ALPN-01 both need.
MSG
exit 1
