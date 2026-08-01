# Warband rules-language reference

## Design model

Warband uses an authored-template/data-value model:

- A writer owns vocabulary, clause order, emphasis, and brevity.
- The content definition owns behavior and values.
- A structured presenter binds them.
- Live UI and fixtures consume the same result.

This follows three useful industry mechanisms:

1. Riot's Data Dragon documents spell tooltips as authored text with placeholders whose values
   resolve from spell data:
   <https://developer.riotgames.com/docs/lol#data-dragon_data-assets>.
2. Unity Localization Smart Strings support replacing placeholders and handling grammar such as
   plurals:
   <https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.localization.html>.
3. Magic's Oracle model maintains one current official wording when printed wording becomes
   ambiguous or stale:
   <https://magic.wizards.com/en/news/feature/power-level-errata-b-gone-2006-07-14>.

Use the mechanisms, not another game's tone.

## Canonical Warband vocabulary

Capitalize named mechanics and resources:

- `Signature`
- `Mana`
- `Shield`
- `Burn`
- `Counter`
- `Leap`
- `Taunt`
- `Disarm`
- `Frenzy`
- `Phase`
- `Company`
- `Hourstone`
- `Revision`

Use:

- `basic attack`, not `auto`, `swing event`, or `Attack event`;
- `basic-attack damage` when distinguishing a damage source;
- `hex` / `hexes` for distance;
- `line`, `radius-N field`, or `within N hexes` for geometry;
- `for Ns`, `for the next N basic attacks`, or `for the rest of the fight`;
- the champion's or ability's proper name when it removes ambiguity.

Avoid implementation nouns:

- owner, event source, event target;
- selector, root event, echo flag;
- ticks, fixed point, status magnitude;
- effect list, trigger index, patch, override.

The presenter converts those into player language.

## Information order

Order clauses by how the player evaluates them:

1. What changes.
2. What causes it.
3. What it does.
4. Who or what it affects.
5. Exact amount, geometry, duration, or cadence.
6. Cost, consumption, exception, or sequencing.

For a standalone ability rule, cause may come first:

`After this champion casts their Signature, gain Sure Strike for the next basic attack.`

For a rank-up delta, change comes first:

`Skewer's line extends from 3 to 4 hexes.`

## Compression laws

- One semantic rule per sentence.
- Merge effects only when they share the same trigger and target.
- Use `then` when effect order matters.
- A keyword may replace its stable definition, but never its target, amount, duration, or
  condition.
- Remove repeated subjects and flavor framing before mechanical information.
- Prefer exact short units (`4s`, `2 hexes`, `30%`) in choice copy.
- Do not say `up to`, `nearby`, `briefly`, or `sometimes` unless the simulation implements that
  uncertainty.
- Do not claim causality the data does not express. A line selector means every enemy on that
  line; it does not mean piercing projectiles unless the mechanic actually uses projectiles.

## Tier examples

### Two-trigger fork composed over an innate

Fact sheet:

- inherited rule: combat start and each Signature cast grant 1 Riposte; each incoming basic
  attack spends 1 Riposte to Counter;
- added trigger: every original basic attack targeting the champion;
- added effect: one Counter against the source;
- trigger: enemy Leap;
- condition: source within 2 hexes;
- effects in order: Counter source, Taunt source;
- Taunt duration: 40 ticks at 10 ticks/second = 4s.

Choice:

`Gain an extra Counter against every basic attack targeting him. When an enemy Leaps within 2 hexes, Counter and Taunt it for 4s.`

Full:

`After an enemy targets him with a basic attack, Counter it in addition to Riposte. After an enemy Leaps within 2 hexes, Counter it, then Taunt it for 4s.`

### Signature geometry patch

Fact sheet:

- inherited signature: Skewer;
- line before: 3 hexes;
- line after: 4 hexes;
- damage and target set otherwise unchanged.

Choice:

`Skewer's line extends from 3 to 4 hexes.`

Full:

`Skewer deals its damage to every enemy on the 4-hex line through its target.`

### Qualitative copy audit

Bad:

`Her critical hits strike far harder.`

Repair pattern:

`Critical hits deal +{criticalDamageFromData}.`

Bad:

`A kill nearby sends her faster for a moment.`

Repair pattern:

`When a unit dies within {rangeFromData} hexes, gain {hasteFromData} for {durationFromData}.`

Do not fill placeholders by reading the old sentence. Resolve them from the content definition.
