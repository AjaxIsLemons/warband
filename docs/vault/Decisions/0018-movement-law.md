# ADR 0018 — Movement law: the committed step

**Date:** 2026-07-24 · **Status:** accepted (Jake's finding) · **Participants:** Jake + Claude

## Context
Jake, watching a fight: *"when I hit play on a fight, everyone teleports instantly. We need
smooth movement with solid sim rules for how it works."*

The cause was structural, not cosmetic. A move was **decided and applied in the same tick**:
`MoveInterval` was a cooldown *between teleports*, not a travel time. The client smoothed the
jump with a ~80 ms lerp, but a step is 500 ms — so a unit snapped, then stood still, then
snapped again. No client easing could fix that, because the client would have been drawing
units where the sim said they were not.

This is exactly the lever render-contract §4 flagged: *"if wind-up or flight time ever affects
outcomes, it becomes a scheduled sim effect."* Travel time affects when a unit arrives, so it
is sim truth, not decoration.

## Decision — the law
A move is a **committed one-hex step**.

1. **Depart and arrive.** A unit that decides to move departs at tick `T` and arrives at
   `T + MoveInterval`. `MoveInterval` is now a **duration**, not a cooldown.
2. **You are where you were until you get there.** Its position — for range, targeting,
   projectile tracing, fields, everything — stays the origin for the whole walk.
3. **The destination is reserved.** A walker occupies **both** hexes: the one it still stands
   on and the one it is walking into. Nothing else may path into either. This is what stops
   two bodies sliding through each other.
4. **A committed step always completes.** Root and Stun gate *starting* a step, never
   finishing one. (No rubber-banding, and a whole class of interrupt edge cases never exists.)
5. **Displacement cancels it.** A Leap clears the commitment, releases the reservation and
   teleports. Death cancels it too — a corpse never arrives.
6. **Nothing else is gated on walking.** A walking unit may still attack and cast, from its
   logical hex, exactly as before. Gating them would have silently nerfed every chaser and
   caster; the goal was honest movement, not a balance pass (content doctrine).

## What this costs
**Cadence is unchanged**: depart `T`, arrive `T+MI`, free to depart again at `T+MI` is still
one hex per `MoveInterval`. The one real change is that **every unit is one step "later" than
before** — you can no longer be somewhere before you have walked there. Fights start engaging
`MoveInterval` ticks later; everything downstream shifts with it.

Because arrival and the next departure land on the same tick, a pursuit is now one continuous
slide rather than a hop every `MoveInterval` ticks — which is the thing that actually reads.

## Wire + render
- Departure emits a new **`MoveStart`** (destination + duration). Arrival keeps today's
  **`Move`**. **A `Move` with no preceding `MoveStart` is a teleport** — that one rule is how
  the renderer tells a slide from a blink, and it is what keeps Leap spectacular.
- `PlaybackUnit` carries the walk (`StepTo` / `StepStart` / `StepEnd`), so it rides the replay
  wire (**v4**) and is covered by the view hash. The per-tick reconstruction guardrail
  therefore proves `MoveStart` is emitted *and* folded correctly, not just `Move`.
- The renderer interpolates linearly across the sim's own window and lands exactly on the
  arrival tick. Constant speed is deliberate: an ease per step would stutter at every hex
  boundary during a continuous chase. The only invented motion is a footfall bob and the turn
  onto the path (`tuning.json` → `motion`, hot-reloadable).

## Addendum — the leap arc (same day)
Jake, right after: *"I'm seeing the Shade teleport instantly, we need to build something so it's a
bit less jarring of a transformation."* Correct, and it is the flip side of the rule above: the law
deliberately keeps a leap **instant in the sim**, so the renderer had nothing to interpolate and
fell back to its snap-lerp — which across half a board is a smear that reads as a blink.

**Decision: the leap stays instant in the sim; the body gets an arc as decoration.** The
alternative — modelling airtime, so the Shade is airborne for N ticks — is a *balance* change (the
Shade's entire point is arriving at the backline **now**), and content doctrine says no balance pass
before the playtest. If a playtest ever wants airborne-and-untargetable, that is a real design lever
and gets its own ADR.

So the arc is decoration inside the tick, in the same class as a lunge or a tracer, and it obeys the
same discipline:
- **Both endpoints ride the `Leap` event** (`Aux2`/`Aux3` = the hex it left). By the time the
  renderer sees the event the fold has already teleported the unit, so the take-off is not
  recoverable from view state — and reconstructing it from the last frame's position breaks in
  frozen previews. Put it on the event.
- **The offset is measured backward from the landing hex**, not forward from the take-off. It decays
  to zero at the end no matter what, so touchdown is exact even on a long frame.
- **An arc owns the body outright**, windup included, and vetoes any lunge on that unit while it
  flies — both write the same offset, and a swing landing inside the airtime would otherwise fight
  it into a twitch.

Authored in `tuning.json` as a normal tell (`motion: "Arc"`), so airtime, height, colour and the
take-off crouch are hot-reloadable. Default 0.10 s crouch + 0.34 s flight; height scales with
distance (clamped), so a one-hex hop does not launch like a cross-board dive.

**Known and accepted:** the arc outlasts its tick (~0.44 s ≈ 4 ticks at 10/s). Leaps almost always
fire at BattleStart when nothing else is happening; mid-fight, a hit during the airtime flashes the
body correctly but pops its number over the landing hex. Cheap to revisit if it ever reads wrong.

## Consequences
- `MoveInterval` now reads as "seconds per hex" — a *speed* stat, and a legible one.
- Movement-haste is now a coherent thing to design (it would shorten the step). Deliberately
  not built: nothing asks for it yet.
- Denser boards shuffle slightly more, since a walker holds two hexes. Blocked units retry
  every tick, so this costs a beat, never a deadlock.
- Open, deferred: **a walk is not interruptible.** If a target steps into range mid-walk, the
  unit finishes its step before swinging. It reads fine and it keeps the rules simple; revisit
  only if a playtest says the delay feels bad.
