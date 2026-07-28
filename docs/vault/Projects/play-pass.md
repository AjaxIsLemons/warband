# Next play pass — what to watch

**Board law (Jake, 2026-07-28): playtesting is NOT a roadmap item.** Jake plays continuously and
feeds notes back to sessions. This page is how sessions spend his passes well: keep it current,
prune what he confirms, add what lands unverified-in-motion. Feedback becomes board items or
tuning; nothing here blocks any board work.

- **Feel:** `impact.punchScale` at 0.5 · the Waning clock + storm tell · cast-sigil hold ·
  status-strobe reduction · the opening hold/leap fix.
- **Fight:** beat sequencer + hit-stop (landed `a1fcf8b`, never seen) · fight-ender slow-mo +
  camera law · KayKit minis in motion · riser mix + announce density · the SFX mix (judge against
  `overtime`, the measured worst case) · a live battle-speed change mid-fight.
- **UI in motion:** combat recap over a REAL fight (the double-readout suppression is unseen) ·
  muster rings on a live deploy · Inscription tray drawer hover + TriggerFired flash/indicator ·
  dossier + armory drawer feel · options (menu/fight buttons, Esc, audible sliders, reduced
  motion) · CONTINUE from a cold start.
- **The new enemy bodies (item 29) want your eye on the DIRECTION, not the details.** Authored
  monsters are now flat-shaded primitives (small hunched Swarm, boxy Anchor, back-leaning Artillery
  on a firing-line lane, legless Ritualist column with a mana clock at its foot, forward-pitched
  Diver) standing next to textured KayKit heroes. Verified in captures, unseen in motion. The call
  that is yours: keep procedural monsters as the placeholder language, or put the roles back on
  hero minis dressed with role kit. Captures: `tmp/item29/final_*.png`.
- **Unit HUD pass (2026-07-28, capture-verified, unseen in motion):** number attribution — damage
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
