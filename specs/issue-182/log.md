# Log: issue #182

One line per pipeline step: date and time of day (UTC), step, member, result. Lead decisions and escalations
are quoted in full.

| Date | Step | Member | Result |
| ---- | ---- | ------ | ------ |
| 2026-11-13 09:54 | 1 Intake | Orchestrator | bug: no feature label; issue #182 (label squad), part 1 moved upstream (Squad-Spec-Repository-Template#382), part 2 open: hint in .squad/stack.md Known pitfalls on building the server image in a sandbox (NU1301 UntrustedRoot, dockerd not running); no reported environment beyond the cloud sandbox; branch claude/cool-thompson-9qs6es (session-prescribed, at main c3e87f6) |
| 2026-11-13 09:57 | 2 Plan | Lead | RESULT: DONE, tier docs; security areas touched: none; no plan.md, no decision record. Edit: append one Known pitfalls entry to .squad/stack.md (dockerd not running by default; build stage does not trust the proxy CA, NU1301 UntrustedRoot; build with a stdin copy of the Dockerfile that adds the CA via --build-context ccr=/root/.ccr and SSL_CERT_FILE in the build stage only, --network host, HTTPS_PROXY build arg; never commit the copy). AC1: entry verbatim, Image build check unchanged, diff outside specs/ only .squad/stack.md (scope-check --tier docs, Format check, Reviewer). AC2 (manual): orchestrator dry run of the entry on the branch head before the review (build passes incl. blazor.web*.js check, clean git status, image Env without SSL_CERT_FILE/proxy, no ccr-ca-bundle.crt in /usr/local/share); a deviation changes the entry to the working command and is logged (opus/medium · 52,373 tokens · 9 tool uses · 137 s · 18 in · 13,223 out · 262,162 cache read · 49,473 cache write · 282,165 weighted) |
