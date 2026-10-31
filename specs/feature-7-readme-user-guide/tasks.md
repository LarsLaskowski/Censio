# Tasks: README and user guide from the start (English, then German)

Plan: [plan.md](plan.md) — Status: Draft

No new API: the change is documentation only (plan, *Verification without tests*).

| # | Group | Task | Files | Criteria | Depends on | Owner | Done |
| - | ----- | ---- | ----- | -------- | ---------- | ----- | ---- |
| 1 | G1 | Rewrite the README to the layout of the plan's *Approach*, English then German | `README.md` | AC1, AC2, AC3, AC6, AC7, AC8 | — | Dev | [ ] |
| 2 | G1 | Rewrite the user guide to the layout of the plan's *Approach*, English then German, links with `../` | `docs/USER_GUIDE.md` | AC5, AC6, AC8 | 1 | Dev | [ ] |
| 3 | G1 | Drop the edge-build reference from *Not here* | `docs/areas/server.md` | AC3 | — | Dev | [ ] |
| 4 | G1 | Code-block parity and relative-link checks, language anchors on GitHub | `README.md`, `docs/USER_GUIDE.md` | AC6, AC8 | 1, 2 | Dev | [ ] |
| 5 | G1 | Mark every step the Product Manager's dry run did not show as "Not yet tried" in both languages | `README.md`, `docs/USER_GUIDE.md` | AC4 | 1, 2, dry run | Dev | [ ] |
