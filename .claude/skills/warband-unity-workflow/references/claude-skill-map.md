# Warband Claude skill map

Read only the source skills that match the task. Paths are relative to the Warband repository root.

## Warband-specific (read these before the generic Unity guides)

| Task | Source skill |
|---|---|
| Combat visuals — spells, weapon attacks, fields, statuses, deaths; tells, recipes, shaders | `.claude/skills/spell-fx/SKILL.md` — owns the determinism laws and verification gate; the generic `unity-lighting-vfx` is background only |
| Generated portraits, icons, weapon art, VFX source images, textures, and art curation | `.claude/skills/warband-art-pipeline/SKILL.md` |
| UI concepts, measured mockups, inbox/outbox review, and approval/acceptance gates | `.claude/skills/warband-ui-review/SKILL.md`; read its fidelity reference before specification or implementation |
| Sound effects, audio mix, clip baking, the audition sheet, combat voice budget | `.claude/skills/warband-audio/SKILL.md` |
| Balance tuning, encounter/enemy composition, act difficulty, boss pose spread | `.claude/skills/warband-encounter-probes/SKILL.md` |
| Building, publishing, launcher, download site | `.claude/skills/warband-ship/SKILL.md` — confirm-first, outward-facing |
| Research → evaluate → recommend passes, and DESIGN-stage roadmap items | `.claude/skills/warband-research-pass/SKILL.md` |
| Unattended overnight or away-from-home blocks | `.claude/skills/warband-overnight-handoff/SKILL.md` |
| Fanning out subagents and reviewing their work | `.claude/skills/warband-subagent-orchestration/SKILL.md` |

## Generic Unity guides

| Task | Source skill |
|---|---|
| C# runtime scripts, MonoBehaviour, coroutines, ScriptableObjects | `.claude/skills/unity-scripting/SKILL.md` |
| Unity components, prefabs, GameObjects, core project structure | `.claude/skills/unity-foundations/SKILL.md` |
| Scene, prefab, Addressables, or generated asset architecture | `.claude/skills/unity-scene-assets/SKILL.md` |
| Inspectors, EditorWindows, property drawers, or editor automation | `.claude/skills/unity-editor-tools/SKILL.md` |
| UI Toolkit, UXML/USS, HUDs, menus | `.claude/skills/unity-ui/SKILL.md`; add `unity-ui-patterns` only for navigation/view architecture, not routine styling |
| New Input System, rebinding, multiplayer input | `.claude/skills/unity-input/SKILL.md`; add `unity-input-correctness` for correctness risk |
| Rigidbody, colliders, CharacterController, collision | `.claude/skills/unity-physics/SKILL.md` |
| Raycasts, overlap checks, LayerMask, physics query selection | `.claude/skills/unity-physics-queries/SKILL.md` |
| Materials, URP, shaders, rendering | `.claude/skills/unity-graphics/SKILL.md` |
| Lighting, particles, VFX Graph | `.claude/skills/unity-lighting-vfx/SKILL.md` |
| Audio engine APIs, mixers, spatial audio | `.claude/skills/unity-audio/SKILL.md` |
| Animator, clips, blend trees, Timeline | `.claude/skills/unity-animation/SKILL.md` |
| Cinemachine cameras | `.claude/skills/unity-cinemachine/SKILL.md` |
| Navigation, NavMesh, Sentis | `.claude/skills/unity-ai-navigation/SKILL.md` |
| NPC behavior and perception | `.claude/skills/unity-npc-behavior/SKILL.md` |
| Game flow and progression | `.claude/skills/unity-game-loop/SKILL.md` |
| State-machine design | `.claude/skills/unity-state-machines/SKILL.md` |
| Architecture and service/component boundaries | `.claude/skills/unity-game-architecture/SKILL.md` |
| ScriptableObject/JSON data pipelines | `.claude/skills/unity-data-driven/SKILL.md` |
| Async, Awaitable, coroutines, cancellation | `.claude/skills/unity-async-patterns/SKILL.md` |
| Profiling and performance | `.claude/skills/unity-performance/SKILL.md` |
| Edit Mode, Play Mode, and NUnit tests | `.claude/skills/unity-testing/SKILL.md` |
| Save/load and PlayerPrefs | `.claude/skills/unity-save-system/SKILL.md` |
| Procedural generation | `.claude/skills/unity-procedural-gen/SKILL.md` |
| Platforms and build targeting | `.claude/skills/unity-platforms/SKILL.md` |
| Package Manager and Unity services | `.claude/skills/unity-packages-services/SKILL.md` |
| Coordinate spaces, Quaternion, Vector3 | `.claude/skills/unity-3d-math/SKILL.md` |
| Level and encounter layout | `.claude/skills/unity-level-design/SKILL.md` |
| Component lifetime and editor/runtime initialization | `.claude/skills/unity-lifecycle/SKILL.md` |
