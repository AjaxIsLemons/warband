# ADR 0017 — Hourstone and persistent Inscriptions

**Date:** 2026-07-24 · **Status:** accepted · **Participants:** Jake + Codex

## Context

The first playable implemented five whole-team rules under the name **Banners**. The PvE
pivot made this layer more important: persistent trigger and passive effects are intended
to connect hero, weapon, Clock, Field, formation, and economy systems into the broken-build
fantasy.

Research found that Guildrun calls its umbrella system Relics and also uses Banner as a
named relic family. “Banners” therefore overlapped both that reference and Warband's own
Banneret and Company Standard. Jake wanted the warband to bring one physical object whose
accumulated effects belong to the whole group, while keeping combat presentation focused on
clarity.

Full design and rollout plan: [Design/inscriptions.md](../Design/inscriptions.md).

## Decisions

1. **The communal object is the Hourstone.** Each expedition carries a tablet cut from the
   Tower. It binds era-spanning champions to one shared Hour and holds the laws recovered
   during that run.
2. **The system is Inscriptions.** Each acquired effect is inscribed into the Hourstone.
   “Banner” remains literal Banneret and Company Standard language, not the global build
   layer.
3. **All acquired Inscriptions persist and remain active for the run.** There are no equip
   slots or active cap. The first implementation allows one copy of each named Inscription;
   different rules compound instead of duplicate stacks.
4. **The catalog is broad engine content.** Triggered effects, passives, start-of-combat
   rules, counters, payoffs, formation rules, economy effects, growth, and explicit
   upside/downside Paradoxes are legal.
5. **Chains are intended, cycles are not.** The planned default is at most one activation
   per named Inscription per root event, with explicit repeaters as authored exceptions.
   Existing deterministic cascade depth and drain budgets remain final safety bounds.
6. **Combat presentation is a top-screen badge rail.** Each owned Inscription has an
   inspectable badge that pulses when it triggers and exposes counter progress when
   relevant. The Hourstone does not need a battlefield model. The rail is driven by
   authoritative replay events, not client-side trigger reconstruction.
7. **The catalog target becomes 24, delivered in waves.** Migrate the current five seeds,
   prove twelve across the authoring families, then reach twenty-four after readability and
   cascade safety hold. This explicitly replaces the old five-banner first-playable cap.
8. **Banneret's blanket multiplier is reopened.** Doubling an unlimited persistent
   collection risks making Banneret compulsory. Replace Bearer of the Mark with an
   Inscription-fed engine; exact behavior remains to settle before catalog expansion.
9. **Acquisition uses a hybrid staple.** Shops may sell Inscriptions for the shared run
   currency, while selected rewards may offer one from a visible choice of three. Bosses
   and exceptional events are the primary source for Paradoxes and major rule rewrites.
   Exact shop layout, cadence, reward placement, prices, and risk-tier interaction remain
   run-layer tuning rather than laws of the Hourstone.

## Consequences

- Amends ADR 0009: shop and run data eventually offer/store Inscriptions rather than
  Banners. Shop offers and one-from-three rewards are both intended; their exact layout,
  cadence, reward placement, and pricing remain open.
- Amends ADR 0010's third binding law: the Hourstone, not a generic run banner, is the
  Tower-issued constant that unifies an expedition.
- Amends ADR 0016 vocabulary and content budget: Inscriptions are a primary
  system-breaking layer.
- Legacy `BannerDef`, `BannerIds`, `DoublesBanners`, and related code are migration debt,
  not current public terminology.
- The sim/replay needs an Inscription-identity trigger event and per-root activation guard
  before a large chained catalog is safe and readable.
- The client needs only the badge rail for the first playable; a physical Hourstone render
  is optional polish.
