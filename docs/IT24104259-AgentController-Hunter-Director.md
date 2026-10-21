# IT24104259(Member B): Agent Controller, Sound Hunter and Encounter Director

Owner: IT24104259 (Agent Controller). Scene: `AgentTest`.
Code: `Scripts/AI/Movement`, `Scripts/AI/Hunter`, `Scripts/AI/Director`, `Tests/Editor`.
Model: `Models/Barricade.fbx` (source `art-source/barricade.blend`).

---

## 1. Movement controller (GV): `AgentMotor`

**Job:** turn a list of route points into smooth, collision-aware movement. It never decides *where* to go; it executes a route and reports `Stopped / Moving / Arrived / Blocked`.

| Step | How it works | Key values |
|---|---|---|
| Receive | `SetPath(points)` replaces any old route | |
| Move | `CharacterController.Move` (collides with walls); speed changes with `Mathf.MoveTowards` | max 3.5 m/s, accel 6, decel 10 |
| Turn | `Quaternion.RotateTowards`, limited degrees per second; turns on the spot if facing > 60° away | 270 °/s |
| Arrive | next point within tolerance; slows near final point | 0.25 m tolerance, slow within 1.5 m |
| Recover | measures *actual* distance moved; low progress for 1 s → `Blocked` | progress < 25% of intended |
| Speed per state | `SpeedMultiplier` (patrol 0.5, investigate 0.8, chase 1.0) | |
| Evidence | `snapMode` (T key in tester) gives instant turning/speed for the before/after video | |

**Concepts to explain**
- **Local vs world coordinates:** route points are world positions; `transform.forward` is the robot's local forward axis expressed in world space. The motor flattens Y so robots move on the floor plane.
- **Movement vectors:** `direction = (target - position).normalized`; displacement = `direction * speed * deltaTime`.
- **deltaTime:** multiplying by the frame time makes speed independent of frame rate (same metres per second at 30 or 144 FPS).
- **Collision:** `CharacterController.Move` slides along colliders instead of passing through.
- **Why root motion is disabled:** code moves the robot root so AI movement stays exact and predictable; animations play in place. The Animator speed comes from `MeasuredSpeed` (real movement), so a blocked robot does not look like it is running.

---

## 2. Sound Hunter (IS agent 1)

### PEAS
| | |
|---|---|
| **Performance** | Reach useful evidence quickly; find visible intruders; avoid repeated unreachable goals; give up searching in bounded time. |
| **Environment** | Partially observable, dynamic, continuous station with walls, doors and a movable barricade. |
| **Actuators** | Movement via `AgentMotor`, turning, attack event. |
| **Sensors** | Hearing (`NoiseSystem` events, range 20 m, linear attenuation) and vision (15 m range, 110° cone, line-of-sight raycast). |

### Agent type
**Model-based, goal-driven agent.** It keeps an internal model (memory of noises and the last-seen position) because it cannot observe the player all the time, and it chooses goals (which evidence to investigate).

### Architecture (logic decoupled from visuals)
| Class | Role | Unity? |
|---|---|---|
| `HunterSensor` | hearing + vision | MonoBehaviour |
| `HunterMemory` | bounded noise list (8), merging (2 m), forgetting (15 s noises, 10 s last-seen) | plain C# |
| `HunterBrain` | finite state machine + heuristic scoring | plain C# |
| `HunterAgent` | wiring: senses → brain (every 0.2 s) → path → motor | MonoBehaviour |

### States
`Patrol → Investigate → Search → Return → Patrol`, plus `Pursue` and `Attack` when the player is seen.
- Sight beats all other evidence.
- After losing sight, it goes to the **last-seen position**, never the hidden real position.
- Search visits up to 4 reachable points on a 3 m ring, limited to 12 s.

### Heuristic evaluation (choosing between noises)
```
score = 0.5 × strength + 0.3 × recency − 0.2 × routeCost      (all scaled 0–1)
```
- **Strength** = perceived loudness after distance attenuation.
- **Recency** = 1 when just heard, falls to 0 at 15 s.
- **Route cost** = route length / 40 m, using A* path length (not straight-line distance).
- **Why these weights:** loudness is the strongest sign of the player (0.5); fresh evidence matters next (0.3); distance only tips close decisions (0.2), so a far but much louder noise can still win. Tested: a near 0.6 noise beats a far 0.7 noise.
- **Hysteresis:** switch target only if a new noise scores 0.15 higher, to stop flip-flopping.
- **Unreachable evidence** is rejected for 5 s instead of retried every tick.

### Alternatives considered
- **Plain if/else or "go to latest noise":** cannot compare evidence, easily distracted.
- **Behaviour tree:** good for many behaviours, but the Hunter has few clear modes; an FSM is simpler to explain and debug.
- **Utility AI for everything:** used for the evidence choice only, where comparison matters.

### Limitations
- Hearing ignores walls (sound passes through geometry).
- Scoring weights were hand-tuned, not learned.
- Search points are a fixed ring, not based on room layout.

### Failure case found and fixed
In testing, the Hunter kept attacking the player hidden behind a 2 m wall. Cause: the capsule pivot is at its middle, so eye height 1.5 m placed the eye at 2.5 m and the sight ray passed over the wall. Fix: eye height 0.6 and aim height 0.3 for the stand-in (an `Eye` transform on the real model). Lesson: line of sight depends on the raycast heights and colliders.

---

## 3. Encounter Director (IS agent 2)

### PEAS
| | |
|---|---|
| **Performance** | Keep pressure suitable for the player's state; never break limits; never spawn unfairly. |
| **Environment** | Whole mission: player condition, relay progress, robot population, bay safety. |
| **Actuators** | Activate one pooled robot at a service bay, or wait. |
| **Sensors** | Global player health and time since damage (its declared sensor model), relays online, active robots per type, bay visibility/distance/route. |

### Agent type
**Utility-based agent** with **hard constraint filtering**. Invisible; decides once per second.

### Decision process
1. **Hard limits (never overridden):** ≤ 5 living robots, ≤ 4 activations per run (deaths never refund), 20 s cooldown (also at level start).
2. **Bay filter:** reject bays in the player's view (100° cone + line-of-sight), closer than 10 m, or with no route.
3. **Pressure model:**
   - desired = 0.35 → 0.80 as relays come online
   - readiness = health × recovery (recovery reaches 1 after 10 s without damage)
   - current = 0.25 per Hunter, 0.30 per Guard, 0.15 per Repair
   - **gap = desired × readiness − current**
4. **Scores:**
   - activate = `1.0 × gap + 0.3 × typeFit + 0.2 × bayFit`
   - wait = `0.15 + 0.6 × (1 − readiness) + 0.8 × overPressure`
   - typeFit favours missing types; Repair is rejected if nobody needs support.
   - bayFit is best for an 18 m route (fair distance, still relevant).
5. Highest score wins; all candidates and rejection reasons are shown in the overlay.

**Worked example (from a test run):** desired 0.35, current 0.55, readiness 1.0 → gap −0.20 → wait = 0.15 + 0.8 × 0.20 = **0.31**, beating Hunter options (0.15, 0.11). The Director correctly waited.

### Object pooling
Robots are created inactive in advance; activation resets health, memory and movement (`ISpawnableAgent.ResetAgentAt`). Avoids creating objects during play and keeps the population bounded.

### Alternatives considered
- **Fixed spawn timer / waves:** ignores the player's state; unfair when the player is hurt.
- **Random spawns:** unpredictable, may spawn in view.

### Limitations
- Pressure values per robot type are estimates, tuned by playtesting.
- Bay visibility uses one ray to the bay centre.

---

## 4. Integration through interfaces
| Interface | Implemented by | Used by |
|---|---|---|
| `IPathProvider` | Member A's `WaypointNavigationService` (A*) | Hunter, Director |
| `IPlayerStatus` | player health system | Director |
| `IMissionProgress` | relay/objective system | Director |
| `ISpawnableAgent` | each robot agent | Director |

Before A* existed, a straight-line fallback let the Hunter be built and tested. Member A's A* then plugged in **without changing Hunter code**. With A*, the Hunter routes around the barricade; without a route, it rejects the noise and returns (blocked-route recovery).

---

## 5. Barricade model (GV)
- Low-poly security barrier, 2 × 0.6 × 1.2 m, **216 triangles**.
- Pivot at bottom centre (stands on the floor, rotates around its base).
- Bevelled edges (catch light), recessed hazard panels (inset + extrude).
- Two materials (dark metal, hazard orange); Smart UV Project with 0.02 island margin.
- Exported FBX: selected mesh only, FBX All scaling, −Z forward / Y up, apply transform.
- One Box Collider (2 × 1.36 × 0.9) fits it; physics added by the Systems Engineer.
- Blocks movement but not sight (Hunter eye is higher than the barricade).

---

## 6. Tests (EditMode, all passing: 36/36 in project, 20 mine)
| Area | Tests |
|---|---|
| Hunter | competing noises (louder wins; near beats slightly louder far), stale evidence forgotten, unreachable rejected, lost sight → last-seen not hidden position, attack in range, hysteresis, reset |
| Hunter memory | merge nearby noises, evict oldest when full, last-seen expiry |
| Director | activates when calm, Repair rejected without allies, cooldown, 5-active cap, 4-activation budget not refunded, low-health waits, no safe bay waits, high pressure waits, reset |

Play-mode checks done in `AgentTest`: smooth vs snap movement, noise investigation and search, lost-sight search behind a wall, blocked-route recovery at the barricade, A* route around the barricade, Director cooldown / waiting / activation / pool exhaustion / reset.

---

