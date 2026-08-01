# Warband UI implementation fidelity

This is the acceptance contract between an approved UI concept and the running Unity client.

## Current viewport tiers

Warband player UI uses `UiPanelProfile`: `1600×900`, `ScaleWithScreenSize`,
`MatchWidthOrHeight`, height-locked (`match = 1`).

| Tier | Physical output | Logical panel | Purpose |
|---|---:|---:|---|
| Primary | 2560×1440 (QHD, 16:9) | 1600×900 | Visual approval and must-match comparison |
| Smoke | 1920×1080 (16:9) | 1600×900 | Containment, legibility, and interaction sanity |
| Target-specific | Named in the brief | Measured from panel | Only for responsive-shell, breakpoint, ultrawide, 4:3, or other explicit work |

The QHD scale factor is `1.6`; the 1080p factor is `1.2`. USS `px` are logical reference pixels,
not physical output pixels. A mockup authored as 2560 CSS pixels and copied into USS will be 1.6
times too large at QHD.

For a QHD HTML review, keep the root canvas at `width: 1600px; height: 900px`, use
`transform-origin: top left; transform: scale(1.6)`, and capture a `2560×1440` browser viewport at
device scale 1. Record any other browser device scale explicitly.

Do not run or block ordinary screen work on every supported monitor size. The broader QA matrix is
a diagnostic tool and a gate for changes that alter shared responsiveness.

## A resolution menu is not an acceptance strategy

A desktop game should normally default to native desktop resolution in borderless fullscreen and
offer display mode and resolution controls where the platform/build needs them. Borderless stays
at native output; a resolution/window-size choice is meaningful in Windowed (or Exclusive
Fullscreen, if a platform-specific build ever supports it). Performance render scale is a
different setting. These controls change the output surface; panel scaling and layout still own
UI behavior.

Representative visual acceptance remains necessary even after a resolution selector exists.
Warband does not gain UI coverage merely by listing every value from `Screen.resolutions`.

## Match the state before comparing pixels

The approved reference and Unity evidence must use:

- the same physical dimensions and crop;
- the same fixture data, copy, selected item, focus, expanded/collapsed state, and tooltip state;
- the same intended font assets and approved art;
- a recorded capture command or fixture name.

If state parity is impossible, record the mismatch before interpreting a diff. Never compare a
busy implementation state to an empty concept and call the result a visual review.

## Feasibility map

Before `SPEC_READY`, assign every must-match feature a Unity path:

| Feature | Valid path examples |
|---|---|
| Layout and type | UXML/USS supported by UI Toolkit |
| Framed panels | sliced sprite, nested elements, or Vector API |
| Gradient, mask, complex clipping | prepared texture, material/shader, or named approximation |
| Icons and ornament | approved runtime asset with import plan |
| Motion | explicit UI Toolkit animation or C# transition |
| Unsupported concept detail | defer or mark illustrative with Jake's approval |

Browser support is not Unity support. CSS gradients, `clip-path`, filters, masks, and blended
effects cannot be must-match unless the spec names a practical Unity implementation path.

## Evidence bundle

An implementation candidate includes:

1. approved reference at the primary physical resolution;
2. actual Unity capture at that exact resolution and state;
3. overlay and absolute-difference image;
4. deviation ledger classifying every meaningful mismatch as fix, accepted deviation, or
   illustrative concept detail;
5. 1080p containment smoke result;
6. focused evidence for interaction or motion when relevant.

Structural tests cover geometry contracts; they do not judge composition, hierarchy, rhythm,
color, font rendering, or perceived density. A zero-failure matrix cannot replace the evidence
bundle.

## Status ownership

Use one of:

- `INTAKE`
- `WAITING_FOR_CODEX`
- `GENERATION_BLOCKED`
- `AWAITING_CONCEPT_REVIEW`
- `APPROVED_DIRECTION`
- `SPEC_READY`
- `IMPLEMENTATION_CANDIDATE`
- `VISUAL_VERIFICATION_BLOCKED`
- `ACCEPTED`

Agents may advance every state except `ACCEPTED`. Only Jake can accept the actual Unity result.
