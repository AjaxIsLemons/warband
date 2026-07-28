# Audio — two systems, one substrate

**2026-07-27 research + plan pass.** Audio ships **muted** (`HubPresentation.json → audio.enabled:
false`) because the UI stings were bad. This page root-causes *why* they were bad (measured, not
guessed), states the law for the two audio systems, specifies the runtime and the missing tooling,
and ends with Jake's decisions.

Related: [[sim-render-audit]] headline **F** ("turn the audio channel on") · [[hall-polish]] §5
(audio as an optional sink) · [[combat-spectacle]] §2 (the windup beat) · roadmap item 9 (options).

---

## 1. Status — what exists

Two independent audio paths, both wired end to end, neither designed.

| | UI / Hall | Board / replay |
|---|---|---|
| player | `UiAudioDirector` (`UiFeedbackOutputs.cs`) | `ReplayPlayer.PlaySfx` |
| voices | 4 round-robin `AudioSource` + 1 ambience loop | **1** lazily-created `AudioSource` |
| dispatch | `Family()` maps cue/transaction → 10 families | tell row `sound` / `critSound` / `castSound` |
| assets | 18 clips in `Resources/UI/SFX` | 17 clips in `Resources/Board/SFX` |
| routing | none | none |
| limiting | 45 ms hover cooldown | **none** |
| mixer | **no `AudioMixer` asset exists in the project** | same |

Both have a real virtue worth keeping: **a missing clip is a silent no-op**, so authoring can lead
audio. That property must survive every change below.

---

## 2. Why the UI sounds are bad — measured

Full analysis of all 35 clips (onset = first sample above −34 dB rel. peak; audible = onset→last
such sample; crest = peak-to-RMS over the audible span):

| clip | onset | audible | total | peak | crest |
|---|---|---|---|---|---|
| `route_1` | 24 ms | **1.001 s** | 1.04 s | −10.9 dB | 15.9 dB |
| `bind_2` | 24 ms | **0.945 s** | 1.04 s | −7.0 dB | 17.3 dB |
| `error_1` | 25 ms | **0.929 s** | 1.04 s | **−20.7 dB** | 13.7 dB |
| `purchase_2` | 29 ms | 0.920 s | 1.04 s | −6.5 dB | 17.8 dB |
| `preview_1` | 42 ms | 0.900 s | 1.04 s | −1.4 dB | **32.9 dB** |
| `major_1` | 8 ms | 0.866 s | 1.04 s | −7.8 dB | 18.8 dB |
| `deal_2` | 25 ms | 0.788 s | 1.04 s | **−0.0 dB** | 21.7 dB |
| `deal_1` | 15 ms | 0.742 s | 1.04 s | −7.3 dB | 15.4 dB |
| `bind_1` | 13 ms | 0.718 s | 1.04 s | −11.9 dB | 20.9 dB |
| `seat_2` | 11 ms | 0.665 s | 1.04 s | **−0.0 dB** | 15.2 dB |
| `commit_1` | **157 ms** | 0.601 s | 1.04 s | −1.1 dB | 26.0 dB |
| `purchase_1` | 14 ms | 0.583 s | 1.04 s | −2.3 dB | 14.8 dB |
| `route_2` | 25 ms | 0.545 s | 1.04 s | −3.4 dB | 17.1 dB |
| `seat_1` | 72 ms | 0.282 s | 1.04 s | **−0.0 dB** | 23.1 dB |
| `select_1` | 14 ms | 0.211 s | 1.04 s | −5.3 dB | 22.8 dB |
| `preview_2` | 36 ms | 0.102 s | 1.04 s | −0.2 dB | 19.6 dB |
| `select_2` | 57 ms | 0.049 s | 1.04 s | −1.7 dB | 17.9 dB |

**It is three defects, not one.**

**① They are 5–20× too long.** A UI click is 40–120 ms. Fourteen of eighteen clips carry between
0.5 s and 1.0 s of *continuous audible content*. `route_1` is a full second of sound for moving a
resource — that is a whoosh, not a cue. Two clips (`select_2` 49 ms, `preview_2` 102 ms) are the
right length, and they are the two that would have sounded fine.

**② The level spread is 20 dB.** `error_1` peaks at −20.7 dBFS; `deal_2`, `seat_1` and `seat_2` peak
at −0.0 dBFS. The most important cue in the set (the error) is the quietest thing in it, and seating
a unit is the loudest. **This alone makes a set feel amateur** even at correct lengths — the
listener reads inconsistent loudness as inconsistent meaning.

**③ There is no shared sonic identity.** Crest factor runs 13.7 → 32.9 dB across a set that is
supposed to be one instrument. `preview_1` is a spike in a long quiet tail; `deal_1` is a sustained
tone. These were generated one prompt at a time with no family constraint, so nothing binds them.

Plus one clip-level nit: **stereo 44.1 kHz, 180 KB each, for a mono tick**, and every one is padded
to exactly 1.04 s (the generator's fixed output length, not a design choice).

**Root cause is process, not taste.** [[hall-polish]] §"all 18 authored audio clips passed
structural validation" — the gate checked *that files existed and imported*. Nothing measured onset
latency, duration, level, or crest, which are the four numbers that decide whether a UI sound feels
good. **There is no reason to expect a regenerated batch to be better than this one until that gate
exists.** See §6.

### 2.1 Two policy defects, independent of the assets

- **Hover makes noise.** `Family()` routes `Cue.Preview`, `TooltipReveal` and `TooltipDismiss` into
  the `preview` family, gated only by a 45 ms cooldown. Jake's call (clicks only) deletes that
  branch outright — and with it the two worst-behaved clips in the set.
- **There is an unrequested ambience bed.** `hall_ambience` is an 8 s loop running at
  `0.72 × 0.18` under the whole Hall, with a duck envelope that pulls it down on commits. A drone
  under a menu is a fast way to make a UI feel cheap. Decision D1 below.

---

## 3. Why board audio would be worse

**The density is measured** — `make sfx-density`, over every committed replay fixture. An "onset"
is an event whose signature reaches a tell row carrying `sound`/`castSound`; the per-weapon and
per-ability rows only decide *which* clip plays, never *whether* one does, so they cannot change the
count. `minAmount` gating can only reduce it, so these are honest upper bounds.

| fixture | events | play s | onsets | **/sec** | voices now | voices baked |
|---|---|---|---|---|---|---|
| **overtime** | 5962 | **216.6 s** | 2070 | **9.6/s** | **7.4** | 4.9 |
| castfest | 612 | 16.0 s | 128 | 8.0/s | 6.2 | 4.1 |
| stomp | 232 | 8.2 s | 66 | 8.0/s | 6.2 | 4.1 |
| glyphwar | 679 | 17.4 s | 138 | 7.9/s | 6.1 | 4.1 |
| skirmish | 528 | 15.4 s | 121 | 7.9/s | 6.1 | 4.0 |
| weaponry | 462 | 20.0 s | 103 | 5.2/s | 4.0 | 2.6 |
| boss-ashfall-battery | 650 | 28.0 s | 114 | 4.1/s | 3.1 | 2.1 |
| boss-waning-crown | 763 | 40.0 s | 162 | 4.0/s | 3.1 | 2.1 |
| statusstorm | 500 | 32.2 s | 130 | 4.0/s | 3.1 | 2.1 |
| wallfort | 203 | 20.0 s | 52 | 2.6/s | 2.0 | 1.3 |
| duel-crit-vs-tank | 160 | 23.0 s | 51 | 2.2/s | 1.7 | 1.1 |

(All eleven fixtures measured. `weaponry` needed a second run — the sim was mid-edit in `Battle.cs`
from a parallel session during the first pass.)

**The worst case is not the fixture the audit named.** `castfest` is the densest *burst*, but
**`overtime` is worse on both axes** — 9.6 onsets/s sustained for **3.6 minutes**. Peak density is
survivable; three and a half minutes of eight overlapping voices is what actually exhausts a
listener. THE WANING (roadmap item 11) is therefore the fixture any board-audio work must be judged
against, not `castfest`.

**Voices** = onsets/s × mean audible board-clip length (shipped **771 ms**; after the §6 bake,
**579 ms** — 514 ms before §6.4a raised the riser caps). At 32 real voices the pool is never the
binding constraint — **the mix is**.

And the playback path is:

```csharp
_audio.PlayOneShot(clip, 0.85f);   // one AudioSource, created once, forever
```

`PlayOneShot` layers, so this is **unbounded overlap at a single priority and a single volume**.
`AudioSource.priority` is never set (defaults 128). Past 32 real voices Unity culls **by
audibility** — meaning a `death` sting loses to four Burn ticks that happen to be louder. There is
no design in that decision at all.

Three more concrete gaps:

- **27 sound ids are referenced by tell rows; 17 clips exist.** The entire per-weapon impact layer
  is authored and mute: `hit_axe · hit_censer · hit_dagger · hit_mace · hit_musket · hit_pike ·
  hit_sabre · hit_shield · hit_staff · hit_standard`.
- **The riser channel is late.** `riser_cleric`'s content does not start until **156 ms** in, and
  risers run 0.49–1.24 s. A windup cue that arrives after the windup is worse than no cue — and the
  windup-ahead-of-the-cast law is the one audio rule [[sim-render-audit]] cites Riot as *requiring*.
- **`cast_fire` has a crest factor of 9.0 dB** — fully compressed, no transient. It will not cut
  through eight other voices no matter how loud it is.

### 3.1 The headline: length beats voice management

Length is the only lever that moves every fixture at once. The §6 bake — which is pure processing,
no new authoring — already takes the worst case from **7.4 concurrent voices to 5.5**, and that
figure is dragged up by the eight risers, which are legitimately long (§6.4a) and are only ~15% of
onsets.
Bring impacts to the contract's 250 ms and the dominant class lands near **2**.

**The cheapest fix for combat audio chaos is short clips.** Everything in §5.3 is the safety net for
the peaks, not the primary mechanism — at 32 real voices, warband was never going to run out of
pool. It was going to run out of *attention*.

---

## 4. Research — what the field actually does

**Riot / TFT.** Sound designer Sandy Zhou's framing is the one to steal: *"In a teamfight, sound is
a competitive interface. If the mix doesn't protect what players must track, it doesn't matter how
beautiful the ambience is."* Her test for whether an adaptive mix is real: **you must be able to
state what stands out when density rises, what steps back, and what the transition curve is.** If
you can't state all three, it won't hold up in play. §5.3 states all three explicitly.

Note the precedent in TFT's *visual* solution too — Riot authored a **second, slower animation set**
because League timings were unparseable in a 9v9 ([[sim-render-audit]] §4). The audio analogue is
not "mix 21 events better", it is **do not sound every event**.

**Wwise / FMOD.** The middleware vocabulary is worth copying without the middleware:
*playback limit* (max concurrent instances per sound and per bus), *priority* (who survives when the
limit is hit), *virtual voice behaviour* (kill / virtualise / keep playing), and *discard oldest vs
discard newest at equal priority*. Those four primitives are ~150 lines of C# and cover everything
warband needs. **Adopting FMOD or Wwise is not proposed** — warband has no 3D audio, no music
system, no adaptive score, and a hard determinism contract; the middleware would be all cost.

**Unity 6 native.** Real/virtual voice limits are project settings (warband is at Unity's defaults:
**32 real / 512 virtual**). `AudioSource.priority` is 0–255, 0 = highest, ties broken by audibility.
`AudioMixer` groups give buses, exposed dB params and snapshots.
**`AudioRandomContainer`** (new in Unity 6) does per-play clip/volume/pitch randomisation with
avoid-repeat — genuinely good, but it is an **editor-authored asset per family**, which fights
warband's JSON-first, hot-reloadable `tuning.json` culture. **Recommendation: skip it**, and keep
the `id` + `_1.._n` variant convention that both warband and Shoota already use. One code path.

**Shoota is the strongest reference, and it is ours.** `Assets/Scripts/Audio/SfxPlayer.cs` is
already the correct architecture: a fixed pool of 32 pooled voices (no per-sound GameObject churn),
category → mixer-group routing, a priority ladder with voice stealing, a **same-clip cap**
(3 instances in a 60 ms window, then *boost the newest instead of adding a voice* — "bigger, not
more"), a sidechain-style ducker with attack/hold/release, and graceful degradation when the mixer
asset is missing. Warband needs a **subset** of it — drop the 3D distance low-pass, occlusion
raycasts, and the ally/enemy `SfxRoles` split, all of which are FPS-specific.

**Guildrun** — the direct competitor, demo public since 16 July 2026, 93% positive over 1,078
reviews — is being praised specifically for *"audio feedback on crits, heals, and relic procs"*
([[sim-render-audit]] §4.7). Note the list: **three event classes, all decisive**. Not every hit.
Not every status. That is the shape of the answer, from the one shipping game aimed at this exact
problem.

---

## 5. The plan

### 5.0 One substrate, two policies

Jake's instinct that UI and board audio are "totally different" is right about **policy** and wrong
about **plumbing**. They compete for the same 32 real voices and the same ears, and the board must
be duckable by a UI commit. So: **one `SfxPlayer` + one `GameMixer`, two policy layers on top.**

```
GameMixer
├─ UI ................. clicks, commits, results        (never ducked)
└─ Board
   ├─ Decisive ........ death, crit, cheat-death, boss   (never ducked, never stolen)
   ├─ Cast ............ risers + cast bodies
   ├─ Impact .......... hits
   └─ State ........... statuses, shields, fields
```

`Decisive` sits outside the ducked group, exactly as Shoota's `Feedback` bus sits outside `Combat`.
Four exposed dB params (`MasterVol`, `UiVol`, `BoardVol`, `BoardDuck`) — the first three become
roadmap item 9's sliders, the fourth is driven by the ducker.

### 5.1 UI audio law

1. **Clicks only.** Sound is confirmation that an *action was taken*. Hover, focus, tooltip and
   preview are silent — delete the `preview` family and its branch.
2. **Onset at sample 0.** Non-negotiable. A cue that arrives 157 ms after the click reads as lag.
3. **Short.** `tick` ≤ 80 ms · `commit` ≤ 180 ms · `major` ≤ 500 ms · `error` ≤ 250 ms.
4. **One instrument.** All families share a material and a level target (see §6 contract), so the
   set is recognisable as one voice.
5. **Six families, not ten.** Today's ten (`preview select route deal purchase seat bind commit
   major error`) collapse to:

   | family | fires on | length | notes |
   |---|---|---|---|
   | `tick` | select, tab, pin, toggle, socket wake | ≤ 80 ms | 3 variants, the workhorse |
   | `commit` | confirm, purchase, seat, equip, route | ≤ 180 ms | 2 variants |
   | `bind` | rank buy, inscription bind | ≤ 400 ms | the one ceremony sound |
   | `major` | result, reward, rank-up | ≤ 500 ms | ducks Board 4 dB |
   | `error` | rejected action | ≤ 250 ms | never stolen |
   | `deal` | reroll, reveal, drawer | ≤ 200 ms | 2 variants |

6. **Limit:** `tick` caps at 2 concurrent, newest wins (fast click-through must not stack).
   `major` and `error` are never stolen.

### 5.2 Board audio law

1. **Not every event sounds.** The `sound` field on a tell row is permission, not obligation.
2. **Silence law**, mirroring the visual fix ([[sim-render-audit]] §1.2 / headline D): DoT
   *re-announce* (`Cause = Burn`) is silent; anything below the row's `minAmount` is silent.
   That removes the single largest noise source before it reaches the mixer.
3. **Windups lead.** `castSound` fires at `StartAt` with onset at 0 — Riot's rule, and the only
   place where a long clip (up to 700 ms) is correct, because it is *filling* the windup.
4. **Impacts are short.** ≤ 250 ms. This is the change that does the real work (§3.1).
5. **Coalesce, don't stack.** Same id inside one tick (200 ms at 5 tps) → **one voice, louder**, not
   N voices. Shoota's `SameClipMax`/`SameClipBoost`, already written and proven.
6. **Decisive events cannot be stolen.** Death, crit, cheat-death, boss cast, overtime.

### 5.3 The audio budget — Riot's three questions, answered

> *what stands out when density rises · what steps back · what the transition curve is*

- **Stands out:** the `Decisive` bus. Never ducked, never stolen, capped at 4 voices, priority 0.
- **Steps back:** `Impact` and `State`. A `Decisive` onset ducks both by **−6 dB**, and a UI `major`
  ducks the whole `Board` group by −4 dB.
- **Curve:** 30 ms attack, hold for the duration of the triggering clip, 250 ms release. (Shoota's
  `SfxDucker` envelope, unchanged.)

Per-bus voice caps: `Decisive` 4 · `Cast` 4 · `Impact` 6 · `State` 3 · `UI` 4 = **21 max**, inside
the 32-voice budget with headroom. Stealing is *oldest of the lowest priority ≤ mine*; nothing
qualifying → drop and increment a `DroppedVoices` counter (a healthy mix keeps it near zero, so it
is a **measurable** design target, not a vibe).

---

## 6. Tooling — the actual missing piece

Jake's read is correct: **the gap is tooling, not a sound designer.** Shoota solved the same class
of problem with `tools/sfxgen` (deterministic layered-JSON synthesis → WAV) plus a `/sfx` web
audition page, and its clips are good. Warband generated clips one prompt at a time and shipped them
unmeasured.

**BUILT 2026-07-27.** One CLI, `tools/sfx/sfx.py`, Python 3 stdlib only (`wave` + `struct` cover
measurement, trim, normalise, mono-mix and the high-pass). Five subcommands, five `make` targets:

```
make sfx-lint      enforce the contract on Resources/           (sfx.py lint)
make sfx-bake      docs/audio/src -> docs/audio/baked           (sfx.py bake)
make sfx-sheet     write docs/audio/index.html                  (sfx.py sheet)
make sfx-density   onsets/sec per replay fixture                (sfx.py density)
make sfx           bake + sheet + lint the result
                   sfx.py measure <dir|file>  — raw numbers, no contract
```

Working files live under `docs/audio/`, **deliberately outside `client/Assets/`** so Unity never
imports them and no `.meta` churn reaches the serialized-asset guard.

### 6.1 `tools/sfx/families.json` — the contract

The per-family law from §5.1/§5.2 as data: `maxOnsetMs`, `maxAudibleMs`, `targetPeakDb`,
`minCrestDb`, `channels`, `sampleRate`, `variants`. One file, both surfaces (UI and Board).

### 6.2 `sfxlint` — the gate

Scans `Resources/{UI,Board}/SFX`, measures onset / audible span / peak / crest / channels / rate,
and fails non-zero on any contract violation. Also cross-checks **both directions** against
`tuning.json` and the UI family map: clips nobody references, and **ids referenced but missing**
(today: the 10 `hit_*`). *This is the thing that would have stopped a 1.04 s click from ever reaching
the game.* Run it in `make test`.

### 6.3 `sfxbake` — the fixer

Source clips live in `client/Assets/ArtSource/SFX/` (raw generative output — matching the existing
`ArtSource` convention); baked clips are written to `Resources/{UI,Board}/SFX/` and are
**regenerable at any time** (Shoota's "presets are source of truth" rule, applied to a
generate-then-process pipeline). Per family it: trims leading silence to the transient, hard-caps
duration with a 15 ms fade-out, normalises to the family's peak target, high-passes ~200 Hz on
`tick`/`commit` so they sit above the board, and downmixes to mono.

**Re-baking the existing 35 clips is the cheapest possible experiment**, and it directly tests
whether the assets or the generation is at fault. A 1.04 s clip whose transient sits at 14 ms,
trimmed to 100 ms and normalised to a shared target, is a genuinely different sound. `select_1`,
`select_2`, `preview_2`, `seat_1` and `hit_melee` all look salvageable on the numbers.

### 6.4 The audition surface — `make sfx-sheet`, served at `/sfx`

An HTML page (`docs/audio/index.html`): every family, every variant, before/after players, drawn
waveforms, the measured numbers and pass/fail against the contract. The audio analogue of the art
pipeline's `make-contact-sheet.sh`, and of Shoota's `/sfx` page. **It turns Jake's ear into a
two-minute pass instead of a play session** — which matters, because
[[jake-play-passes-are-scarce]] is the binding constraint on this project.

**Reachable from Jake's desktop at `https://warband.inhouseboyz.com/sfx/`** (`site/sfx.go`,
built 2026-07-27). Locally: `make sfx-serve`.

The route serves the page the Python tool already wrote, rather than re-deriving it in Go —
**the measurements live in exactly one place**, so the page can never disagree with `make sfx-lint`.
One `http.FileServer` mounted at `/sfx/` covers the page *and* the clips its `<audio>` elements
reference, because the generated page uses relative URLs. Edit → `make sfx` → refresh; no deploy.

**It is admin-gated and fails closed**, which is not the same gate as the launcher. The launcher is
deliberately open to *any* signed-in Discord account (`site/main.go`), so "signed in" would show
every friend the work-in-progress audio. `/sfx` checks `WARBAND_ADMIN_IDS` and, with that unset,
**404s for everyone rather than falling open**. It answers 404 rather than 403 so an unauthorised
visitor learns nothing about what is there. Verified: anonymous 404 · signed-in non-admin 404 ·
admin 200 · path traversal 404 · `Cache-Control: no-store` (a cached page showing the previous bake
is the exact failure this tool exists to prevent).

**Two things the sheet gets right that the obvious implementation gets wrong**, both found by
looking at the rendered page in a browser rather than at the code: waveforms are drawn on an
**absolute** amplitude scale (full height = 0 dBFS) and a **shared** time axis per pair — normalise
either one and a −11.9 dB source draws identically to its −5.0 dB bake, and a 945 ms clip draws the
same width as a 49 ms one, hiding the exact two things the page exists to compare. The shared axis
is then capped at 6× the baked length, or a 50 ms tick is a 5% sliver of its own 1040 ms source and
you can see *that* it was cut but not *what is left*.

### 6.4a Endings — the second bake pass (Jake, 2026-07-27: *"some def end really abruptly"*)

First bake verdict was *"much better than before … overall massive improvement"*, with one defect.
It measured cleanly: **12 of 28 clips were being cut while still near full amplitude** —
`riser_phalanx` at **−3.0 dB** — and every one got the same 12 ms linear fade. That is a gate, not
a decay. Two causes, and the second was a design error in this page.

**① One fade for two different endings.** A clip that *decayed to the threshold on its own* needs
only a short anti-click fade; a clip the cap *truncated* needs a real release. Split into
`fadeOutMs` (12 ms, linear) and `releaseMs` (60–160 ms, **exponential** — a linear fade on a loud
sound reads as a fader pull, where a real decay is fast-then-tapering). The release lives **inside**
the length budget, so §3's density arithmetic is untouched.

**② The caps were a board law applied to surfaces that have no density problem.** Caps exist because
the board runs at ~9.6 onsets/s. But you click one thing at a time, and a `bind` or `major` is a
once-per-interaction ceremony; a `riser_*` is a one-per-cast windup that §5.2.3 explicitly says
*should* be long because it is filling the windup. Capping those at 400/500/700 ms was fighting the
design. Raised: `bind` 400→700 · `error` 250→400 · `major` 500→900 · `riser_*` 700→1100. **Held**
for everything that actually repeats or overlaps: `tick`/`commit`/`deal`, and every board impact
and cast. Most risers now end naturally rather than being cut at all.

Cost: mean board clip 514 → 579 ms, so worst-case concurrency 4.9 → 5.5 voices. Comfortably inside
the §5.3 per-bus caps (`Cast` sits near 1.2 against a cap of 4), and the right trade — the windups
are the one family where length is the point.

**Two bugs the verification caught, both worth keeping:**

- **Truncation detection was a coin flip.** `last + 1 >= len(a)` requires the very last sample to be
  above threshold, but a waveform crosses zero constantly. `cast_generic` missed by **one sample**
  (17638 of 17640) and shipped with a 12 ms gate on a −11 dB tail, while neighbours got their
  release. Now: *is it still loud within 5 ms of the end?*
- **The dead-tail threshold must stay peak-relative.** An absolute floor looks more principled and
  is wrong here: the shipped padded clips are **not digital silence**. `select_1` carries 820 ms of
  tail at −34 dB rel. peak but only **20 ms at −60 dBFS** — the padding is low-level noise near
  −40 dB, so an absolute floor would have scored the worst clip in the set as clean. The budget
  instead widened to 1.5× the release, which still leaves a 3–10× margin against real padding.

### 6.5 Where clips come from

Split by what each source is good at:

- **UI ticks → deterministic synthesis** (an `sfxgen`-style layered model). A crisp tick is a
  filtered noise burst plus a short modal ring; **onset at zero is structural rather than something
  you trim to**, and family consistency is enforced by sharing the model. This is precisely why
  Shoota's UI sounds are good and warband's are not.
- **Board impacts, casts and risers → generative** (ElevenLabs via the existing MCP) or curated CC0,
  then always through `sfxbake`. Timbre matters here and synthesis is the wrong tool.

---

## 7. Build order

**Steps 0–2 are BUILT (2026-07-27).** They are pure tooling and asset work — no Unity, no play pass,
fully uncontended against a parallel session. Nothing under `Resources/` was touched, so the game is
bit-identical and the shipped clips still resolve; promotion happens at step 5 alongside the code
change that renames the families.

- [x] **0. Measure.** `make sfx-density` — onsets/s per fixture (§3). Found the worst case is
      **`overtime`, not `castfest`**, and that it is *sustained* rather than peak.
- [x] **1. `families.json` + `sfx.py lint`.** The contract as data; both-directions cross-check
      against `tuning.json`. Against shipped `Resources/` it reports every §2 defect by name
      (`commit_1`: stereo · 157 ms onset · 601 ms audible · 287 ms dead tail · peak 4.9 dB hot).
- [x] **2. `sfx.py bake` + `make sfx-sheet`.** 28 clips baked and passing; **`docs/audio/index.html`
      is the audition sheet** (`make sfx-serve` → http://127.0.0.1:8091 — browsers block `file://`
      media, which is why Shoota's equivalent is a served page too). → **Jake auditions.** Decide
      per family: keep the bake, regenerate, or synthesise (D2).
- [x] **3. `SfxPlayer` + the mixer builder — BUILT 2026-07-27, compile-verified.**
      `client/Assets/Scripts/Warband/SfxPlayer.cs` (runtime: 24-voice pool, bus routing, priority
      ladder, per-bus caps, same-id coalescing, duck envelope) and
      `client/Assets/Editor/WarbandMixerTools.cs` (`Warband/Audio/Create Game Mixer`, reflection
      over the internal `AudioMixerController` — a `.mixer` cannot be authored by public API).
      Both are NEW files, so no collision with the parallel session. **Not yet wired to anything —
      it is dead code until step 4/5 call it.**
      **Still owed:** the mixer asset itself. `EnsureMixerOnLoad` (`[InitializeOnLoadMethod]`)
      now builds it on the next domain reload if it is absent, so **no menu item and no MCP call is
      required — Unity just has to reload once.** Until it does, `SfxPlayer` runs unrouted with one
      warning, by design.
      **Why it self-heals rather than waiting on a menu item:** `Unity_RunCommand` compiles into a
      library, so it rejects top-level statements (`CS8805`), and a class-shaped payload compiles but
      the harness then finds no entry point ("No logs available"). Verified across five shapes on
      2026-07-27 — so "just call `ExecuteMenuItem`" is not reliably available to an agent right now.
      Unity's own asset watcher had already imported the new scripts and clips unattended (their
      `.meta` files synced back), so a reload is the only missing trigger.
- [x] **4. UI policy — BUILT 2026-07-27, compile-verified.** `UiAudioDirector` is now a ~90-line
      cue→family adapter over `SfxPlayer`; all playback, pooling and limiting moved out. Hover,
      tooltip, drag-projection and `Attention` are silent. Ten families → six. The ambience bed, its
      duck, both synthesizers and the hover cooldown are deleted, along with the now-dead
      `hoverCooldownMs`/`ambienceVolume`/`commitDuck` config (C# + `HubPresentation.json`) and the
      orphaned `SetHallActive` call site.
      **Second law added while writing it:** an unmapped *cue* is now SILENT, where the old
      `Family()` fell through to `"commit"` — under clicks-only, a default that makes noise means
      any future ambient signal starts clicking on its own. Unmapped *transactions* still fall back
      to `commit`, because a transaction is by definition something the player just committed to.
      **Baked UI clips promoted** so the six families actually resolve (a rewrite pointing at
      families that do not exist is a button that does nothing).
      **Still owed:** the mixer asset — see step 3.
- [x] **5. Promote + board policy — BUILT 2026-07-27.** All 17 baked board clips promoted. **16
      tell rows repointed** onto the 5 D3 families (dangling ids 12 → 3). `ReplayPlayer.PlaySfx` now
      delegates to `SfxPlayer` with a bus per event class (`BusFor`: death/crit → Decisive, damage →
      Impact, everything else → State; risers → Cast), and a Decisive onset ducks the board −6 dB.
      **`audio.enabled` flipped true on BOTH surfaces** — `tuning.json` (board, live under F1) and
      `HubPresentation.json` (Hall UI, hot-reloadable). No options screen yet (step 7), so those two
      values are the mute.
      **Silence law, and what it actually needed:** the status-refresh half was already free — item
      2b's onset filter `return`s before reaching a tell at all, so no sound could fire. The chip
      half was NOT: `minAmount` only ever gated the floating *number*. Added, guarded on
      `Amount != 0` so it judges only events that carry a magnitude — a Cast reports 0 and would
      otherwise have been silenced by a threshold of 1.
      **Design bug fixed first:** `SfxPlayer` briefly had a global `Muted` flag written by
      `UiAudioDirector`. That made the board depend on the Hall having initialised — in a fight scene
      with no Hall, nothing would ever write it and the board would be silent forever with no clue
      why. Each surface now owns its own switch.
      **Still silent — the authoring work-list:** `hit_blunt` · `hit_pierce` · `hit_powder`
      (mace/shield/staff/censer, pike/standard, musket). Missing = silent no-op by design.
- [x] **6. Author the 3 missing impact families — DONE 2026-07-27.** `hit_blunt` · `hit_pierce` ·
      `hit_powder` generated via `elevenlabs-sound-effects-v2` (Jake consented) into the gitignored
      `GeneratedAssets/`, then through the same contract as everything else.
      **The gate earned its keep on first contact:** the raw batch came back with the *identical*
      pathology as the original one — all three padded to 1.045 s, and a **23 dB level spread**
      (−22.9 to −0.0 dBFS). Baked, they land inside ±2 dB at 98–232 ms. Generating without the gate
      would have reproduced the exact defect this whole pass exists to fix.
      **`make sfx-lint` now PASSES against shipped `Resources/` — 0 violations, no dangling ids, no
      silent weapons.** First time the contract has been clean end to end.
- [ ] 7. **Wire `MasterVol`/`UiVol`/`BoardVol` into roadmap item 9's options screen.** The mixer
      params exist and `SfxPlayer.SetBusVolume` drives them; this is now purely a screen job, and it
      belongs to item 9 rather than to audio.

**Also swept 2026-07-27:** 15 superseded clips deleted from `Resources/` — **3.49 MB that shipped in
every build**, because a `Resources/` folder includes everything it contains whether or not anything
references it. `hall_ambience` alone was 1.4 MB of a bed D1 cut. UI is now 11 clips / 360 KB, board
20 clips / 1.1 MB.

### 7.1 What the first real run found

- **The bake works on the assets we already have.** 28/28 baked; every one meets the contract.
  UI ticks land at **41–51 ms** with onset at ~1 ms (from 49–211 ms audible with up to 157 ms of
  lead-in). The whole set now sits inside a **±2 dB** window where it spanned **20 dB**.
- **`error` was the biggest single win**: −20.7 → −4.0 dBFS. The most important cue in the game went
  from the quietest thing in the set to one of the loudest, by processing alone.
- **`cast_fire` fixed itself.** Predicted to fail the transient check at 9.0 dB crest; the high-pass
  pulled out the low-frequency mud and it came back at **16.2 dB**. The clip was never
  characterless — it was buried under its own bottom end.
- **The bake order was wrong on the first pass and `lint` caught it.** Trimming the tail before
  filtering left up to 105 ms of sub-threshold tail on `commit_*` and `riser_pyromancer`, because
  the high-pass genuinely shortens a clip's audible span. Dead tail is not just wasted bytes — it
  holds a pooled voice open for its whole length. The gate caught a defect in the tool that built
  the gate, on its first run, which is the entire argument for having it.
- **Only 3 clips are genuinely missing** (`hit_blunt`, `hit_pierce`, `hit_powder`) — the D3 collapse
  cut the authoring work-list from 10 to 3.

---

### 7.2 The naming trap the promotion walked into

`SfxPlayer` resolves `{id}_1..n` before the bare `{id}`, and the shipped set names every
single-variant UI clip `_1`. So promoting the bake as `error.wav` / `major.wav` left the **stale
1.04 s `error_1.wav` / `major_1.wav` shadowing them** — the two families would have loaded exactly
the clips this whole pass exists to replace, silently, with no warning (both files exist, both
import, both play). Caught by asking "which file would the loader actually pick?" rather than "did
the copy succeed?".

Fixed by making the contract match the convention already on disk: `error` and `major` are
`variants: 1`, so every UI family now goes through the variant path and the promotion overwrites
the stale clip instead of sitting behind it.

**Generalisable:** a fallback chain plus a rename is a silent-wrong-answer machine. Any promotion
step here should verify *resolution*, not *copying*.

### 7.3 Pricing the per-bus caps found a real routing bug

The §5.3 caps were set from design reasoning and nothing had checked them, so `make sfx-density`
now also prices **per-bus pressure** (onsets/s on a bus × that bus's mean clip length) against every
committed fixture. Building that report immediately exposed a bug in the shipped routing.

**The per-weapon hit sounds are authored on `EventKind.Attack` — the swing — not `DamageDealt`.**
`Damage/Attack` carries no sound row at all; the hit is heard when the weapon lands. `BusFor`
routed only `DamageDealt` to `Impact`, so **every weapon hit in the game** fell through to `State`:
the lowest priority and the smallest cap (3), i.e. the first thing stolen under load. In the densest
fights the most common sound in the game would have been the one most likely to silently disappear.
`Cast` bodies and `CheatDeath` were mis-filed the same way.

Fixed routing, then re-priced — peak pressure across all eleven fixtures:

| bus | peak | cap | |
|---|---|---|---|
| Cast | 1.7 | 4 | busiest, because a Cast row fires its riser *and* its body, and risers are long |
| State | 1.6 | 3 | tightest margin; `overtime` drives it |
| Impact | 1.0 | 6 | |
| Decisive | 0.2 | 4 | never contended, which is the point |

**No bus steals from itself on any committed fixture**, so nothing vanishes. `State` at 1.6/3 is the
one worth re-checking if status density ever grows.

The report duplicates `BusFor`'s rule in Python, which is a second source of truth — it is a handful
of lines, labelled, and offline-only. That trade bought a bug the code review did not.

## 8. Jake's decisions

- **D1 — the Hall ambience bed.** Keep it, cut it, or ship it as an options toggle defaulted off?
  *Recommend: cut.* It was never asked for, and "clicks only" implies a quiet Hall.
- **D2 — re-bake or regenerate?** *Recommend: re-bake first* (step 2). It is an hour of tooling
  time, it costs no authoring, and it answers whether the source material is usable.
- **D3 — per-weapon impacts.** 11 distinct weapon sounds, or collapse to ~5 material families
  (blade / blunt / pierce / bow / powder)? *Recommend: collapse.* At ~10 onsets/s nobody hears the
  difference between a sabre and a dagger, and it is 5 clips instead of 11.
- **D4 — does combat get a bed at all?** Dry board, or a low battle ambience under the fight?
- **D5 — priority.** *Board item 23* (audit headline **F**). Not on the critical path to playtest
  #1, but steps 0–2 are cheap and uncontended, and the genre calls audio *the* readability fix.

### Answered 2026-07-27 (Jake)

- **D1 → CUT the Hall ambience bed.** Delete `hall_ambience`, the loop source and the duck envelope.
  Consistent with clicks-only; a quiet Hall is the design.
- **D3 → COLLAPSE to ~5 material families**: `hit_blade` · `hit_blunt` · `hit_pierce` · `hit_bow` ·
  `hit_powder`. The 11 per-weapon rows in `tuning.json` repoint at these. 5 clips, not 11.
- **D5 → BUILD steps 0–2 now.** Ends at the audition sheet, where D2 and D4 answer themselves.
