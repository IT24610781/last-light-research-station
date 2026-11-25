# Last Light: team ownership and integration boundaries

Source of responsibility: **Last_Light_Full_Game_Requirements_v1_3.pdf, page 3** (2 October 2026). This identifies assigned roles, not proof that a person authored every current line. Git history and each member's evidence remain the record of actual contributions.

| Role | AI ownership | Other primary work | Original modelling responsibility |
| --- | --- | --- | --- |
| Member A / Student 1 | Tactical Guard (utility decisions) | Station environment, lighting, NavMesh, shared A*, relay objectives, objective display, exit unlocking, service bays and scene integration | Power-relay console |
| You / Systems Engineer | Repair Robot (utility selection and health repair effect) | Player controls, interactions, weapons, shared health, noise emission, menus/HUD and game-state authority; barricade physics; GitHub, builds, demo and packaging | Articulated robot body |
| Member B / Agent Controller | Sound Hunter AND Encounter Director | Shared robot movement, turning, animation, agent pause/reset and debug; Director limits, cooldowns, activation and tests | Movable barricade |

## Interfaces and evidence in this repository

- A: `Scripts/AI/Guard`, `Scripts/Navigation`, `Scripts/Interaction/StationProgressionManager.cs`, `StationRelay.cs`, `StationSlidingDoor.cs`, `StationEvacuationTerminal.cs`, `Prefabs/RelayConsole_Root.prefab`.
- You: `Scripts/AI/Repair`, `Editor/Repair`, `Tests/PlayMode/RepairRobotScenarioTests.cs`, `Scripts/Player`, `Scripts/Combat`, `Scripts/Interaction`, `Scripts/UI`.
- B: `Scripts/AI/Hunter`, `Scripts/AI/Director`, `Scripts/AI/Movement`, animation controllers, `Models/Barricade.fbx`; B's handoff is `docs/IT24104259-AgentController-Hunter-Director.md`.
- These folders are feature evidence, not exclusive authorship assertions. Shared integration fixes do not transfer ownership of another member's agent.

## Preserve the established game

Later agreed changes supplement the older PDF: retain **two floors**, start unarmed, collect weapons in the Armory, collect medicine in Medical, activate Relay A in Power and Relay B in Main Lab, then evacuate. Relay A opens the yellow shortcut; both relays open the red evacuation gate. Preserve an alternate route to Relay B.

Four robot models are planned: Repair spider, Tactical Guard cyborg, Sound Hunter Onua, and Service drone. There are also four assessed AI agents, but these are **Guard, Repair, Hunter, Director**. The Director is an invisible encounter coordinator, not a fifth robot model. Service patrol is a supporting gameplay feature, not a replacement for the Director assessment. Repair supports injured allies; Guard and Hunter provide combat pressure.

## Modelling claims

Downloaded and adapted robot meshes are useful game assets but are not automatically evidence of an original model made from scratch. Keep source files, modelling steps, animation work and third-party credits separate. The existence of a ServiceRobot visual prefab alone does not prove the articulated-body originality requirement. Preserve and review the team's relay console and barricade sources as separate original-model evidence; do not claim somebody else's mesh as yours.

## Academic confirmation still needed

The supplied IS assignment describes a four-person group. The three-person plan explicitly says to confirm the role swap and Member B's two-agent ownership with the lecturer. No lecturer approval is established by these files. Keep any approval with the submission evidence.

## Integration handoff

A owns the Station scene, you own PlayerTest, B owns AgentTest. The recheck repairs are a requested team integration contribution on `feture/recheck`; they do not redefine these responsibilities. Review the scene diff with A before the team merge. Each member should explain and demonstrate their own decisions and tests.
