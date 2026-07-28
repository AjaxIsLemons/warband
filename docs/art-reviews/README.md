# Warband art reviews

Use matching job names for source material and agent output:

```text
inbox/<job>/   # Jake drops notes, references, screenshots, or sketches
outbox/<job>/  # Claude and Codex share prompts, candidates, proofs, and approvals
```

Claude prepares complete jobs and marks them `WAITING_FOR_CODEX`. Codex generates candidates with
native image generation. Nothing enters Unity until Jake approves both a source candidate and its
processed import proof.

Start or resume work with `$warband-art-pipeline`.
