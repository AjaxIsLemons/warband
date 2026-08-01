# Warband UI reviews

Use matching job names for source material and review output:

```text
inbox/<job>/   # Jake drops screenshots, notes, references, and constraints here
outbox/<job>/  # Claude or Codex puts samples, comparisons, and approval records here
```

Inbox files are source material and are never overwritten. Loose files may be organized into a
job folder when their destination is unambiguous. Agent-authored work belongs only in the matching
outbox folder.

Use measured HTML/SVG at the `1600×900` logical panel size for implementation-grade structure.
The primary review render is QHD `2560×1440` (a `1.6` panel scale); `1920×1080` is the ordinary
containment smoke. Raster generation is useful for mood and art direction, but generated text and
geometry are illustrative until rebuilt in a measured source.

Start or resume a review with `$warband-ui-review`. There are two explicit gates:

1. Jake approves an exact concept filename before Unity implementation begins.
2. After implementation, Jake accepts matched Unity evidence before the job is `ACCEPTED`.

Code completion and a passing layout matrix do not close the visual review.
