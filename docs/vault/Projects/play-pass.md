# Next play pass — what to watch

- **Combat inspection rebuilt (2026-07-29, VERIFIED: UI QA 19/19 captures, 0 structural failures;
  the Hall dossier and Deploy panel have been seen, the in-fight card has NOT — the QA matrix has
  no live-fight surface, so items 1-6 below are unseen in motion).** The hover tooltip and the
  world-space text nameplates are DELETED; unit inspection is now one pinned card opened by click.
  **Watch:**
  1. *Does losing hover hurt?* Identity on the board is now silhouette + ground disc + status row
     only. If you find yourself clicking bodies just to find out who they are, that is the signal —
     the answer is a nameplate redesign, not bringing the tooltip back.
  2. *The floating card* — it flips left/right to whichever side has room and clamps to the safe
     area. Watch it near the board edges and while its subject WALKS: the vertical anchor clamps
     rather than flipping, deliberately, so it should never jump.
  3. *The tether ring* on the inspected body — does it read on a crowded board, or vanish under the
     status icons?
  4. *A subject that dies while inspected* keeps its card open and flips to DEFEATED instead of
     vanishing. Right call, or is it clutter?
  5. *Esc* closes the card before it opens Options. Check that ordering feels right.
  6. *Keyword hover on the card* (weapon, passives, specs rows) — this is the first time keyword
     drill-down has been reachable in a fight at all.
  7. *Rank badge* C/B/A/S in the Hall dossier — does S read as an event next to A?
  8. *Deploy's enemy panel* is now selectable rows opening the same card. Three cards' worth of
     information behind one click — enough, or do you want the behaviour line on the row itself?


- **Revision presentation (2026-07-29, compile + tuning-parse verified, UNSEEN IN MOTION — needs
  Play Mode, nobody but you can judge it):** two changes, both feel-only.
  1. *Choosing a return anchor plays time to it.* The held board walks backward/forward to the new
     second instead of cutting. The walk existed; it was rebuilding the whole board every frame
     (director, corpses, dress, camera reset) — now it re-folds only, and the rewind sweep does the
     same. Rate is up (`scrubPerSecond` 0.16→0.30, cap 0.40→0.85s). **Watch:** is it smooth now, and
     is the new rate travel or drag? Both live under F1.
  2. *A committed split now opens ~5s BEFORE the anchor and runs into it.* The pre-branch footage is
     frame-identical (same seed, same opening), so you re-watch known ground under a half-held dress
     (`runUpDress` 0.45), then colour floods back on the punch exactly as the intervention lands.
     **Watch:** does the divergence actually read, or is 5s of re-watching a wait? Does the
     half-held dress look deliberate or broken? `runUpSeconds` → 0 restores the old cut-to-anchor.
     Reduced Motion deliberately keeps the old landing.
  3. *Time selection is now an instrument, not a dial* (capture-verified at 4 and 6 anchors,
     `client/McpCaptures/revrail-e6.png`). Anchors are ticks ON the rail at the second they mean,
     each showing what it carries (`+33`), with a readout spelling out
     `carry 33 → 25 Mana + 8 Shield · returns before Bram Oathkeeper died`. A playhead slides with
     the walk, `NOW` marks the present, landmarks are team-coloured (blue = yours, salmon = theirs),
     and the numbered steps finally render 1-then-2. **Watch:** do the carry numbers change how you
     pick a second, or is it noise? · are the landmark bars readable at that size, or do they want
     to be taller? · is the readout's second line worth the space? · the target chip still can't be
     chosen without the mouse — say if that bites in play.

- **Rim dressing on the shard (item 35 Stage 2, 2026-07-30, capture-verified on two fixtures across
  two probe rounds, UNSEEN IN MOTION).** The board's edge is no longer a bare cut: 16 seeded props
  (banners, planted spears/halberds, stuck swords/axes, propped shields) stand on the flat apron
  between the outermost tiles and the lip. The near side stays deliberately bare — `nearGapDeg` 96
  keeps the camera-side arc clear so nothing stands between lens and front rank. **Watch:**
  1. *Does it read as a place, or as clutter?* The kit is now **KayKit Medieval Hexagon** (CC0):
     tents, barrels, crates, weapon racks, arrow buckets, rocks — an encampment at the shard's
     edge. Judge the DIRECTION; the props are placeholder era dressing.
  2. *Density.* 22 around the 8×8 perimeter. `environment.rim.count` is live under F1.
  2b. *Warmth.* The hexagon atlas is terracotta and `rim.tint` can only multiply, so it darkens/cools
     but cannot desaturate. If the props still read too warm against the cold void, that's a real
     shader-side fix — tell me and I'll add a saturation knob rather than darkening further.
  3. *Do props ever fight the fight?* They sit outside the tile field and cast no shadows, but the
     right-hand edge props sit near the frame boundary at some camera angles.
  4. *Value.* `tint` `#B7BFCC` multiplies the kit texture. Round one was `#707D94` and was too dark
     to see at all — say if this overshot the other way and now competes with the board.
  5. `environment.rim.enabled: false` removes them cleanly; `environment.enabled: false` still
     restores the exact pre-item-35 board.
  6. *The void backdrop* (`sunken-strata`, shipped at `voidArtOpacity` 0.55). **Expect very little** —
     measured, it only reaches a 1202×136 strip along the frame's top edge at ≤34/255, because the
     8×8 board fills the dialed camera almost completely. It reads as faint cold haze, not
     architecture. The question for you is simply: *does the haze help, or is it dirt on the lens?*
     `voidArt: ""` removes it entirely, and opacity is live under F1. If you want it to actually
     matter, that's a **camera framing** conversation (item 22 / ADR 0027), not an art one.

- **Workbench column refactor (2026-07-28, capture-verified 70/70, unseen in motion):** the
  whole screen changed — market grid + vertical reroll rail beside a 30% dossier column,
  46px header with the beat-track pips, 186px rail cards with Signature/W/T + B/A/S path
  slots, armory as a floating rack. Watch: does the reroll rail read as a button? · rack
  covering the dossier's right edge during equip — annoying or fine? · PATH rows + rail
  path slots — does the "fill out the card" feeling land? · obsidian glass vs the approved
  PNG (engine has no glow/gradients — flat-tint stand-ins) · offer tier strips (RANK C ◇◇◇)
  legible at a glance? · dossier empty-state hint text (the old header brief) still useful?

**Board law (Jake, 2026-07-28): playtesting is NOT a roadmap item.** Jake plays continuously and
feeds notes back to sessions. This page is how sessions spend his passes well: keep it current,
prune what he confirms, add what lands unverified-in-motion. Feedback becomes board items or
tuning; nothing here blocks any board work.

- **Feel:** `impact.punchScale` at 0.5 · the Waning clock + storm tell · cast-sigil hold ·
  status-strobe reduction · the opening hold/leap fix.
- **Fight feel after Item 30 (build closed 2026-07-29):** Codex capture-reviewed normal/boss/swarm/
  ritual/Inscription beats and live-inspected the same contact at 0.5×/1×/2×; implementation and
  readability gates are green. Jake's remaining pass is taste, not verification: is 0.5×
  `impact.punchScale` enough? · does the fight-ender slow-mo feel earned? · do KayKit bodies and
  procedural enemies feel like one world in motion? · does a live speed change feel smooth?
  **Ears:** riser/announce density and the SFX mix against `overtime` (measured worst case; every
  bus is under cap, but the final mix still needs ears).
- **UI in motion:** combat recap over a REAL fight (the double-readout suppression is unseen) ·
  deployment direct-drag (body picking, miniature following the pointer, gold/blue/red drop
  target, cancellation snap-back) · Revision return/target dock moving opposite the eligible
  units while board picking stays live · muster rings on a live deploy · Inscription tray drawer
  hover + TriggerFired flash/indicator · dossier + armory drawer feel · options (menu/fight
  buttons, Esc, audible sliders, reduced motion) · CONTINUE from a cold start.
- **The new enemy bodies (item 29) want your eye on the DIRECTION, not the details.** Authored
  monsters are now flat-shaded primitives (small hunched Swarm, boxy Anchor, back-leaning Artillery
  on a firing-line lane, legless Ritualist column with a mana clock at its foot, forward-pitched
  Diver) standing next to textured KayKit heroes. Verified in captures, unseen in motion. The call
  that is yours: keep procedural monsters as the placeholder language, or put the roles back on
  hero minis dressed with role kit. Captures: `tmp/item29/final_*.png`.
- **Unit HUD pass (2026-07-28, capture-verified; Item 30 live contact checked, Jake feel pass remains):** number attribution — damage
  your units TAKE is crimson, damage you DEAL stays type-colored, gold = your crits only (now with
  a "!") — does the split read at 1×/2×, and is crimson punchy enough through the post stack?
  (`numbers.allyHit` toward `#E02818` live if not) · the delayed damage trail (pale sand drain
  behind the HP fill) — does anyone read it as recoverable HP? · shield now caps the bar TIP in
  grey-white · status rows sit on dark pills · small hits render smaller/dimmer/shorter-lived —
  do chip hits still register at 2× speed? · crit pop is crit-ONLY now (normals spawn at final
  size — does combat feel calmer or flatter?).
- **Known-dormant:** Heal carries no `Cause`, so Boon pulses never fire — one-line fix when wanted.
- **F1 knobs to tune live:** field brightness · icon size · wall tint · cleric sigil.
- **Finishing any run uploads its telemetry** — the first human data point; sessions read it off
  `~/warband-runlogs/` on homeserv.
- **Ears (audio D2, per family):** re-bake vs regenerate — audition at
  `https://warband.inhouseboyz.com/sfx/`; judge the mix in motion, `overtime` is the worst case.
- **Feedback-gated work that wants these notes:** camera/framing (+ item 22's board shape) ·
  Inscription wave 3 legibility · the balance items (4/18) once telemetry accumulates.
