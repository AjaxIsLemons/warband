# Workbench frame — implementation evidence and deviation ledger

Status: IMPLEMENTATION_CANDIDATE
Captured: 2026-07-29

## Evidence

- Approved-size Unity captures:
  - `unity-muster-1600x900.png`
  - `unity-rankup-1600x900.png`
- Readability captures:
  - `unity-muster-1920x1080.png`
  - `unity-rankup-1920x1080.png`
  - `unity-muster-2560x1440.png`
  - `unity-rankup-2560x1440.png`
- Matched visual comparisons:
  - `comparison-muster.png`
  - `comparison-rankup.png`

## Must-match result

| Contract | Result |
|---|---|
| Muster is the Workbench frame with five candidates, picked badges, instruction cell, free reroll, gated BEGIN RUN, and three-slot rail | MATCH |
| Candidate dossier keeps stats, signature, weapon, passive, and the dormant B/A/S promise with the B fork named | MATCH |
| Starting Revision uses the existing blocking choice-scrim beat before the run controller is created | MATCH |
| Rank-up uses dedicated interruptive chrome with option/card/option composition and a pending path slot | MATCH |
| Rank-up entrance has scale/rise, option slides, awaiting pulse, cancellation, and reduced-motion fallback | MATCH |

## Engine deviations

1. The approved Muster sample predates the current standard Workbench dossier anatomy. Unity
   uses the current five-fact strip and icon-led rule sections instead of the sample's colored
   chip grid. Region order and required mechanics are preserved.
2. Dormant Muster labels are compacted to `AWAKENS · THE FORK`, `AWAKENS AT A`, and
   `AWAKENS AT S` so all three promises remain readable above the pinned action at every matrix
   viewport.
3. The rank-up underlay is the current live Workbench/rail state, not the sample's illustrative
   card population. The blocking composition and hierarchy match.
4. Unity uses the established uniform scrim tint and flat borders/tints instead of the
   illustrative radial blur/glow.
5. Live rank-up options may include an additional authored comparison line (for example,
   `SIGNATURE LINE 3 → 4`) when the option model carries it.

## Verification

- `make check-client`: PASS — 0 errors.
- `make test`: PASS — 281 Sim + 253 Run = 534/534.
- Workbench full matrix `20260729-214400`: PASS — 95/95; live pending-rank regression PASS.
- Matched evidence matrix `20260729-214556`: PASS — 4/4.
- Live run-start seam: Muster reroll → pick 3 → first Revision → Wager PASS.
- Unity console after the definitive matrix and evidence capture: 0 warnings, 0 errors.

Human visual acceptance remains with Jake.
