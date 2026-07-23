# ADR 0012 — Weapon access: universal equip, mastery riders, heal-weapons

**Date:** 2026-07-22 · **Status:** accepted (Cleric dive, weapons lens) · **Participants:** Jake + Claude

## Context
Dive #1 surfaced the precedent question: does a fork unlock weapon categories? Jake's call
went further than the options offered: no locks at all. His worries with class-locked
weapons: every class needs its own weapon types (content burden) and rolling a weapon your
classes can't use is a dead offer (feel-bad).

## Decisions
1. **Universal equip.** Any class wears any weapon. Range and attack profile stay fully
   tinkerable (ADR 0005's dagger-ranger spirit, now universal). No shop offer is ever dead.
2. **Specializations, not locks.** Every weapon has a **category tag** (staff, mace, blade,
   bow, censer, …) and ships **one latent "mastery rider"** — a bonus line active only when
   the wielder specializes in that category. **The weapon carries the rider** (Jake's
   floated direction, adopted): content scales linearly (one line per weapon, one tag list
   per class), never as a class×weapon matrix.
3. **Classes declare 1–2 weapon specializations on the chassis.** Forks MAY add one when
   thematically part of the operation (War-Priest adding maces) — extending wardrobes,
   never gating them.
4. **Heal-weapons are legal.** A weapon family whose auto-attack targets the lowest-HP
   ally and heals (the censer). Heal-swings build mana like any attack; crit applies
   (attacks-only crit law — heal crits are big heals). Support becomes a wardrobe option
   for anyone; mastery riders keep specialists ahead.

## Consequences
- heroes.md §4 "category-locked by chassis" is reversed — updated.
- **Sim gap:** ally-targeting auto-attacks (target selection + heal swings) — new build
  item for the sim backlog; expressible cleanly (attack loop with ally-lowest-HP selector).
- Shop/economy untouched (weapons already universal offers in ADR 0009's model).
- The weapons/itemization pass (campaign 1d) inherits this access model; it still owes the
  category catalog + attack shapes + rider vocabulary.
