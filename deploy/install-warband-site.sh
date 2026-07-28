#!/usr/bin/env bash
# User half of the warband site setup — no sudo needed, idempotent. Installs the env skeleton
# (first run only, with a generated cookie secret), the systemd --user unit, builds the binary, and
# starts the service. Pair with `sudo bash deploy/setup-warband-site.sh` for the Caddy vhost and the
# releases directory.
#
# Same shape as Shoota's install-shoota-site.sh. Secrets live ONLY in ~/.config/warband-site/env —
# never in the repo, never in the Syncthing tree.
set -euo pipefail
cd "$(dirname "$0")/.."

mkdir -p ~/.config/warband-site ~/.local/bin ~/.config/systemd/user

if [ ! -f ~/.config/warband-site/env ]; then
	cat > ~/.config/warband-site/env <<EOF
# warband-site secrets — chmod 600, never committed or synced.
#
# EITHER a new Discord app (recommended) at https://discord.com/developers/applications:
#   new application "warband" -> OAuth2 -> copy Client ID + Client Secret below.
#
# OR reuse the existing Shoota app. That works — a Discord app may hold several redirect URIs —
# but it does NOT save the portal trip (the redirect URI must still be registered), friends see
# "Shoota" on the consent screen, and the two sites then share one secret, so rotating it breaks
# both. To reuse, without the secret passing through an agent's context:
#   sed -n 's/^SHOOTA_DISCORD_CLIENT_\(ID\|SECRET\)=/WARBAND_DISCORD_CLIENT_\1=/p' ~/.config/shoota-site/env >> ~/.config/warband-site/env
#   ...then delete the two empty WARBAND_DISCORD_* lines below.
#
# EITHER WAY, the Discord app's redirect list must contain EXACTLY:
#   https://warband.inhouseboyz.com/auth/discord/callback
# Any signed-in Discord account may download the launcher; there is no allowlist by design.
WARBAND_DISCORD_CLIENT_ID=
WARBAND_DISCORD_CLIENT_SECRET=
WARBAND_COOKIE_SECRET=$(head -c 32 /dev/urandom | base64)
WARBAND_ADDR=127.0.0.1:8092
WARBAND_BASE_URL=https://warband.inhouseboyz.com
WARBAND_RELEASES_DIR=/srv/warband-releases

# Dev surfaces (/sfx, the SFX audition sheet) are ADMIN-ONLY and FAIL CLOSED: leave this empty and
# /sfx 404s for everyone, including you. It is a separate gate from the launcher on purpose —
# any signed-in Discord account may download the launcher, so "signed in" would show every friend
# the work-in-progress audio. Comma-separated Discord user ids.
# To reuse the id already configured for Shoota, without it passing through an agent's context:
#   sed -n 's/^SHOOTA_ADMIN_IDS=/WARBAND_ADMIN_IDS=/p' ~/.config/shoota-site/env >> ~/.config/warband-site/env
WARBAND_ADMIN_IDS=
# Where \`make sfx\` writes the audition sheet. Served read-only, admin-gated.
WARBAND_SFX_DIR=$HOME/Work/warband/docs/audio
EOF
	chmod 600 ~/.config/warband-site/env
	echo ">> created ~/.config/warband-site/env — fill in the two Discord values, then re-run."
fi

install -m 644 deploy/warband-site.service ~/.config/systemd/user/warband-site.service

echo ">> building warband-site..."
go -C site build -trimpath -o "$HOME/.local/bin/warband-site" .

systemctl --user daemon-reload
systemctl --user enable warband-site
systemctl --user restart warband-site

sleep 1
if curl -fsS "http://$(grep -E '^WARBAND_ADDR=' ~/.config/warband-site/env | cut -d= -f2- | tr -d '"')/healthz" >/dev/null 2>&1; then
	echo ">> warband-site is up."
else
	echo ">> WARNING: healthz did not answer. Recent logs:" >&2
	journalctl --user -u warband-site -n 20 --no-pager >&2
	exit 1
fi
