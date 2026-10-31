# Spec: README and user guide from the start (English, then German)

Source: Issue #7
Status: Draft

## Problem

People outside the project (testers, Anno 117 players) need to find out from `README.md` what Censio is, how far
it is, and how to install and start the current state; the user guide (`docs/USER_GUIDE.md`) is where they look up
details and how to report a problem. Today (main at `07b0bb1`):

- `README.md` and `docs/USER_GUIDE.md` hold the same text (only the title differs): a one-sentence description,
  then the sections *Docker* and *Companion*, each in English and then in German (`Docker (Deutsch)`,
  `Companion (Deutsch)`). The SmartScreen note and the `docker run` example (no compose) exist.
- Missing: a *Status* section (what works, what does not), the licence (`LICENSE` is MIT, the README does not say
  so), how to get the software when no release exists, what the Companion stores and logs, and how to report
  problems (`.github/ISSUE_TEMPLATE/bug_report.md` and private reporting in `SECURITY.md` exist, neither is linked).
- Both files already put the full English text first, then the full German text (`AGENTS.md`), but the German
  part keeps three English headings (`### Pull`, `### Run`, `### Port`).

Facts the issue rests on, checked:

- #5 (release workflow) is built: `.github/workflows/release.yml` publishes `networlddev/censio:<version>`, moves
  `latest` at the promotion and attaches `Censio-<version>-win-x64.zip` to the release. **No release exists yet**
  (`gh api …/releases` is empty, no `v*` tag on origin, Docker Hub `networlddev/censio` has no tag); the milestone
  *M1* names `v0.1.0` as the first real release.
- #6 (edge builds) was **closed as not planned** on 2026-10-31. `ci.yml` pushes no image and builds no edge ZIP; its
  job `release-check` uploads the artifact `companion-dry-run` (`Censio-1.2.3-win-x64.zip`, a test fixture
  version, retention 14 days), which is not a build meant for users. The milestone *M0* description still promises
  edge builds.
- No configuration table exists in the README, although `.squad/project.md` (*Integration surface*) names one;
  the server has the settings `Censio:DataDirectory` / `CENSIO__DATADIRECTORY` and uses `ASPNETCORE_HTTP_PORTS`.

## Scenarios

- S1: Given a person who has never seen Censio opens `README.md` on GitHub, when they read its English part from
  the top, then they read what Censio is, the *Status* section, how to get it, how to run the server with
  `docker run`, how to start the Companion, a link to the user guide and the licence (MIT, link to `LICENSE`).
- S2: Given that person reads *Status*, then it names the milestone and version it describes, what works today
  (the server's start page with `Version <version>`, `GET /health` answering `Healthy`, the Companion's tray icon
  with `Version <version>` and `Exit`) and what does not yet (no game data is read, nothing is sent to the
  server, no history), and that no release exists while that is true.
- S3: Given a published release, when the person follows the README's Docker steps, then they pull and run
  `networlddev/censio:<version>`, and `http://localhost:8080/` shows `Version <version>`.
- S4: Given a published release, when the person downloads `Censio-<version>-win-x64.zip` from the release's
  *Assets*, extracts it and starts `Censio.Companion.exe`, then the README's SmartScreen note tells them how to get
  past "Windows protected your PC" and the tray menu shows `Version <version>`.
- S5: Given no release exists or the person wants the state of `main`, when they look for an edge build, then the
  README says that no edge builds are offered and — as the Product Manager decides (open question Q1) — how to get
  the current state.
- S6: Given a person reads `docs/USER_GUIDE.md`, then it covers installation, the first start, where data and
  logs are stored (server: `/config` with `data-protection-keys`, log on the console via `docker logs censio`;
  Companion: stores no data and writes no log yet), and how to report problems.
- S7: Given a person has a problem, when they follow the user guide, then they open a GitHub issue with the
  *Bug report* template and include the version (start page or tray menu), how they run it and the log
  (`docker logs censio`); a security vulnerability goes to private reporting as `SECURITY.md` says, never a public
  issue.
- S8: Given a German reader, when they read the German part of either file, then it has the same sections in the
  same order with the same facts, the same commands and the same links as the English part.
- S9: Given an operator who wants to change a setting, when they read the README, then a configuration table
  lists each setting (key, environment variable, default) in English and in German.
- S10: Given a later milestone changes what works, when its pull request updates the documentation, then it
  changes the *Status* section in both languages and nothing else has to move.

## Errors and edge cases

- E1: The release page is empty (no release yet): the README says so in *Status* and keeps every statement about
  the published image and ZIP marked "Not yet tried" (as today) until a release has shown it.
- E2: SmartScreen blocks the unsigned exe: "More info", then "Run anyway" (English) and the German labels Windows
  shows ("Weitere Informationen", "Trotzdem ausführen").
- E3: The known start failures stay documented with the text the user sees: port taken ("port is already
  allocated"), container name taken (`docker rm -f censio`), unwritable data directory and a data directory that is
  not an absolute path (exit code 1 and the exact log lines from `docs/areas/server.md`).
- E4: An unsupported host (arm64, Windows containers, Windows other than 10 22H2 / 11 x64) is named as unsupported.
- E5: A reader who finds a vulnerability is sent to private reporting, not to the bug template.
- E6: A link in either file (`LICENSE`, `SECURITY.md`, the user guide, the issue template) resolves on GitHub.

## Non-functional requirements

- Language: each file holds the full English text first, then the full German text (`AGENTS.md`).
- Truth: every statement holds for main at merge time; a statement no run has shown is marked as not yet tried in
  both languages (*Operator environments and documented procedures*, `.squad/routing.md`), and the plan schedules a
  dry run of the documented steps by the Product Manager.
- Target hosts, as documented today and confirmed by #4 and #5: Docker 23 or later (Linux Docker Engine, or Docker
  Desktop for Windows with WSL 2 and Linux containers), linux/amd64; the Companion on Windows 10 22H2 or 11, x64,
  without .NET, installer or administrator rights. A host for building from source depends on Q1.
- No new configuration, no code change; external addresses only `github.com/LarsLaskowski/Censio` and
  `hub.docker.com/r/networlddev/censio`.
- No secret, private path or real user data in examples.

## Acceptance criteria

- [ ] AC1: The English part of `README.md` holds, in this order: what Censio is, *Status*, getting Censio,
  Docker (with `docker run`, no compose), Companion (with the SmartScreen note), configuration table, user guide
  link, licence (MIT, link to `LICENSE`) (S1, S3, S4, S9, E2).
- [ ] AC2: *Status* names the milestone and version it describes, lists what works and what does not yet as in S2,
  and states that no release exists while none does (S2, S10, E1).
- [ ] AC3: The README states that no edge builds are offered and gives the way to the current state that the
  Product Manager chose in Q1 (S5, E1).
- [ ] AC4: In the Product Manager's dry run an external person installs and starts the current state using only
  the README; every step the run did not show is marked "Not yet tried" in both languages (S3, S4, S5, E1, E3).
- [ ] AC5: `docs/USER_GUIDE.md` covers installation, first start, where data and logs are stored for the server and
  the Companion, and how to report problems with the *Bug report* template and the facts to include, and sends
  vulnerabilities to private reporting per `SECURITY.md` (S6, S7, E3, E5).
- [ ] AC6: In both files every English section has a German section in the same position with German headings,
  the same facts, byte-identical code blocks and the same link targets (S8, E2).
- [ ] AC7: The configuration table lists `Censio:DataDirectory` / `CENSIO__DATADIRECTORY` (unset outside the image,
  `/config` in the image) and `ASPNETCORE_HTTP_PORTS` (`8080` in the image), with values as in
  `docs/areas/server.md` (S9).
- [ ] AC8: Unsupported hosts are named (E4), and every relative link of both files resolves to a file of the
  repository (E6).

## Out of scope

- Edge builds themselves (#6, closed as not planned) and changes to any workflow.
- Docker Compose, code signing of the exe, an installer.
- The final review of the documentation (#45, M7) and languages other than English and German.
- Correcting the *M0* milestone description on GitHub (proposed to the orchestrator, not a repository change).

## Open questions

- Q1: With #6 closed as not planned and no release yet, how does an external person get the current state from
  the README? Pending — escalated to the Product Manager (options in the Lead's result).
- Q2: Does the user guide repeat the README's install steps? Answered: the issue lists installation under the user
  guide and AC "only the README" under the README, so both carry them; how the two files share the text is the
  plan's choice.
- Q3: Where does the Companion store data and logs? Answered: nowhere yet — `docs/areas/companion.md`, *Limits and
  errors*: "The Companion shows no error message and logs nothing yet"; it reads and writes no file.
- Q4: Which settings exist for the configuration table? Answered: `docs/areas/server.md` — `Censio:DataDirectory`
  (`CENSIO__DATADIRECTORY`) and the standard ASP.NET Core address settings, of which the image supports only
  `ASPNETCORE_HTTP_PORTS`; the Companion has none.

## Area documents

- None describes new behavior: the change documents existing behavior for users. `docs/areas/server.md`, *Not
  here*, names "the edge builds (#6)" as a later topic; since #6 is not planned, that line drops the reference.
