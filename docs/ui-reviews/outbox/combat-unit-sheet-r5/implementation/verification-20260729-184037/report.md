# Responsive UI QA

- Run: `20260729-184037`
- Matrix: `combat-unit-sheet`
- Captures: 8
- Live rank-up regression: PASS · pending B fork safely defers the following A-rank Market preview
- Pixel captures require human review; structural layout is the automated gate.
- Captures marked `offscreen-panel-fallback` were rendered to the exact target without opening or focusing a Game View window.

| Surface | Fixture | Viewport | Phone | Copy stress | Layout | Capture |
|---|---|---:|:---:|:---:|---|---|
| combat | ally | 1024x768 | no | nominal | Combat inspector: PASS · card=389.1×692.6; contained=True; weapon facts=4; properties=1; passives=1; capture=offscreen-panel-fallback | ui-qa-20260729-184037-1024x768-combat-ally.png |
| combat | ally | 1280x720 | no | nominal | Combat inspector: PASS · card=388.8×706.3; contained=True; weapon facts=4; properties=1; passives=1; capture=offscreen-panel-fallback | ui-qa-20260729-184037-1280x720-combat-ally.png |
| combat | ally | 1600x900 | no | nominal | Combat inspector: PASS · card=388×705; contained=True; weapon facts=4; properties=1; passives=1; capture=offscreen-panel-fallback | ui-qa-20260729-184037-1600x900-combat-ally.png |
| combat | enemy | 1600x900 | no | nominal | Combat inspector: PASS · card=388×450; contained=True; weapon facts=4; properties=0; passives=1; capture=offscreen-panel-fallback | ui-qa-20260729-184037-1600x900-combat-enemy.png |
| combat | ally | 2556x1317 | no | nominal | Combat inspector: PASS · card=388.2×697.7; contained=True; weapon facts=4; properties=1; passives=1; capture=offscreen-panel-fallback | ui-qa-20260729-184037-2556x1317-combat-ally.png |
| combat | enemy | 2556x1317 | no | nominal | Combat inspector: PASS · card=388.2×446.2; contained=True; weapon facts=4; properties=0; passives=1; capture=offscreen-panel-fallback | ui-qa-20260729-184037-2556x1317-combat-enemy.png |
| combat | ally | 3440x1440 | no | nominal | Combat inspector: PASS · card=388.1×701.3; contained=True; weapon facts=4; properties=1; passives=1; capture=offscreen-panel-fallback | ui-qa-20260729-184037-3440x1440-combat-ally.png |
| combat | stress | 1600x900 | no | 130% | Combat inspector: PASS · card=388×828; contained=True; weapon facts=5; properties=2; passives=2; capture=offscreen-panel-fallback | ui-qa-20260729-184037-1600x900-combat-stress-expanded.png |
