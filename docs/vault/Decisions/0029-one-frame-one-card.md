# 0029 — One frame, one card: the workbench is the game's main UI system

**Date:** 2026-07-29 · **Status:** accepted (Jake, in chat, stated three times) · **Owner:** item 34

## Decision

The **workbench frame is the main UI system of the game**, not one screen among several. Every
post-round player choice is a state of that frame and reuses the existing market card + dossier
components:

- interlude choice,
- revision pick,
- revision tier-up / upgrade choice,
- muster (the run-opening offer),
- in-combat unit inspection.

No bespoke screen and no second card layout for a new choice surface. If a surface needs something
the shared card lacks, **extend the shared card** — the workbench dossier and the combat inspect card
are the same card.

**The one exception Jake named:** unit **rank-up** gets its own popped modal, because it should be a
"dopamine shot." That is the only interruptive chrome, and its entrance is a big animated moment
(reduced-motion respected).

## Why

Jake asserted this three times in sixteen hours, each time *after* a session had proposed or built a
bespoke surface, and it had never been written down:

> "not quite, what I mean is the same way we do the market in the current work bench, we should reuse
> those cards here. I think we should do that for pretty much all of our choices that come as part of
> a post round — interludes, choicing new revision tier up, etc."

> "we might need to rework the 'workbench' into just the main UI system of the game more or less.
> Choosing revisions, the interlude choice, the revision upgrade choice, it should all be housed in
> this same frame right? any reason not to? The only thing I think currently worth a pop up or
> different screen/chrome is a unit rank up choice."

> "I also feel this should merge with the workbench dossier right? ... Why wouldnt we show the same
> cards here - its basically the unit card." → "Yeah same card I think, whatever we land on for both
> workbench dossier + combat inspect."

The underlying reasons, in his framing: fewer frames means less menuing (his standing "show, don't
tell" instinct), and a player who has learned one card can read every choice in the game.

## Consequences

- Item 34 (BUILD) is the first implementation: muster becomes a workbench state, starting revision
  becomes choice-scrim beat #0, and `RecruitView` + `MusterCard` + `RevisionDraftView` retire.
- `Projects/planning-system.md`'s "Opening Muster presentation contract" describes the retiring flow.
  Its **contract requirements** carry over to the shared card — the five projected objects, the
  rejection rules, and cadence derived from the composed weapon rather than copied card data. The
  container changes; the honesty rules do not.
- A merged card must carry an explicit **per-region space budget**. The first merge attempt shrank
  the hero image and squeezed the specs out entirely ("This is smooshing out the specs enitrely, I
  cant see them at all") while every layout contract stayed green — green contracts do not prove a
  space budget.
- Wager-screen fold-in is explicitly **out of scope** and remains an open question.

## Not decided here

Whether the Hall/Hourstone station screens collapse into the same frame. Only post-round *choices*
and unit inspection are in scope; navigation chrome was not discussed.
