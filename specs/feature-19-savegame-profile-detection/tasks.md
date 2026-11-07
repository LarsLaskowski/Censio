# Tasks: Savegame profile detection from SessionStart

Plan: [plan.md](plan.md)

G1 adds no new API.

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Decision record 0041 and its index row | `docs/decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md`, `docs/decisions/README.md` | AC1, AC2 | — | Lead | [x] |
| 2 | G1 | Pipe area: figure name, session events, related decisions (*Documentation updates* 1) | `docs/areas/pipe.md` | AC3 | 1 | Dev | [ ] |
| 3 | G1 | Protocol: sources, SessionStart, connect sequence (*Documentation updates* 2) | `docs/protocol.md` | AC3 | 1 | Dev | [ ] |
| 4 | G1 | "headline" wording in the recording and log rules (*Documentation updates* 3) | `docs/areas/pipe-recording.md`, `docs/areas/companion.md` | AC4 | — | Dev | [ ] |
| 5 | G1 | User guide privacy paragraph, English and German (*Documentation updates* 4) | `docs/USER_GUIDE.md` | AC4 | — | Dev | [ ] |
| 6 | G1 | Issue comments linking record 0041 on #27, #29, #30, #31, #32, #43 | — | AC5 | 1 | Orchestrator | [ ] |
| 7 | G1 | Figure-name grep of the diff; scope check | — | AC6 | 2–5 | Orchestrator | [ ] |
