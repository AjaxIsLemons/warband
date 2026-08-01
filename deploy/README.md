# warband delivery — build, publish, launch

Roadmap item 8. Copied in shape from Shoota's pipeline (`~/Work/Shoota/Shoota/deploy/`,
`launcher/`) because that infra is proven and already running on this box. warband has **no
server**, so this is strictly: build on Windows → publish to homeserv → friends run a launcher.

```
Unity (Windows)                    homeserv                        friend's PC
─────────────────                  ────────                        ───────────
Warband/Build Windows Client   →   make ship                   →   WarbandLauncher.exe
  ~/warband-builds/WindowsClient     scp, verify, zip                reads manifest
  ~/warband-builds/release.json      publish atomically              downloads if newer
                                     /srv/warband-releases           verifies SHA-256
                                                                     installs + launches
```

## Why builds live outside the repo

`~/warband-builds/` on the Windows box, **not** `client/Builds/`. A few-hundred-MB player build
inside the Syncthing tree would sync straight back to homeserv and land in `git status`. Same rule
the render captures already follow.

## The shader traps this pipeline exists to close

Runtime-created board materials use URP Lit/Unlit plus six hand-written Warband shaders. They are
not reliably reached through serialized material assets, so an Editor play pass can be perfect while
a player build strips them. The first preflight caught the six Warband shaders. The first real player
pass proved URP Unlit was also absent: every legacy tracer/burst threw `new Material(null)`.

`WarbandBuild.EnsureRuntimeShadersAreIncluded()` therefore registers them itself on every build and
logs anything it had to add. A build step cannot be forgotten; a wiki note can.

There is a second, independent player-only trap. In Unity 6000.3, `GameObject.CreatePrimitive` can
start with `Hidden/InternalErrorShader` in a player (UUM-136536). Cloning the primitive's default
material therefore deliberately propagates pink to the board, HP bars, and Mana bars even when URP
Lit is included. `ReplayPlayer` ignores that default and explicitly creates its replacement material
from the registered `Universal Render Pipeline/Lit` shader.

## Commands

| Command | Where | What |
|---|---|---|
| `make release` | homeserv | **All in one:** tests, rebuilds sim DLLs, waits for sync, builds in the open Windows Editor, publishes, verifies the public launcher manifest. |
| `Warband/Build Preflight` | Unity menu | Cheap pre-check: scenes, shader stripping, StreamingAssets, content version. Seconds, not minutes. |
| `Warband/Build Windows Client` | Unity menu | Registers shaders, builds, writes `release.json` **last and only on success**. |
| `make ship-preflight` | homeserv | Pull + verify + stage. Publishes nothing. |
| `make ship` | homeserv | Publish zip + launcher manifest atomically. |
| `make release-status` | homeserv | What is currently published. |
| `make content-version` | homeserv | The content fingerprint, to compare against a build's manifest. |
| `make launcher-release` | homeserv | Cross-compile `WarbandLauncher.exe`. |

`release.json` is written last, so "is there a publishable build" is answerable without trusting a
synced `ProjectSettings.asset` — and **a failed build cannot be shipped**, because no manifest exists
to ship. `make ship EXPECTED_VERSION=0.1.x` refuses a stale one.

Publishing uses same-filesystem renames throughout, so a launcher polling mid-publish never sees a
half-copied zip or a manifest pointing at one.

### One-command release

From the repository root on homeserv:

```sh
make release
```

This deliberately drives the **already-open** Warband Editor through its MCP relay. It does not
launch a competing batch-mode Unity against the same `Library/`. The command takes the
`unity-warband` lease, refuses Play Mode or a busy Editor, writes a request-scoped build status
outside the synced repository, and publishes only after that exact request succeeds. Useful timeout
overrides are `SYNC_TIMEOUT=300 make release` and `UNITY_BUILD_TIMEOUT=2400 make release`.

## Content fingerprint is checked at ship time

`make ship` compares the build's `contentVersion` against homeserv's own (`make content-version`)
and **refuses on mismatch**. That almost always means the Windows `Assets/Plugins/Warband/*.dll` are
stale — check `make sync-status`, re-run `make unity-sim`, rebuild. Shipping a mismatch would mean a
save made on one machine refuses to load on the other, and the symptom looks like save corruption
(ADR 0008).

## Access: Discord sign-in gates the LAUNCHER, nothing else

**Jake's call, 2026-07-26:** *"no gate needed since this doesn't really need a server. I think if
someone signs in with discord, they can dl the launcher."*

| Path | Gate | Why |
|---|---|---|
| `/` | open | landing page |
| `/launcher` | **Discord session** | a human, in a browser, once |
| `/releases/*` | **open** | the launcher is not a browser and carries no session |

The layering is the whole point. Gating `/releases` would mean embedding a secret in every launcher
binary to un-gate it — which anyone can read straight back out of the exe. Shoota reached the same
conclusion the hard way; its launcher README notes the update zip is deliberately *no longer* tied
to a browser Discord session. Gating the exe download is honest about what it buys: friend-scale
friction, not DRM. Any signed-in Discord account works; there is no allowlist by design.

`site/main.go` is one file, no database, no accounts, no telemetry, no admin — Shoota's session and
OAuth scheme (HMAC-signed cookie, state-cookie CSRF, `identify` scope only) with everything else cut.

## Setting the site up

```sh
sudo bash deploy/setup-warband-site.sh     # /srv/warband-releases + the Caddy vhost
bash deploy/install-warband-site.sh        # env skeleton, systemd --user unit, build, start
# fill the two Discord values it prints the path to, then:
bash deploy/install-warband-site.sh        # idempotent; re-run to pick them up
make launcher-release                      # publish WarbandLauncher.exe
```

**Jake still owns two things:**
1. Running those two scripts (one needs sudo).
2. Creating the Discord OAuth app at <https://discord.com/developers/applications> — application
   "warband" → OAuth2 → **Redirect URI must be exactly**
   `https://warband.inhouseboyz.com/auth/discord/callback` — and pasting the client id/secret into
   `~/.config/warband-site/env` (chmod 600, never committed or synced).

## Verified so far

- Launcher **end-to-end against a local HTTP server**: cold install → SHA-256 verify → atomic
  install with rollback → version marker → launch (the stand-in exe really ran); second run
  correctly short-circuits to "Up to date" with no download. Cross-compiles to a 6.1 MB Windows
  PE32+ binary.
- `ship-release.sh` `status` mode against a staged manifest; syntax clean.
- **The first build, for real:** preflight caught all six shaders as strippable → the build
  registered them → **all six are physically present in the shipped `Warband_Data`**, along with
  StreamingAssets (`tuning.json`, `tuning.ranges.json`, 10 replay fixtures). v0.1.260726.1352,
  162 MB, 0 errors, published as a 58 MB zip.
- **The launcher against the REAL published build, through the live site at
  `https://warband.inhouseboyz.com`:** downloaded 58 MB, verified SHA-256, installed 157 MB to the
  client dir, and failed only at `exec` — a Windows PE on Linux, which is the correct failure and
  proves every step before it. Second run short-circuits to "Up to date" with no download.

  The first pass at this ran against a **staging** releases dir (`/srv/warband-releases` did not exist
  yet), so the pipeline was proven while nothing was actually downloadable and the launcher died with
  `manifest returned HTTP 404`. `make ship` now verifies the **public** manifest URL after publishing
  and fails if the site does not serve the version just shipped.
- **The gate:** signed out, `/` offers sign-in and `/launcher` 302s to Discord; `/releases/*` serves
  200. Signed in, `/` offers the download and `/launcher` returns the exe byte-identical to the
  published one. Tampered and expired session cookies are both rejected.
- **Player verification:** the first real player pass booted through the menu and reached a fight,
  exposing the runtime shader failures described above. Build `0.1.260726.1658` proved that merely
  including URP Lit/Unlit did not repair the pink primitives; build `0.1.260726.1706` now replaces
  their `InternalErrorShader` explicitly, succeeded with 0 errors, and passes preflight. It still
  needs one visual rerun before publishing. The Discord round-trip also remains unverified (needs
  the OAuth app and credentials).
