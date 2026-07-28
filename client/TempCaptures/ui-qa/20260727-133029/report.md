# Workbench UI QA

- Run: `20260727-133029`
- Matrix: `smoke`
- Captures: 10
- Pixel captures require human review; structural layout is the automated gate.
- Captures marked `offscreen-panel-fallback` were rendered to the exact target without opening or focusing a Game View window.

| Fixture | Viewport | Copy stress | Layout | Capture |
|---|---:|:---:|---|---|
| market-recruit | 1280x720 | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-1280x720-market-recruit.png |
| market-rankup-long | 1280x720 | 130% | Workbench QA: FAIL · Workbench: FAIL · wb-inspector__line-copy 0 clips wrapped text (49.5 > 42); Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-1280x720-market-rankup-long-expanded.png |
| armory-full | 1280x720 | nominal | Workbench QA: FAIL · Workbench: FAIL · wb-inspector__line-copy 0 clips wrapped text (99 > 42); wb-inspector__line-copy 1 clips wrapped text (132 > 42); wb-inspector__line-copy 2 clips wrapped text (132 > 42); Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-1280x720-armory-full.png |
| tooltip-keyword | 1280x720 | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-1280x720-tooltip-keyword.png |
| tooltip-equipment | 1280x720 | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-1280x720-tooltip-equipment.png |
| market-recruit | 2558x1313 | nominal | Workbench QA: FAIL · Workbench: FAIL · wb-inspector__line-copy 0 clips wrapped text (649.9 > 17.3); wb-inspector__line-copy 1 clips wrapped text (957.6 > 17.3); wb-inspector__line-copy 2 clips wrapped text (957.6 > 17.3); Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-2558x1313-market-recruit.png |
| market-rankup-long | 2558x1313 | 130% | Workbench QA: FAIL · Workbench: FAIL · wb-inspector__line-copy 0 clips wrapped text (51.8 > 41.6); Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-2558x1313-market-rankup-long-expanded.png |
| armory-full | 2558x1313 | nominal | Workbench QA: FAIL · Workbench: FAIL · wb-inspector__line-copy 0 clips wrapped text (85.6 > 41.6); wb-inspector__line-copy 1 clips wrapped text (120.1 > 41.6); wb-inspector__line-copy 2 clips wrapped text (120.1 > 41.6); Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-2558x1313-armory-full.png |
| tooltip-keyword | 2558x1313 | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-2558x1313-tooltip-keyword.png |
| tooltip-equipment | 2558x1313 | nominal | Workbench QA: PASS · Workbench: PASS; Runtime tooltip: PASS; capture=offscreen-panel-fallback | ui-qa-20260727-133029-2558x1313-tooltip-equipment.png |
