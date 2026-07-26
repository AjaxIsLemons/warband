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

## The one trap this pipeline exists to close

All six hand-written URP shaders are resolved at runtime by `Shader.Find("Warband/…")` and are
referenced by **no material asset**, so a player build strips every one of them and each
`new Material(null)` silently degrades — the entire combat-spectacle arc renders as nothing, and it
looks like a bug in the FX code rather than a build setting. Verified before the first build:
`GraphicsSettings.asset` held seven always-included shaders and **all seven were Unity built-ins**.

`WarbandBuild.EnsureRuntimeShadersAreIncluded()` therefore registers them itself on every build and
logs anything it had to add. A build step cannot be forgotten; a wiki note can.

## Commands

| Command | Where | What |
|---|---|---|
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

## Content fingerprint is checked at ship time

`make ship` compares the build's `contentVersion` against homeserv's own (`make content-version`)
and **refuses on mismatch**. That almost always means the Windows `Assets/Plugins/Warband/*.dll` are
stale — check `make sync-status`, re-run `make unity-sim`, rebuild. Shipping a mismatch would mean a
save made on one machine refuses to load on the other, and the symptom looks like save corruption
(ADR 0008).

## STILL NEEDS JAKE — the public side

Everything above works locally. Serving it to friends is outward-facing, needs sudo, and is
deliberately left for Jake to apply.

**1. Release directory** (root-owned parent, so this is a sudo step):
```sh
sudo install -d -o jake -g jake /srv/warband-releases
```

**2. Caddy route.** No application server is needed — the launcher resolves the zip URL relative to
its manifest URL, so a plain static directory is enough. Add alongside the existing site blocks:
```caddy
warband.inhouseboyz.com {
    handle /releases/* {
        root * /srv
        file_server browse
    }
    # Optional friend-scale gate. The launcher already sends the header when built with a token,
    # so this can be added later WITHOUT reissuing launchers:
    # @nogate not header X-Warband-Launcher-Token "<token>"
    # respond @nogate 403
}
```
Then `sudo systemctl reload caddy`. DNS: dnsmasq already answers `*.inhouseboyz.com` →
`100.109.185.119` over Tailscale, so a new subdomain needs no DNS work for devices on the tailnet;
public access follows whatever the other subdomains do.

**3. Decide the gate.** Until the header check above is enabled, the manifest and zip are
**unlisted but public**. For friend-scale that may be fine — Shoota's own README calls its token
"friction control, not strong DRM" — but it should be a conscious choice, not a default.

**4. Then:** `WARBAND_LAUNCHER_TOKEN=... make launcher-release` and hand friends the exe.

## Verified so far

- Launcher **end-to-end against a local HTTP server**: cold install → SHA-256 verify → atomic
  install with rollback → version marker → launch (the stand-in exe really ran); second run
  correctly short-circuits to "Up to date" with no download. Cross-compiles to a 6.1 MB Windows
  PE32+ binary.
- `ship-release.sh` `status` mode against a staged manifest; syntax clean.
- **Not yet run:** the Unity build itself, and therefore nothing has been published. The build
  script and preflight are committed and waiting on the editor.
