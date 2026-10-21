# Tactical Guard integration

The combat/security robot uses the downloaded CYBORG SOLDIER RX-1500 model.
The existing TacticalGuardAgent, GuardSensor, Health, CharacterController and
AgentMotor remain authoritative. The new presentation component only selects
animation states and displays shot feedback.

## Assets

- Prefab: `Assets/LastLight/Prefabs/AI/TacticalGuard.prefab`
- Scene: `Assets/LastLight/Scenes/GuardTest.unity`
- Models: `Assets/LastLight/Models/TacticalGuard/`
- Materials: `Assets/LastLight/Materials/TacticalGuard/`
- Controller: `Assets/LastLight/Animations/TacticalGuard/TacticalGuard.controller`
- Source Blender file: `AI Chat/output/robots/guard_ready/TacticalGuard_Rigged.blend`

The model is approximately 1.9 m tall. The root retains the existing centered
2 m CharacterController; RobotVisual is offset down by 1 m. The imported rig
retains the source skin weights, bone-parented armor, fingers and rifle.
Idle and Walk loop. Attack, Reload and Death are non-looping clips.
Actual attack events restart recoil and briefly display the tracer.
Root motion is disabled.

GuardTest retains its existing navigation provider and two retreat points as
scene overrides. New prefab instances need their own navigation provider and
retreat positions configured for the destination scene. The prefab otherwise
uses the existing straight-line fallback. Full ResearchStation deployment and
level-specific cover/route tuning are separate integration work.

## Verification — 21 October 2026

- Existing Edit Mode suite: 36 passed, 0 failed.
- Live TacticalGuardPresentationTests: 13 passed, 0 failed.
- Verified idle, skin/armor hierarchy, measured-speed walking, moving leg joints,
  actual damage and ammunition use, shot animation/tracer, reload, wall occlusion,
  pause, death final-pose hold, doorway passage and solid-wall collision.
- Door fixture: 1.3 m clear width, 2.2 m clear height.
- No missing scripts or Unity console errors after the run.
- Final Unity screenshot: `guard_ready/TacticalGuard_Unity.png`.

To repeat the live suite, enter Play Mode, add TacticalGuardPresentationTests to
an empty GameObject, and read its final Console report. Its test fixtures are
created away from the level and removed when complete. The component is editor
only and is not attached to the production prefab.

## Source provenance

This is an adaptation of CYBORG SOLDIER RX-1500, not an original model authored
from scratch. The downloaded files inspected did not include the CGTrader
listing/license evidence. Retain that evidence with the project's asset records
before distributing the game or sharing source assets outside the project team.
