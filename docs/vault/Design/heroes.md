# Hero anatomy — v0.2 (2026-07-22, round-4 decisions in)

Status: **directions decided on paper** (Jake, round 4) — still unplayed, so per ADR 0001
everything here is falsifiable by the first playable. Remaining opens marked ❓.

**Round-4 calls:** mana engine ✓ ("cooldown is balanced by mana cost; focused units casting
more is the good part") · rank-up via **duplicates, no XP** (Guildrun's model — XP felt
micromanagy in prior games) · respec allowed for now, can close later · fork transforms the
signature, never replaces the kit ✓ · **no trait counting — synergies emergent, not
explicit** ✓.

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

### 2. Casting — the mana engine (decided)
Heroes auto-attack by Attack Interval; attacks and damage *taken* build Mana (+ small time
trickle so nothing stalls); at full Mana the signature auto-casts, Mana resets. No player
input mid-fight, ever. "Cooldown" is balanced via mana cost. Why: casting couples to
combat geometry — a focused tank casts *more*, an untouched backliner casts on a clock —
so placement decisions feed cast tempo. Timing identities fall out free: cheap/fast =
Guildrun "Rush", huge/slow = "Stall".

### 3. Spec tree + ranks — duplicates, no XP (decided)
- **Ranks C → B → A → S, gained by buying duplicates of a hero you own** (Guildrun's
  model — confirmed via wiki research; there is no XP). Shop offers are the pacing valve
  and anchor to act number (anti-snowball law).
- Each rank-up presents the spec choice at that step:
  - **C (recruit):** chassis as-is.
  - **B — THE FORK:** pick 1 of 2 **paths**; a role change (Cleric → *War-Priest* vs
    *Lifebinder*). The path **transforms the signature ability** (enhance-not-replace,
    PoE-support-gem model) — your hero stays *your hero*.
  - **A / S:** pick 1-of-2 nodes *within* the path — passives, keyword adds, movement
    mods, mana-curve tweaks.
- **The central economic tension** (steal from Guildrun): widen the roster vs. deepen
  the core — same currency buys both.
- **Respec: allowed for now** (cheap or free while we learn; can close later). Bricking
  is forbidden either way.
- v1 budget: 8 heroes × 2 paths × ~2 node choices each (ADR 0001 cap).

### 4. Items — the churn axis (❓ slot model)
- Lean: **2 slots per hero — Weapon + Trinket.** Weapon reshapes the auto-attack
  (profile + a keyword: cleave arc, pierce line, lifesteal…), category-locked by
  chassis; Trinket = defense/utility/mana mods. Freely re-equippable between fights —
  items are the tinkering verb, heroes are sticky (circuit lesson).
- Items come from wager fights, shops, act rewards.

### 5. Party layer (decided: no traits)
- **Banners** (our relics): whole-team rules bought/earned across the run — "front row
  +HP", "first ally death: everyone rages", "your Leaps stun". 300-relic-style variety
  is Guildrun's replayability engine; ours at v1 scale: ~10.
- **Roster growth 2→6** is itself the biggest party upgrade (❓ slots bought in shops
  vs granted per act).
- **NO trait counting** ("3 Knights = bonus") — Jake: "synergies should be more
  emergent than explicit." Team texture comes from positional bonds/auras + banners +
  ability interactions, never from comp-math.
- ❓ Currency: proposal — **one currency** (working name: shards) earned from fights
  (wager-scaled), spent on everything: new heroes, duplicates, items, banners, rerolls.

### 6. Movement on the sheet
Move Speed stat + movement *keywords* granted by chassis/specs/items: v1 set is tiny —
**Walk** (default pathing), **Leap** (ignore board, land on target hex). Everything
else (charge-through, kiting, displacement) is post-v1 vocabulary.

## Why this shape
- Sim vocabulary stays small: stats, mana, keywords, hex movement — the
  event→trigger→effect engine expresses all content (circuit's proven grammar).
- Every system has one job: chassis = identity, tree = depth, items = churn,
  banners = team texture, placement = the exam answer.
