# Warband UI reviews

Use matching job names for source material and review output:

```text
inbox/<job>/   # Jake drops screenshots, notes, references, and constraints here
outbox/<job>/  # Claude or Codex puts samples, comparisons, and approval records here
```

Inbox files are source material and are never overwritten. Loose files may be organized into a
job folder when their destination is unambiguous. Agent-authored work belongs only in the matching
outbox folder.

For raster concepts, Claude prepares the brief and marks it `WAITING_FOR_CODEX`; Codex generates
the candidates. Coded HTML/SVG mockups remain available when Jake wants a structural prototype.

Start or resume a review with `$warband-ui-review`. Concept generation stops for Jake's explicit
approval before any Unity implementation begins.
