# Responsive UI QA

- Run: `20260727-200859`
- Matrix: `smoke`
- Captures: 15
- Live rank-up regression: PASS · pending B fork safely defers the following A-rank Market preview
- Pixel captures require human review; structural layout is the automated gate.
- Captures marked `offscreen-panel-fallback` were rendered to the exact target without opening or focusing a Game View window.

| Surface | Fixture | Viewport | Phone | Copy stress | Layout | Capture |
|---|---|---:|:---:|:---:|---|---|
| workbench | market-recruit | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1280x720-workbench-market-recruit.png |
| workbench | market-rankup-long | 1280x720 | no | 130% | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1280x720-workbench-market-rankup-long-expanded.png |
| workbench | armory-full | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1280x720-workbench-armory-full.png |
| workbench | rail-full | 1280x720 | no | nominal | Workbench QA: FAIL · Workbench: FAIL · semantic-text--interactive 0 escapes decision-body [536.3,647.5–905,711.3] vs [37.5,580–1358.8,695]; semantic-text--interactive 0 runs behind actions; semantic-text--interactive 1 escapes decision-body [977.5,638.8–1345,702.5] vs [37.5,580–1358.8,695]; semantic-text--interactive 1 runs behind actions; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1280x720-workbench-rail-full.png |
| workbench | tooltip-keyword | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1280x720-workbench-tooltip-keyword.png |
| workbench | tooltip-equipment | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1280x720-workbench-tooltip-equipment.png |
| workbench | market-recruit | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1600x900-workbench-market-recruit.png |
| workbench | market-rankup-long | 1600x900 | no | 130% | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1600x900-workbench-market-rankup-long-expanded.png |
| workbench | armory-full | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1600x900-workbench-armory-full.png |
| workbench | rail-full | 1600x900 | no | nominal | Workbench QA: FAIL · Workbench: FAIL · semantic-text--interactive 0 escapes decision-body [536,647–904,711] vs [37,579–1359,695]; semantic-text--interactive 0 runs behind actions; semantic-text--interactive 1 escapes decision-body [976,638–1345,702] vs [37,579–1359,695]; semantic-text--interactive 1 runs behind actions; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1600x900-workbench-rail-full.png |
| workbench | tooltip-keyword | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1600x900-workbench-tooltip-keyword.png |
| workbench | tooltip-equipment | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1600x900-workbench-tooltip-equipment.png |
| wager | wager-nominal | 1280x720 | no | nominal | Wager: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1280x720-wager-wager-nominal.png |
| deploy | deploy-nominal | 1280x720 | no | nominal | Deploy: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1280x720-deploy-deploy-nominal.png |
| result | result-nominal | 1280x720 | no | nominal | Result gate: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-200859-1280x720-result-result-nominal.png |
