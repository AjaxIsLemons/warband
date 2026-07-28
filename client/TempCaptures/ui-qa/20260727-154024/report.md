# Responsive UI QA

- Run: `20260727-154024`
- Matrix: `smoke`
- Captures: 13
- Pixel captures require human review; structural layout is the automated gate.
- Captures marked `offscreen-panel-fallback` were rendered to the exact target without opening or focusing a Game View window.

| Surface | Fixture | Viewport | Phone | Copy stress | Layout | Capture |
|---|---|---:|:---:|:---:|---|---|
| workbench | market-recruit | 1280x720 | no | nominal | Workbench QA: FAIL · Workbench: FAIL · wb-inspector__line-copy 1 clips wrapped text (60 > 53.8); Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1280x720-workbench-market-recruit.png |
| workbench | market-rankup-long | 1280x720 | no | 130% | Workbench QA: FAIL · Workbench: FAIL · wb-choice-preview__rule 0 clips wrapped text (78.8 > 76.3); wb-choice-preview__rule 1 clips wrapped text (78.8 > 76.3); wb-inspector__line-copy 0 clips wrapped text (78.8 > 55); Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1280x720-workbench-market-rankup-long-expanded.png |
| workbench | armory-full | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1280x720-workbench-armory-full.png |
| workbench | tooltip-keyword | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1280x720-workbench-tooltip-keyword.png |
| workbench | tooltip-equipment | 1280x720 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1280x720-workbench-tooltip-equipment.png |
| workbench | market-recruit | 1600x900 | no | nominal | Workbench QA: FAIL · Workbench: FAIL · wb-inspector__line-copy 1 clips wrapped text (59 > 54); Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1600x900-workbench-market-recruit.png |
| workbench | market-rankup-long | 1600x900 | no | 130% | Workbench QA: FAIL · Workbench: FAIL · wb-choice-preview__rule 0 clips wrapped text (78 > 76); wb-choice-preview__rule 1 clips wrapped text (78 > 76); wb-inspector__line-copy 0 clips wrapped text (78 > 54); Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1600x900-workbench-market-rankup-long-expanded.png |
| workbench | armory-full | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1600x900-workbench-armory-full.png |
| workbench | tooltip-keyword | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1600x900-workbench-tooltip-keyword.png |
| workbench | tooltip-equipment | 1600x900 | no | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; Permanent warband rail: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1600x900-workbench-tooltip-equipment.png |
| wager | wager-nominal | 1280x720 | no | nominal | Wager: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1280x720-wager-wager-nominal.png |
| deploy | deploy-nominal | 1280x720 | no | nominal | Deploy: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1280x720-deploy-deploy-nominal.png |
| result | result-nominal | 1280x720 | no | nominal | Result gate: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-154024-1280x720-result-result-nominal.png |
