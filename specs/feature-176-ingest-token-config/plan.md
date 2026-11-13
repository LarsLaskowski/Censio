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
| 9 | An environment variable set to the empty string reaches the configuration as `""`, not as unset, so E3 and E4 fire for `CENSIO__INGEST__TOKENFILE=` | Security's probe through `WebApplication.CreateBuilder` (*Security review*): `VALUE: "" len=0` | confirmed |
| 10 | `FileStream` on a directory throws `UnauthorizedAccessException`, whose message holds the path; a value with a NUL character throws `ArgumentException` | Security's probe: `directory: System.UnauthorizedAccessException`, `nul-char: System.ArgumentException` | confirmed |
| 11 | `FileStream` follows a symbolic link; a link to a missing file throws `FileNotFoundException` (an `IOException`) | Security's probe: `symlink-to-file: read 3 bytes`, `symlink-to-missing: System.IO.FileNotFoundException` | confirmed |
| 12 | `docker run --mount type=bind,source=<host file>,target=/run/secrets/censio-ingest-token,readonly` mounts a single host file read-only on Linux Docker Engine, readable by the container user `1654` after `chown 1654:1654` and `chmod 0400`, and refuses to start when the host file is missing | dry run (AC8) by the orchestrator at the Product Manager's choice, Docker Engine 29.9.2 on Linux: healthy start with the file `1654:1654`/`0400`; file removed: `--mount` refused with exit code 125 and created nothing (`log.md`) | confirmed |
| 13 | `docker logs` shows `The ingest token is read from the file named by Censio:Ingest:TokenFile.` on the documented start | the same dry run | confirmed |
| 14 | The Product Manager can build the image of the branch head on the Linux Docker Engine host | settled by the fallback: the orchestrator built the image of the branch head (code of `443e839`) and ran the dry run itself; only the build stage trusted the session's proxy CA, the runtime stage and the repository's Dockerfile unchanged | confirmed |

## Tests per criterion

All new tests are in `IngestTokenSetupTests` unless named otherwise; the token in every content row contains `MARKER`, and
the configured file lies in a temporary directory whose name contains `PATHMARK`, so every test also asserts that no
entry's message or exception holds `MARKER` or `PATHMARK` (AC7).

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `IngestTokenSetupTryCreateAccessWithTokenFileReadsConfiguredToken` (rows: data directory with a different `ingest-token`, data directory without the file, no data directory): accepts the configured token, refuses the `ingest-token` token and another one, one `Information` entry with the S1 text, the data directory's entries and bytes unchanged; `IngestTokenSetupTryCreateAccessWithTokenFileChangedReadsNewToken` (S4: a second call after the file changed accepts only the new token) | `tests/Censio.Server.Tests/IngestTokenSetupTests.cs` |
| AC2 | `IngestTokenSetupTryCreateAccessTokenFileNotReadableStops` (rows: missing file, a directory, a symbolic link to a missing file, a file held with `FileShare.None`, a fully qualified path containing `\0`): `null`, one `Critical` entry with the E1 text, `Exception` `null`, the missing file not created; `ProgramStartWithMissingTokenFileExitsWithCodeOne` | `IngestTokenSetupTests.cs`; `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC3 | `IngestTokenSetupTryCreateAccessTokenFileInvalidStops` (rows: empty, white space only, 31 characters, 1,025 characters, a space inside, a byte `0x7F`, a byte above ASCII, 65,537 bytes): E2 text, file unchanged; `IngestTokenSetupTryCreateAccessTokenFileAtBoundsReadsToken` (rows: 32 characters starting with `!` and ending with `~`, 1,024 characters, a token and a line feed, 1,024 characters padded to 65,536 bytes); `IngestTokenSetupTryCreateAccessTokenFileSymbolicLinkReadsToken` | `IngestTokenSetupTests.cs` |
| AC4 | `IngestTokenSetupTryCreateAccessTokenFileNotAbsoluteStops` (rows: `""`, `token`, `relative/token`, a 43-character `MARKER` token value): E3 text, the value in no entry | `IngestTokenSetupTests.cs` |
| AC5 | `IngestTokenSetupTryCreateAccessTokenFileWithRequireTokenFalseStops` (rows: `false` and `FALSE` with a valid configured file, `false` with `""`): E4 text, `null`; `IngestTokenSetupTryCreateAccessTokenFileWithInvalidRequireTokenReportsSwitch` (rows: `yes`, `""`): only the existing switch line | `IngestTokenSetupTests.cs` |
| AC6 | adapted `IngestTokenSetupTryCreateAccessInvalidFileStops` (new row "31 characters", E6 text) and `IngestTokenSetupTryCreateAccessFileAboveSizeBoundStops` (E6 text); adapted `IngestTokenSetupTryCreateAccessFileAtBoundsReadsToken` (row `!~` replaced by 32 characters starting with `!` and ending with `~`); `IngestTokenSetupTryCreateAccessWithFileReadsSameToken` gets the category `AC6` (a generated token read at the next start) | `IngestTokenSetupTests.cs` |
| AC7 | the `MARKER`/`PATHMARK` assertions of every AC1–AC6 test above | `IngestTokenSetupTests.cs` |
| AC8 | manual: the Product Manager's dry run on a Linux Docker Engine host, on an image built from the branch head with `docker build -f src/Censio.Server/Dockerfile -t censio .`, of the user guide's new steps: start with the host file owned by `1654` with mode `0400`, the documented `--mount ...,readonly` and the setting (log line of S1); the same with the host file removed, recording what happens (a `docker run` error with `--mount` expected). Fallback when the PM cannot build there: the orchestrator builds the image where Docker runs, saves it (`docker save`) and hands it over (`docker load`) | — |

The existing tests of #28 in this class carry the ids `AC1`, `AC4`, `AC7`, `AC8` of that issue; `--matrix` counts them
for this spec's ids of the same name. The verification matrix is therefore read against the test names above, and the
Tester adds this spec's id to an old test only where it is listed here (AC6).

## Approach

`IngestTokenSetup.TryCreateAccess` decides in this order (each failure logs one line and returns `null`):

1. `RequireToken` neither `true` nor `false`: the existing line (E5).
2. `RequireToken` `false`: with `Censio:Ingest:TokenFile` present (not `null`, the empty string included) the E4 line;
   otherwise the existing switched-off path.
3. The setting present: not `Path.IsPathFullyQualified` → the E3 line. Otherwise the configured file is read by the
   same bounded read and `ParseToken` as `ingest-token`; a read failure (`IOException`, `UnauthorizedAccessException`,
   and on this path also `ArgumentException`, which a NUL character in the value raises) logs the E1 line **without**
   the exception, an invalid content the E2 line, a valid one the S1 line and returns
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
  set or change it"; the switch bullet: "…neither reads nor creates `ingest-token`; with `Censio:Ingest:TokenFile` set
  the start stops instead"; *Bounds and times* token row; *Start failures* — the opening condition becomes "With the
  token required, or with `Censio:Ingest:TokenFile` set", the last sentence "With the switch `false` and the setting
  unset, none of the file checks runs", the four new lines, their order (switch, conflict, path, file) and the new
  range; *Log lines* — no configured path; *Accepted risks* — the configured file's
  access rules are the operator's; *Related decisions* — 0061's new title.
- `docs/areas/server.md`: *Data directory and key ring* (lines 58–61) and *Docker image* (line 84) — `ingest-token`
  only without the setting; *Related decisions* 0061 line.

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
  (`--mount type=bind,source=<host file>,target=/run/secrets/censio-ingest-token,readonly -e CENSIO__INGEST__TOKENFILE=/run/secrets/censio-ingest-token`,
  as the dry run confirms it), the permission step "Make the file readable by the container user only, for example
  `sudo chown 1654:1654 <host file> && sudo chmod 0400 <host file>` (use the `--user` ID instead if you set one); do
  not make it readable for everyone.", the generation recommendation
  (`openssl rand -base64 32`), rotation by changing the file and restarting; Unraid, Synology and Docker Desktop for
  Windows "not yet tried", their operators keep the generated `ingest-token`; the start failures with the new lines and
  the range 32 to 1,024 (lines 126, 488); the no-data-directory sentence (129, 491) only without the setting; lines 61
  and 349 (English) and 423 and 711 (German): `/config/ingest-token` holds the token only when the setting is unset.
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
- [x] Exceptions reaching a message: `IOException`, `UnauthorizedAccessException` and `ArgumentException` (a NUL in the
  value) of the configured read are caught and not attached (the fixed E1 text reaches the log). The data
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

## Challenge

- 1 (minor, documentation list incomplete): accepted. *Documentation updates* adds user guide lines 61, 349, 423 and
  711, and *Areas* adds `docs/areas/server.md` line 84, each conditional on the setting being unset.
- 2 (minor, `-v` creates a directory at a missing host path): accepted. The user guide uses
  `--mount type=bind,...,readonly`, which refuses a missing source; the dry run records what happens with the file
  removed (fact 12, *Tests per criterion* AC8) and the guide follows what it observes.

## Security review

Security approved the plan; its probe confirmed facts 9 to 11 (now `confirmed` with its output) and the
`FileShare.None` test case. The three non-blocking findings are fixed in this plan:

- 1 (`ArgumentException` for a NUL in the value is not caught): fixed. The configured-file reader also catches it and
  logs the E1 line without the exception (*Approach*, *Security considerations*), with a `\0` row in AC2's test.
- 2 (`ingest.md` *Start failures* and the switch bullet contradict E4): fixed with the exact sentences in *Areas*.
- 3 (the permission advice invites a world-readable file): fixed. The user guide gives `chown 1654:1654` and
  `chmod 0400` and says never to make it readable for everyone (*Documentation updates*); the dry run uses that setup
  (fact 12, AC8).
