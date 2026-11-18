# Log: Issue #125

One line per pipeline step: date and time of day (UTC), step, member, result. Lead decisions and escalations
are quoted in full.

| Date | Step | Member | Result |
| ---- | ---- | ------ | ------ |
| 2026-11-18 16:39 | 1 Intake | Orchestrator | Bug: after the game update (build 26.39954.4.58957, main menu 3.0.1.1743890) the Companion shows Protocol mismatch. Branch claude/friendly-cerf-1uvsyw off main. |
| 2026-11-18 16:42 | 2 Plan | Lead | RESULT: ESCALATE — game pipe now protocol 3 (types 4 and 5 new); asks PM: real v3 recording (A) or reference reader alone (B) (opus/medium · 73,851 tokens · 20 tool uses · 122 s · 32 in · 9,107 out · 720,950 cache read · 71,289 cache write · 341,452 weighted) |
| 2026-11-18 16:45 | 2 Plan | Product Manager | Answer: log confirms 'received version 3'. Reference is Ubisoft commit 4cc8ad78d63b33855b4b35dd002380fc466e4b1d (anno117_pipe_example). Build from the reference reader now; the user sends a raw recording later, once the Companion records. |
| 2026-11-18 16:54 | 2 Plan | Lead | RESULT: DONE — tier standard; accept protocol 2 and 3, skip types 4/5 as UndecodedMessage, anonymiser v3; record 0094 (opus/medium · 166,973 tokens · 60 tool uses · 501 s · 114 in · 42,491 out · 5,440,388 cache read · 152,227 cache write · 1,349,744 weighted) |
| 2026-11-18 16:56 | 3 Plan security | Security | VERDICT: CHANGES_REQUIRED — (1) blocking: tier should be security (anonymiser guard narrowed); (2) blocking: raw-recording privacy statements (pipe-recording.md:112, USER_GUIDE 317/341/795/819) omit trade-route names; (3) non-blocking: AC5 should assert exact [type,0,0,0,0] with count>0 and trailing bytes (opus/medium · 71,267 tokens · 13 tool uses · 110 s · 24 in · 8,779 out · 510,798 cache read · 67,849 cache write · 308,540 weighted) |
| 2026-11-18 16:56 | 2 Plan challenge | Devil's Advocate | VERDICT: OBJECTIONS 0 major, 3 minor — (1) AC3 tests must send the undecoded message before QuietDeadline; (2) rename/retype PipeMessageReaderUnknownTypeBeforeVersionReturnsMismatch to type 6, add own trade-routes-before-Version test; (3) AC7 deferred to verification issue, open it before the PR and link in PR body and pipe.md Residual risks (opus/medium · 63,245 tokens · 19 tool uses · 121 s · 36 in · 8,589 out · 753,042 cache read · 60,052 cache write · 311,396 weighted) |
