# Plan: Ingest token from a file named by configuration

Source: Issue #176 | [spec.md](spec.md)
Tier: security — it changes how the authentication secret of `/ingest` is obtained, validated and kept out of the log (a new source of the credential and a stricter rule for every token).
Security areas touched: Ingest token and Companion authentication (`IngestTokenSetup`, the file `ingest-token`, the new setting and the file it names)

## Problem / root cause

See [spec.md](spec.md), *Problem*: the server reads its token only from `ingest-token` in the data directory, and a
hand-written token may be one character long.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The token comes only from `<data directory>/ingest-token`, which is read if it exists and otherwise generated | `src/Censio.Server/IngestTokenSetup.cs` lines 73–90 | confirmed |
| 2 | A hand-written valid `ingest-token` is used unchanged, so "operators cannot hand a token of their own" is partly refuted | lines 85–87, 108–145 | confirmed |
| 3 | The file rule: at most 65,536 bytes, trimmed of space, tab, CR, LF, then 1 to 1,024 bytes of `0x21`–`0x7E` | `ParseToken`, lines 152–175 | confirmed |
| 4 | The "cannot be read" line carries the exception, whose .NET message names the path | lines 120–131, 298–299; test asserts `Exception` not null at `tests/Censio.Server.Tests/IngestTokenSetupTests.cs` line 488 | confirmed |
| 5 | `Censio:DataDirectory` is checked with `Path.IsPathFullyQualified`, the empty string included | `src/Censio.Server/DataProtectionSetup.cs` lines 49, 74 | confirmed |
| 6 | A `null` from `TryCreateAccess` makes `Program` return exit code 1 before `RunAsync`, after the key ring | `src/Censio.Server/Program.cs` lines 20–25 | confirmed |
| 7 | `IngestAccess.CreateForToken` keeps only the SHA-256 and compares in fixed time; it checks no length, so tests with short tokens through it are unaffected | `src/Censio.Server/IngestAccess.cs` lines 69–99 | confirmed |
| 8 | Record 0061 is unreleased: it was added after `v0.2.0` (`abc3527`); tags are `v0.1.0`, `v0.2.0` only | `git ls-remote --tags origin`; `git log`: `6a7ca74` after `abc3527` | confirmed |
| 9 | An environment variable set to the empty string reaches the configuration as `""`, not as unset (E3 "the empty string included"); if it did not, the setting would count as unset and the generated token would apply, which still fails closed | to verify: the Dev starts the published server once with `CENSIO__INGEST__TOKENFILE=` in step 6 and records the E3 line or the default path in the log; the in-memory tests cannot show it | unverified |
| 10 | `FileStream` on a path that is a directory throws `UnauthorizedAccessException` (or `IOException`) on Linux, both already caught | to verify: the Tester's AC2 row "a directory" in step 5; the Dev catches whatever it throws as "cannot be read" | unverified |
| 11 | `FileStream` follows a symbolic link to its target | to verify: the Tester's AC3 symbolic link row (`File.CreateSymbolicLink`) in step 5 | unverified |
| 12 | `docker run -v <host file>:/run/secrets/censio-ingest-token:ro` mounts a single host file read-only on Linux Docker Engine, readable by the container user when the host file's owner or mode allows it | to verify: the Product Manager's dry run (AC8), before the documentation review | unverified |
| 13 | `docker logs` shows `The ingest token is read from the file named by Censio:Ingest:TokenFile.` and, with the file missing, the E1 line and exit code 1 | to verify: the same dry run | unverified |
| 14 | The Product Manager can build the image of the branch head on the Linux Docker Engine host | to verify: the orchestrator asks the Product Manager when it schedules the dry run (after step 7); fallback below | unverified |

## Tests per criterion

All new tests are in `IngestTokenSetupTests` unless named otherwise; the token in every content row contains `MARKER`, and
the configured file lies in a temporary directory whose name contains `PATHMARK`, so every test also asserts that no
entry's message or exception holds `MARKER` or `PATHMARK` (AC7).

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `IngestTokenSetupTryCreateAccessWithTokenFileReadsConfiguredToken` (rows: data directory with a different `ingest-token`, data directory without the file, no data directory): accepts the configured token, refuses the `ingest-token` token and another one, one `Information` entry with the S1 text, the data directory's entries and bytes unchanged; `IngestTokenSetupTryCreateAccessWithTokenFileChangedReadsNewToken` (S4: a second call after the file changed accepts only the new token) | `tests/Censio.Server.Tests/IngestTokenSetupTests.cs` |
| AC2 | `IngestTokenSetupTryCreateAccessTokenFileNotReadableStops` (rows: missing file, a directory, a symbolic link to a missing file, a file held with `FileShare.None`): `null`, one `Critical` entry with the E1 text, `Exception` `null`, the missing file not created; `ProgramStartWithMissingTokenFileExitsWithCodeOne` | `IngestTokenSetupTests.cs`; `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC3 | `IngestTokenSetupTryCreateAccessTokenFileInvalidStops` (rows: empty, white space only, 31 characters, 1,025 characters, a space inside, a byte `0x7F`, a byte above ASCII, 65,537 bytes): E2 text, file unchanged; `IngestTokenSetupTryCreateAccessTokenFileAtBoundsReadsToken` (rows: 32 characters starting with `!` and ending with `~`, 1,024 characters, a token and a line feed, 1,024 characters padded to 65,536 bytes); `IngestTokenSetupTryCreateAccessTokenFileSymbolicLinkReadsToken` | `IngestTokenSetupTests.cs` |
| AC4 | `IngestTokenSetupTryCreateAccessTokenFileNotAbsoluteStops` (rows: `""`, `token`, `relative/token`, a 43-character `MARKER` token value): E3 text, the value in no entry | `IngestTokenSetupTests.cs` |
| AC5 | `IngestTokenSetupTryCreateAccessTokenFileWithRequireTokenFalseStops` (rows: `false` and `FALSE` with a valid configured file, `false` with `""`): E4 text, `null`; `IngestTokenSetupTryCreateAccessTokenFileWithInvalidRequireTokenReportsSwitch` (rows: `yes`, `""`): only the existing switch line | `IngestTokenSetupTests.cs` |
| AC6 | adapted `IngestTokenSetupTryCreateAccessInvalidFileStops` (new row "31 characters", E6 text) and `IngestTokenSetupTryCreateAccessFileAboveSizeBoundStops` (E6 text); adapted `IngestTokenSetupTryCreateAccessFileAtBoundsReadsToken` (row `!~` replaced by 32 characters starting with `!` and ending with `~`); `IngestTokenSetupTryCreateAccessWithFileReadsSameToken` gets the category `AC6` (a generated token read at the next start) | `IngestTokenSetupTests.cs` |
| AC7 | the `MARKER`/`PATHMARK` assertions of every AC1–AC6 test above | `IngestTokenSetupTests.cs` |
| AC8 | manual: the Product Manager's dry run on a Linux Docker Engine host, on an image built from the branch head with `docker build -f src/Censio.Server/Dockerfile -t censio .`, of the user guide's new steps: start with the read-only mount and the setting (log line of S1), the same with the host file removed (E1 line, exit code 1). Fallback when the PM cannot build there: the orchestrator builds the image where Docker runs, saves it (`docker save`) and hands it over (`docker load`) | — |

The existing tests of #28 in this class carry the ids `AC1`, `AC4`, `AC7`, `AC8` of that issue; `--matrix` counts them
for this spec's ids of the same name. The verification matrix is therefore read against the test names above, and the
Tester adds this spec's id to an old test only where it is listed here (AC6).

## Approach

`IngestTokenSetup.TryCreateAccess` decides in this order (each failure logs one line and returns `null`):

1. `RequireToken` neither `true` nor `false`: the existing line (E5).
2. `RequireToken` `false`: with `Censio:Ingest:TokenFile` present (not `null`, the empty string included) the E4 line;
   otherwise the existing switched-off path.
3. The setting present: not `Path.IsPathFullyQualified` → the E3 line. Otherwise the configured file is read by the
   same bounded read and `ParseToken` as `ingest-token`; a read failure (`IOException`, `UnauthorizedAccessException`)
   logs the E1 line **without** the exception, an invalid content the E2 line, a valid one the S1 line and returns
   `IngestAccess.CreateForToken`. The data directory is neither read nor checked on this path.
4. Otherwise the existing data-directory path, unchanged except that `ParseToken` now requires `MinTokenLength`.

Private members (described, not in *Signatures*): a shared reader that returns the bounded content or the caught
exception, used by both sources so the bound stays in one place; `ParseToken` with the minimum; the new
`[LoggerMessage]` methods, none with a placeholder:

| Level | Text |
| ----- | ---- |
| Information | `The ingest token is read from the file named by Censio:Ingest:TokenFile.` |
| Critical | `The ingest token file named by Censio:Ingest:TokenFile cannot be read; the server stops.` |
| Critical | `The ingest token file named by Censio:Ingest:TokenFile holds no valid token (32 to 1,024 visible ASCII characters); the server stops.` |
| Critical | `The setting Censio:Ingest:TokenFile must be an absolute path; the server stops.` |
| Critical | `The settings Censio:Ingest:TokenFile and Censio:Ingest:RequireToken false exclude each other; the server stops.` |

The existing `LogTokenFileInvalid` text becomes
`The ingest token file '{TokenFile}' holds no valid token (32 to 1,024 visible ASCII characters); the server stops.`
No other existing text changes. No file is written on the configured path. No new user-visible output beyond these
lines.

## Options considered

The product choices (file path vs a token value, which source wins, the conflict with `RequireToken` `false`, where the
minimum applies) are weighed in record 0061 and the spec's *Open questions*. Implementation choices:

1. **Extend `IngestTokenSetup`** (chosen) — the decision order and both sources in one place, sharing the bounded read
   and `ParseToken`.
2. **A new type for the configured file** — splits one decision order across two types and duplicates the read; the
   class stays small enough (about 80 more lines).
3. **Attach the exception to the configured-file line, with the path scrubbed** — keeps the reason, but scrubbing a
   .NET message is fragile, and the reason (missing, permission) is what the operator checks first anyway
   (user guide).
4. **Check the configured file's mode or owner** — rejected in the spec (N4): a swarm secret is `0444`.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server | `IngestTokenSetup` (`src/Censio.Server/IngestTokenSetup.cs`) | the setting, the order above, the minimum, five log methods, one text |
| Censio.Server.Tests | `IngestTokenSetupTests`, `ProgramTests` | new and adapted tests |

`Program.cs`, `appsettings.json`, the `Dockerfile` and `IngestAccess` stay unchanged (no default for the setting).

## Signatures (for the Dev's skeleton)

In the static class IngestTokenSetup, region Constants:

- `public const string TokenFileKey = "Censio:Ingest:TokenFile";`
- `public const int MinTokenLength = 32;`

TryCreateAccess keeps its signature. Both names follow the class's existing constants (RequireTokenKey,
MaxTokenLength).

## Test files

- `tests/Censio.Server.Tests/IngestTokenSetupTests.cs`, `tests/Censio.Server.Tests/ProgramTests.cs`.

Existing test code: the private helper `CreateConfiguration(string? dataDirectory, string? requireToken)` gets the token
file setting (a third parameter or an overload) — Tester. Adapted by the Tester: the text asserts at lines 391 and 456,
the row `!~` at line 407 and a new "31 characters" row in `IngestTokenSetupTryCreateAccessInvalidFileStops`. Kept
unchanged, checked against the new criteria: `ProgramStartWithInvalidTokenFileExitsWithCodeOne` (`"not a valid token"`
stays invalid), `IngestTokenSetupTryCreateAccessRequireTokenFalseTouchesNoFile` (no setting), the tests of
`IngestAccess`, `IngestSession` and `IngestEndpoint` (they use `CreateForToken` or a generated file).

Files no unit test can cover: none.

## Areas

- `docs/areas/ingest.md`: *Scope* (the token from a configured file); *Token* — the setting, which source wins, the
  configured file read only and never logged by path, read at start only, rotation per source, the rule of 32 to 1,024
  characters, the no-data-directory warning only without the setting, the replaced sentence "There is no other way to
  set or change it"; *Bounds and times* token row; *Start failures* — the four new lines, their order (switch,
  conflict, path, file) and the new range; *Log lines* — no configured path; *Accepted risks* — the configured file's
  access rules are the operator's; *Related decisions* — 0061's new title.
- `docs/areas/server.md`: *Data directory and key ring* — `ingest-token` only without the setting; *Related decisions*
  0061 line.

## Documentation updates

All by the Dev, after the code is green:

- `docs/areas/ingest.md`, `docs/areas/server.md`: as *Areas*.
- `README.md`, English and German: the not-yet-released note (lines 24, 198) and the token paragraph (lines 78, 252) name
  the setting; the `Censio:DataDirectory` rows (149, 323) say `ingest-token` is kept only without the setting; a new
  row `Censio:Ingest:TokenFile` | `CENSIO__INGEST__TOKENFILE` | unset | absolute path of a file holding the token (32 to
  1,024 visible ASCII characters), read at start instead of `ingest-token`; cannot be combined with `RequireToken`
  `false`; never logged — after the `RequireToken` row in both tables.
- `docs/USER_GUIDE.md`, English (*Ingest endpoint and token*, lines 97–129) and German (459–491): a bullet "Your own
  token from a file" with the one `docker run` example of S6
  (`-v <host file>:/run/secrets/censio-ingest-token:ro -e CENSIO__INGEST__TOKENFILE=/run/secrets/censio-ingest-token`),
  that the host file must be readable by the user the container runs as, the generation recommendation
  (`openssl rand -base64 32`), rotation by changing the file and restarting; Unraid, Synology and Docker Desktop for
  Windows "not yet tried", their operators keep the generated `ingest-token`; the start failures with the new lines and
  the range 32 to 1,024 (lines 126, 488); the no-data-directory sentence (129, 491) only without the setting.
- `SECURITY.md` lines 34–36 (the token from the generated file or a configured file, 32 characters at least, the
  configured file's permissions are the operator's) and line 44 (the configured file in scope).
- `docs/ARCHITECTURE.md` lines 69 (the configured file as second source and its start failures), 88 (the token is
  generated with 256 bits or set by the operator with at least 32 characters; the owner-only mode holds for the
  generated file) and 101 (unless a token file is configured).
- `.squad/project.md`: the Dev reports, the orchestrator writes in — *Security areas*, the ingest line: add "the
  setting `Censio:Ingest:TokenFile` and the file it names"; *Integration surface*, the key list: "`Censio:Ingest:RequireToken`
  and `Censio:Ingest:TokenFile` in `IngestTokenSetup`".

No changelog exists; the PR template asks for nothing else.

## Architecture check

The guarantee "the server stores data only with the shared ingest token, which only `RequireToken` `false` switches off"
holds: every new failure stops the start, a missing or empty configured token never opens the endpoint (record 0019),
and the conflict with `false` stops rather than chooses. Fixed-time comparison by hash is unchanged. `docs/ARCHITECTURE.md`
changes as listed; record 0061 is linked from it already.

## Security considerations

- [x] Limits: the configured file is read at most 65,537 bytes (65,536 plus one to detect more) before decoding; the
  length 32 to 1,024 is counted in bytes, each a visible ASCII character, so before and after decoding are equal.
- [x] Exceptions reaching a message: `IOException` and `UnauthorizedAccessException` of the configured read are caught and
  not attached (the fixed E1 text reaches the log); no other exception is expected from a bounded read. The data
  directory path keeps its current exception handling.
- [x] Nothing of the setting's value or the configured path is logged, also on success (N2).
- [x] Fails closed: every invalid combination stops the start before listening.
- [x] Accepted: no check of the configured file's mode (N4); a FIFO at the configured path blocks the start until it is
  written (the operator's own configuration, not an attacker's input).

## Decision records

- `docs/decisions/0061-ingest-token-generated-into-a-file-of-the-data-directory.md` — edited in place (unreleased,
  fact 8): new title, #176 in *Source*, option 3 rewritten, option 6 (file setting) chosen, the source precedence, the
  conflict with `RequireToken` `false` and the minimum weighed; index row in `docs/decisions/README.md` updated. Done by
  the Lead with this plan; the file name stays, so no link changes.

## Out of scope / follow-ups

As the spec's *Out of scope*. No follow-up issue.

## Challenge notes

Devil's Advocate, mode plan, for specs/feature-176-ingest-token-config/ (/home/user/Censio/specs/feature-176-ingest-token-config/plan.md)

The plan holds up on substance. I checked these claims against the code and found them correct:
- Facts 1–7 match the code:
  - `/home/user/Censio/src/Censio.Server/IngestTokenSetup.cs` lines 73–90 (one token source), 120–131 and 298–299 (the "cannot be read" line attaches the exception) and 152–175 (`ParseToken`).
  - `/home/user/Censio/src/Censio.Server/DataProtectionSetup.cs` lines 49 and 74 (`IsPathFullyQualified`).
  - `/home/user/Censio/src/Censio.Server/Program.cs` lines 15–25 (key ring first, then a `null` access gives exit code 1).
- No other production code reads `ingest-token`. `Program.cs:20` is the only caller.
- The existing tests the plan keeps still pass under the 32-character minimum:
  - `IngestTokenSetupTests.cs:467–488` uses the 11-character "MARKERTOKEN", but the file is held with `FileShare.None`, so the read fails before `ParseToken` runs.
  - `IngestTokenSetupTests.cs:113–128` reads a generated 43-character token.
  - `ProgramTests.cs:289–306` uses "not a valid token", which stays invalid.
  - `IngestEndpointTests.cs:221` and `:249` read a generated file.
  - The rows the plan does change (`!~` at line 407, the texts at 391/456) are named correctly.
- CI runs only on `ubuntu-latest` (`.github/workflows/ci.yml:14`), so the symbolic-link and directory rows of AC2/AC3 run where the plan expects.
- Fact 9 (an empty environment value reaches the configuration as `""`) already has a documented precedent: `docs/areas/server.md` line 84–85 says an empty `CENSIO__DATADIRECTORY` "stops the start". The Dev's one-time check of fact 9 should confirm the same holds for the new setting.

Objections:

1. minor — Documentation list is incomplete. Why: four user-guide sentences say without condition that `/config` holds the ingest token, and they would be wrong once the setting is set. Instruction for the Dev: in `docs/USER_GUIDE.md`, also change lines 61 and 349 (English) and 423 and 711 (German) to say `/config/ingest-token` holds the token only when `Censio:Ingest:TokenFile` is unset. This adds to the lines 97–129 / 459–491 the plan already lists. Also read `docs/areas/server.md` line 84 (the image's `/config` "and the ingest token in `/config/ingest-token`") as part of the *Data directory and key ring* / image change.

2. minor — The `docker run -v <host file>:...:ro` example of S6 behaves badly when the host file is missing. Why: with `-v`, Docker silently creates a root-owned directory at the missing host path. The container then fails with E1 because the target is a directory, and the operator is left with a directory where their token file belongs, which breaks the next attempt. Instruction for the Dev: in the user guide (English and German), either use `--mount type=bind,source=<host file>,target=/run/secrets/censio-ingest-token,readonly`, which refuses to start when the source is missing, or keep `-v` and warn that a missing host file is created as a directory that must be removed. Instruction for the Product Manager's dry run (fact 13): for the "host file removed" step, record what actually happens with the documented form: a `docker run` error with `--mount`, or the E1 line and exit code 1 plus a directory left on the host with `-v`. Write the user guide to match what is observed.

## Security notes

I approve the plan. Every invalid value fails closed, no path or token reaches a log, and the three library behaviours the plan left unverified hold when measured. I have three non-blocking findings, listed after the checks.

## Measurements (run before approving)

I ran a file-based C# program `/tmp/claude-0/-home-user-Censio/bca437bd-76a4-59d8-8cc0-d6aabd37e45a/scratchpad/probe/probe.cs` with dotnet 11.0.100 as uid 0. It uses the same configuration chain as `Program.cs` (`WebApplication.CreateBuilder`) and the exact `FileStream` call from `ReadToken`.

Command: `CENSIO__INGEST__TOKENFILE= dotnet run probe.cs`

Output:
```
VALUE: "" len=0
directory: System.UnauthorizedAccessException: Access to the path '/tmp/probe-PATHMARK-3f57.../' is denied.
symlink-to-file: read 3 bytes
symlink-to-missing: System.IO.FileNotFoundException: Could not find file '/tmp/probe-PATHMARK-.../dlink'.
held-FileShare.None: System.IO.IOException: The process cannot access the file '/tmp/probe-PATHMARK-.../locked' because it is being used by another process.
mode-000 (as uid ): read 1 bytes
dev-zero: read 65537 bytes
nul-char: System.ArgumentException: Null character in path. (Parameter 'path')
IsPathFullyQualified("")=False ("/x")=True ("~/x")=False
```

What this shows:
- **Fact 9 confirmed:** an empty environment variable reaches the configuration as `""`, not as unset. So E3 and E4 fire for `CENSIO__INGEST__TOKENFILE=`. The Dev's run of the published server stays useful but is no longer what the approval rests on.
- **Fact 10 confirmed:** a path that is a directory throws `UnauthorizedAccessException`, which is already caught. Its message holds the full path, so leaving the exception off the E1 line is needed, not optional.
- **Fact 11 confirmed:** a symbolic link is followed (E7). A link to a missing file gives `FileNotFoundException`, which is an `IOException`, so the E1 line.
- **The test plan works:** a file held with `FileShare.None` makes the reader throw `IOException` on Linux in the same process. The plan rightly does not use mode `000` for the "cannot read" case, because a root test runner reads it anyway.

## Checks requested

- **No token or path in logs or exception text:** all five new log methods have no placeholder, and the E1 line carries no exception. The value is not logged on the E3, E4 or success paths either. AC7's `MARKER`/`PATHMARK` checks on both message and exception cover every new row. This holds for a token put in the setting by mistake that starts with `/` (a `base64` token can): it is read as a path and hits E1 with nothing logged.
- **Fail-closed:** every invalid combination returns `null`, and `Program` exits with code 1 before `RunAsync` (`Program.cs` lines 20–25). An empty or missing configured token never opens the endpoint. With the setting present, the data-directory fallbacks are never reached, so there is no refuse-all and no generation.
- **`RequireToken false` conflict:** the setting counts as present when it is not `null`, the empty string included, and the check runs before the switched-off path. E5 comes first. The test rows `false`, `FALSE` and `false` with `""` cover it.
- **Symlink and directory:** both measured as above.
- **Minimum length:** 32 to 1,024 counted in bytes after trimming, all visible ASCII, applied to both sources through the shared `ParseToken`. The generated token (43) and `openssl rand -base64 32` (44) both pass. The size bound is kept: one read of at most 65,537 bytes at start (`/dev/zero` reads exactly 65,537 bytes and is rejected).
- **Hash comparison:** unchanged (`IngestAccess.CreateForToken`).
- **Docs:** record 0061 matches the spec. The `SECURITY.md` and `docs/ARCHITECTURE.md` line 88 edits correctly limit the owner-only mode to the generated file.

## Non-blocking findings

1. **One exception type is not caught.**
   - Plan, *Security considerations*, says: "no other exception is expected from a bounded read".
   - Measured `nul-char` row: a value containing a NUL character throws `ArgumentException`, which neither the plan nor `src/Censio.Server/IngestTokenSetup.cs` lines 120–131 catch. JSON or a command-line argument can carry one; an environment variable cannot.
   - It still fails closed (unhandled exception, non-zero exit), and the message does not name the path. But it is an unhandled crash, not the E1 line.
   - Smallest fix: in the configured-file reader, also catch `ArgumentException` (or `catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)`) and log E1, plus one test row with a `\0` value.
   - Recommendation: fix now.

2. **The ingest area document's start-failure section will contradict the new conflict rule.**
   - `docs/areas/ingest.md` starts *Start failures* with "With the token required, the server stops…" and ends it with "With the switch `false` none of the file checks runs." The *Token* bullet for the switch says the server "neither reads nor creates the file".
   - E4 makes all three wrong once the setting is present. The plan's *Areas* entry names "their order" but not these sentences.
   - Smallest fix: add to the plan's *Areas* entry for ingest.md: "*Start failures*: the opening condition becomes 'With the token required, or with `Censio:Ingest:TokenFile` set'; the last sentence becomes 'With the switch `false` and the setting unset, none of the file checks runs.' *Token*, switch bullet: '…neither reads nor creates `ingest-token`; with `Censio:Ingest:TokenFile` set the start stops instead.'"
   - Recommendation: fix now.

3. **The user guide's file-permission advice invites a world-readable secret.**
   - Plan, *Documentation updates*, USER_GUIDE bullet: "the host file must be readable by the user the container runs as".
   - On Linux Docker Engine that user is `1654` unless `--user` is given (`src/Censio.Server/Dockerfile`, `USER 1654:1654`). Without a concrete step, operators tend to `chmod 644` the host file, which leaves the token readable by every host user. That contradicts `SECURITY.md`'s "keep the ingest token private".
   - The documented procedure covers only Linux Docker Engine (N6), so naming the uid there does not conflict with the NAS concern from the challenge.
   - Smallest fix, as the guide text (and its German equivalent): "Make the file readable by the container user only, for example `sudo chown 1654:1654 <host file> && sudo chmod 0400 <host file>` (use the `--user` ID instead if you set one); do not make it readable for everyone."
   - The AC8 dry run should confirm that the container can read the file this way.
   - Recommendation: fix now.

Files reviewed:
- /home/user/Censio/specs/feature-176-ingest-token-config/spec.md
- /home/user/Censio/specs/feature-176-ingest-token-config/plan.md
- /home/user/Censio/src/Censio.Server/IngestTokenSetup.cs
- /home/user/Censio/docs/areas/ingest.md
- /home/user/Censio/docs/decisions/0061-ingest-token-generated-into-a-file-of-the-data-directory.md
- /home/user/Censio/SECURITY.md
- /home/user/Censio/docs/ARCHITECTURE.md
- /home/user/Censio/src/Censio.Server/Dockerfile
