# Beyond the Hour implementation spec

Approved reference: `../samples/01-banked-victory.png`  
Approval: Jake, 2026-07-30, no conditions

## State and placement

- Render only for `RunPhase.VictoryChoice`.
- Use the existing Workbench and its blocking `_choiceScrim`; the background remains visible,
  dimmed, and inert.
- Author at the responsive contract's 1600×900 logical resolution. Primary evidence is
  2560×1440; 1920×1080 is the containment smoke.
- No required motion. Reduced motion uses the identical static composition.

## Modal

- Equal two-card direction, centered over the Workbench.
- Eyebrow: `VICTORY BANKED · THE WANING CROWN HAS FALLEN`
- Title: `The Hour held. What happens next?`
- Copy: `The authored run is won. Leave with the victory, or carry this exact warband into
  escalating cycles until a deeper Hour finally breaks it.`
- Both choices are peers in width and reading order. Retirement uses the established gold tone;
  continuation uses the established blue/tempo tone.

## Retirement card

- Eyebrow: `SEAL THE HOUR`
- Title: `Retire with victory`
- Rule: `End the expedition here. The completed run and final warband become the record.`
- Facts: `3 ACTS CLEARED`, `VICTORY PRESERVED`
- Action: `RETIRE WITH VICTORY`

## Continuation card

- Eyebrow: `BEYOND THE HOUR`
- Title: `Continue with this warband`
- Rule: `Enter three escalating fights and face the Waning Crown again. Defeat cannot erase the
  victory already earned.`
- Facts: live `CYCLE 1`, `ACT 3 POOL`, `CROWN +25%`
- Action: `CONTINUE BEYOND THE HOUR`

## Behavior

- Retirement logs the choice and run end, deletes the resumable save through the normal
  completed-run path, and opens RunOver.
- Continuation logs the choice, enters virtual Act 4 with this exact warband, saves immediately,
  and returns the Workbench to ordinary management.
- Victory-choice resume reconstructs this same blocking state.
- Endless progression presents three combat beats and then the Crown; it does not expose an
  Interlude or any authored Act-map lookup.
- Endless defeat presents a preserved-victory conclusion, including completed cycle and current
  beat score.

## Evidence gate

- QHD Unity capture of this exact fixture/state
- Approved-reference/Unity side-by-side and overlay/diff
- 1920×1080 containment capture
- choice interaction evidence for both controller paths
- layout contract and relevant Unity tests green
- final status remains `IMPLEMENTATION_CANDIDATE` until Jake accepts the Unity result
