# First-playable roster — PvE contracts v1.0

**Date:** 2026-07-24
**Status:** current first-playable candidates, mechanically authored but unplayed.

The roster is not balanced or locked. Its job is to offer several legible ways to build an
outrageous warband against authored PvE. The first playtest decides which promises actually
work.

## Source-of-truth boundaries

- This page owns each class's **stable PvE contract**: fantasy, engine, encounter
  contribution, dependency, and weakness.
- `Design/dives/` owns the complete spec-tree intent and named build web.
- `sim/Warband.Content/Kits.cs` owns the currently runnable expression and placeholder values.
- `Design/weapons.md` and ADR 0015 own weapon physics and mastery.
- `Design/inscriptions.md` and ADR 0017 own persistent teamwide engine rules.
- `Design/pve-encounters.md` owns what enemies may demand and how those demands are disclosed.

Do not duplicate every node or number here. Update this page when a class changes identity,
not whenever a tuning value moves.

## Roster at a glance

| Class | Base promise | Main fork | Primary PvE answers |
|---|---|---|---|
| Cleric | Bruiser-healer whose cast rewards contact | War-Priest Burn engine / Lifebinder remote support | Sustain, clustered fights, healing and Burn engines |
| Bulwark | The wall the warband is built behind | Juggernaut area control / Warden threat redirection | Frontline, swarms, caster denial |
| Shade | Backline assassin that chains through vulnerable targets | Reaper crit-Execute / Phantom Phase scaling | Protected threats, cleanup, pressure escape |
| Sharpshot | Distance converts directly into damage | Sniper cast payload / Volleyer swing payload | Artillery, lines, opening burst, long-fight scaling |
| Pyromancer | Burn turns targets or ground into a damage engine | Inferno Field control / Starfall focused burst | Clumps, territory, tall single-target Burn |
| Berserker | Gets faster and more dangerous as health falls | Bloodreaver sustain / Rampager area damage | Brawls, swarms, self-sustaining aggression |
| Phalanx | Second-rank reach and directional retaliation | Pikewall anti-dive / Lancer line damage | Formation defense, Leaps, aligned enemies |
| Banneret | Converts formation and allies into party tempo | Herald protection / Warcaller disruption | Team acceleration, shielding, Clock control |

## Cleric — Sister Maren of the Waning Bell

**Contract:** A bruiser-healer who wants to stand near the part of the formation taking
damage. Being exposed feeds mana, and casting Sanctified Pyre turns that danger into damage
and healing around the Cleric.

- **War-Priest:** commits to the scrum. A larger Pyre applies Burn, and later choices turn
  attacks, deaths, and casts into Burn transfer, detonation, sustain, or tempo.
- **Lifebinder:** retreats from the scrum while projecting support into it. The Pyre becomes
  a remote heal-and-Haste pulse on the most wounded ally, with healing-ground, tempo, repeat,
  or Shield riders.
- **PvE contribution:** stabilizes focused allies, rewards clustered formations, enables
  Burn interactions, and can turn sustained incoming pressure into more casts.
- **Engine wants:** allies receiving meaningful damage; useful clusters around the Cleric or
  pulse target; repeated casts; Burning enemies for War-Priest payoffs.
- **Deliberate weakness:** Lifebinder gives up much of the base offensive pressure;
  War-Priest must accept frontline danger. Fast damage races can end before the healing or
  Burn engine compounds.
- **Watch in play:** whether remote pulse targeting and repeat-pulse behavior are legible;
  whether War-Priest and Lifebinder genuinely demand different placement.

Detailed tree: [dives/cleric.md](dives/cleric.md).

## Bulwark — Brakka, Shieldmaid of the Bronze Hour

**Contract:** The wall the formation is built behind. Starting Shield and incoming attacks
buy time and mana; Shield Slam converts that attention into control.

- **Juggernaut:** breaks whatever crowds the wall. Area Slams, Stun, Slow, Shield generation,
  and Shield-scaled damage turn enemy density into value.
- **Warden:** draws the storm inward. The Slam becomes a wide Taunt, Silence, and self-Shield
  package that redirects pressure instead of dealing damage.
- **PvE contribution:** holds a lane, interrupts clustered enemies, protects fragile allies
  through targeting, and denies dangerous casters.
- **Engine wants:** enemies willing to engage or focus the Bulwark; crowds for Juggernaut;
  important attack or cast threats inside Warden reach.
- **Deliberate weakness:** low independent damage and limited access to enemies outside the
  controlled area. Encounters that ignore the frontline reduce the value of raw durability.
- **Watch in play:** Warden currently dominates small-board control tests; verify that Taunt
  produces interesting protection rather than solving every formation.

Detailed tree: [dives/bulwark.md](dives/bulwark.md).

## Shade — Null, the Redacted

**Contract:** The knife that picks its moment. Ambush opens in the enemy backline; Backstab
and kill-triggered Leaps turn vulnerable targets into a route through the formation. Shade
forks later than the other classes because the early ranks establish that shared assassin.

- **Reaper:** accepts variance for lethal payoff. Crits threaten Execute against wounded
  enemies and later choices amplify marked victims or crit damage.
- **Phantom:** converts danger into absence and scaling. Burst damage triggers Phase; return
  Leaps and permanent combat power reward surviving repeated dives.
- **PvE contribution:** reaches protected support or artillery, cleans up weakened units,
  and disrupts target safety created by conventional frontlines.
- **Engine wants:** valuable distant targets, enemies that can be finished in sequence,
  crit support for Reaper, and meaningful incoming pressure for Phantom.
- **Deliberate weakness:** fragile when caught between escape windows. Singular encounters
  with no secondary targets reduce the shared kill-and-releap engine.
- **Watch in play:** Phase uptime is currently near-degenerate in the synthetic sweep; judge
  readability and counter-pressure before touching its numbers.

Detailed tree: [dives/shade.md](dives/shade.md).

## Sharpshot — Calamity Vance, the Last Deadeye

**Contract:** The queen of visible distance. Full Draw makes every hex matter, while the
fork asks whether damage lives in the mana-funded signature or ordinary swings.

- **Sniper:** turns Piercing Bolt into farthest-target, board-length artillery. Later choices
  emphasize enormous shots, aligned enemies, repeat swings, or mana refunds on kills.
- **Volleyer:** turns casts into widening multishot windows. The ability feeds an auto-attack
  engine that becomes broader and heavier during a long fight.
- **PvE contribution:** attacks protected backliners, punishes lines, provides opening burst,
  and offers a ranged engine that scales without relying on Burn or taking damage.
- **Engine wants:** distance, protected firing space, aligned or multiple enemies for line
  and volley value, and enough fight duration for Volleyer to ramp.
- **Deliberate weakness:** divers and collapsed spacing directly remove Full Draw value.
  Short fights can end before Volleyer becomes spectacular.
- **Watch in play:** One Breath needs encounters long enough to express its cadence; confirm
  that multishot splash and line geometry match what the player reads.

Detailed tree: [dives/sharpshot.md](dives/sharpshot.md).

## Pyromancer — Ilion-7, Cinder of a Dead Star

**Contract:** The caster who makes either the ground or the target into a weapon. Attacks
load a shared Burn pool; the fork decides where that accumulated pressure lives.

- **Inferno:** expands Fire Glyphs, spreads fields from Burning deaths, and can turn occupied
  ground into permanent damage and Slow pressure.
- **Starfall:** removes the field and concentrates power into a heavy hit plus Burn. Later
  choices consume, multiply, or chain the target's accumulated pool.
- **PvE contribution:** punishes clumps, paints hostile territory, clears linked groups, and
  supplies focused magical pressure against durable targets.
- **Engine wants:** enemies remaining inside useful areas for Inferno; repeated Burn
  application and targets that survive long enough to load for Starfall.
- **Deliberate weakness:** fragile, cast-dependent, and sensitive to enemy spacing or
  movement. Very short fights can end before Burn pays out.
- **Watch in play:** fields and Burn transfers must be immediately readable; current short
  scaffolding fights substantially undervalue the class.

Detailed tree: [dives/pyromancer.md](dives/pyromancer.md).

## Berserker — Ulfrik, Who Burns His Hours

**Contract:** The engine that runs hotter as it breaks. Missing health accelerates attacks;
Frenzy cashes that tempo into a rapid swing window.

- **Bloodreaver:** makes Frenzy feed the Berserker through Lifesteal, kill extensions,
  cheat-death, or overheal-to-Shield.
- **Rampager:** makes Frenzy hit the surrounding formation through Cleave, risk amplification,
  a final ring impact, longer windows, or full-weight area damage.
- **PvE contribution:** anchors messy melee fights, clears swarms, finishes wounded enemies,
  and creates a self-sustaining damage engine without dedicated support.
- **Engine wants:** enough incoming pressure to activate Burning Hours without dying;
  repeated swings; adjacent enemies or kills for the chosen path.
- **Deliberate weakness:** burst can kill the Berserker before the low-health engine pays off,
  while denial or unreachable targets starve Frenzy of useful swings.
- **Watch in play:** the synthetic sweep shows unusually broad reliability. Verify that this
  is satisfying robustness rather than a universally correct answer.

Detailed tree: [dives/berserker.md](dives/berserker.md).

## Phalanx — Leonnatos of the Unbroken Line

**Contract:** The second rank that fights over the first. Reach, directional Counter, and
Skewer make formation geometry part of every attack.

- **Pikewall:** holds the player's line. Repeated Counters, Leap reactions, Taunt, Disarm,
  shared Riposte, and damage reduction punish enemies that attack the protected formation.
- **Lancer:** breaks the enemy line. Longer and stronger Skewers turn aligned bodies and open
  spacing into a melee artillery lane.
- **PvE contribution:** protects against dives, supports a frontline from safety, punishes
  attackers, and exploits enemy columns or lines.
- **Engine wants:** enemies attacking into the defended formation; Leaps for Pikewall;
  multiple occupied hexes on a line for Lancer.
- **Deliberate weakness:** sparse or badly aligned enemies can leave line effects empty.
  Threats that neither attack the formation nor enter reach reduce Counter value.
- **Watch in play:** measure Skewer and Overreach connection rates; the current last-wins
  signature composition causes Sarissa to lose Deep Thrust's escalation.

Detailed tree: [dives/phalanx.md](dives/phalanx.md).

## Banneret — Capitana Vespera, Banner of the Turning Age

**Contract:** The warband's party multiplier. Allies mustered around the Standard become the
Company and retain its tempo benefit; live Rally casts reward maintaining useful formation
after movement begins.

- **Herald:** steadies the Company through Rally Shields, damage reduction, wounded-ally
  triage, or broader and stronger muster effects.
- **Warcaller:** fights the Clock in both directions through allied swing windows, enemy
  Slow, a live disruption aura, global Company reach, or interaction with the expedition's
  Inscriptions.
- **PvE contribution:** accelerates casts and attacks across a team, protects clustered
  allies, suppresses nearby enemies, and multiplies an already coherent party engine.
- **Engine wants:** several allies capable of exploiting Haste or Mana, deliberate muster
  placement, useful live Rally geometry, and frequent Inscription activations if Bearer of
  the Mark becomes the planned Inscription-fed engine.
- **Deliberate weakness:** low personal output and poor value when isolated or paired with
  allies that cannot exploit tempo. The class magnifies a plan rather than replacing one.
- **Watch in play:** solo-oriented harnesses understate support value; evaluate at team level.
  Confirm Wide Banner's intended muster-and-Rally reach against its current runnable form.

Detailed tree: [dives/banneret.md](dives/banneret.md).

## Roster coverage law

The eight candidates already cover the first-playable PvE answer space:

- **enemy width:** Inferno, Rampager, Juggernaut, Volleyer, and Lancer;
- **durable focus targets:** Sniper, Starfall, Reaper, Bloodreaver, and Shield engines;
- **sustain and attrition:** Cleric, Bloodreaver, Bulwark, Herald, and Phantom;
- **protected backlines:** Shade, Sniper, Lancer, and long-range wardrobes;
- **dive defense:** Pikewall, Warden, formation, and support tools;
- **Clock pressure:** Stun, Taunt/Silence, Slow, Haste, Mana, and attack denial;
- **Field pressure:** Inferno and Lifebinder, plus authored Inscriptions and enemy glyphs.

The roster deliberately lacks universal cleanse/tenacity, enemy-field removal, anti-heal,
and generic Shield break. PvE encounters must not require a counter the offered roster and
items cannot provide; see `pve-encounters.md`.

Do not add a ninth hero because a concept sounds appealing. Add one only after an authored
encounter and playtest prove that an important problem has no satisfying answer in this
roster. Until then, deepen cross-hero engines through weapons, Inscriptions, placement, and
enemy composition rather than expanding class count.
