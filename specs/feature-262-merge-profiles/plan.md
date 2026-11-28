# Plan: Merge two profiles

Source: Issue #262 | [spec.md](spec.md)
Tier: standard — a new behavior across Companion, contract and server that reuses the token guard of the deletion and creates, changes or removes no security control.
Security areas touched: Ingest token and Companion authentication (`IngestSession` handles the new message type); Server database (`ServerStore.MergeProfileAsync`, `ServerState`); Parsing of external input (`ContractReader`, `ContractRules`, `CompanionProfiles`); File paths the Companion writes (`CompanionProfiles`, `profiles.json`); Logging and showing external data (`CompanionLog`, `SettingsWindow` and the new dialog showing profile names); Outbound calls (`LiveSender.ForServer` sends the new message)
Plan size: the change spans five projects, a file format, a contract message and a database operation; the signatures and the per-criterion tests need the room.

## Problem / root cause

See [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | Contract version 3 and the profiles file of record 0115 are unreleased | `git ls-remote --tags origin`: latest `v0.4.0`; the commit adding `profileDeletion` to `docs/areas/contract.md` is no ancestor of `v0.4.0`; `docs/areas/companion-profiles.md` does not exist at `v0.4.0` | confirmed |
| 2 | Waiting deletions are one list `_deleted` of (identity, confirmed), bounded by `MaxDeletions` 1,000, written as `deleted` with version 2 | `CompanionProfiles.cs:80`, `:36`, `Serialize` `:1203-1254`, `ReadDeleted` `:811-839` | confirmed |
| 3 | `Delete` refuses with `ArgumentException` (`IsDeletable`) and with `InvalidOperationException` when the queue is full | `CompanionProfiles.cs:524-564` | confirmed |
| 4 | Live sending sends waiting deletions after the hello, one at a time, before catalog and buffer, and confirms them on acknowledgement or refusal | `LiveSender.cs` `StartTurnAsync` `:1293`, `SendDeletionsAsync` `:1404-1447`, `FirstUntried` `:527` | confirmed |
| 5 | A buffered message's profile is read from its JSON (`ProfileOf`, a full `ContractReader.Read`), and it is sent as its stored bytes | `LiveSender.cs:545-555`, `SendBufferedAsync` `:1457` | confirmed |
| 6 | The hello falls back to the active profile when the named one is not in the list | `ChooseHelloProfile`, `LiveSender.cs:1134-1167` | confirmed |
| 7 | The message type's wire name comes from the enum (camel case), no literal to add | `ContractScanner.cs:148` (`Enum.GetValues<ContractMessageType>()`), `ContractJsonContext.cs:9-18` | confirmed |
| 8 | The reader refuses unknown properties | `ContractJsonContext.cs:10` (`UnmappedMemberHandling.Disallow`) | confirmed |
| 9 | The server's deletion runs under the state's write gate, re-reads profiles and held states, falls back to dropping the profile on a failed read | `ServerState.cs:545-596`; logs in `HistoryRetention.cs:147-170, 193-202` | confirmed |
| 10 | Unique keys: `Islands` (profile, session GUID, island, area); `HourlyPoints` (profile, hour); `HourlyIslands` (point, key); `HourlyEntries` (hourly island, product GUID); `HourlyMapPairs` (entry, kind, GUID); `TradeRouteSets` keyed by profile; `TradeRoutes` (profile, position); `Ticks.Id` and `FoldedTicks.Id` unique | `CensioDbContext.cs:30-200` | confirmed |
| 11 | An island's name follows the later stamp (`NameReceiveTicks`, `NameTickNumber`), in the profile's list and in an hourly point | `TickWriter.cs:106-108`, `HistoryFolder.cs:38-40` | confirmed |
| 12 | A resent tick is recognised by its identity across profiles (unique index), folded or not | fact 10; `docs/areas/server-data.md` *Tick* | confirmed |
| 13 | The Companion waits 30 seconds for an acknowledgement | `docs/areas/live-sending.md` status table | confirmed |
| 14 | A merge takes at most 3 s for each case of *Measurement* | to verify: Dev, step 6, throwaway test outside the repository through the real `ServerStore` | unverified |
| 15 | The largest `profiles.json` with 1,000 merge entries is 678,718 bytes | arithmetic: the test's 630,718 (`CompanionProfilesTests.cs:1968-1972`) plus 48 bytes `"target":"<36>",` per entry; the AC5 test asserts it | confirmed |

## Tests per criterion

Every test carries `[TestCategory("AC<n>")]`.

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `CompanionProfilesMergeMovesLinksAndRemovesSource` (list, links, notice cleared, `Profile merged` without name or identity, `DeletionsChanged` once, one waiting merge; data rows: target `Live`, target the active profile, target another), `CompanionProfilesMergeRefusedThrowsAndChangesNothing` (data rows: source `Live`, source active, source unknown, target unknown, target equal to source), `CompanionProfilesMergeWithFullQueueThrowsAndChangesNothing` (500 deletions and 500 merges waiting), `CompanionProfilesIsDeletionQueueFullCountsMerges` | `tests/Censio.Companion.Core.Tests/CompanionProfilesMergeTests.cs` |
| AC2 | `SettingsViewCanMergeAllButLiveActiveAndUnknown`, `SettingsViewMergeTargetsListOthersInListOrder`; `SettingsWindowSessionMergeMergesAndShowsProfiles` (the view's `WaitingMerges`, the unsaved values of the last view kept), `SettingsWindowSessionMergeIgnored` (data rows: other sender, before first view, during save, `Live`, active, full queue, after close); `CompanionTextsMergeTextsInBothLanguages` (every row of the spec's *Texts*, `ChangesQueueFullText` included); the window and the dialog: check (Reviewer, step 8) | `SettingsViewTests.cs`, `SettingsWindowSessionTests.cs`, `CompanionTextsTests.cs` (all `tests/Censio.Companion.Core.Tests/`) |
| AC3 | `LiveSenderMergeSentAfterHelloBeforeCatalogAndBuffer` (`profileMerge` with both identities, confirmed, `Profile merge confirmed by the server`), `LiveSenderMergeRefusedConfirmsAndWarns`, `LiveSenderMergeLostConnectionKeepsWaitingAndResends`, `LiveSenderMergeAndDeletionSentInOrderMade` (data rows: merge `A` into `B` then delete `B`; delete `C` then merge `A` into `B`; each also after a reload of `profiles.json` and a new sender); `CompanionProfilesMergeWaitsAcrossReload` | `LiveSenderMergeTests.cs`; `CompanionProfilesMergeTests.cs` |
| AC4 | `LiveSenderMergeBufferedMessageSentUnderTarget` (data rows: tick, trade routes, session start, session end; same message identity, hello names the target), `LiveSenderMergeChainResolvesToLastTarget`, `LiveSenderMergeTargetDeletedDropsBufferedMessage`, `LiveSenderMergeOfConnectionProfileReconnectsAsSwitch`; `CompanionProfilesResolveProfileFollowsMerges` (data rows: not merged, merged, chain of two, target deleted); `ContractMessageConverterWithProfileReplacesOnlyProfile` (data rows: the four kinds; `null` for a status) | `LiveSenderMergeTests.cs`, `CompanionProfilesMergeTests.cs`, `ContractMessageConverterTests.cs` |
| AC5 | `CompanionProfilesMergeWrittenAndReadBackInOrder` (deletions and merges interleaved, waiting and confirmed, the entry text `{"id":"…","target":"…","confirmed":false}`), `CompanionProfilesLoadRejectsMalformedMerge` (data rows: unknown property, repeated `target`, target not `D` form, target all zero, target equal to its id, source `Live`, source listed, source repeated as a deletion, loop of two, loop of three), `CompanionProfilesLargestFileWithMergesUnderBound` (678,718 bytes) | `CompanionProfilesMergeTests.cs` |
| AC6 | `ContractReaderProfileMergeReadsAllProperties`, `ContractReaderProfileMergeInvalidRefused` (data rows: equal identities, all-zero `id`, `profileId`, `targetProfileId`, missing `targetProfileId`, unknown property); `ContractWriterProfileMergeWritesWireForm` (the example line of `contract.md`) | `tests/Censio.Contracts.Tests/ContractReaderTests.cs`, `ContractWriterTests.cs` |
| AC7 | `ServerStoreMergeMovesTicksIslandsAndRemovesSource` (target name kept, storing order kept, island names by the later stamp), `ServerStoreMergeCombinesHourlyPointsOfSameHour` (counts, every sum, `EntryCount`, pair `AmountSum`, game-time range, hourly island names; a resent folded tick of the source reported as stored), `ServerStoreMergeKeepsLaterTradeRouteSet` (data rows: source later, target later, equal, source only, target only), `ServerStoreMergeIntoUnstoredTargetCreatesUnnamed`, `ServerStoreMergeOfUnstoredSourceChangesNothing`, `ServerStoreMergeFailureChangesNothing` (the store made to fail as the deletion's failure tests do); `IngestSessionProfileMergeMergesAndAcknowledges`, `IngestSessionProfileMergeOfUnknownSourceAcknowledged`, `IngestSessionProfileMergeFailedClosesWith1011`; `CensioServerMergeFromCompanionMovesHistory` (a Companion merges and the server lists the target only); N4: check (Reviewer: set-wise statements, no rows read into memory but point pairs) | `tests/Censio.Server.Data.Tests/ServerStoreMergeTests.cs`; `tests/Censio.Server.Tests/IngestSessionTests.cs`, `CensioServerTests.cs` |
| AC8 | `ServerStateMergeProfileRereadsAndNotifies` (source neither listed nor held, target held with the newer latest tick, notices `Profile` for both and `TradeRoutes` for the target), `ServerStateMergeProfileReadFailureDropsSource`; `RegionsMergedProfileShowsNotFound`; `ServerStoreMergeResentTickReportedAsStored` (data rows: resent under the target; under the source after the merge recreates it unnamed, E13); E14: check (Reviewer, step 8: one transaction, each part reads in its own) | `tests/Censio.Server.Tests/ServerStateTests.cs`, `RegionsTests.cs`; `ServerStoreMergeTests.cs` |
| AC9 | `HistoryRetentionMergeProfileLogsWithoutName`, `HistoryRetentionMergeProfileFailedLogsWarning`; the Companion lines are asserted in the AC1 and AC3 tests | `tests/Censio.Server.Tests/HistoryRetentionTests.cs` |
| AC10 | check: the Dev measures (*Measurement*) and records the table in `history-deletion.md`; the Reviewer reads it in step 8 | — |
| AC11 | check: the Reviewer reads the README and user guide sections, English and German, in step 8 | — |

## Approach

Early draft PR: no

**Contract (G1).** `ContractProfileMerge` (`id`, `profileId`, `targetProfileId`) joins version 3 as the thirteenth message,
Companion to server, answered by `acknowledgement`. `ContractRules` checks the three identities as a deletion's and
requires `profileId != targetProfileId` (else `invalid`). Plumbing per *Integration surface*: `ContractMessageType`
(appended), `ContractJsonContext`, `ContractReader` (`Deserialize`, `GetRefusedId`), `ContractWriter`; version texts unchanged.

**Server (G2).** `ServerStore.MergeProfileAsync` runs in one `StoreAccess.WriteAsync` transaction:
1. source not stored → `NothingToDo`; target not stored → insert its row without a name;
2. `Islands`: upsert the source's rows into the target by key, the name and stamp taken when the source's stamp is not
   older (the comparison of `TickWriter`), then delete the source's rows;
3. `Ticks`: relabel to the target (numbers, so storing order, unchanged);
4. `HourlyPoints` of hours the target lacks: relabel; for the hours both hold, the internal static class
   `HourlyPointMerge.CombineAsync(SqliteConnection, Guid, Guid, CancellationToken)` reads only the pairs of point
   numbers and, per pair with set-wise statements: adds `TickCount`, takes the minimum and maximum game times; moves the
   source's hourly islands the kept point lacks, updates the name of a shared one by the later stamp; under a shared
   island moves the entries the kept one lacks and adds every count and sum of a shared entry (`EntryCount` included);
   under a shared entry moves the pairs it lacks and adds `AmountSum`; re-points `FoldedTicks`; deletes the source's
   remaining rows of the point;
5. trade routes: the source's set replaces the target's when the target has none or the source's `ReceiveTicks` is
   greater (the existing `DeleteTradeRoutesAsync` removes the loser, then `TradeRouteSets` and `TradeRoutes` are
   relabelled); otherwise the source's set is deleted;
6. delete the source's profile row.
Memory: no history row is read into memory; the point pairs are two numbers per shared hour.

`ServerState.MergeProfileAsync` takes the write gate and, once the store reports `Done`, re-reads as the deletion does
through one private helper both share (`ApplyRemovalAsync(Guid removed, CancellationToken)`; on a failed read the
source is dropped from list, held states and marks), then notifies `Profile` for source and target and `TradeRoutes`
for the target. `HistoryRetention.MergeProfileAsync` logs `The history of a profile was merged into another.`
(Information) or `The history of a profile could not be merged into another.` (Warning, exception) beside the deletion's
lines; it asks for no sweep. `IngestSession` handles `ContractProfileMerge` as the deletion: `Done` or `NothingToDo`
acknowledged, `Failed` closes with 1011 without a line of its own.

**Measurement (AC10).** Through the real `ServerStore` over a database file, three runs each: (a) a source with a year of
hourly points (8,684) and 20,190 unfolded ticks into a target holding none of those hours; (b) two profiles each with 7
days of fixture ticks folded (168 shared hours). Bound: 3 s per merge (fact 14). A run over the bound: the Dev reports
it and the Lead decides (record 0119, option 4).

**Companion profiles (G3).** `_deleted` becomes `List<ProfileRemoval>` (identity, optional target, confirmed), one
ordered queue; a merge entry is written as `{"id":"…","target":"…","confirmed":false}` (that property order) inside
`deleted`, version 2 as before. `Merge` checks `IsMergeable` (`IsDeletable` and a listed target other than the source),
then the queue (`InvalidOperationException`), removes the source, moves its figure links to the target in place,
clears the notice, appends the waiting entry (forgetting the oldest confirmed one beyond 1,000 as `Delete` does),
writes, logs `Profile merged` and raises `DeletionsChanged`. `IsDeletionQueueFull` counts all waiting entries;
`WaitingDeletions` keeps only deletions; `ConfirmMerge` mirrors `ConfirmDeletion` through one private helper.
`ResolveProfile` follows merge entries, waiting or confirmed, at most as many steps as there are merge entries, and
gives `null` beyond (unreachable once the reader rejects loops: one uncovered line accepted). The reader takes an entry
with exactly `id`/`confirmed` or `id`/`target`/`confirmed`, applies today's `id` rules across all entries, requires a
target in `D` form, not zero, not its own `id`, and rejects a loop by following each merge's chain.

**Live sending (G4).** `FirstUntried` walks `WaitingRemovals` in order; `SendRemovalsAsync` (renamed from
`SendDeletionsAsync`) sends a `ContractProfileDeletion` or a `ContractProfileMerge`, confirms, and logs the deletion's
lines or `Profile merge confirmed by the server` / `Profile merge refused by the server: <refusal>` (Warning). After the
acknowledgement of a merge whose source is the connection's profile it ends the connection as a switch (`_helloProfile`
null, `_isSwitching`). For a buffered message `ProfileOf` keeps the read message; its profile goes through
`ResolveProfile`: `null` or a deleted identity drops it as today; another identity is written again by
`ContractMessageConverter.WithProfile` and `ContractWriter.Write` (a write error logs `LiveMessageNotWritten` and drops
it) and sent with the same message identity; `IsOfOtherProfile` and the hello take the resolved identity. One message is
read and written at a time.

**Window (G5).** `Merge profile…` right of `Delete profile…`, enabled by `SettingsView.CanMerge` (the deletion's rule),
tooltip `MergeProfileDisabledText`. Dialog `ProfileMergeDialog`, laid out as `ProfileDeletionDialog`: title, the label
`Move the profile <name> into:` with a drop-down of `MergeTargets` (no preselection; `Merge` disabled until one is
chosen), the explanation paragraph, the queue-full text when full (then `Merge` disabled), buttons `Merge` and
`Cancel`. The line `Merged profiles whose history the server has not moved yet: <n>` follows the deletion line and shows
while `WaitingMerges` > 0. `ChangesQueueFullText` replaces `DeleteProfileHistoryFullText` in both dialogs.

## Options considered

Records [0118](../../docs/decisions/0118-profile-merged-in-the-companion-one-ordered-queue-with-the-deletions.md)
(Companion: pages, rename-merges, a separate list, rewriting the buffer on disk, a merge without history — each lost
there) and [0119](../../docs/decisions/0119-history-merged-through-the-ingest-connection-in-one-transaction.md)
(contract version 4; a tombstone-like marker with steps). Also weighed: a new area document `history-merging.md` (the
spec's first outline) — lost, a few hundred words fold into `history-deletion.md` (*Areas* rule of the README).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Contracts | `ContractProfileMerge` (new), `ContractMessageType`, `ContractJsonContext`, `ContractReader`, `ContractRules`, `ContractWriter` | message |
| Censio.Server.Data | `ServerStore`, `HourlyPointMerge` (new, internal) | merge transaction |
| Censio.Server | `ServerState`, `HistoryRetention`, `IngestSession` | state, logs, ingest |
| Censio.Companion.Core | `ProfileRemoval` (new), `CompanionProfiles`, `LiveSender`, `ContractMessageConverter`, `SettingsMerge` (new), `ISettingsWindow`, `SettingsView`, `SettingsWindowSession`, `CompanionTexts`, `CompanionLog` | queue, sending, window model, texts, logs |
| Censio.Companion | `ProfileMergeDialog.xaml(.cs)` (new), `SettingsWindow.xaml(.cs)`, `ProfileDeletionDialog.xaml.cs` | Windows shell |

## Signatures (for the Dev's skeleton)

Properties are written as their member name followed by type and accessors in plain text; every member is public.

Censio.Contracts:
- `ContractProfileMerge : ContractMessage` (public sealed class) with `ContractProfileMerge.Id`, `ContractProfileMerge.ProfileId`, `ContractProfileMerge.TargetProfileId` (each Guid, required, get/init) and `ContractProfileMerge.Type` (override, returns ContractMessageType.ProfileMerge)
- `ContractMessageType.ProfileMerge` (the last value)

Censio.Server.Data / Censio.Server:
- `public Task<HistoryResult> ServerStore.MergeProfileAsync(Guid profileId, Guid targetProfileId, CancellationToken cancellationToken)`
- `public async Task<HistoryResult> ServerState.MergeProfileAsync(Guid profileId, Guid targetProfileId, CancellationToken cancellationToken)`
- `public async Task<HistoryOutcome> HistoryRetention.MergeProfileAsync(Guid profileId, Guid targetProfileId, CancellationToken cancellationToken)`

Censio.Companion.Core:
- `ProfileRemoval` (public sealed class) with `ProfileRemoval.Id` (Guid, required), `ProfileRemoval.Target` (Guid?, optional), `ProfileRemoval.IsConfirmed` (bool, required), each get/init
- `CompanionProfiles.WaitingRemovals` (IReadOnlyList of ProfileRemoval, get), `CompanionProfiles.WaitingMerges` (IReadOnlyList of Guid, get)
- `public static bool CompanionProfiles.IsMergeable(Guid id, Guid target, Guid active, IReadOnlyList<CompanionProfile> profiles)`
- `public void CompanionProfiles.Merge(Guid id, Guid target)`
- `public void CompanionProfiles.ConfirmMerge(Guid id)`
- `public Guid? CompanionProfiles.ResolveProfile(Guid id)`
- `public static ContractMessage? ContractMessageConverter.WithProfile(ContractMessage message, Guid profileId)`
- `SettingsMerge` (public sealed class) with `SettingsMerge.Profile` and `SettingsMerge.Target` (each Guid, required, get/init)
- `ISettingsWindow.MergeRequested` (event, EventHandler of SettingsMerge, nullable)
- `SettingsView.WaitingMerges` (int, get/init)
- `public bool SettingsView.CanMerge(Guid selected)`
- `public IReadOnlyList<CompanionProfile> SettingsView.MergeTargets(Guid selected)`
- `CompanionTexts.MergeProfileText`, `CompanionTexts.MergeProfileDisabledText`, `CompanionTexts.MergeProfileTitle`, `CompanionTexts.MergeProfileTargetFormat`, `CompanionTexts.MergeProfileTextFormat`, `CompanionTexts.MergeProfileConfirmText`, `CompanionTexts.WaitingMergesFormat`, `CompanionTexts.ChangesQueueFullText` (each string, required, get/init)
- Removed: `CompanionTexts.DeleteProfileHistoryFullText` (replaced by `ChangesQueueFullText`).
- `public static partial void CompanionLog.ProfileMerged(ILogger logger)`
- `public static partial void CompanionLog.ProfileMergeConfirmed(ILogger logger)`
- `public static partial void CompanionLog.ProfileMergeRefused(ILogger logger, ContractAcknowledgementRefusal refusal)`

Names checked against *Writing code*: no parameter count above four, no nested type, no underscore; the skeleton holds no
real logic, so every test of the plan fails on it.

## Test files

As in *Tests per criterion*; new files `CompanionProfilesMergeTests.cs`, `LiveSenderMergeTests.cs`
(`tests/Censio.Companion.Core.Tests/`) and `ServerStoreMergeTests.cs` (`tests/Censio.Server.Data.Tests/`).

Existing test code the change affects, adapted by the Tester: `FakeSettingsWindow` (the event `MergeRequested`),
`ScriptedIngestServer` (`GetId` of `ContractProfileMerge`), `tests/Censio.Server.Tests/IngestClient.cs` (a
`ProfileMerge` builder beside `ProfileDeletion`), `CompanionTextsTests` (the row of `DeleteProfileHistoryFullText`
becomes `ChangesQueueFullText` with the new texts). A test kept unchanged has no input a criterion handles differently:
the deletion tests' queues hold no merge.

Files no unit test can cover: `src/Censio.Companion/ProfileMergeDialog.xaml(.cs)` and the `SettingsWindow` edits — the
Windows shell, excluded already (`.squad/stack.md`, *Coverage gate*).

## Areas

- `docs/areas/companion-profiles.md (+450 words)`: *Merging* section; the queue rules in *Deleting*, *Writing* and
  *Limits and errors* name merges; "Merging two profiles is not done" goes.
- `docs/areas/companion-settings.md (+110 words)`: button, tooltip, dialog, waiting line.
- `docs/areas/companion-texts.md (+110 words)`: *Profile merging* table; the changed queue-full row.
- `docs/areas/companion-log.md (+30 words)`: the three lines.
- `docs/areas/live-sending.md (+30 words)`: the deletion sentence of *Profiles on the connection* covers merges, the
  resolution and the reconnect; details stay in companion-profiles (the document is near `AREA_MAX_WORDS`).
- `docs/areas/contract.md (+90 words)`: the row, the example line, "thirteen messages", identity rules.
- `docs/areas/ingest.md (+70 words)`: the answer and the accepted risk beside the deletion's.
- `docs/areas/history-deletion.md (+550 words)`: scope widened; *Merging a profile* (steps 1–6, failure, log lines,
  measurement table).
- `docs/areas/server-state.md (+90 words)`: *Merging profiles* beside *Deleting a profile*.
- `docs/areas/server-data.md (+50 words)`: a *Merge* bullet beside the deletion.
- `docs/areas/history.md (+20 words)`: "a fold, a deletion or a merge between two parts".
- `docs/areas/web-charts.md (+4 words)`: "a profile deleted or merged while the page is open". `web-pages.md` stays
  untouched (at `AREA_MAX_WORDS`): `server-state.md` says the pages treat a merged profile as a deleted one.

## Documentation updates

All by the Dev, once the code is green, except where named:
- the area documents of *Areas*, and the index row of `history-deletion` in `docs/areas/README.md` (scope: merging);
- `README.md` (lines 154 and 419): a merge sentence block after the deletion's, English and German, the spec's texts;
- `docs/USER_GUIDE.md`: the `Profile` rows (lines 219, 884) name `Merge profile…`; a **Merge** / **Zusammenführen**
  bullet after **Delete** (260, 925); the deletion bullets' queue-full text becomes `1,000 changes already wait for the
  server.` / `Schon 1.000 Änderungen warten auf den Server.`;
- `docs/ARCHITECTURE.md`: line 22 (`Censio.Server.Data`: "the deletion or merge of a profile"), line 64 (one sentence on
  the merge, linking record 0119), line 105 (the Companion also merges through `/ingest`);
- `.squad/project.md (+2 words)`: in the ingest security line `ContractProfileDeletion` becomes `ContractProfileDeletion`,
  `ContractProfileMerge` — the Dev reports the text, the orchestrator writes it.
No changelog file exists; the release-notes label is the orchestrator's (step 10).

## Architecture check

Record 0019 (the token guards every stored message) holds: the merge is one more message after an accepted hello.
Records 0021 and 0116 (deletable per profile, deletion through `/ingest`) are unchanged; the merge keeps history.
Record 0031 (buffered ticks delivered in order, gaps never zeros) holds: buffered messages keep their order and identity,
only their profile is resolved. No guarantee of `.squad/project.md` is weakened.

## Security considerations

- [x] Limits: `profiles.json` keeps its 1 MiB raw-byte bound (before decoding) and 1,000 queue entries; the message's
  size is bounded by `MaxMessageBytes` on raw bytes as every message.
- [x] Exceptions reaching a user: none — `Merge`'s exceptions are prevented by the session's checks; the server's failure
  reaches the Companion as a closed connection; the log lines carry no message text of an exception to a page.

## Decision records

- `docs/decisions/0118-profile-merged-in-the-companion-one-ordered-queue-with-the-deletions.md` (Proposed)
- `docs/decisions/0119-history-merged-through-the-ingest-connection-in-one-transaction.md` (Proposed)
- `docs/decisions/0117-…` (unreleased, edited in place): option 7 links record 0118.

## Out of scope / follow-ups

The spec's *Out of scope*; a merge on the pages becomes an issue only if asked for.

## Security notes

Security plan review: specs/feature-262-merge-profiles (spec.md + plan.md), mode plan

**Verdict: approved, with no blocking findings and 5 non-blocking ones.** `standard` is the right tier. The merge adds a new operation behind the existing ingest token and browser-upgrade refusal (records 0019, 0064, 0116). It creates, changes or removes no security control, and the risk that any token holder can merge any profile is accepted in record 0119 and spec N6.

**What I checked and found sound**
- **A dropped connection does not undo a merge.** The deletion runs under the server's stopping token, not the connection's (`src/Censio.Server/IngestSession.cs:597`), and the plan has the merge handled the same way. So if the Companion stops waiting after 30 s, the merge still finishes; the resend then finds the source gone and is acknowledged (E6).
- **A merge cannot bring back a deleted history.** A deleted profile has no row in `Profiles`, and its ticks and hourly points carry a random tombstone identity (`docs/areas/history-deletion.md`). A merge whose source was deleted on the pages is therefore `NothingToDo`, and a merge into a deleted target creates a fresh unnamed row. The tombstoned rows are never touched.
- **Database access.** The raw SQL in this store is parameterised through `StoreAccess.CreateCommand` (`ServerStore.cs:107,137,528`). The plan reads only the pairs of point numbers into memory, which is bounded by retention and small; that is acceptable for N4.
- **Logging.** None of the plan's log lines carry a name or an identity; `ProfileMergeRefused` logs only the refusal enum.
- **`IngestSession` cases.** The unknown-type default branch acknowledges without storing (`IngestSession.cs:603-605`), and `GetId` returns `null` by default (`:246`). A missing `ContractProfileMerge` case would therefore silently acknowledge merges that never ran. The plan's three `IngestSessionProfileMerge*` tests cover this.
- **`profiles.json` reader.** I read `CompanionProfiles.Load`/`ReadDeleted`/`ReadProperties`/`ReadId` (`src/Censio.Companion.Core/CompanionProfiles.cs:262-294, 811-876`):
  - It parses with `JsonDocument.Parse`, default options: no comments, no trailing commas.
  - Property names are matched ordinal and exactly once.
  - Identities go through `Guid.TryParseExact(…, "D")`.

**[NON-BLOCKING] 1. The *Network exposure* area is touched but not listed.**
- The `.squad/project.md` Network exposure line names "the history deletion (`DeletionHostRule` on the pages, the token through `/ingest`)".
- With `Censio:Ingest:RequireToken` set to `false`, the merge is a second history-changing action that anyone who reaches `/ingest` can use, the same way as the deletion. N6 ("The pages offer no merge") is itself a claim about this area.
- Smallest fix: add "Network exposure (`/ingest` merges history, `DeletionHostRule` unchanged)" to the plan's *Security areas touched*. Extend the plan's `.squad/project.md` update so the Network exposure line reads "the history deletion (`DeletionHostRule` on the pages, the token through `/ingest`) and the history merge (the token through `/ingest`)".
- Recommendation: fix now

**[NON-BLOCKING] 2. `SECURITY.md` is missing from *Documentation updates*, verbatim.**
- `SECURITY.md:36` lists what a token holder, and anyone when the token is off, can do through `/ingest`. `SECURITY.md:51` lists the deletion through `/ingest` in scope. After this change both leave out the merge.
- Add `SECURITY.md` to the plan's *Documentation updates* with these three replacements:
  - **`SECURITY.md:36`, sentence 1.** Replace "a Companion with the token can also delete the history of any profile by its identity, as the settings window does when a profile is deleted with its history (`docs/areas/history-deletion.md`)." with "a Companion with the token can also delete the history of any profile by its identity, as the settings window does when a profile is deleted with its history, and merge the history of any profile into any identity, as the settings window does when a profile is merged (`docs/areas/history-deletion.md`)."
  - **`SECURITY.md:36`, sentence 2.** Replace "fill its database file and delete the history of any profile, so switch it off" with "fill its database file, delete the history of any profile and merge it into another, so switch it off".
  - **`SECURITY.md:51`.** Replace "the deletion of a profile's history from a data page and its host rule (`AllowedHosts`), and through `/ingest` with the token" with "the deletion of a profile's history from a data page and its host rule (`AllowedHosts`), and through `/ingest` with the token, and the merge of a profile's history into another through `/ingest` with the token".
- Recommendation: fix now

**[NON-BLOCKING] 3. The `profiles.json` reader rules for merge entries miss an ordering case and identity spelling variants.**
- **What the reader accepts as one identity.** I ran a probe through the scratchpad project `/tmp/claude-0/-home-user-Censio/19d301d8-944d-51f7-be2e-0b04cb3e38e8/scratchpad/guidprobe` (`dotnet run`, SDK 11.0.100). It printed:
  `[AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE] -> True aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee`
  `[ aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee ] -> True aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee`
  and a JSON string `"aaaaaaaa-…"` came back from `GetString()` as `aaaaaaaa-…`.
- So "D form" in this reader accepts uppercase, surrounding whitespace and JSON escapes. Any rule written as a string comparison can be bypassed: target equals source, repeated source, loop, or source listed.
- **The ordering gap.** The plan rejects loops by following each chain, but it does not reject a merge whose target is the id of an *earlier* entry (an earlier deletion, or an earlier merge's source). The Companion can never write that: a target must be listed at merge time, and an earlier entry's id has already left the list for good.
- If such a file were read, the server would get the merge after the target was already removed. It would recreate the target unnamed (E9) with the source's history, while the Companion resolves or drops the buffered messages differently.
- That one rule ("a merge's target is not the id of any earlier entry") also makes every loop impossible, in linear time.
- Smallest fix: compare parsed `Guid` values only, and add that rule to *Companion profiles (G3)*. Add these data rows to `CompanionProfilesLoadRejectsMalformedMerge`:
  - target the uppercase or whitespace-padded form of its own id;
  - target equal to an earlier deletion's id;
  - target equal to an earlier merge's source.
- Recommendation: fix now

**[NON-BLOCKING] 4. The measurement leaves out the merge's longest path.**
- *Measurement* case (a) moves a year into a target that holds none of those hours, so no hours are combined. Case (b) combines only 168 shared hours.
- The per-pair combine of E10 (`HourlyPointMerge`, several statements per shared hour) is never measured at a year (8,684 shared hours). That is the merge's longest hold of the write gate, during which ingest waits (N3).
- Beyond the Companion's 30 s acknowledgement wait (fact 13), the Companion reconnects and is refused `companionConnected` until the session ends. It converges, because of the stopping token above, but the 3 s bound would be stated without covering this case.
- Smallest fix: add case (c), two profiles that each hold the same year of hourly points built from the fixture hours, all hours shared, three runs, to *Measurement* and to the AC10 table.
- Recommendation: fix now

**[NON-BLOCKING] 5. The dialog shows the profile name inside a label.**
- *Window (G5)* calls `Move the profile <name> into:` "the label" of the drop-down. As a WPF `Label`, its text becomes an access-key text, and the first `_` of a profile name (which `CheckName` allows) is swallowed.
- `ProfileDeletionDialog.xaml:11` uses a `TextBlock`. Smallest fix: state in G5 that the name text is a `TextBlock`, as in `ProfileDeletionDialog`.
- Recommendation: fix now
