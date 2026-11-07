# Tasks: Protocol documentation from our own recordings

Plan: [plan.md](plan.md)

G1 has no new API (documentation only; no skeleton).

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Write the protocol document as *Approach* says, facts re-checked against the local recording | `docs/protocol.md` | AC1, AC2, AC3 | — | Dev | [x] |
| 2 | G1 | Update *Scope*, *Session events* and *Residual risks* | `docs/areas/pipe.md` | AC2 | 1 | Dev | [x] |
| 3 | G1 | Link the protocol document in *Data flow* | `docs/ARCHITECTURE.md` | AC1 | 1 | Dev | [x] |
| 4 | G1 | Privacy grep and close-reason re-derivation (*Verification without tests*), counts only | scratch, outside the repository | AC1, AC3 | 1, 2, 3 | Dev | [x] |
