# First-playable content budget (hard cap — ADR 0001 + ADR 0016)

Moved off the roadmap 2026-07-28 (actionable-only re-cut). The cap is law; the measured block
updates when content lands (`make baseline` + `make content-version`).

Current 8 heroes × 2 paths · 11 weapons + **5 trinkets** · **24 Inscriptions, delivered through the
ADR 0017 proof waves** · **ONE THREE-ACT RUN, one boss per act** (a tiny reusable enemy-role grammar,
several encounters, three act bosses, one event) · shops + placement · crude post-win endless seam
that may reuse and scale the slice · **2 Revision lineages × 3 authored two-way evolution tiers**
(ADR 0028; no random pool or metagame) · programmer art, no sound.

**Measured against the cap, 2026-07-27 evening** (`make baseline`, fingerprint `b8640a3ea7cd360b` —
moved by ADR 0026: hash schema + twelve Inscriptions + Living Inscription, saves invalidated once):
8 chassis ✓ · 78 spec nodes · 11 weapons ✓ · **5 trinkets** (this line said "1 trinket" until today —
ADR 0022 added four and the budget was never updated) · **12 Inscriptions of 24** ← *was 5 — the one place the
build is far under its own budget, and it is the layer ADR 0016's identity depends on* ·
7 enemy unit types · 6 node encounters · 3 act bosses · **1 event still unspent** (see item 15).
Random hero-kits-as-monsters remain scaffolding, not acceptable final PvE content. Do not expand
beyond three acts, to a full endless mode, or to a catalog beyond the 24-effect proof before
playtest #1.

**Scope decision, Jake 2026-07-26. Three acts is the cap.** (This previously read "one act / one
boss", contradicting ADR 0019's shipped three-act shape and ADR 0024's three bosses.)
