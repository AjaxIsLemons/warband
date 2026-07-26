# Deep dive #8 — Banneret (v1.0 SETTLED, 2026-07-23) — THE LAST DIVE

Status: **SETTLED** (Jake's calls: **dread captain over fighting captain** — Warcaller's
ADD is disruptor, not dps · **Bearer of the Mark cross-layer intent APPROVED**, while its
original blanket multiplier is superseded and reopened by ADR 0017 · **ally-facing
cheat-death REJECTED** — The Colors Do Not Fall cut; cheat-death stays
Berserker-exclusive · freed slots lean TEMPO per Jake: more allied haste + a global-haste
crown). Champion working name adopted: **Capitana
Vespera, Banner of the Turning Age** (Renaissance). Born from this dive: **ADR 0014**
(muster for states, live for casts) and **the Company**. Fork law: ✓ — Herald DEEPENs
support, Warcaller ADDs disruptor. **First consumer of mana-grant.**

**ADR 0017 amendment, 2026-07-24:** run-wide Banners are now unlimited persistent
**Inscriptions** held by the expedition's Hourstone. Bearer's blanket multiplier is reopened
because it would scale with the entire collection; the preferred replacement feeds
Inscription activations into Vespera's own engine.

## The kit

**Identity: the warband's soul — you don't place him, you muster around him.**
Fork question: **does the banner steady YOUR line (Herald) or shake THEIRS
(Warcaller)?**

- **C — recruit** *(support)*. Starter: **Company Standard** (r1, light swings — the
  pole doubles as a weapon). Innate: **Standard-Bearer** [MUSTER] — *allies placed
  adjacent to him (his **Company**) swing X% faster for the fight, wherever they
  drift.* Signature: **Rally** [CAST, live] — *grant Mana to allies within 2 hexes*
  (live geometry at the moment the banner dips — r2, sized for scrum drift per ADR
  0014). Specializations: standards + sabres.
- **B — Herald** *(DEEPEN support — the shieldward)*: *Rally also Shields each
  recipient.* The banner that keeps the line standing.
- **B — Warcaller** *(ADD disruptor — the dread captain)*: *Rally is also a
  war-shout: enemies within 2 hexes of him swing X% slower for a few seconds* (live,
  enemy-facing — ADR 0014 clause 3). One cast fights the tempo war both ways: your
  side surges, theirs staggers.

## A/S web (verb-riders, explicit)

- **Herald A:** **Steady the Line** [MUSTER] — *his Company also gains X% damage
  reduction* | **Second Wind** [SIG] — *allies below half HP receive double Rally
  (mana AND Shield — the triage rider, target-below-HP condition reused)*.
- **Herald S:** **The Quickening** [MUSTER] — *Standard-Bearer's haste is doubled for
  his Company (intensity — the few, faster)* | **The Wide Banner** [MUSTER+CAST] —
  *his muster reach AND Rally radius grow by 1 (breadth — Company = placed within 2,
  Rally live r3)*.
- **Warcaller A:** **Drumbeat** [SIG] — *Rallied allies' next 3 swings come Y% faster
  (the next-N-swings status GRANTED cross-unit — 4th vote for the shape)* | **Dread
  Presence** [AURA, live] — *enemies adjacent to him swing X% slower (enemy-facing
  persistent aura — legal under clause 3: they bring themselves to him)*. The A pair
  IS the fork question in miniature: speed yours vs slow theirs.
- **Warcaller S:** **Bearer of the Mark** [INSCRIPTION, REOPENED] — *preferred direction:
  whenever an Inscription activates, he gains Mana, at most once per root event; exact
  output remains to settle* | **The Last March**
  [MUSTER] — *Standard-Bearer reaches every ally: the whole warband is his Company
  (Jake's global-haste crown)*.

## The build web (A × S braid)
Herald: Steady+Quickening = **The Iron Cadence** ✦ · Steady+WideBanner = **The Broad
Wall** · SecondWind+Quickening = **Second Heart** · SecondWind+WideBanner = **Mercy's
Reach** ✦.
Warcaller: Drumbeat+Bearer = **March of the Hour** ✦ · Drumbeat+LastMarch = **The Red
Parade** · DreadPresence+Bearer = **The Tower's Own** · DreadPresence+LastMarch =
**The Turning Tide** ✦ (everyone hastened, everyone near him slowed — the tempo war
won outright).

## Weapon wardrobe (ADR 0012 — standards debut as the muster-weapon; sabres as the
officer's blade)
Standard mastery (placeholder rider: Company potency). Herald × standard: the classic
colors · Herald × sabre: the duelist who guards the wounded · Warcaller × standard:
the dreadnought colors — the shout rolls out from under the banner · Warcaller ×
sabre: the officer who wades in where his shout bites · unmastered spice: Banneret ×
censer — the chaplain: Rally plus heal-swings, the full support stack in one body.
⚠ Category-count flag: standards + sabres bring weapon categories to 10 vs the
~12-item first-playable cap — fine under placeholder doctrine; the itemization pass
(next on the board) inherits the squeeze.

## Inscription hooks
He keeps the literal banner texture while the system becomes the Hourstone: cast-cadence
Inscriptions (Rally cycles fast) · Shield Inscriptions (Herald output) ·
formation/muster family (*"allies placed adjacent to an ally gain X"* — ADR 0014
texture) · and Bearer of the Mark turns Inscription activity back into his own engine.

## Sim gaps this dive adds
**Mana-grant effect** (first consumer — Rally; vocabulary day 1) · **Company muster
set** (fight-start membership snapshot + member conditions — ADR 0014's machinery;
placement passives already built) · muster/cast radius params (Wide Banner) ·
innate-potency multiplier (Quickening) · global-muster flag (Last March) · granted
next-N-swings on allies (Drumbeat — 4th vote) · attack-speed-down status (war-shout +
Dread Presence; 3 votes total w/ Pyro's Choking Smoke) · **Inscription activation hook**
(Bearer replacement; exact effect reopened by ADR 0017).
Cut with Colors: ally-facing cheat-death (rejected — cheat-death stays
Berserker-exclusive).

## Open
Bearer of the Mark's exact Inscription-fed output · all magnitudes (haste %, shout duration,
radii) placeholder until sweep/playtest. Roster's original row-reach Warcaller retired with
the fighting captain (both superseded by the dread-captain rework).
