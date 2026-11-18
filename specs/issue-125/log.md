# Log: Issue #125

One line per pipeline step: date and time of day (UTC), step, member, result. Lead decisions and escalations
are quoted in full.

| Date | Step | Member | Result |
| ---- | ---- | ------ | ------ |
| 2026-11-18 16:39 | 1 Intake | Orchestrator | Bug: after the game update (build 26.39954.4.58957, main menu 3.0.1.1743890) the Companion shows Protocol mismatch. Branch claude/friendly-cerf-1uvsyw off main. |
| 2026-11-18 16:42 | 2 Plan | Lead | RESULT: ESCALATE — game pipe now protocol 3 (types 4 and 5 new); asks PM: real v3 recording (A) or reference reader alone (B) (opus/medium · 73,851 tokens · 20 tool uses · 122 s · 32 in · 9,107 out · 720,950 cache read · 71,289 cache write · 341,452 weighted) |
| 2026-11-18 16:45 | 2 Plan | Product Manager | Answer: log confirms 'received version 3'. Reference is Ubisoft commit 4cc8ad78d63b33855b4b35dd002380fc466e4b1d (anno117_pipe_example). Build from the reference reader now; the user sends a raw recording later, once the Companion records. |
