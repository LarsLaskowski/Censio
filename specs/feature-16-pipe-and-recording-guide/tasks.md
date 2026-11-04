# Tasks: Enabling the pipe and recording sessions (README and user guide)

Plan: [plan.md](plan.md) — Status: Approved

All groups: no new API (documentation only; steps 4 and 5 not applicable).

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Guide: walkthrough, launcher steps, table column, recording and replay, English and German, labels marked "Not yet tried" | `docs/USER_GUIDE.md` | AC1, AC2, AC3, AC4, AC6 | — | Dev | [x] |
| 2 | G1 | README: *Status* and the short *First recording* path with the link, English and German | `README.md` | AC5, AC6 | 1 | Dev | [x] |
| 3 | G2 | Dry run on Windows with the real game and a local publish of the branch head, launcher in German; report Windows version, launcher, labels, each step's result | — | AC7, AC2, AC4 | 1, 2 | Product Manager | [x] |
| 4 | G3 | Apply the dry run: confirmed labels, "Not yet tried" removed only where shown; `companion.md` verified points | `docs/USER_GUIDE.md`, `README.md`, `docs/areas/companion.md` | AC2, AC7 | 3 | Dev | [x] |
