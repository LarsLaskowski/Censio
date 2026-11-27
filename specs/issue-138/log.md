# Log: issue #138

One line per pipeline step: date and time of day (UTC), step, member, result. Lead decisions and escalations
are quoted in full.

| Date | Step | Member | Result |
| ---- | ---- | ------ | ------ |
| 2026-11-27 17:34 | 1 Intake | Orchestrator | RESULT: DONE — bug (#138: apostrophe shown as underscore in Companion diagnostics Name column); user adds: trade routes also have umlaut problems, to be covered in the same run; session branch claude/elegant-johnson-4u0kq2 off main 1e03c0e; env: Companion Windows, Anno 117 current |
| 2026-11-27 17:39 | 2 Plan | Lead | RESULT: DONE — tier trivial, docs only; root cause: underscore already sent by the game pipe (non-ASCII written as _), no Censio code writes it; trade-route names share TryReadString, kept in scope; AC5 needs PM confirmation of what trade-routes page shows (opus/medium · 107,043 tokens · 40 tool uses · 299 s · 76 in · 25,070 out · 2,495,871 cache read · 105,636 cache write · 764,529 weighted) |
| 2026-11-27 17:39 | 2 Challenge skipped | Orchestrator | challenge skipped — tier trivial is never challenged |
| 2026-11-27 17:39 | 4-5 Skipped | Orchestrator | no production or test code: see *Verification without tests* |
| 2026-11-27 17:39 | 6 Implement | Dev | RESULT: PASS — AC1–AC4 docs edits (protocol.md, pipe.md, companion-diagnostics.md, USER_GUIDE.md EN+DE); no code; AC5 open with PM (sonnet/medium · 84,160 tokens · 7 tool uses · 30 s · 12 in · 4,217 out · 383,891 cache read · 82,631 cache write · 162,775 weighted) |
| 2026-11-27 17:40 | 7 Gate stamp | Orchestrator | PASS on 393cadef08384130d5d1b9f7dc3851d3a4562785 (tier trivial): Format check pass; decision-check.py pass; scope-check.py pass |
| 2026-11-27 17:44 | 7 Manual test | Product Manager | PASS AC5 — trade-routes page shows _ where the game shows an umlaut; same cause as #138, docs stand as written |
| 2026-11-27 17:45 | 8 Review | Reviewer | VERDICT: BLOCKING 0 \| NON-BLOCKING 1 on 393cadef08384130d5d1b9f7dc3851d3a4562785 — protocol.md:29 marker sentence lacks *observed* (verbatim fix offered) (opus/medium · 46,652 tokens · 6 tool uses · 39 s · 12 in · 3,404 out · 170,396 cache read · 40,320 cache write · 151,904 weighted) |
| 2026-11-27 17:46 | 7 Gate stamp | Orchestrator | PASS on fe356c5aca2539c8ba94f51bcce72ac6a21f0e73 (tier trivial, verbatim): Format check pass; decision-check.py pass; scope-check.py pass |
| 2026-11-27 17:46 | 8 Verbatim fix | Orchestrator | verbatim: [NON-BLOCKING, verbatim] docs/protocol.md:26 (commit fe356c5) |
