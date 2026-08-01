---
name: warband-rules-copy
description: Write, review, or trace exact player-facing Warband rules text from authoritative game data. Use for rank-up/spec descriptions, signatures, passives, weapons, trinkets, Inscriptions, statuses, encounters, cards, tooltips, dossiers, fixtures, or any report that copy is vague, stale, duplicated, misleading, or unlike Warband's established mechanical language.
---

# Warband Rules Copy

Use this skill whenever mechanics become words. The goal is concise decision copy with Warband's
voice, backed by the same structured data the simulation resolves.

Read `references/rules-language.md` before authoring or changing rules text.

## The contract

1. **The simulation is the authority.** Trace the content ID to `Kits.cs`, `Weapons.cs`,
   `Catalog.cs`, `Enemies.cs`, `Encounters.cs`, or the relevant `Warband.Sim` primitive. A fixture,
   screenshot, comment, `ContentLexicon` summary, or existing tooltip is not proof of behavior.
2. **Author the sentence shape; derive the facts.** Targets, amounts, percentages, ranges,
   durations, cadence, conditions, costs, and consumption come from structured data at runtime or
   through a tested presenter. Never copy a tuning value into Unity UI or a visual fixture.
3. **Say the complete decision rule.** Name the trigger, actor, action, target, and every
   decision-relevant qualifier. Include geometry, duration, cadence, condition, consumption, or
   exception when it changes the player's choice.
4. **Use game language, not code language.** Prefer `Signature`, `Counter`, `Leap`, `Taunt`,
   `Shield`, `Burn`, `Company`, `hex`, and the named ability. Do not expose selectors, event roots,
   fixed-point units, status IDs, or presenter machinery.
5. **One mechanic, one official wording.** The production presenter is the canonical rules
   source. Every live surface and fixture consumes it. Flavor may sit in a separate flavor field;
   it never substitutes for the rule.
6. **Short does not mean vague.** Delete scene-setting before deleting a target, number, range,
   duration, or condition. `"Counters everything"` is both broad and incomplete.

## Source map

- Spec nodes and chassis: `sim/Warband.Content/Kits.cs`
- Weapons: `sim/Warband.Content/Weapons.cs`
- Trinkets and Inscriptions: the registries in `sim/Warband.Content/`
- Enemy and encounter rules: `sim/Warband.Content/Enemies.cs` and `Encounters.cs`
- Structured-to-text grammar: `sim/Warband.Content/MechanicalRulePresenter.cs`
- Names, kinds, and optional flavor: `sim/Warband.Content/ContentLexicon.cs`
- Live rank-up projection: `client/Assets/Scripts/Warband/RunShell.cs`
- Review fixtures: `client/Assets/Scripts/Warband/WorkbenchFixtures.cs`

Search by the rendered phrase and by content ID. Trace both production and fixture paths; Warband
has had screenshots backed by hand-authored fixture prose while the live surface used a different
generated paragraph.

## Workflow

### 1. Resolve the rule before writing

Make a mechanical fact sheet with no prose:

- event or always-on condition;
- actor and target;
- ordered effects;
- magnitude and scaling;
- range, shape, and anchor;
- duration or number of attacks;
- cadence, cost, consumption, refresh, cap, and echo behavior.

If any fact is unclear, inspect the primitive and its simulation tests. Do not infer behavior from
a comment or a flavorful description.

### 2. Choose the display tier

- **Choice:** one or two short sentences containing every fact needed to choose between options.
  Lead with the changed behavior. Use a second sentence for a genuinely separate trigger.
- **Full rule:** canonical exact wording for dossiers, expanded cards, and rules inspection. Retain
  every authored clause and sequencing rule.
- **Flavor:** optional identity line. It may explain fantasy, never behavior.

Do not collapse several rules to `+N rules`. That reports document structure instead of the
choice. Do not repeat a comparison chip in prose unless the prose needs it to make the rule
grammatical.

### 3. Write in Warband order

Use these shapes unless the mechanic requires another:

- Trigger: `After/When [event or condition], [verb] [target] [amount/duration].`
- Activated signature: `[Verb] [target] [amount], then [ordered rider].`
- Persistent modifier: `[Named ability/basic attacks] [gain/change] [exact modifier].`
- Geometry: `[Ability] reaches [N] hexes and affects [exact occupants/line/radius].`
- Delta choice: `[Ability] [changes] from [before] to [after].`

Prefer active verbs: `Deal`, `Heal`, `Grant`, `Gain`, `Apply`, `Counter`, `Taunt`, `Leap`,
`Recast`, `Execute`, `Remove`, `Swear`. Preserve authored ordering with `then`.

### 4. Implement the reusable seam

- Extend the structured presenter when a new primitive or reusable sentence shape appears.
- Add one enum/shape handler, not one branch per content ID.
- Keep name/role/flavor lookup separate from rules generation.
- Make fixtures call the same production presenter or projection as the live surface.
- If a truly authored template is needed, bind its variables from the content definition and
  fail when a token cannot resolve.
- For client changes, also follow `$warband-unity-workflow`.

### 5. Prove it

- Add an exact example test for the new grammar.
- Keep catalog coverage: every authored primitive must render without fallback text.
- Mutation-test at least one magnitude, range, duration, or target. Changing the data must change
  the rendered rule without editing copy.
- Negative-control any new audit or gate.
- Run the smallest relevant headless tests. For Unity surfaces, run the client compile and inspect
  the real capture under the Unity workflow; a fixture-only screenshot is not live proof.

## Review checklist

Reject copy that uses an unbounded claim or qualitative tuning word where data exists:
`everything`, `anything`, `whoever`, `nearby`, `for a moment`, `harder`, `faster`, `weaker`,
`larger`, `longer`, `small`, `large`, `extra`, `more`, or `farther`.

These words are allowed only when the mechanic is genuinely unbounded or the named keyword itself
owns the omitted rule. Ask:

- Everything of what type, on which event, in which area?
- How much, how often, and for how long?
- Who is the source and who is the target?
- Does it consume, refresh, stack, repeat, or exclude echoes?
- Would changing the game data make this sentence lie?
- Can the player compare the two options without opening source code?

## Phalanx reference

Bad:

`The wall: he counters everything, and punishes anyone who Leaps in.`

Choice:

`Gain an extra Counter against every basic attack targeting him. When an enemy Leaps within 2 hexes, Counter and Taunt it for 4s.`

This is two triggers, so it earns two sentences. `Extra` is load-bearing: the node is composed
on top of charged Riposte, so the first attack while Riposte is ready provokes both Counters. The
range and duration come from the node data.

Bad:

`The lunge: the thrust runs through every enemy standing on the line.`

Choice:

`Skewer's line extends from 3 to 4 hexes.`

The identity lives in the ability name; the decision is the exact geometry delta.
