# Combat unit sheet R5 — implementation verification

Verified: 2026-07-29

## Result

The approved compact live sheet now uses the same structured `InspectorPanel` renderer as the
Workbench dossier. Workbench, allied combat units, and authored enemies supply context-specific
data adapters to that renderer; they do not maintain parallel card markup.

The renderer iterates ordered core facts, Weapon facts, Weapon properties, Passives, Specs, and
statuses. The combat stress fixture passed with five Weapon facts, two Weapon properties, two
Passives, and expanded copy without changing the renderer.

Enemy sheets omit unavailable player-facing regions and do not surface borrowed hero identity,
mastery, Signature, or Specs.

## Evidence

- Unity responsive matrix: 8/8 structural passes from 1024×768 through 3440×1440.
- Nominal allied card at 1600×900: 388×705 px, contained.
- Nominal enemy card at 1600×900: 388×450 px, contained.
- Expanded stress card at 1600×900: 388×828 px, contained.
- Unity console after the final matrix: 0 errors, 0 warnings.
- `make check-client`: PASS, 0 errors.
- `make test`: PASS, 281 simulation tests and 253 run tests (534 total).
- `make unity-sim`: PASS.
- `make replay && make scenarios`: PASS; the golden replay and all 14 scenario replays
  regenerated and round-tripped with replay format v10.

The final captures and machine-readable report are in
`verification-20260729-184037/`.

