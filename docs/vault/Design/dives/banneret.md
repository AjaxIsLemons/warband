# Deep dive #8 — Banneret (v0.2 PROPOSAL, 2026-07-23) — THE LAST DIVE

Status: **PROPOSED** (v0.2 — reworked onto **ADR 0014**, the aura/muster law this dive
gave birth to: muster for states, live range for moments). **The Company**: allies
placed adjacent to him at muster are "under the banner" for the fight — his permanent
effects follow them wherever they drift; his casts read live geometry, sized honestly.
Audit's "~lean" resolved: Warcaller reworked into the **fighting captain** (support +
melee dps ADD — the Cleric/War-Priest move). Alt kept warm: the **dread captain**
(support + disruptor — Rally doubles as a war-shout slowing nearby enemies' swings).
Fork timing: B default. **First consumer of mana-grant** (v1 vocabulary since day 1).

## The kit (proposed)

**Identity: the warband's soul — you don't place him, you muster around him.**
Fork question: **does the banner protect the line (Herald) or lead the charge
(Warcaller)?**

- **C — recruit** *(support)*. Starter: **Company Standard** (r1, light swings — the
  pole doubles as a weapon). Innate: **Standard-Bearer** [MUSTER] — *allies placed
  adjacent to him (his **Company**) swing X% faster for the fight, wherever they
  drift.* Signature: **Rally** [CAST, live] — *grant Mana to allies within 2 hexes*
  (live geometry at the moment the banner dips — r2, sized for scrum drift per ADR
  0014). Specializations: standards + sabres.
- **B — Herald** *(DEEPEN support — the shieldward)*: *Rally also Shields each
  recipient.* The banner that keeps the line standing.
- **B — Warcaller** *(ADD melee dps — the fighting captain)*: *Rally becomes a
  war-cry: it reaches every ally on his current ROW, grants AttackUp alongside the
  mana — and includes HIMSELF.* The captain whose own sabre work becomes real damage
  as he drums the company forward.

## A/S web (verb-riders, explicit)

- **Herald A:** **Steady the Line** [MUSTER] — *his Company also gains X% damage
  reduction* | **Second Wind** [SIG] — *allies below half HP receive double Rally
  (mana AND Shield — the triage rider, target-below-HP condition reused)*.
- **Herald S:** **The Colors Do Not Fall** [MUSTER] — *once per fight, the first
  Company member who would die instead drops to 1 HP and gains a big Shield (the
  banner catches them — ally-facing extension of the Deathless cheat-death class)* |
  **The Wide Banner** [MUSTER+CAST] — *his muster reach AND Rally radius grow by 1
  (Company = placed within 2; Rally = live r3 — the whole field hears him)*.
- **Warcaller A:** **Lead From the Front** [AUTO, live] — *his swings deal +X% while
  adjacent to 2+ allies (self-measured press-of-bodies — stays live per ADR 0014
  clause 3)* | **Drumbeat** [SIG] — *Rallied allies' next 3 swings come Y% faster
  (the next-N-swings status GRANTED cross-unit — 4th vote for the shape)*.
- **Warcaller S:** **Bearer of the Mark** [BANNER] — *your equipped run-banner's
  effect is doubled while he lives (THE cross-layer crown — the class named for
  banners carries the Tower's actual Mark; needs Jake's explicit yes, it crosses
  sim/run layers)* | **The Captain's Cut** [AUTO] — *after each Rally, his swings
  deal +Z% per ally it reached, until his next cast (he swings with the weight of
  the company)*.

## The build web (A × S braid)
Herald: Steady+Colors = **The Last Stand** ✦ · Steady+WideBanner = **The Broad Wall** ·
SecondWind+Colors = **No One Left** ✦ · SecondWind+WideBanner = **Mercy's Reach**.
Warcaller: LeadFront+Bearer = **The Tower's Own** ✦ · LeadFront+CaptainsCut = **First
Through the Breach** · Drumbeat+Bearer = **March of the Hour** · Drumbeat+CaptainsCut =
**The Red Parade** ✦.

## Weapon wardrobe (ADR 0012 — standards debut as the muster-weapon; sabres as the
officer's blade)
Standard mastery (placeholder rider: Company potency). Herald × standard: the classic
colors · Herald × sabre: the duelist who guards the wounded · Warcaller × sabre: the
fencing captain — Captain's Cut lives here · Warcaller × standard: the banner-charge,
laying about with the pole · unmastered spice: Banneret × censer — the chaplain: Rally
plus heal-swings, the full support stack in one body. ⚠ Category-count flag: standards
+ sabres bring weapon categories to 10 vs the ~12-item first-playable cap — fine under
placeholder doctrine, but the itemization pass (next on the board) inherits the squeeze.

## Banner hooks
He IS the banner texture: cast-cadence banners (Rally cycles fast) · Shield banners
(Herald output) · formation/muster family (*"allies placed adjacent to an ally gain
X"* — ADR 0014 texture, third vote after Phalanx/Berserker) · and Bearer of the Mark
literally doubles whichever run-banner you brought — the class and the item system
shake hands.

## Sim gaps this dive adds
**Mana-grant effect** (first consumer — Rally; vocabulary day 1) · **Company muster
set** (fight-start membership snapshot + member conditions — ADR 0014's machinery;
placement passives already built) · ally-facing cheat-death (Colors — extends the
approved Deathless class cross-unit) · muster/cast radius params (Wide Banner) ·
granted next-N-swings on allies (4th vote, first cross-unit grant) · live
adjacent-ally-count condition (Lead From the Front) · per-cast reach-count damage
rider (Captain's Cut) · live-row cast targeting (Warcaller Rally) · **run-banner
effect multiplier** (Bearer — crosses into ProgressionFold/run layer; only if Jake
blesses).

## Open (for Jake)
Bless the fork frame + the **Warcaller ADD rework** (fighting captain; dread-captain
disruptor alt available) · **Bearer of the Mark** — cross-layer crown, yes/no (it's a
precedent like Deathless was) · **The Colors Do Not Fall** — ally-facing cheat-death
OK? · the four A/S pairs · standards + sabres debut (category count → 10, flagged) ·
champion name (floating: **Capitana Vespera, Banner of the Turning Age**, Renaissance).
