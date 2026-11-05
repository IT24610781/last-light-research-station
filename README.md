# Last Light — Research Station

A single-player, first-person escape game set inside a two-floor research station. Restore power, manage noise, survive robot encounters, and unlock evacuation.

Developed as a team project for **GV and Intelligent Systems (IS)**, Last Light combines a playable Unity environment with explainable AI decisions, shared navigation, physics interactions, and animated robot models.

## Gameplay

The station is in lockdown. Explore its rooms, collect equipment, activate two power relays, and reach the evacuation terminal while dealing with hostile robots and their repair support.

### How to win

1. Start in **Arrival** with no equipped weapon.
2. Visit the **Armory** to collect a pistol or rifle.
3. Activate **Relay A** in the ground-floor **Power** room.
4. Use the stairs to reach the upper floor. Collect medkits in **Medical** when needed.
5. Activate **Relay B** in the **Main Lab**.
6. Return to the ground-floor **Evacuation** room and interact with its terminal.

Both relays must be active before evacuation succeeds. Yellow access doors require Relay A; red access doors require both relays. Other doors can be operated normally.

Sprinting, throwing objects, and firing weapons produce noise that can attract the Sound Hunter. Walls and doors affect movement and visibility; reaching an objective may require managing encounters rather than simply running past them.

## Features

- **First-person controls:** walking, sprinting, jumping, crouching, mouse look, and collision-aware movement.
- **Physics interactions:** pick up, carry, drop, and throw supported objects, including movable barricades.
- **Combat and health:** pistol/rifle selection, firing, magazines, reloading, damage, medkits, and death handling.
- **Mission progression:** interactive relays, access-controlled doors, objective feedback, and an evacuation win condition.
- **Game presentation:** start/pause/win/loss screens, player health and ammunition display, contextual interaction prompts, room notifications, robot health displays, and audio feedback.
- **Robot presentation:** animated models driven by gameplay state, with movement separated from animation and decision logic.
- **Development tools:** focused test scenes, AI decision displays, automated tests, and live verification scenarios.

## Robots and intelligent systems

The project contains four robot model roles. The Encounter Director is a separate, non-physical AI system, not a fifth robot model.

| System | Role and implementation |
| --- | --- |
| **Tactical Guard** | Combat/security robot. Evaluates tactical actions through its agent and sensor components; movement, weapon feedback, and animation follow gameplay state. |
| **Sound Hunter** | Uses hearing, vision, and bounded memory to investigate noise, search last-known locations, pursue visible targets, and attack. It should not continuously track a hidden player's live position. |
| **Repair Robot** | Utility-based support agent. Compares injured allies, requests a reachable approach route, restores health in range, and retreats from observed danger. |
| **Service Robot** | Animated service/cargo robot with patrol behavior. It is not presented as an additional completed combat AI. |
| **Encounter Director** | Evaluates encounter pressure and whether to activate pooled robots or wait, subject to population, cooldown, budget, and spawn-location constraints. Integration and tuning depend on the scene. |

### Repair Robot decision model

The Repair Robot separates observation, decision-making, route planning, movement, and healing:

```text
Receive nearby ally reports and observe threats
    → Reject invalid or unreachable targets
    → Compare utility and retain worthwhile commitments
    → Request a route and approach
    → Repair only within range and clear line of sight
    → Reconsider when conditions change
```

Default target utility:

```text
utility = 100 × missing-health fraction
        − 2 × route length in metres
        − 30 × observed danger
```

- Team health reports are restricted to a configurable **25 m support radius**.
- A challenger must improve on the current target's score by more than the default **15-point switch margin**.
- Repair operates within **2 m**, at **20 HP per second**, with health clamped to its maximum.
- The robot cannot repair itself, resurrect a dead ally, or heal through a wall.
- Pause, death, invalid targets, and immediate danger interrupt repair.
- Threat estimates use observations and memory, not unrestricted knowledge of a hidden player.

Weights and timing values are editable in the Inspector. The development display exposes candidate scores, rejection reasons, selected target, route provider, and repair status.

## Technology

| Component | Version / approach |
| --- | --- |
| Unity Editor | **6000.3.25f1 — Unity 6.3 LTS** |
| Language | C# |
| Rendering | Universal Render Pipeline (URP) |
| Input | Unity Input System |
| Navigation | Shared `IPathProvider` interface; waypoint/A* and NavMesh provider implementations |
| Movement | Shared `AgentMotor` with `CharacterController` |
| Modelling and animation | Blender sources and imported FBX assets |
| Source control | Git and Git LFS |

Exact package versions are recorded in [`Game/Packages/manifest.json`](Game/Packages/manifest.json) and the package lock file. Navigation providers are assigned per scene; the ResearchStation Repair Robots use `NavMeshPathProvider`.

## Getting started

### Requirements

- Unity Hub and **Unity 6000.3.25f1**.
- Git and **Git LFS** for the repository's models, textures, and audio.
- Internet access for the first Unity package restore.
- Windows build support if producing the project's Windows standalone build.

### Clone and open

```bash
git lfs install
git clone https://github.com/IT24610781/last-light-research-station.git
cd last-light-research-station
git lfs pull
```

1. In Unity Hub, choose **Add project from disk** and select the **`Game`** folder, not the repository root.
2. Open it with the specified Editor version.
3. Allow Unity to restore packages, import assets, and finish compiling.
4. Open **`Assets/LastLight/Scenes/ResearchStation.unity`**.
5. Enter Play Mode and use the start screen to begin.

The repository includes development integrations such as Unity MCP. An AI client or a running Blender session is not required to play the game; Unity must still resolve the dependencies listed in the project manifest.

### Controls

| Input | Action |
| --- | --- |
| **W / A / S / D** or arrow keys | Move |
| **Mouse** | Look |
| **Left Shift** | Sprint |
| **Space** | Jump |
| **Hold C** | Crouch |
| **E** | Interact, pick up an object, or drop the held object |
| **Q** | Throw the held object |
| **Left mouse button** | Fire the equipped weapon |
| **R** | Reload |
| **1** | Select rifle, if acquired |
| **2** | Select pistol, if acquired |
| **Escape** | Pause / resume in the main game |

Aim at an interactable object and move within interaction range before pressing **E**. Weapon selection requires acquiring the corresponding weapon.

## Project structure

```text
last-light-research-station/
├── Game/                         Unity project — open this folder in Unity Hub
│   ├── Assets/LastLight/
│   │   ├── Animations/           Animation clips and controllers
│   │   ├── Audio/                Audio assets
│   │   ├── Editor/               Editor tools and verification commands
│   │   ├── Environment/          Station environment assets
│   │   ├── Materials/            Materials
│   │   ├── Models/               Imported models
│   │   ├── Prefabs/              Reusable gameplay and visual prefabs
│   │   ├── Scenes/               Main scene and focused test scenes
│   │   ├── Scripts/              Gameplay, AI, navigation, and UI
│   │   ├── Settings/             Gameplay configuration assets
│   │   └── Tests/                Automated and live verification scenarios
│   ├── Packages/                 Package manifest and lock file
│   └── ProjectSettings/          Unity project configuration
├── art-source/                   Tracked source art
└── docs/                         Implementation and team handover documents
```

## Scenes and testing

| Scene | Purpose |
| --- | --- |
| `ResearchStation` | Main integrated game and escape progression |
| `PlayerTest` | Focused player, interaction, combat, and Repair Robot testing |
| `AgentTest` | Hunter, movement, navigation, and Director testing |
| `GuardTest` | Tactical Guard behavior and presentation testing |

Run automated EditMode tests from **Window → General → Test Runner**.

For Repair Robot verification:

1. Choose **Last Light → Repair Robot Debug** to inspect decisions.
2. Choose **Last Light → Run Repair Robot Verification**.
3. Allow approximately 40 seconds for the live scenario report in the Console.
4. Stop Play Mode when verification finishes.

These live repair scenarios are custom editor-only checks, separate from the standard NUnit PlayMode runner. A standard PlayMode run reporting zero discovered tests does not execute them.

**Recorded verification, 22 October 2026:** 41 Repair AI scenario checks, 11 Repair presentation checks, and 38 EditMode tests passed. A live ResearchStation test also confirmed healing an injured Guard to full health followed by target reconsideration. These are dated results, not a CI badge or a guarantee for later revisions; rerun the checks after merging gameplay changes.

## Building

1. Open **File → Build Profiles** and select the Windows standalone target.
2. Confirm `ResearchStation` is enabled as the startup scene in the scene list.
3. Resolve any compilation errors, then choose **Build**.
4. Write the build to a separate output folder and distribute the complete generated build folder.

## Team responsibilities

| Role | Primary responsibilities |
| --- | --- |
| **Member A — World and navigation** | Environment, lighting, navigation, Tactical Guard AI, station integration, relay objectives, exit access, and power-relay console model. |
| **Systems Engineer** | Player systems, weapons, physics interactions, health, UI/game state, Repair Robot AI and model, and coordination of integration, builds, and demonstrations. |
| **Member B — Agent controller** | Shared robot movement/turning/animation, Sound Hunter, Encounter Director, agent debugging/reset, and movable barricade model. |

Model preparation and integration may include support contributions across roles. A shared visual asset does not change ownership of an agent's decision logic.

## Development notes and limitations

- This is an academic development project. Encounter balance and navigation should be validated in the integrated scene, not only in test rooms.
- The recorded Repair Robot checks do not constitute a complete two-floor staircase traversal test.
- Prefabs placed in a new scene need appropriate route providers, ally references, and support or patrol positions.
- An unassigned Repair route provider uses a checked direct-route fallback; it cannot find detours around walls or routes between floors.
- Two Repair Robots can heal the same ally. Their combined healing rate needs to be considered when balancing encounters.
- Keep `.meta` files with Unity assets. Fetch LFS content before investigating missing models or textures.
- Avoid committing generated `Library`, `Temp`, `Logs`, and local build output. Use focused feature branches and review changes before merging into `dev`.

## Documentation

- [Repair Robot: implementation, utility scoring, and demonstration](docs/RepairRobot-Step5.md)
- [Tactical Guard: model and presentation integration](docs/TacticalGuard-Integration.md)
- [Member B: movement, Sound Hunter, Encounter Director, and barricade](docs/IT24104259-AgentController-Hunter-Director.md)

Some handover documents describe earlier milestones. Check their dates and the current scene configuration when using them for integration.

## Asset credits and licensing

The project combines team-authored code and models with third-party assets. The Tactical Guard adapts the **CYBORG SOLDIER RX-1500** model; other downloaded robot and environment assets retain their respective creators' rights and license terms.

No repository-wide license is declared in the current checkout. Repository access does not grant permission to redistribute third-party models, textures, or audio. Keep the original asset listings, creator credits, and license records with the project, and verify redistribution permissions before publishing source assets or a public release.
