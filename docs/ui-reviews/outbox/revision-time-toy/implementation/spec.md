# Implementation spec — Revision time selection as the Hourstone

Approved 2026-07-29 from `samples/b-r3-borrowed-future.png` + `samples/b-r3-recall.png`.

## Information hierarchy

Four facts, nothing else.

1. **How far back** — the dial's numeral.
2. **What is being given up** — the board rewinding. No widget. Already built (the smooth anchor
   walk landed earlier today).
3. **What the ability does, to whom** — one cluster anchored to the chosen unit.
4. **How to commit** — a hold that fills the ring.

## Regions

### A · The Hourstone (screen chrome, bottom-centre)

- Ring ~620px, anchored bottom-centre, sunk below the fold so ~390px shows.
- Notches for `1..MaxSeconds`, laid on an arc, **deepest second at the left**. Notch angle spans
  −58°..+58°; with `n` anchors, anchor `k` (1-based, 1 = shallowest) sits at
  `lerp(+58°, −58°, (k−1)/(n−1))`.
- **Notch weight carries payoff.** Each notch's diameter and opacity lerp across its normalised
  payoff (0..1 within the current anchor set). Borrowed Future only; Recall passes a flat 1.
- Grab knob rides the ring at the **live scrub position**, not the selected notch — so the knob
  slides while the board walks and settles on the notch. This replaces the old rail playhead.
- Centre: numeral `Ns` + `ROLLED BACK`.
- Rim: lineage name (`BORROWED FUTURE` / `RECALL TO FORMATION`) — the only place the ability is named.
- Below: one contextual line (see States).

### B · The ability cluster (board-anchored, follows the unit)

Positioned from `ReplayPlayer.TryGetUnitScreenBounds` → `RunShell.ScreenToPanel`, refreshed **every
frame while Selecting**, because scrubbing moves the unit.

- **Borrowed Future:** carry as hero number (`+33`); mana orb showing the post-split total and
  whether it caps; shield spill drawn as an arc breaking out of the orb, with its own number.
  Spill arc and its number hide when `shield == 0`.
- **Recall:** tether from the unit to its deployment hex; dashed hex ring + ghost at the
  destination; `Ns CANNOT SWING` badge. Destination resolved from the battle's opening roster
  position via new `ReplayPlayer.TryGetHexScreenPosition`.
- Eligible-but-unchosen units keep their existing board highlight and get **no** cluster and no text.
- No cluster at all before a target is chosen.

## Deleted

The entire two-column draft panel: step labels, prompt paragraph, status line, target chips, the
landmark rail (track, markers, playhead, NOW, readout), the per-anchor price row, and the
CANCEL/SPLIT buttons. `BuildRevisionMarkers`, `DescribeRevisionCrossing`, `BuildRevisionReadout`,
`UpdateRevisionPlayhead` and the marker model go with them.

The "cannot be reached at −Ns" message is retained but demoted to the contextual line: units never
resurrect, so it only fires on the `Omitted` edge case.

## Actions and states

| State | Dial | Cluster | Contextual line |
|---|---|---|---|
| Selecting, no target | live | none | `CHOOSE A CHAMPION` / `CHOOSE AN ENEMY` |
| Selecting, target chosen | live | shown | `HOLD ⏎ TO SPLIT THE HOUR` |
| Illegal pick | live | none | the refusal reason |
| Final chance | bone/amber shift, `revision-combat--final` | shown | `HOLD ⏎ TO SPLIT · ESC TO ACCEPT FATE` |
| Holding | ring fills 0→1 | shown | unchanged |

- **Turn:** click a notch · `←`/`→` · dpad · horizontal drag on the dial.
- **Commit:** hold `Enter`/gamepad-south, or hold pointer on the dial. `HoldSeconds = 0.45`.
  Releasing early resets the fill to 0. Reaching 1 fires `ConfirmRevision` exactly once.
- **Cancel:** `ESC`, as today.
- **Reduced Motion:** dial and cluster unchanged; the board cuts between anchors instead of walking
  (already the case). Hold still required.

## Must match vs illustrative

- **Must match:** the four-fact hierarchy; ability context anchored on the unit and nowhere else;
  the ability named only on the rim; hold-to-commit; no rail.
- **Illustrative:** exact sand/stone material, glyphs, notch spread angle, the specific amber.

## Laws re-checked

- ADR 0028 law 6 — every number shown is a past fact from two witnessed moments. No branch forecast.
- ADR 0028 law 5 — nothing here touches `Time.timeScale`.
- Held-Hour dress stays on the **board** (camera post), never on the overlay tree.

## Acceptance

1. Headless client compile clean.
2. Unity Editor compile clean, 0 console errors.
3. Play Mode captures of: Borrowed Future with target · Recall with target · no-target state ·
   6-anchor (Long Memory) dial. Compared against the approved samples, differences reported.
4. Existing sim suite still green (this is client-only, so it must be untouched).
