# Plan: Profiles file reader refuses the all-zero profile identity

Source: Issue #254
Tier: standard — the profiles file reader rejects one more input value, a behavior change of a parser's validation (input handling, no security control).
Security areas touched: Parsing of external input (`CompanionProfiles`, `docs/areas/companion-profiles.md`)

## Problem / root cause

`CompanionProfiles.ReadId` (`src/Censio.Companion.Core/CompanionProfiles.cs:788-791`) accepts every string
`Guid.TryParseExact(…, "D", …)` parses, `00000000-0000-0000-0000-000000000000` included. It is the one reader of every
identity of the file: a profile's `id` (`ReadProfiles`, line 809), `active` (`Read`, line 664), a figure's `profile`
(`ReadFigures`, line 856) and a deleted `id` (`ReadDeleted`, line 737). The contract rules refuse the all-zero identity
in every message (`ContractRules.CheckIdentity`, `src/Censio.Contracts/ContractRules.cs:216-219`), so a profile or a
deletion under it can never be sent. `LiveBufferFile.ReadHeader` (`LiveBufferFile.cs:126`) already reads it as outside
the rules.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `ReadId` accepts the all-zero identity for profile ids, the active id and deleted ids | `CompanionProfiles.cs:788-791`: only `ValueKind == String` and `TryParseExact "D"`; no check in `ReadProfiles` (809-819), `Read` (664) or `ReadDeleted` (737-748) excludes `Guid.Empty`; `LiveSenderProfileTests.cs:1506,1525` loads such a deletion as waiting | confirmed |
| 2 | The contract writer refuses that identity | `ContractRules.cs:171-172` (deletion `id`, `profileId`), 291 (hello `profileId`), 343-344 (message `id`, `profileId`), `CheckIdentity` 216-219; `docs/areas/contract.md:21` | confirmed |
| 3 | Such a profile's messages are never written | `LiveSender.Enqueue` (`LiveSender.cs:393-406`) converts with `Active.Id`, a refused write logs `Live message not sent: the contract writer refused it` and drops the item | confirmed |
| 4 | Related effect the issue does not name: with the all-zero profile active, no connection opens — the hello cannot be written | `LiveSender.ChooseHelloProfile` (1067-1086) names `Active`; `IngestClient.cs:456-460` returns `Fail(ConnectionLost)` on a write error, on every attempt | confirmed |
| 5 | Such a deletion is logged as not written on every connection and waits forever, counting toward `MaxDeletions` | `SendDeletionsAsync` (`LiveSender.cs:1305-1322`): write error logs and `continue`s without `ConfirmDeletion`; `IsDeletionQueueFull`/`CountWaiting` (236-245, 1039-1042) count every unconfirmed entry; test `LiveSenderProfileDeletionNotWrittenKeepsWaitingAndSendsNext` asserts it stays waiting | confirmed |
| 6 | The Companion never writes the all-zero identity itself: `Create` uses `Guid.NewGuid()`, `Live` is fixed, a deleted identity is a listed profile's | `CompanionProfiles.cs:377-381`, `LiveSender.cs:226`, `Delete` 445-466 | confirmed |
| 7 | Every other rule breach of the file rejects the whole file: `Read` returns `null`, `Load` logs `Profiles file invalid: not of its format` and uses `Live` alone, `IsFileInvalid` | `CompanionProfiles.cs:283, 636-641, 650-678`; `docs/areas/companion-profiles.md`, *Limits and errors* | confirmed |
| 8 | Forms that parse to the all-zero identity: the plain text, the text padded with white space (`TryParseExact` trims), and JSON escapes of its characters (`0…`, unescaped by `GetString`); letter case does not apply (no letters). The braces/parentheses forms and the form without hyphens are not `D` and are refused today | `TryParseExact` trims: test row `Listed profile deleted, padded with white space` (`CompanionProfilesTests.cs:1569`) is refused as a *listed* identity, so the padded text parsed; escapes: `JsonElement.GetString` returns the unescaped value; rows `Active without hyphens`, `Active in parentheses` | confirmed |

## Acceptance criteria

- AC1: A profiles file in which any identity read by `ReadId` is the all-zero identity — a profile's `id` (also with that
  profile `active`), a figure's `profile`, or a deleted `id` waiting or confirmed — in any form of fact 8 (plain, padded
  with white space, JSON-escaped), is not of its format and fails closed as every rejected file does: `Live` alone and
  active, `IsFileInvalid` true, exactly one `Profiles file invalid: not of its format` warning, no figure link, no
  notice, and no identity deleted or waiting.
- AC2: Live sending with a version-2 file whose `deleted` holds the all-zero identity beside a valid waiting one sends no
  `profileDeletion`, logs no `Live message not sent: the contract writer refused it`, and the hello names `Live`; a tick
  enqueued after the hello is sent on that connection.
- AC3: A file that holds no all-zero identity reads as before (the existing reader tests stand unchanged).

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `CompanionProfilesLoadAllZeroIdentityFailsClosed` (data rows: profile id; profile id and active; figure of the all-zero profile; waiting deleted id; confirmed deleted id; deleted id padded with white space; deleted id JSON-escaped; profile id JSON-escaped) | `tests/Censio.Companion.Core.Tests/CompanionProfilesTests.cs` |
| AC2 | `LiveSenderProfileAllZeroDeletionFileSendsNoDeletion` (replaces `LiveSenderProfileDeletionNotWrittenKeepsWaitingAndSendsNext`) | `tests/Censio.Companion.Core.Tests/LiveSenderProfileTests.cs` |
| AC3 | existing tests: `CompanionProfilesLoadValidFileReadsProfilesActiveAndLinks`, `CompanionProfilesLoadAcceptsUppercaseIds`, `CompanionProfilesInvalidFileUsesLiveAndLinksNothing`, `CompanionProfilesRejectsInvalidDeletedList` | `tests/Censio.Companion.Core.Tests/CompanionProfilesTests.cs` |

AC1's rows mutate `ValidFile` or `VersionTwoFile` with the existing `Mutate`/`Expand` helpers and assert with
`AssertFailedClosed` plus, for the deleted rows, `IsDeleted` false and `WaitingDeletions` empty, as
`CompanionProfilesRejectsInvalidDeletedList` does. The profile-id rows add the all-zero profile as a further entry
named other than `Live` and `MARKER-Second`, so the only rule the file breaks is this one; the figure row needs that
profile listed too, so it isolates nothing more than the profile row and is kept as the case the guard on a figure's
`profile` covers.

## Approach

- `ReadId` returns `null` for `Guid.Empty`, after the parse: `… && Guid.TryParseExact(…, "D", out var id) && id != Guid.Empty ? id : null`.
  Its XML summary says the identity is not all zero. Being the one reader of all four identity fields, this covers them
  all; the guard is on the parsed value, so every form of fact 8 is caught. No other check changes.
- The refused file is a rejected file like any other (fact 7): problem `not of its format`, no new log text, no new
  window text, the replacement on a save as today. Its deleted identities are forgotten, the consequence the area
  document already states for every rejected file.
- `SendDeletionsAsync`'s branch for a refused write (`LiveSender.cs:1316-1321`) stays as defense; after the fix no
  loaded or created state reaches it. It is unchanged code, so the coverage gate on changed lines is not affected.
- No user-visible output is new.

Early draft PR: no

## Options considered

- **Reject the whole file (chosen).** Matches every other rule of the reader and record 0033 (readers fail closed); the
  Companion never writes the identity (fact 6), so only a hand-edited or damaged file is refused, and nothing it wrote
  is lost.
- **Drop only the offending entry and keep the rest.** Lost: it would be the reader's only repairing rule; an all-zero
  `active` or a figure of that profile would need further repair rules (which profile becomes active, which links go);
  and a file that broke one rule in an unknown way is not trustworthy for its other entries — the reasoning behind
  failing closed.
- **Accept the identity and filter it at sending.** Lost: the state would keep an entry that can never leave (the issue's
  waiting-forever deletion), and the hello with it active still fails (fact 4).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Companion.Core` | `CompanionProfiles` (`src/Censio.Companion.Core/CompanionProfiles.cs`) | `ReadId` refuses `Guid.Empty`; its doc comment |

## Signatures (for the Dev's skeleton)

None: the change is inside the private method `ReadId`, whose signature stays.

## Test files

- `tests/Censio.Companion.Core.Tests/CompanionProfilesTests.cs` — new test `CompanionProfilesLoadAllZeroIdentityFailsClosed` (Tester).
- `tests/Censio.Companion.Core.Tests/LiveSenderProfileTests.cs` — `LiveSenderProfileDeletionNotWrittenKeepsWaitingAndSendsNext`
  (line 1495) rests on the bug: it loads an all-zero waiting deletion and asserts it stays waiting. The Tester replaces it
  by `LiveSenderProfileAllZeroDeletionFileSendsNoDeletion` (AC2), whose file is the same; it fails on the unfixed head
  (a `Live message not sent` line is logged and the valid deletion is sent).

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: none.

## Areas

- `docs/areas/companion-profiles.md (+35 words)` — *Limits and errors*: the identity rules name the all-zero identity.

## Documentation updates

Owned by the Dev, written once the code is green:

- `docs/areas/companion-profiles.md`, *Limits and errors*:
  - the second bullet's "an `active`, `id` or `profile` that is not an identity in the `D` form (either letter case)"
    adds "or is the all-zero identity `00000000-0000-0000-0000-000000000000`", with one sentence that the contract
    refuses that identity ([`contract.md`](contract.md)), so a profile or deletion under it could never be sent;
  - the third bullet's "an `id` not in the `D` form" adds "or all zero".
  The fourth bullet (a rejected file forgets the deleted identities) already covers the refused file; no change.
- `docs/ARCHITECTURE.md`, `README.md`, `docs/areas/live-sending.md`, `docs/areas/contract.md`, `docs/areas/companion-log.md`:
  swept (identity rules, `Profiles file invalid` problems, the writer's refusal line); none states the reader's
  identity rule, none changes.

## Architecture check

No guarantee of `.squad/project.md` or `docs/ARCHITECTURE.md` is touched: the reader stays strict and fails closed
(record 0033, `AGENTS.md` *Code style*); the file format and the writer are unchanged.

## Security considerations

- [x] No limits: the change adds a value rule, no byte or length limit.
- [x] None: no new exception; the refusal is a `null` from `ReadId`, logged with the fixed problem `not of its format`,
  never a value of the file.

## Decision records

- none: no decision beyond the obvious fix — the reader's existing fail-closed rule (record 0033) applied to one more
  value; the rule itself lives in `docs/areas/companion-profiles.md`.

## Out of scope / follow-ups

- The unreachable write-refusal branch of `SendDeletionsAsync` stays; removing defensive code is not this issue.
