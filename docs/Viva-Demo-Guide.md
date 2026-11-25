# Viva demo guide — Last Light

Open `Game` in Unity 6000.3.25f1 and open `Assets/LastLight/Scenes/ResearchStation.unity`. Press Play, then **ENTER STATION**.

## Shared controls and demo tools

| Key / menu | Use |
| --- | --- |
| WASD, mouse, Shift, Space, hold C | Move, look, sprint (makes noise), jump, crouch |
| E | Use / pick up / drop. **Hold E** at a relay for 2 s |
| Q | Throw the held prop (makes an impact noise) |
| Left mouse, R, 1, 2 | Fire, reload, rifle, pistol |
| Esc | Pause (lists the analysis keys) |
| M | Enlarge / shrink the station map (bottom right). It shows your floor, your position and facing, relay and exit state, and a red dot for any robot **while it is firing** |
| **F1 / F2 / F3 / F4** | Show the decision panel for **Hunter / Guard / Repair / Director**. Off during normal play |
| Last Light → Demo → Go To … | Play mode only: puts the player in front of a demo location (Armory, Relay A/B, Hunter, Guard, Repair Robot, Medical, crate, Evacuation) |
| Last Light → Quality → … | Audit, gameplay check, smoke test, review screenshots |

Every door has an illuminated name plate above it on the corridor side (amber = needs Relay A, red = needs both relays).

Mission: Armory (take a gun) → Relay A in Power (opens the yellow door to the Main Lab) → upstairs → Relay B in Main Lab → red Evacuation door opens → walk into the evacuation bay or use the evacuation console. Either relay order works.

## Member A — World Builder, Tactical Guard, shared A*

**Show:** the two-floor station (lighting, kit textures, baked NavMesh); the relay console model; relays and locked doors; the Guard's utility decisions.

1. Demo → Go To Tactical Guard (Security). Press **F2**. Let the Guard see you: the panel shows Attack / TakeCover / Reload / Retreat / Hold scores, the chosen action and the reason.
2. Shoot the Guard: its injury raises Cover/Retreat scores; empty magazine raises Reload.
3. Explain hysteresis: an action is held at least **0.6 s** and a challenger must beat it by **0.15** (`GuardUtilityBrain`).
4. A*: open `GuardTest` (uses `WaypointNavigationService`). `PathFinder.FindPath` uses f = g + h with straight-line h; with `useHeuristic = false` it is Dijkstra. `PathFinderTests` compares both.

Weights (`GuardWeights`): Attack = 0.30 health + 0.25 ammo + 0.15 proximity + 0.30 covered; Cover = 0.55 exposure + 0.20 injury + 0.25 cover quality; Reload = 0.60 missing ammo + 0.40 safety; Retreat = 0.60 injury + 0.20 exposure + 0.20 quality; Hold = 0.10.

Code: `Scripts/AI/Guard`, `Scripts/Navigation`, `StationProgressionManager`, `StationRelay`, `StationSlidingDoor`.
The station robots use the NavMesh adapter behind the same `IPathProvider` interface; say this honestly if asked.

## Systems Engineer — player, guns, physics, HUD, Repair Robot

**Show:** first-person controls; Armory pickup; rifle (12 rounds, 0.15 s, automatic, 2 s reload) and pistol (8 rounds, 0.35 s, 1.4 s reload) with separate magazines; doors; grab/drop/throw crates and the barricade; medkits; HUD; pause/defeat/victory/restart.

1. Demo → Go To Armory Bench → E takes the gun (prompt "[E] TAKE RIFLE"). Fire, reload, switch with 1/2.
2. Demo → Go To Throwable Crate → E to lift, Q to throw: the impact emits a noise event (`ImpactNoise` → `NoiseSystem`).
3. Repair Robot: Demo → Go To Repair Robot, press **F3**. Damage a Guard (shoot it), then watch the Repair Robot choose it, travel and repair.

Repair utility (`RepairTargetSelector`): **U = 100 × missing-health fraction − 2 × route metres − 30 × observed danger**. Switch only if a challenger beats the current target by more than **15**. Team reports only within **25 m**; repair range **2 m**, **20 HP/s**, line of sight required, no self-repair, no resurrection; pause/death cancel repair. Evidence: Last Light → Run Repair Robot Verification (41/41).

There are two Repair Robots: **Workshop** (upper floor, supports the Tactical Guard) and **Storage** (ground floor, supports the Sound Hunter). Shoot the Hunter in Storage and the Storage Repair Robot selects it and repairs it.

HUD map (`Scripts/UI/StationMinimap.cs`, `StationMapLayout.cs`): it subscribes to the Guard's and Hunter's existing `AttackPerformed` events, so it never reads AI internals and never reveals robots that are not shooting.

Code: `Scripts/Player`, `Scripts/Combat`, `Scripts/Interaction`, `Scripts/UI`, `Scripts/AI/Repair`, `Editor/Repair`.

## Member B — Agent Controller, Sound Hunter, Encounter Director

**Show:** smooth movement/turning/animation (`AgentMotor`), the Hunter's memory-based decisions and the Director.

1. Demo → Go To Sound Hunter (Storage), press **F1**. Throw a crate down the room: the panel lists each remembered noise with strength S, recency R and route cost C, and the Hunter switches to **Investigate**.
2. Let it see you, then hide behind a wall: it searches the **last seen position**; it does not track your hidden position.
3. Director: press **F4**. In the station it shows "Active cap reached (5/5)" because Hunter, Guard, two Repair Robots and the Service robot are alive (plan: never more than 5). Destroy one robot and it can activate a reserve Hunter at a hidden service bay after the **20 s** cooldown. `AgentTest` shows the Director with more freedom.

Hunter noise score: **0.5 × strength + 0.3 × recency − 0.2 × normalised route cost** (switch margin 0.15, hearing 20 m, memory 15 s, last-seen 10 s).
Director limits: 5 active robots, 4 activations per run, 20 s cooldown, 10 m minimum distance, no bay in player view; waiting is always a candidate.

Code: `Scripts/AI/Hunter`, `Scripts/AI/Director`, `Scripts/AI/Movement`; tests `Tests/Editor/HunterBrainTests.cs`, `DirectorBrainTests.cs`.

## Service robot (supporting feature, not an assessed agent)

The white drone in Crew Quarters / Main Lab is a neutral maintenance robot (`Scripts/AI/Service/ServiceRobotPatrol.cs`). It walks a loop of four service stations, faces each one and plays a 3-second "Deliver" animation, then moves on; a blocked stop is skipped. It never attacks. It has health and shuts down if shot, Repair Robots treat it as an ally, and the Director counts it toward the five-robot limit.

## Before the viva

- Run Last Light → Quality → Run Station Gameplay Check once (about 40 s). It should end with `RESULT: ALL PASSED`.
- Keep the F1–F4 panels off while showing normal play; turn on only your own agent's panel when explaining it.
- Third-party assets (Creepy Cat kit, robot packs) must be installed locally; they are not in Git.
