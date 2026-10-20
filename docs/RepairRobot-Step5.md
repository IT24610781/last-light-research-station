# Repair Robot — Step 5 implementation and handoff

## What is implemented

The Repair Robot compares eligible allied robots using missing-health fraction, returned route length and observed danger. A challenger must beat the current target by the editable switch margin. Full-health, dead, disabled and self targets are rejected. The robot requests an approach position beside its ally, sends route points to Member B's AgentMotor, and restores health gradually only inside repair range with clear line of sight. It never attacks the player.

Movement failures and unreachable targets enter a timed cooldown; other allies can be selected and blocked targets are retried. Immediate danger interrupts repair and makes the robot compare reachable support positions. Without work it returns to or waits at support. Death and pause stop repair. All target/support utility weights are editable.

## Current navigation limitation — do not claim final A* integration

Member A's A* provider is not in this checkout. RepairRoutePlanner accepts a MonoBehaviour implementing the existing LastLight.AI.Movement.IPathProvider interface through its Path Provider Inspector field. Until assigned, it uses collision-checked direct routes: it rejects obstructed routes rather than walking through walls. This fallback does not find detours and explicitly rejects significant floor-height changes.

AgentMotor is Member B's existing implementation. It currently follows horizontal waypoints with gravity. The two-floor research-station stair routes still need joint testing and any required motor/path-provider improvements by their owners. The completed regression scenarios are for the current flat PlayerTest room, not proof that the final two-floor level works.

## Objects in PlayerTest

RepairRobot retains Health, RepairTargetSelector, AgentMotor, CharacterController, RepairRobotMovement and RepairRobotRepair. Added RepairRobotSensor and RepairRoutePlanner. Its two assigned allies remain Ally_A and Ally_B. HealthTester remains on test allies, never on the player.

RepairSupportPositions contains Support_1, Support_2 and Support_3. They are known support locations in the test room, not guaranteed safe points in the future environment. Adjust these to cover/service positions when the level is integrated. Runtime reachability and observed danger are checked before choosing a point. Starting position is also considered as home.

## How to demonstrate

1. Open PlayerTest and choose Last Light > Repair Robot Debug. Enter Play mode.
2. Select RepairRobot or assign its RepairTargetSelector in the debug window.
3. Damage an ally using its HealthTester context menu. Watch utility contributions, chosen target, route provider and movement state.
4. Robot approaches and heals at 20 health/second, in small accumulated intervals. Health clamps at maximum.
5. Put a collider between robot and ally: no repair through the obstruction. Checked direct routing may temporarily reject this ally; unblock it and let the retry timer expire.
6. Heal or kill the selected ally: another eligible target is considered. Disabled targets are excluded too.
7. Move the player into the robot's view and within its immediate-danger radius (default 3m), or damage the robot: repair stops and it retreats to a reachable support position. If all supports are blocked, it holds and retries.
8. Hide the player behind a wall: live hidden movement does not update remembered threat position. Memory expires after four seconds by default. Heard sounds record their source location with distance attenuation and wall muffling; they do not reveal the player's live transform.
9. Once danger expires and allies are healthy, it waits at a reachable support position.

The allowed allied-health information is the explicit allied telemetry list in the Inspector. The robot is not using the player's hidden health or hidden position to rank targets. Only its sensor's confirmed sightings and audible events influence danger. Test Danger is ignored unless Use Test Danger is explicitly enabled; leave it off in normal gameplay.

## Team interfaces

- Member A: supply a MonoBehaviour implementing IPathProvider.TryFindPath(start, goal, pathOut, out pathLength), returning a complete valid route and length, false when unreachable. Assign it on RepairRoutePlanner. Goal is a collision-free repair/support position, not the ally's occupied collider centre.
- Member B: AgentMotor.SetPath consumes points; Status reports Moving, Arrived, Blocked or Stopped. FaceTowards handles repair orientation. RepairRobotRepair.IsRepairing, RepairingTarget and RepairStateChanged(bool) expose animation/beam state. No Animator/model dependency is required for intelligence tests.
- User: RepairTargetSelector owns decisions, RepairRobotSensor owns observations, RepairRobotMovement connects movement/support behaviour, RepairRobotRepair owns the actual health effect.

## Development display and testing

Last Light > Repair Robot Debug shows candidate eligibility/rejection reasons, injury/route/danger contributions, selected target, utility margin, observations, route-provider label, repair state and support scores. This is editor UI, not a finished player HUD.

Last Light > Run Repair Robot Verification starts or uses Play mode and creates an isolated temporary test fixture away from the normal scene. Results appear in Console after approximately 35 seconds. Stop Play mode afterward. The tests are editor-only scenario checks, not NUnit Test Runner tests. They do not modify saved scene health or place permanent runtime objects.

Verification covers healthy/self/dead/disabled exclusion; utility stability; injected route cost; blocked target cooldown and retry; gradual health restoration; wall, range, pause and death restrictions; actual AgentMotor approach; vision; hidden-position memory; memory expiry; audible noise; retreat and support waiting. The passing report is RepairRobot-verification.json.

## Tooling and Git

Unity's official com.unity.pipeline editor package was added to configure the scene and run verification. The official Unity CLI was installed locally; teammates do not need the CLI to play the scene or open the debug window.

Existing .meta GUIDs and unrelated gameplay code were preserved. Changes are on feature/repair-robot-ai, uncommitted for review. Include new script/folder .meta files, PlayerTest, package manifest/lock and these docs when committing. Do not commit Library or temporary verification objects.
