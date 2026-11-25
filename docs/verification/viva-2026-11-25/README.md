# Verification — 25 November 2026 (viva preparation)

Unity 6000.3.25f1, branch `feture/recheck`, scene `ResearchStation`.

| Check | How it was run | Result |
| --- | --- | --- |
| Scene audit (all 4 scenes) | Last Light → Quality → Fix And Audit All Scenes | 0 problems ([fix-and-audit.txt](fix-and-audit.txt)) |
| Live station gameplay | Last Light → Quality → Run Station Gameplay Check | 60/60 passed, 0 errors logged ([gameplay-check.txt](gameplay-check.txt)) |
| Scene smoke test | Last Light → Quality → Smoke Test All Scenes | 4/4 scenes, 0 errors in 10 s play each ([smoke-test.txt](smoke-test.txt)) |
| EditMode unit tests | Window → General → Test Runner → EditMode → Run All | 44/44 passed |
| Repair Robot live scenarios | Last Light → Run Repair Robot Verification | 41 passed, 0 failed |
| Manual play | Real keyboard/mouse in Play mode | Rifle taken from Armory bench with E, prompt shown, automatic fire 12 → 7 rounds |

Fixed in this pass:
- Armory weapons had no collider, so the E ray could not hit them. All five bench guns now use the real pulse-rifle/pistol models and are takeable.
- 11 material slots on wall cubes used the built-in Standard shader (magenta in URP). They now use tracked URP copies.
- Medkits and the evacuation terminal were invisible markers with no collider. Medkits are now visible medical cases; the evacuation console is the usable terminal, and an exit trigger covers the evacuation bay (plan Stage 8).
- Only the barricade was carryable. Four small throwable crates were added (Arrival, Power, Storage ×2) for the noise-distraction demo.
- Props produced a fake impact noise while settling at level start, which pulled the Hunter out of patrol. ImpactNoise now ignores the first second after a prop is enabled.

Not covered: a full continuous human playthrough of every room, a standalone Windows build test, and visual-art approval.

## Second pass (same day)

- Main Lab now has the same wall console bank and overhead pipes as the Power room (mirrored); the old Main Lab bank faced into the wall and was hidden.
- Security Repair Robot moved to Storage (ground floor) with three NavMesh-checked support positions; the robot cap stays at five.
- Service robot model was 0.57 m below the floor; raised 0.62 m (measured gap now +0.08 m, like the other robots).
- Movable barricade scaled ×1.35 (≈3.0 × 1.8 × 2.2 m, 30 kg); still carryable.
- Illuminated room name plates above all 10 doors (corridor side).
- HUD map with player position/facing, relay and exit state, and robot markers only while a robot is firing (M to enlarge). HUD room titles now use the same measured room layout.

All checks above were re-run after the second pass: audit 0 problems, gameplay 60/60, smoke 4/4, EditMode 44/44, Repair 41/41.
