# Plan: Delete profiles in the Companion, and how the server's copy follows

Source: Issue #251 | [spec.md](spec.md)
Tier: security — the server gains a second way to delete a profile's history, authorized by the ingest token instead of the pages' trusted-host rule (record 0085), and the contract changes.
Security areas touched: Ingest token and Companion authentication (`IngestSession` with the new message type, `IngestEndpoint`); Network exposure (the history deletion, now also through `/ingest`); Server database (its history: `HistoryRetention.DeleteProfileAsync` from a new caller); Parsing of external input (`ContractReader` and the contract types, `CompanionProfiles` reading `profiles.json` version 2); File paths the Companion writes (`CompanionProfiles`, `profiles.json`; `LiveBuffer` deleting buffer files); Outbound calls (live sending sends the new message); Logging and showing external data (`SettingsWindow` with profile names in the new dialog)
Plan size: four projects, a contract version, a file format version and a second deletion path, each with its own rules.

## Problem / root cause

Feature: [spec.md](spec.md), *Problem*.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The Companion creates and selects profiles only; nothing removes one | `CompanionProfiles.cs` has `Create`, `Select`, no removal; `companion-profiles.md` *The list and its file* | confirmed |
| 2 | The reader accepts `version` 1 only and exactly the root properties `version`, `active`, `profiles`, `figures` | `CompanionProfiles.Read` (l. 516), `TryReadRoot` (l. 544) | confirmed |
| 3 | A figure link to an identity outside the list makes the file invalid | `ReadFigures` (l. 664) | confirmed |
| 4 | A new profile name is checked against the list in memory at `Save` | `CheckNewName` (l. 282) locks and reads `_profiles` ; spec AC15 needs only the removal from `_profiles` | confirmed |
| 5 | A buffered message's profile is read only from its JSON when it is sent; the buffer's index has no profile | `LiveBufferItem.cs` (id, taken time, length, sequence); `LiveSender.ProfileOf` (l. 499) | confirmed |
| 6 | A start without live sending neither reads nor changes the buffer | `live-sending.md` *Buffer*, l. 101-105; `LiveSender.IsSending` | confirmed |
| 7 | The message in flight at a lost connection is released and resent first | `AttemptAsync` `finally` → `_buffer.ReleaseInFlight()` (l. 996); `TakeHead` takes the head again | confirmed |
| 8 | `LiveBuffer` drops an item by deleting its file and counting it as dropped, logged by `LogDropped` | `LiveBuffer.Drop` (l. 573), `CountDropped`, `TakeUnloggedDropped`; `LiveSender.LogDropped` (l. 744) | confirmed |
| 9 | A message of a profile the list does not know is sent on the current connection | `IsOfOtherProfile` (l. 1127): `NameOf(id) is null` → no switch | confirmed |
| 10 | A reconnect's hello names `_helloProfile` when set, which `IsOfOtherProfile` sets from the list | `ChooseHelloProfile` (l. 1024), l. 1134 ; a deleted profile could be named by the next hello unless filtered (*Approach*) | confirmed |
| 11 | A hello stores its profile with its name before the server answers; a tick of an unknown identity creates the profile unnamed | `ingest.md` *The hello* step 6; `server-state.md` *Profiles* | confirmed |
| 12 | The pages delete through `HistoryRetention.DeleteProfileAsync`, which logs and asks for the sweep; outcomes `Done`, `NothingToDo`, `Failed` | `HistoryRetention.cs` l. 147-168; `Regions.razor.cs` l. 126; `HistoryOutcome.cs` | confirmed |
| 13 | `IngestSession` closes with 1011 when a message cannot be stored and acknowledges a stored one by `id` | `HandleMessageAsync` l. 518-530 | confirmed |
| 14 | Messages after the hello are handled only once the hello is accepted with the token | `RunHelloPhaseAsync` / `RunMessagePhaseAsync`; `ingest.md` *The hello* | confirmed |
| 15 | `IngestSession`'s constructor has seven parameters (S107's limit) | `IngestSession.cs` l. 163 ; the history joins `ServerState` in one type | confirmed |
| 16 | Contract version 2 is released, so a new message needs version 3 | `git show v0.4.0:src/Censio.Contracts/ContractReader.cs`: `CurrentVersion = 2` | confirmed |
| 17 | Message type names come from `ContractMessageType` (camel case), so `ProfileDeletion` reads `profileDeletion` | `ContractScanner._typeNames = ContractEnumNames.Create<ContractMessageType>()` | confirmed |
| 18 | The refusals a message can get are `malformed`, `unsupportedVersion`, `invalid`, `tooLarge` (plus the catalog kinds) | `contract.md` l. 42-44; `HandleMessageAsync` | confirmed |
| 19 | Records 0085 and 0096 are released; 0111 is not | `git merge-base --is-ancestor` of their adding commits with `v0.4.0`; 0111 added in `bada5d0` after it | confirmed |
| 20 | `src/Censio.Companion/**` is excluded from coverage | `.squad/stack.md` l. 180 | confirmed |
| 21 | The largest version-2 file stays under 1 MiB | 567,086 bytes today (`companion-profiles.md` *Writing*) + `,"deleted":[]` + 1,000 × `{"id":"<36>","confirmed":false},` (64 bytes) ≈ 631,100 bytes ; arithmetic, the Tester's bound test measures it | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `CompanionProfilesDeleteRemovesProfileAndFigureLinks`, `CompanionProfilesDeleteClearsPendingNotice`, `CompanionProfilesDeleteWritesFileWithoutProfile`; `SettingsWindowSessionDeleteKeepsOtherEditsAndShowsProfiles`, `SettingsWindowSessionDeleteCancelledChangesNothing` | `CompanionProfilesTests.cs`; `SettingsWindowSessionTests.cs` |
| AC2 | `SettingsViewCanDeleteRefusesLiveAndActive`, `CompanionProfilesDeleteOfLiveOrActiveThrows` | `SettingsViewTests.cs`; `CompanionProfilesTests.cs` |
| AC3 | `LiveSenderProfileDeletionWithoutHistorySendsWaitingMessages` | `LiveSenderProfileTests.cs` |
| AC4 | `LiveSenderProfileDeletionDropsWaitingMessages`, `LiveSenderProfileDeletionDropReleasesDeferredCatalog`, `LiveSenderProfileDeletionDropsResentInFlightMessage`, `LiveSenderProfileDeletionDropsAfterConfirmation`, `LiveSenderProfileDeletionHelloNeverNamesDeletedProfile`; `LiveBufferDropInFlightDeletesFileAndCounts` | `LiveSenderProfileTests.cs`; `LiveBufferTests.cs` |
| AC5 | `CompanionProfilesDeletionWaitsAcrossLoad`, `CompanionProfilesDeletionKeptInMemoryWhenWriteFails`; `LiveSenderProfileDeletionSentAfterHello` | `CompanionProfilesTests.cs`; `LiveSenderProfileTests.cs` |
| AC6 | `IngestSessionProfileDeletionDeletesAndAcknowledges`, `IngestSessionProfileDeletionOfUnknownProfileAcknowledges`, `IngestSessionProfileDeletionFailedCloses1011` | `IngestSessionTests.cs` |
| AC7 | `LiveSenderProfileDeletionConfirmedOnAcknowledgement`, `LiveSenderProfileDeletionRefusedLogsAndConfirms`, `LiveSenderProfileDeletionKeptWaitingWhenConnectionLost`; `CompanionProfilesConfirmedDeletionForgottenOldestFirst` | `LiveSenderProfileTests.cs`; `CompanionProfilesTests.cs` |
| AC8 | `SettingsWindowSessionShowsWaitingDeletionCount` | `SettingsWindowSessionTests.cs` |
| AC9 | `CompanionProfilesDeleteWithHistoryWhenFullThrows`, `SettingsWindowSessionViewMarksDeletionQueueFull` | `CompanionProfilesTests.cs`; `SettingsWindowSessionTests.cs` |
| AC10 | `CompanionProfilesWritesVersionOneWithoutDeletions`, `CompanionProfilesReadsVersionTwo`, `CompanionProfilesRejectsInvalidDeletedList` (data rows, among them an upper-case `Live` identity, a white-space-padded listed profile's identity and an upper-case repeat within `deleted`), `CompanionProfilesLargestFileUnderBound`; `CompanionTextsDeletionTextsInBothLanguages`; `CompanionLogDeletionLinesHoldNoValue` | `CompanionProfilesTests.cs`; `CompanionTextsTests.cs`; `CompanionLogTests.cs` |
| AC11 | `ContractReaderReadsProfileDeletion`, `ContractReaderRefusesProfileDeletionBreakingRules` (data rows), `ContractWriterWritesProfileDeletion` | `ContractReaderTests.cs`; `ContractWriterTests.cs` |
| AC12 | `check: the Reviewer reads README.md and docs/USER_GUIDE.md (English, then German) for deletion, the box, waiting deletions, S5 and S6` | — |
| AC13 | `CensioServerProfileDeletionInModeOffDropsBufferedMessages` (a buffer filled under `B`, `B` deleted with the box ticked while no sender runs, then a sender against the real server: no message of `B` stored, the profile list without `B`) | `CensioServerTests.cs` |
| AC14 | `LiveSenderProfileDeletionSentBeforeCatalogAndBuffer` | `LiveSenderProfileTests.cs` |
| AC15 | `CompanionProfilesDeletedNameAcceptedAtOnce` | `CompanionProfilesTests.cs` |

## Approach

Early draft PR: no

**Contract (version 3).** `ContractMessageType.ProfileDeletion` is appended; `ContractProfileDeletion` carries `id` and
`profileId`, Companion to server. `ContractRules` checks both identities (`CheckIdentity`), `GetRefusedId` returns its
`id` so a refused one is acknowledged, `Deserialize`, `ContractWriter` and `ContractJsonContext` get their case.
`CurrentVersion` becomes 3; the four version texts of `CompanionTexts` (`LiveSendVersionNotSupported` and
`ReplaySendReasonVersionNotSupported`, English and German) name 3. Bounds: the generic ones of the scanner (no string field).

**Server.** A new type `IngestStores` holds `ServerState` and `HistoryRetention`; `IngestSession` takes it instead of
`ServerState`, so the constructor stays at seven parameters. `GetId` gets the case `ContractProfileDeletion deletion => deletion.Id`
(without it `HandleMessageAsync` closes with 1008 before storing). `StoreMessageAsync` handles `ContractProfileDeletion` by
`_stores.History.DeleteProfileAsync(profileId, _stoppingToken)`: `Done` and `NothingToDo` are stored (acknowledged,
counted accepted); `Failed` and any other outcome return not stored, so the session closes with 1011 — without
`LogStoreFailed`, since the retention job already logged its warning. `TrySetUpAsync` returns `IngestStores` and
`MapEndpoints` passes it to `MapCensioIngest`; local mode shares the path.

**profiles.json.** The state gains a list of deleted identities, oldest first, each waiting or confirmed. `Serialize`
writes `version` 1 with the four properties while the list is empty; otherwise `version` 2 with a fifth property
`"deleted":[{"id":"<id>","confirmed":false}]`. The reader accepts version 1 with exactly the four properties and version
2 with exactly five; it rejects in `deleted`: a non-array, more than 1,000 entries, an entry with a missing, unknown or
repeated property, an `id` not in `D` form, `confirmed` that is not `true` or `false`, a repeated identity, `Live`'s
identity or an identity of the list of profiles — the last three compared as parsed `Guid` values, never as strings
(`Guid.TryParseExact` with `D` accepts either letter case and surrounding white space). A rejection is `not of its format`, as today.
An empty `deleted` array is accepted (the state of a version-1 file; the Companion itself never writes one) and the
next write is version 1 (N1); no test pins it.

**Deleting.** `CompanionProfiles.Delete(id, isHistoryDeleted)`, under the lock: refuses (`ArgumentException`) what
`IsDeletable` refuses; refuses (`InvalidOperationException`) a ticked deletion with 1,000 waiting; removes the profile
and its figure links (a running session's figure link to it included); clears the pending notice; with the box ticked
appends the identity as waiting, first forgetting the oldest confirmed one when 1,000 are kept; `MarkChanged`. Outside
the lock it raises `PendingNoticeChanged` when the notice changed and `DeletionsChanged` when the list changed.
`ConfirmDeletion(id)` turns a waiting identity into a confirmed one (`MarkChanged`, `DeletionsChanged`); `IsDeleted`
answers for either state. `IsDeletable(id, active, profiles)` is the one static rule (`id` in the list, not `Live`,
not `active`) that `Delete` and `SettingsView.CanDelete` share.

**Live sending.** `LiveSender.UseProfiles` also subscribes to `DeletionsChanged`, which wakes the send loop as a change
of the active profile does (a second signal; `NextOrFailureAsync` waits on it too). In `SendLoopAsync`, at the top of
each turn, before `UploadDueCatalogAsync`: each waiting identity not yet tried on this connection is written as a
`ContractProfileDeletion` with a new message identity and sent with `client.SendAsync`, one at a time. No
acknowledgement → the connection's problem is returned and the identity stays waiting; an accepted acknowledgement →
`ConfirmDeletion` and `Profile deletion confirmed by the server`; a refusal → `ConfirmDeletion` and `Profile deletion
refused by the server: <refusal>` (Warning); a write the writer refuses → the existing `Live message not sent: the
contract writer refused it`, the identity stays waiting and is tried again on the next connection. After
`NextOrFailureAsync` returns a message, its profile is read once (`ProfileOf`); when `profiles.IsDeleted` holds for it,
`_buffer.DropInFlight(id)` and `LogDropped()` run, the drop counts toward the messages that waited at the hello (as
`acknowledged++` does, so a deferred catalog is released once they are sent or dropped, `live-sending.md` *Catalog*),
and the loop continues, before `IsOfOtherProfile`, which takes the
profile read. `ChooseHelloProfile` ignores a `_helloProfile` whose identity is deleted and names the active profile.
A replay and a start without live sending have no `LiveSender`, so they send nothing and drop nothing.

**Settings window.** `SettingsView` gains `WaitingDeletions`, `IsDeletionQueueFull` and `CanDelete(selected)`.
`ISettingsWindow` gains `DeleteRequested` (a `SettingsDeletion`: profile and box) and `ShowProfiles(view)`, which
replaces the drop-down's items with the active one selected, the count line and the button state, and nothing else.
`SettingsWindowSession` handles `DeleteRequested` (ignored while a save runs or for a profile `CanDelete` refuses):
`Profiles.Delete`, `Profile deleted` (Information), then `ShowProfiles`; it also shows `ShowProfiles` again on
`DeletionsChanged` while the window is open (posted to the window's thread). The WPF window and a new dialog
`ProfileDeletionDialog` (in `Censio.Companion`) only show and report.

Layout: `Delete profile…` is a button right of `New profile…` on the `Profile` line, with the same padding, disabled
with E1's tooltip (shown on a disabled button: `ToolTipService.ShowOnDisabled`) while `CanDelete` refuses the drop-down's
selection. The count line stands below the drop-down (above the name box), in the message style but not the error
colour, only while `WaitingDeletions` is above 0. The dialog is modal to the window, titled `Delete profile?`, with,
top to bottom: the text, the box, E12's text below the box only while it is disabled, then `Delete` (default) and
`Cancel` (cancel) right-aligned; closing it is `Cancel`.

Texts (English / German, `CompanionTexts`):

| English | German |
| ------- | ------ |
| `Delete profile…` | `Profil löschen…` |
| `Live and the active profile cannot be deleted.` | `Live und das aktive Profil können nicht gelöscht werden.` |
| `Delete profile?` | `Profil löschen?` |
| `The profile {0} is removed from this Companion, with the figures linked to it.` | `Das Profil {0} wird aus diesem Companion entfernt, mit den Figuren, die mit ihm verknüpft sind.` |
| `Also delete its history on the server` | `Auch seinen Verlauf auf dem Server löschen` |
| `1,000 deletions already wait for the server.` | `Schon 1.000 Löschungen warten auf den Server.` |
| `Delete` | `Löschen` |
| `Deleted profiles whose history the server has not deleted yet: {0}` | `Gelöschte Profile, deren Verlauf der Server noch nicht gelöscht hat: {0}` |

`Cancel` is the existing `CancelText`. Log lines are English only, as every line of `CompanionLog`.

## Options considered

- Whether the server's history follows (none, always, by choice): spec Q1, answered C by the Product Manager; record 0115.
- Filtering the buffer: once at the deletion (fails for runs without live sending and for a resent message in flight),
  or a lasting list of deleted identities (chosen); record 0115, spec *Challenge* 1.
- The transport of a deletion: a new message (chosen), a hello property (frozen), an HTTP route (new exposure), the
  pages (no offline wait); record 0116.
- Passing the history to `IngestSession`: an eighth parameter (S107), a delegate (still eight), routing the deletion
  through `ServerState` without the retention job (no sweep request, no log line), or one type `IngestStores` (chosen).
- `profiles.json`: always version 2 (an older Companion would lose its list after any update), or version 2 only with
  deleted identities (chosen, spec *Challenge* 5).
- Deleting at `Save` with the other fields, or at once in the dialog (chosen, spec Q5).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Contracts | `ContractMessageType`, `ContractProfileDeletion` (new), `ContractReader`, `ContractRules`, `ContractWriter`, `ContractJsonContext` | new message, version 3 |
| Censio.Companion.Core | `CompanionProfiles` | deleted identities, `Delete`, `ConfirmDeletion`, format version 2 |
| Censio.Companion.Core | `LiveBuffer` | `DropInFlight` |
| Censio.Companion.Core | `LiveSender` | sends deletions, filters the buffer, hello rule |
| Censio.Companion.Core | `ISettingsWindow`, `SettingsDeletion` (new), `SettingsView`, `SettingsWindowSession` | deletion request and view |
| Censio.Companion.Core | `CompanionTexts`, `CompanionLog` | texts, three log lines, version texts |
| Censio.Companion | `SettingsWindow.xaml(.cs)`, `ProfileDeletionDialog.xaml(.cs)` (new) | button, count line, dialog |
| Censio.Server | `IngestStores` (new), `IngestSession`, `IngestEndpoint`, `CensioServer` | deletion through the ingest |

## Signatures (for the Dev's skeleton)

Properties are named Type.Member with their type and accessors in prose; methods and constructors in full.

Censio.Contracts:
- `ContractMessageType.ProfileDeletion`: appended as the last value of the enum.
- `ContractProfileDeletion`: a public sealed class deriving from ContractMessage, with `ContractProfileDeletion.Id`
  and `ContractProfileDeletion.ProfileId` (public required Guid, get and init) and `ContractProfileDeletion.Type`
  (public override ContractMessageType, JsonPropertyOrder -2, returning ProfileDeletion).
- `ContractReader.CurrentVersion`: the public constant's value becomes 3.

Censio.Companion.Core:
- `CompanionProfiles.MaxDeletions`: public const int, 1000.
- `CompanionProfiles.DeletionsChanged`: public event EventHandler?.
- `CompanionProfiles.WaitingDeletions`: public IReadOnlyList<Guid>, get only, oldest first.
- `CompanionProfiles.IsDeletionQueueFull`: public bool, get only.
- `public static bool IsDeletable(Guid id, Guid active, IReadOnlyList<CompanionProfile> profiles)` (CompanionProfiles).
- `public void Delete(Guid id, bool isHistoryDeleted)` (CompanionProfiles).
- `public bool IsDeleted(Guid id)` (CompanionProfiles).
- `public void ConfirmDeletion(Guid id)` (CompanionProfiles).
- `public void DropInFlight(Guid id)` (LiveBuffer).
- `SettingsDeletion`: a public sealed class with `SettingsDeletion.Profile` (public required Guid, get and init) and
  `SettingsDeletion.IsHistoryDeleted` (public required bool, get and init).
- `ISettingsWindow.DeleteRequested`: an event of EventHandler<SettingsDeletion>?.
- `void ShowProfiles(SettingsView view)` (ISettingsWindow).
- `SettingsView.WaitingDeletions` (public int, get and init) and `SettingsView.IsDeletionQueueFull` (public bool, get
  and init).
- `public bool CanDelete(Guid selected)` (SettingsView).
- `CompanionTexts.DeleteProfileText`, `CompanionTexts.DeleteProfileDisabledText`, `CompanionTexts.DeleteProfileTitle`,
  `CompanionTexts.DeleteProfileTextFormat`, `CompanionTexts.DeleteProfileHistoryText`,
  `CompanionTexts.DeleteProfileHistoryFullText`, `CompanionTexts.DeleteProfileConfirmText`,
  `CompanionTexts.WaitingDeletionsFormat`: public required string, get and init.
- `public static partial void ProfileDeleted(ILogger logger)`, `public static partial void ProfileDeletionConfirmed(ILogger logger)`
  and `public static partial void ProfileDeletionRefused(ILogger logger, ContractAcknowledgementRefusal refusal)` (CompanionLog).

Censio.Server:
- `IngestStores`: a public sealed class with `IngestStores.State` (public required ServerState, get and init) and
  `IngestStores.History` (public required HistoryRetention, get and init).
- `public IngestSession(WebSocket webSocket, IngestAccess access, IngestAdmission admission, IngestStores stores, TimeProvider timeProvider, ILogger logger, string serverVersion)`
  (changed: the ServerState parameter is replaced).
- `public static IEndpointConventionBuilder MapCensioIngest(this IEndpointRouteBuilder endpoints, IngestAccess access, IngestStores stores)` (changed).

The skeleton holds for real: the enum value, the message class, the JSON context attribute, the version 3, the text
values, the log methods, IngestStores and its wiring through CensioServer, IngestEndpoint and the IngestSession
constructor. No new name ends in Flags or Enum, no type is nested, no parameter is a keyword of another language, no
member has more than seven parameters.

## Test files

`tests/Censio.Contracts.Tests/ContractReaderTests.cs`, `ContractWriterTests.cs`;
`tests/Censio.Companion.Core.Tests/CompanionProfilesTests.cs`, `LiveBufferTests.cs`, `LiveSenderProfileTests.cs`,
`SettingsViewTests.cs`, `SettingsWindowSessionTests.cs`, `CompanionTextsTests.cs`, `CompanionLogTests.cs`;
`tests/Censio.Server.Tests/IngestSessionTests.cs`, `CensioServerTests.cs`.

Existing test code that calls a changed signature or literal, adapted by the Tester in step 5:
- `IngestSessionTests.StartSession` (l. 2349): `new IngestStores` with a `HistoryRetention` over its database.
- `IngestEndpointTests` (l. 267): `MapCensioIngest` with `IngestStores`.
- `FakeSettingsWindow`: the new event and `ShowProfiles` (recording the views shown); `QueuedIngestConnector` answers a
  deletion like any message.
- Contract version 2 in literals: `ContractReaderTests.cs`, `ContractWriterTests.cs`, `IngestSessionTests.cs` and the
  server tests' `IngestClient.cs` (`"contractVersion":2`, `InvalidText`, `supportedVersions`); a test that pins the
  release `0.3.0` refusal (`receivedVersion` 2 against version 1) keeps its numbers where they describe another release.
- `CompanionTextsTests` cases that pin `contract version 2`; `LiveSenderTests.cs` (l. 113, l. 267 `Assert.AreEqual(2, ContractReader.CurrentVersion…)`,
  l. 1039) and `ReplaySenderTests.cs` (l. 260, 283, the data row at l. 809; the reply at l. 271 becomes `receivedVersion`
  3 against an older supported version, or stays a pinned older-release case).

Files no unit test can cover: `src/Censio.Companion/SettingsWindow.xaml(.cs)` and `ProfileDeletionDialog.xaml(.cs)` —
the WPF shell, excluded from coverage (`.squad/stack.md`, *Coverage gate*); their logic sits in `SettingsView` and
`SettingsWindowSession`.

## Areas

- `docs/areas/companion-profiles.md` (+690 words): in *Limits and errors* also "A file the reader rejects also forgets
  the deleted identities: their waiting deletions are not sent, and buffered messages of those profiles are sent again
  and recreate the profiles on the server, unnamed."; a section *Deleting* (rules, dialog, figure links, notice,
  deleted identities and their states, the bound, when deletions are sent, the buffer filter, confirmation, refusal,
  the relation to the pages' deletion and to a profile only the server has); *Writing* with the version-2 format and
  the largest file; *Limits and errors* with the reader's new rules and the downgrade; "neither renamed nor deleted"
  replaced.
- `docs/areas/companion-settings.md` (+120 words): *Fields* with the button, its tooltip and the count line.
- `docs/areas/companion-texts.md` (+130 words): the new rows, the version texts.
- `docs/areas/companion-log.md` (+50 words): the three lines.
- `docs/areas/contract.md` (+90 words): version 3, the twelfth message, its rules and refusals, the version texts.
- `docs/areas/ingest.md` (+140 words): *Messages after the hello* with the deletion; *Accepted risks* gets the bullet
  "**Deletion by a token holder.** A Companion with the token (or anyone, with the token off) can delete the history of
  any profile by its identity, and each deletion of a stored profile writes one log line, re-reads the profile list and
  asks for a sweep, so a tick of a new profile followed by its deletion grows the log by one line per pair without a
  bound."
- `docs/areas/history-deletion.md` (+60 words): the second entry point and its access rule (record 0116).
- `docs/areas/live-sending.md` (+45 words): one sentence in *Profiles on the connection* pointing to
  `companion-profiles.md` (deletions first after the hello, deleted identities dropped); version texts.
- `docs/areas/replay-sending.md` (+0 words): version 2 → 3 in its three places.
- `docs/areas/web-pages.md` (+0 words): none of its rules change; its deletion paragraph stays the pages'.

## Documentation updates

All by the Dev once the code is green:
- `README.md`: the *Profiles* paragraph (English, l. 154; German, l. 419) gets deletion, the box, waiting deletions, a
  profile deleted on the pages staying in the Companion; the *Warnings* paragraph (l. 214, l. 481) names deleting in
  the Companion beside the pages.
- `docs/USER_GUIDE.md`: the settings window's profile part (English, then German) as the README, plus the downgrade
  note; every `contract version 2` text (l. 428, 523, 535, 984, 1152, 1247, 1259) becomes 3; the Companion's log list
  (English and German, l. 1375 and its English counterpart) gets the three lines; *History and deleting a profile*
  names the Companion's way.
- `docs/ARCHITECTURE.md`: l. 12 (the server deletes a profile's history from the pages or on a Companion's request),
  l. 61-62 (contract version 3), l. 105 (the pages' one state-changing action, and the Companion's deletion through
  `/ingest`, linking record 0116).
- `SECURITY.md` (l. 34): "…requires the token by default and refuses browser upgrades." becomes "…refuses browser
  upgrades; a Companion with the token can also delete the history of any profile by its identity, as the settings
  window does when a profile is deleted with its history (`docs/areas/history-deletion.md`)."; "…send data, which the
  server stores, and fill its database file, so switch it off…" becomes "…send data, which the server stores, fill its
  database file and delete the history of any profile, so switch it off…"; l. 51: append ", and through `/ingest` with
  the token" after "its host rule (`AllowedHosts`)".
- The area documents of *Areas*.
- `.squad/project.md` (+30 words), exact text for the orchestrator: in *Ingest token and Companion authentication*
  after `ContractTradeRoutes included` insert `, ContractProfileDeletion`, and after `IngestSession` insert
  ` (and IngestStores)`; in *Server database* after `DeleteProfileAsync` insert ` (also from IngestSession)`; in
  *Network exposure* replace "the history deletion (`DeletionHostRule`);" with "the history deletion (`DeletionHostRule`
  on the pages, the token through `/ingest`);"; in *Guarantees*, the line "History in SQLite … deletable per profile",
  after the link to record 0085 append `, [0116](../docs/decisions/0116-history-deleted-through-the-ingest-connection-with-the-token.md)`.
- `docs/UNIT_TESTS.md`: none (the doubles keep their description).
- The changelog entry the PR template asks for, if it asks for one.

## Architecture check

- "History deletable per profile" (`.squad/project.md`, *Guarantees*) holds and gains a second way; the trusted-host
  rule of the pages is not weakened (record 0116 amends 0085 only for `/ingest`).
- "Unsent ticks buffered and delivered in order; dropped ones show as a gap": messages of a profile deleted with its
  history are dropped and counted as dropped; they belong to no history any more.
- "Stores only with the token": the deletion is a message after an accepted hello.
- The Companion still does not read the server's list (record 0111).

## Security considerations

- [x] Limits: the contract's existing bounds apply before deserializing (the message holds two identities only);
  `profiles.json` keeps its 1 MiB bound on the raw bytes and caps `deleted` at 1,000 entries, checked after parsing.
- [x] Exceptions reaching the user: none new. A failed write of `profiles.json` logs the exception type only; a failed
  deletion on the server reaches no page and closes the connection with 1011, which the Companion shows as its existing
  `Server:` retry lines; `Delete` throws only for what the window prevents (the session checks first).
- Fail closed: a `profiles.json` the reader rejects is `Live` alone (no deleted identities), so the window's banner
  shows as today; the deleted list never holds `Live` or a listed profile, so no live profile's messages are dropped.
- Authorization: with the token required, a token holder can delete any profile by identity; the Companion sends only
  identities it deleted. The identity is no secret (the pages' addresses `/profiles/{profile}` carry it), so the token
  alone guards the deletion, as it guards storing; with the token off, `/ingest` refuses browser upgrades (record 0064).
- Privacy: the message carries no name; no new log line holds a name or an identity.

## Decision records

- `docs/decisions/0115-profile-deleted-in-the-companion-history-on-the-server-by-the-players-choice.md` (Proposed)
- `docs/decisions/0116-history-deleted-through-the-ingest-connection-with-the-token.md` (Proposed; amends 0085)
- `docs/decisions/0111-…` (unreleased) edited in place: its consequence about deletion points to 0115.

## Challenge

- Note 1 (`GetId` has no case for the deletion, so it would close with 1008): accepted; *Approach* (Server) adds the
  case.
- Note 2 (a dropped message holds back a deferred catalog): accepted; the drop counts toward the messages that waited
  at the hello, test `LiveSenderProfileDeletionDropReleasesDeferredCatalog` (AC4).
- Note 3 (four version texts and more test call sites): accepted; *Approach* names the four texts, *Test files* the
  call sites in `LiveSenderTests.cs` and `ReplaySenderTests.cs`.

## Security review

- A (Network exposure not listed): accepted; added to *Security areas touched*, and the `.squad/project.md` text now
  changes *Network exposure* and links record 0116 from *Guarantees*.
- B (`SECURITY.md` not updated): accepted; its three edits are in *Documentation updates*.
- Note (i) (identities compared as strings): accepted; the reader compares parsed `Guid` values, and three data rows
  test it.
- Note (ii) (a rejected file forgets the deleted identities): accepted; the sentence goes into `companion-profiles.md`
  (*Areas*), as record 0115 already states.
- Note (iii) (deletion by a token holder as an accepted risk): accepted; the bullet goes into `ingest.md` (*Areas*).

## Out of scope / follow-ups

- Proposed issue: "[Companion] Rename a profile in the settings window" — body: "Renaming a profile in the Companion; the
  server's copy follows through the next hello naming the profile (server-state.md, *Profiles*), offline included.
  Split from #251."
- Merging profiles, reading the server's profile list, deleting from the tray menu (spec *Out of scope*).
