# Responsive UI QA

- Run: `20260727-185533`
- Matrix: `smoke`
- Captures: 15
- Pixel captures require human review; structural layout is the automated gate.
- Captures marked `offscreen-panel-fallback` were rendered to the exact target without opening or focusing a Game View window.

| Surface | Fixture | Viewport | Phone | Copy stress | Layout | Capture |
|---|---|---:|:---:|:---:|---|---|
| workbench | market-recruit | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1280x720-workbench-market-recruit.png |
| workbench | market-rankup-long | 1280x720 | no | 130% | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1280x720-workbench-market-rankup-long-expanded.png |
| workbench | armory-full | 1280x720 | no | nominal | Workbench QA: FAIL · Workbench: FAIL · wb-trait-chip--inline 1 escapes decision-body [92.5,623.8–230,656.3] vs [37.5,353.8–762.5,655]; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1280x720-workbench-armory-full.png |
| workbench | rail-full | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1280x720-workbench-rail-full.png |
| workbench | tooltip-keyword | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1280x720-workbench-tooltip-keyword.png |
| workbench | tooltip-equipment | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1280x720-workbench-tooltip-equipment.png |
| workbench | market-recruit | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1600x900-workbench-market-recruit.png |
| workbench | market-rankup-long | 1600x900 | no | 130% | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1600x900-workbench-market-rankup-long-expanded.png |
| workbench | armory-full | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1600x900-workbench-armory-full.png |
| workbench | rail-full | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1600x900-workbench-rail-full.png |
| workbench | tooltip-keyword | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1600x900-workbench-tooltip-keyword.png |
| workbench | tooltip-equipment | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1600x900-workbench-tooltip-equipment.png |
| wager | wager-nominal | 1280x720 | no | nominal | Wager: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1280x720-wager-wager-nominal.png |
| deploy | deploy-nominal | 1280x720 | no | nominal | Deploy: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1280x720-deploy-deploy-nominal.png |
| result | result-nominal | 1280x720 | no | nominal | Result gate: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-185533-1280x720-result-result-nominal.png |
