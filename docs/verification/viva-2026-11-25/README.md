# Verification — 25 November 2026 (viva preparation)

Unity 6000.3.25f1, branch `feture/recheck`, scene `ResearchStation`.

| Check | How it was run | Result |
| --- | --- | --- |
| Scene audit (all 4 scenes) | Last Light → Quality → Fix And Audit All Scenes | 0 problems ([fix-and-audit.txt](fix-and-audit.txt)) |
| Live station gameplay | Last Light → Quality → Run Station Gameplay Check | 49/49 passed, 0 errors logged ([gameplay-check.txt](gameplay-check.txt)) |
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
