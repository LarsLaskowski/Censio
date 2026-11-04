# Log: feature 16-pipe-and-recording-guide

One line per pipeline step: date and time of day (UTC), step, member, result. Lead decisions and escalations
are quoted in full.

| Date | Step | Member | Result |
| ---- | ---- | ------ | ------ |
| 2026-11-04 11:56 | 1 Intake | Orchestrator | Issue 16 read (documentation label, user calls it a feature); branch claude/charming-galileo-5qc8r0 at a94a33d; work folder created |
| 2026-11-04 12:01 | 2a Spec | Lead | ESCALATE: launch-argument path in Ubisoft Connect/Steam unknown; spec.md drafted (opus/medium · usage pending) |
| 2026-11-04 16:37 | 2a Spec | Product Manager | Answer: option (b) both launchers described, Ubisoft Connect default path, PM dry-runs the launcher owned, other 'Not yet tried'; PM's Ubisoft Connect and Steam are set to German |
| 2026-11-04 16:38 | 2a Spec (revise) | Lead | DONE: spec updated with PM answer, plan-check --spec passes (opus/medium · usage pending) |
| 2026-11-04 16:39 | 2a Spec challenge | Devil's Advocate | OBJECTIONS 1 major (--replay needs tray Exit and a named console), 6 minor (opus/medium · usage pending) |
| 2026-11-04 16:48 | 4-5 Skeleton, tests first | Orchestrator | skipped: documentation only, no production or test code |
| 2026-11-04 16:48 | 6 Implement G1 | Dev | DONE: USER_GUIDE walkthrough, launcher steps, tray table, README short path, EN+DE; labels marked Not yet tried (sonnet/medium · usage pending) |
| 2026-11-04 17:55 | 6 G2 dry run | Product Manager | Windows 11 26H2, Ubisoft Connect (German): Bibliothek > drei Punkte > Verwalten > Eigenschaften > Startargumente hinzufügen: /pipe eintragen > Schließen. No other step reported yet |
| 2026-11-04 17:56 | 6 Implement G3 | Dev | DONE: Ubisoft Connect German path applied to guide and README; everything else stays Not yet tried; companion.md unchanged (sonnet/medium · usage pending) |
| 2026-11-04 17:58 | 6 G2 dry run (follow-up) | Product Manager | No Steam. Tests the Companion with the real game continuously: recording works, replay works, tray overflow area works, all tooltips correct. No release exists so no download. SmartScreen: file Properties > Allow (Zulassen) on the extracted exe or the archive avoids the dialog |
| 2026-11-04 17:58 | 8 Review round 1 | Reviewer | BLOCKING 1 (AC7 unmet / run record without build; resolved by PM follow-up report, build still to name), NON-BLOCKING 1 (README Status link wording) (opus/medium · usage pending) |
| 2026-11-04 17:59 | 6 G2 dry run (build) | Product Manager | Build: branch head fetched and built with the documented dotnet publish command (README, Build the Companion from source); replay via Exit and the Run dialog confirmed |
| 2026-11-04 18:00 | 6 Fix round 1 | Dev | DONE: PM results applied (recording, replay via Exit+Run dialog, overflow, tooltips tried), SmartScreen tip, README link fix, companion.md tooltip verified, build named as source publish (sonnet/medium · usage pending) |
| 2026-11-04 18:02 | 8 Verbatim fixes | Orchestrator | Applied reviewer's verbatim replacements (USER_GUIDE 167/226/416/475, companion.md 229, SmartScreen tip EN/DE in README and guide) |
| 2026-11-04 18:03 | 9 PR approval | Lead | APPROVED: AC1-AC7 met; follow-up issue for Steam/English labels (opus/medium · usage pending) |
