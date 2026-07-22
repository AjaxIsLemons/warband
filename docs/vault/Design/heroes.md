# Hero anatomy — DRAFT v0.1 (2026-07-22, round-4 deep dive in progress)

Status: **strawman under discussion.** Decision points marked ❓. Nothing here is locked
(and per ADR 0001, nothing locks until played).

## The anatomy — what a hero IS

**Hero = Chassis + Spec Tree + Items + Rank.** Combat is fully auto (settled identity);
everything below exists to make the *between-fights* choices deep.

### 1. Chassis (fixed per hero — the identity)
- Name, silhouette, base role (Tank / Melee / Ranged / Support flavor).
- **Stats (deliberately few, integers only):** HP · Attack · Attack Interval · Range ·
  Move Speed · Mana Max. (Circuit lesson: ONE damage number; flavor lives in keywords.
  ❓ armor stat or not in v1 — lean: no, defense comes from HP/keywords/items.)
- **1 innate passive** — the chassis trick, often spatial (e.g. Assassin: "combat start:
  leap to the farthest enemy hex").
- **1 signature ability** — the spell the hero auto-casts (below).

### 2. Casting — how abilities happen (❓ core decision)
**Lean: the mana engine (TFT/circuit, proven):** heroes auto-attack by Attack Interval;
attacks and damage *taken* build Mana (+ small time trickle so nothing stalls); at full
Mana the signature auto-casts, Mana resets. No player input mid-fight, ever.
- Why: couples casting to combat geometry — a focused tank casts *more*, an untouched
  backliner casts on a clock — so placement decisions feed the cast tempo. Timing
  identities fall out free: cheap/fast = Guildrun "Rush", huge/slow = "Stall".
- Alternative: pure cooldowns (autobattle's model) — more predictable/readable for
  blind-vs-ghost, but placement stops influencing tempo.

### 3. Spec tree — the roguelite pillar (❓ shape + pacing)
- **Rank 1 (recruit):** chassis as-is.
- **Rank 2 — THE FORK:** pick 1 of 2 **paths**. The path is a role change (multiple
  hats): Cleric → *War-Priest* (frontline smiter) vs *Lifebinder* (backline sustain).
  The path **transforms the signature ability** (circuit's enhance-not-replace /
  PoE-support-gem model) rather than replacing the kit — your hero stays *your hero*.
- **Ranks 3–4:** pick 1-of-2 nodes *within* the path — passives, keyword adds,
  movement mods, mana-curve tweaks.
- **Rank 5 — capstone** (post-v1?).
- ❓ Rank pacing: XP from fights (synergizes with wagering — elite fights = faster
  ranks) vs fixed rank-up offers per act. ❓ Respec: free-ish (Guildrun) vs costed
  retrain (circuit's "never bricked" rule) — either way bricking is forbidden.
- v1 budget: 8 heroes × 2 paths × ~2 node choices each (ADR 0001 cap).

### 4. Items — the churn axis (❓ slot model)
- Lean: **2 slots per hero — Weapon + Trinket.** Weapon reshapes the auto-attack
  (profile + a keyword: cleave arc, pierce line, lifesteal…), category-locked by
  chassis; Trinket = defense/utility/mana mods. Freely re-equippable between fights —
  items are the tinkering verb, heroes are sticky (circuit lesson).
- Items come from wager fights, shops, act rewards.

### 5. Party layer (❓ traits or not)
- **Banners** (our relics): whole-team rules bought/earned across the run — "front row
  +HP", "first ally death: everyone rages", "your Leaps stun". 300-relic-style variety
  is Guildrun's replayability engine; ours at v1 scale: ~10.
- **Roster growth 2→6** is itself the biggest party upgrade (❓ slots bought in shops
  vs granted per act).
- **Lean: NO TFT-style trait counting** ("3 Knights = bonus"). It pulls depth into
  comp-math, and our depth pillar is per-hero speccing. Positional bonds/auras
  (adjacency effects from specs/banners, circuit's geometry layer) give team-texture
  without a second synergy system. ❓ Jake to confirm.

### 6. Movement on the sheet
Move Speed stat + movement *keywords* granted by chassis/specs/items: v1 set is tiny —
**Walk** (default pathing), **Leap** (ignore board, land on target hex). Everything
else (charge-through, kiting, displacement) is post-v1 vocabulary.

## Why this shape
- Sim vocabulary stays small: stats, mana, keywords, hex movement — the
  event→trigger→effect engine expresses all content (circuit's proven grammar).
- Every system has one job: chassis = identity, tree = depth, items = churn,
  banners = team texture, placement = the exam answer.
