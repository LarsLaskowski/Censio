# Log: issue #138

One line per pipeline step: date and time of day (UTC), step, member, result. Lead decisions and escalations
are quoted in full.

| Date | Step | Member | Result |
| ---- | ---- | ------ | ------ |
| 2026-11-27 17:34 | 1 Intake | Orchestrator | RESULT: DONE — bug (#138: apostrophe shown as underscore in Companion diagnostics Name column); user adds: trade routes also have umlaut problems, to be covered in the same run; session branch claude/elegant-johnson-4u0kq2 off main 1e03c0e; env: Companion Windows, Anno 117 current |
| 2026-11-27 17:39 | 2 Plan | Lead | RESULT: DONE — tier trivial, docs only; root cause: underscore already sent by the game pipe (non-ASCII written as _), no Censio code writes it; trade-route names share TryReadString, kept in scope; AC5 needs PM confirmation of what trade-routes page shows (opus/medium · 107,043 tokens · 40 tool uses · 299 s · 76 in · 25,070 out · 2,495,871 cache read · 105,636 cache write · 764,529 weighted) |
| 2026-11-27 17:39 | 2 Challenge skipped | Orchestrator | challenge skipped — tier trivial is never challenged |
| 2026-11-27 17:39 | 4-5 Skipped | Orchestrator | no production or test code: see *Verification without tests* |
