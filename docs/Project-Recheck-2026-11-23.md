# Project recheck — 23 November 2026

## Current result — 24 November 2026

The remaining relay and Director wiring is implemented in the review checkout. Fresh EditMode tests passed **44/44**. Live repair scenarios passed **41/41**, station combat/progression checks passed **11/11**, and navigation/Director checks passed **6/6**. Evidence is preserved in [verification/recheck-2026-11-24](verification/recheck-2026-11-24).

Verified in Unity 6000.3.25f1:

- Both relays begin red, require a two-second hold, and turn green. Relay A permits yellow-door access while evacuation remains locked; both relays allow a living player to reach the victory state.
- The station starts unarmed. Actual Guard attacks reduced player health from 100 to 52; Hunter attacks reduced it from 100 to 70.
- Repair checks cover target utility and stability, route costs, approach, range, line of sight, healing limits, danger response, and immediate pause/death cancellation. They prohibit self-repair and resurrection.
- The motor physically traversed a 23.7 m stair route to the upper floor and reported Arrived near (0.03, 4.08, -8.25).
- The Director is connected to player health, mission progress, reachable service bays and four inactive Hunter reserves. It counts the neutral Service Robot toward the five-robot cap, activates a real reserve Hunter when eligible, and does not spawn during pause.
- The restored scene has zero missing scripts. The current Console reports no compilation failure and zero errors. Historical failed fixture assertions remain in the diagnostic log; they precede the corrected passing run.

Implementation also restores an upright NavMesh bake, corrects zero-thickness floor colliders, removes the duplicate Hunter dummy visual, includes inactive reserves in health-marker setup, and keeps route endpoints on the sampled NavMesh. Temporary recovery scene and intermediate navigation bakes were removed. The ResearchStation scene is open in Edit Mode; temporary run-in-background setting was restored.

**Verification limits:** combat and progression were tested by controlled runtime positioning and calls to the real gameplay components. Stair traversal used the real motor and collisions. This is not a continuous first-person Arrival-to-Evacuation walkthrough, a visual-art approval, or a standalone-build test. The scene currently uses the NavMesh-backed IPathProvider adapter; Member A's custom A* source remains in the project, but these checks do not demonstrate its use in the station.

All changes are local on `feture/recheck`; no commit, push or merge was performed. Original working checkout was left untouched. Team ownership remains documented in [Team-Ownership.md](Team-Ownership.md).

## Initial audit (historical)

The findings and license failure below describe the checkout before the repair pass; the verified result above supersedes the initial blocked status.

## Checkout and scope

- Requested branch: `feture/recheck` (spelling preserved).
- Base: freshly fetched `origin/dev`, commit `1082962b002c63160a236918434b187b5ca6da14`.
- Latest dev commit: merge of README PR #33, dated 5 November 2026.
- Review checkout: `C:/Projects/GV + IS/AI Chat/recheck`; open its `Game` folder in Unity Hub.
- Original checkout and its uncommitted changes remain untouched at `C:/Projects/GV + IS/last-light-research-station`.
- The initial inventory changed no gameplay files. The subsequent repair pass changes gameplay files as summarized above. It inventories source, scenes, prefabs, asset GUID references, LFS content, packages, test configuration and differences from earlier integrated work. It includes targeted source review of repair, combat, service patrol, player controls and mission progression. It is not an exhaustive line-by-line correctness proof or a successful live playthrough.

## Findings, in priority order

### 1. Critical: the tracked main scene lacks the integrated mission and robot setup

The saved `ResearchStation.unity` directly references the tracked `FlickerLight`, `StationSlidingDoor` and `StationMedkit` scripts. Its only resolvable source-prefab asset from the checkout is `Player.prefab`. The other 20 distinct source-prefab GUIDs are unresolved in the checkout.

The scene contains no tracked Repair/Hunter/Guard/Service AI prefab references and no direct references to their agent scripts. It also contains no direct references to `StationProgressionManager`, `StationRelay`, `StationEvacuationTerminal`, `StationWeaponPickup` or `GameSessionController`. The Player prefab does not supply these mission systems. Source searches found no gameplay bootstrap that assembles them at startup. Object names such as “F1 Encounter — Sound Hunter” and “Relay A” do not prove that functional components are present.

**Impact:** the fresh dev checkout cannot be considered equivalent to the integrated station previously tested locally. Robot models and AI source still exist in the repository, but their presence alone does not integrate them into the main scene. Pickup-based weapons, relay progression and victory need to be wired back into the current level.

**Recovery:** retain the current environment/NavMesh changes and deliberately reapply gameplay roots and references. Do not replace the entire scene with the old local scene: that risks losing newer level work. Verify all robot instances, player references, navigation providers, pickups, relays, UI/session controller and evacuation terminal in Unity.

### 2. Critical: fresh checkout is missing environment dependencies

The serialized-reference audit found **41 distinct referenced GUIDs** that resolve to files under the original workspace's untracked `Game/Assets/Creepy_Cat` folder but are absent from this Git checkout. Examples include corridor lights, crates, wall-console pieces, walls and door prefabs from `3D Scifi Kit Starter Kit_HD`.

**Impact:** teammates or reviewers cloning Git alone will not receive those environment dependencies. This is not an LFS pointer issue: the 41 tracked LFS entries were materialized in the new checkout.

**Recovery:** establish an agreed, license-compliant method of supplying the exact asset package and preserve its original GUIDs. Do not blindly publish third-party source assets in the public repository. Document any required package installation. Then recheck missing prefab, mesh and material references in Unity.

The scan also found 11 unresolved rendering/profile GUIDs after comparison with the old package cache. These are **unconfirmed**, because package/internal Editor resources can resolve references outside project Assets. Recheck them after successful package import; do not label all of them missing user assets.

### 3. High: door locking logic has regressed

`Game/Assets/LastLight/Scripts/Interaction/StationSlidingDoor.cs`, `Interact()`, directly toggles `open`. It does not call `StationProgressionManager.CanOpen(requiredAccess)`.

All **10** serialized `requiredAccess` values in ResearchStation are `0` (`None`). The enum now also contains `RelayB`, but `StationProgressionManager.CanOpen()` has no explicit RelayB case and returns true through its default branch.

**Impact:** relay-based access requirements are neither configured on the saved doors nor enforced by interaction code. The existing `StationProgressionTests` exercises the manager's predicates, not interaction with an actual locked door, so it does not cover this regression.

**Recovery:** restore access checks in door interaction, configure each gate to the intended requirement, define RelayB behavior, and add tests invoking the door's actual interaction before and after relay activation.

### 4. High: verified local Repair Robot fixes are not in dev

The new branch contains the older Repair Robot implementation:

- `RepairTargetSelector` has no support/report radius.
- `RepairRobotRepair.Update()` returns on global pause without clearing `IsRepairing` and its target.
- The immediate health-event cancellation changes and expanded scenarios from 22 October are absent.

The original dirty workspace still holds the changed selector, sensor, movement, repair, debug display and scenario files. Its scene also contains local ally-roster changes. These were explicitly left unpushed previously.

**Recovery:** port the focused code/test changes, review them against current dev, and integrate scene references separately. Rerun the expanded scenarios. Do not overwrite current dev wholesale with the dirty workspace.

### 5. Medium: previous presentation and navigation work is missing from dev

Compared with earlier integrated commit `c61a3c5`, latest dev removes:

- `Scripts/Navigation/NavMeshPathProvider.cs`
- `Scripts/UI/RobotHealthHud.cs`
- `Scripts/Atmosphere/StationAudioDirector.cs`

It also changes relay feedback, mission status messages and session UI. The saved scene has a baked NavMesh asset, but that does not by itself supply an `IPathProvider` adapter to the agents. Where no provider is assigned, the agent implementations use their documented fallback behavior.

**Recovery:** reconcile intended functionality with these removals. Restore or replace each necessary feature explicitly, then validate route execution and presentation in the current scene.

### 6. Medium: README and prior test results describe a different state

The README advertises the 25m Repair report radius and presentation systems that are absent from the fetched code. Its historical 41 AI / 11 presentation / 38 EditMode results were obtained on the older local integrated workspace, not this fresh checkout. Keep historical evidence clearly scoped and update the README after integration is restored.

## Systems still present in source

- Player movement, sprint, crouch, jump, collision, look and noise generation.
- Shared health, pistol/rifle shooting, ownership, ammunition and reload handling. PlayerShooter starts with no equipped weapon; the scene must supply pickups.
- Carry/drop/throw interactions and impact noise.
- Utility Guard, model-based Hunter, Repair utility logic and Service patrol implementation.
- Encounter Director source and an AgentTest scene with Hunter/Director components.
- Shared AgentMotor and waypoint/A* navigation code.
- Relay/progression/evacuation scripts, reusable robot prefabs and four robot model families.
- Five tracked scene files, test code, editor verification tools, Blender/FBX assets and Git LFS configuration.

These are inventory observations, not new runtime pass claims. The scene integration problems above explain why source/model completeness does not imply a complete playable game.

## Verification performed

| Check | Result |
| --- | --- |
| Fetch current origin/dev | Successful; advanced local reference from 974477f to 1082962 |
| Create feture/recheck from origin/dev | Successful; HEAD exactly matches fetched dev |
| Preserve original dirty workspace | No stash/reset/checkout or overwrite performed there |
| LFS checkout | 41 entries reported materialized (`*`) by git lfs ls-files |
| Asset reference inventory | 323 project asset GUIDs; 41 confirmed local-only environment dependencies; 11 additional rendering references require Editor verification |
| Main scene serialized review | Missing tracked mission/robot setup; ten unrestricted doors |
| Source diff against earlier integrated commit | Identified removed presentation/navigation features |
| git diff --check before report | Clean |
| Unity EditMode execution | **Blocked before import/compilation by license failure** |

Unity 6000.3.25f1 was launched in batch mode against this checkout. It exited with code **198**, reporting: `No valid Unity Editor license found. Please activate your license.` No test results XML was produced. This is not a test failure or test pass; no compilation or gameplay validation completed.

Diagnostic files outside the repository:

- `C:/Projects/GV + IS/AI Chat/output/recheck-unity.log`
- `C:/Projects/GV + IS/AI Chat/output/recheck-assets.json`

## Original repair plan (completed items retained for audit history)

1. Restore a valid Unity Editor license through Unity Hub.
2. Supply the missing environment dependencies and verify the fresh checkout imports.
3. Reintegrate mission and robot components into the current ResearchStation level, preserving newer environment changes.
4. Fix actual door interaction and serialized gate requirements; test the full two-relay escape route.
5. Port the preserved Repair Robot fixes and expanded scenarios.
6. Reconcile navigation, HUD and audio regressions; test both floors and stairs.
7. Run EditMode tests, custom live AI/presentation scenarios, and a complete playable run from Arrival to victory.
8. Update documentation to match the verified result, then commit focused changes on this branch and submit a PR into dev.

The branch is local and initially tracks origin/dev. No push or merge was performed during this review. For a later first push, explicitly use `git push -u origin feture/recheck` so its upstream becomes the matching feature branch.


