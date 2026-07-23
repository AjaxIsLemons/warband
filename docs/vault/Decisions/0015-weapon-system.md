# ADR 0015 — The weapon system: catalog, engine riders, temper tiers, the forge

**Date:** 2026-07-23 · **Status:** accepted (Jake: "sounds good as a start" — full v0.1
locked; names/numbers stay placeholder-grade) · **Participants:** Jake + Claude

## Context
The weapons/itemization pass (board 1d), opened after the 8-dive campaign closed.
Full proposal + catalog table: **Design/weapons.md** (settled same day). Inputs: ADR
0005 (weapon = attack profile), ADR 0012 (universal equip, weapon carries the rider),
ADR 0006/0009 (shop machinery, act pools), all 8 dive wardrobes, the ~12-item cap.

## Decisions
1. **The catalog is the 11 debuted categories** — twin daggers, sabre, mace, greataxe,
   tower shield, pike, censer, staff, bow, musket, standard. One weapon per category
   at first-playable: **the categories ARE the item list** (11 weapons + 1 trinket =
   12, cap honored; tiers are not items).
2. **The engine-rider law:** mastery riders amplify ENGINES (mana, tempo, defense,
   crit, potency), never add verbs — verbs live in spec nodes. Sanctioned exception:
   reach categories ride physics (bow +1 range, pike braced-spear) — reach IS their
   engine. New riders locked: bow +1 range · musket opening-shot double · sabre
   auto-crit after cast · greataxe overkill-carry · pike +X% vs enemies engaged with
   an ally · daggers +crit chance.
3. **Rarity = temper tiers on the same weapons: Worn → Honed → Relic** (working
   names). Stat scale per tier (placeholder +25/+50%). **The Relic rule:** at Relic,
   the mastery rider is live for EVERY wielder; masters get it doubled — the dive
   wardrobes' unmastered-spice builds mature into real lines late-run.
4. **Stock is act-gated, never record-gated** (`IRunContent` act pools, ADR 0009).
   **Wager-linked rarity is DEAD** (anti-snowball: gold advantage already exists,
   ADR 0007 — no gear axis on top).
5. **Upgrades = the Tower's forge:** reforge in place at any shop tick (gold,
   escalating), **capped at the current act's stock ceiling** ("the forge follows the
   front"). **Starters begin Worn.** Reforged weapons sell at 50% of total sunk
   (mirrors hero rule). Rejected: dupe-merge, kill-fed weapon XP.
6. **Attack shapes (ADR 0005 debt resolved):** all single-target except **greataxe
   cleave** (target + adjacent at Y%). Pierce/splash stay ability-side in v1.

## Consequences
- Loadout composer: tier param (stat scalar + rider gate: mastered / Relic-any /
  master-doubled) + bow's +range rider.
- ADR 0009 amendment: **reforge** joins the shop actions; item pools fill per act.
- Sim build items: cleave shape · overkill-carry · forced-crit-after-cast (2nd vote) ·
  engaged-with-ally condition · first-swing-double flag.
- Economy: reforge is the third axis of the core tension — widen vs deepen vs
  **sharpen**.
