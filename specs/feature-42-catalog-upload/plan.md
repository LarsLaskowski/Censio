# Plan: Catalog upload on connect and after re-extraction

Source: Issue #42 | [spec.md](spec.md)
Tier: standard — a behavior change of live sending (a new message flow and a larger reply bound for one reply kind) that creates, changes or removes no security control; the reply bound is input handling.
Security areas touched: Outbound calls (live sending to `serverAddress` and to `127.0.0.1` now carries the catalog and icons: `LiveSender`, new `LiveCatalog`); Parsing of external input (the server's replies in `IngestClient`, read through `ContractReader`, get a larger bound for catalog acknowledgements); Logging and showing external data (five new `CompanionLog` lines)

## Problem / root cause

See [spec.md](spec.md), *Problem*: the server side exists; the Companion never sends `catalogUpload` or `iconImage`.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The server stores a catalog upload, acknowledges it with the sorted missing icon references and replaces an earlier catalog | `IngestSession.cs:523-531`, `StoreMessageAsync` :560-607; `ingest.md`, *Messages after the hello* | confirmed |
| 2 | `helloAccepted.catalogIdentity` is the SHA-256 of the stored document, named only when all its icons are stored | `IngestSession.cs:433` (`CompleteCatalogIdentity`), `ServerStore.cs:337` (`ComputeIdentity`), record 0070 | confirmed |
| 3 | `IngestClient` drops the content of `helloAccepted` | `IngestClient.cs:441-446` | confirmed |
| 4 | Every reply is read up to 65,536 bytes; a longer one is `InvalidReply` | `IngestClient.cs:17`, :327-339 | confirmed |
| 5 | A catalog acknowledgement can reach 1,097,872 bytes (16,384 `missingIcons`) | `ingest.md`, *Memory of a catalog upload* (measured); contract bound 16,384 (`contract.md`, *Bounds*) | confirmed |
| 6 | Nothing calls `GameCatalogDocumentConverter.ToDocument` in production | `grep -rn ToDocument src` gives only its definition | confirmed |
| 7 | The loader publishes the new catalog before its icons (`ExtractingIcons`), so `CatalogChanged` is no ready signal; the final statuses are `Ready`, `NotCached`, `IconsFailed` | `GameCatalogLoader.cs:381-409`, :434-440 | confirmed |
| 8 | Under `IconsFailed` the published icons are `null` (cleared at `ExtractingIcons`, not set) | `GameCatalogLoader.cs:381-406`, :480-488 | confirmed |
| 9 | The build of the published catalog is known only inside the loader (`_lastInstallation`) | `GameCatalogLoader.cs:70`, :225, :294 | confirmed |
| 10 | The document writer is deterministic, so cache load and extraction of the same catalog give the same identity | `game-catalog-format.md`, *Writing*; the cache stores every field the converter reads (`game-catalog-cache.md`, *Layout*) | confirmed |
| 11 | A document within its 16 MiB bound always fits the message bound (Base64 of 16 MiB is 22,369,624 characters, under 23,767,724), so only the document writer can refuse | `contract.md`, *Bounds*; arithmetic | confirmed |
| 12 | Live sending in local mode uses the same `LiveSender` | `App.xaml.cs:333-335` | confirmed |
| 13 | The server's catalog behavior is in release 0.4.0 (records 0070 and 0096 precede the tag) | `git tag --contains` of the commits adding 0096 and 0103: `v0.4.0` | confirmed |
| 14 | The Product Manager can build the image and the Companion from the branch head on his PC, as he did for #40 | to verify: the Product Manager answers the orchestrator's question before step 7 (`docs/USER_GUIDE.md:336`, :540 show earlier builds) | unverified |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `LiveSenderCatalogTests.LiveSenderUploadsCatalogBeforeWaitingMessages`, `LiveSenderSendsMissingIconsInListedOrder`, `LiveSenderUploadsWhenServerNamesAnotherIdentity`, `LiveSenderUploadsToLocalServer`; `GameCatalogLoaderTests.GameCatalogLoaderBuildIsBuildOfPublishedCatalog` | `LiveSenderCatalogTests.cs`, `GameCatalogLoaderTests.cs` |
| AC2 | `LiveSenderCatalogTests.LiveSenderSkipsUploadWhenServerNamesIdentity` | `LiveSenderCatalogTests.cs` |
| AC3 | `LiveSenderCatalogTests.LiveSenderUploadsCatalogReadyAfterHelloAfterMessageInFlight`, `LiveSenderSkipsReextractedCatalogWithSameIdentity`, `LiveSenderNewerCatalogStopsIconsOfOlder`, `LiveSenderIgnoresCatalogBeforeItsFinalStatus` | `LiveSenderCatalogTests.cs` |
| AC4 | `LiveSenderCatalogTests.LiveSenderResendsCatalogAndListedIconsAfterLostConnection`, `LiveSenderRetriesCatalogAfterAcknowledgementTimeout` | `LiveSenderCatalogTests.cs` |
| AC5 | `LiveCatalogTests.LiveCatalogWithoutIconsWritesNullIconReferences`; `LiveSenderCatalogTests.LiveSenderSendsNoCatalogWithoutGame`, `LiveSenderClearsCatalogAfterFailedExtraction` | `LiveCatalogTests.cs`, `LiveSenderCatalogTests.cs` |
| AC6 | `LiveSenderCatalogTests.LiveSenderDoesNotUploadRefusedCatalogAgain`, `LiveSenderCountsRefusedIcon`, `LiveSenderLogsUnwritableCatalogAndSendsNothing` | `LiveSenderCatalogTests.cs` |
| AC7 | `IngestClientTests.IngestClientReadsCatalogAcknowledgementWithMaximalMissingIcons`, `IngestClientRefusesLargeReplyToOtherMessage`, `IngestClientRefusesCatalogReplyAboveCatalogBound`, `IngestClientKeepsCatalogIdentityOfHello` | `IngestClientTests.cs` |
| AC8 | `LiveSenderCatalogTests.LiveSenderNeverBuffersCatalogMessages`, `LiveSenderDisposeDuringUploadEndsRun` | `LiveSenderCatalogTests.cs` |
| AC9 | `LiveSenderCatalogTests.LiveSenderCatalogLogLinesHaveTextAndLevel` (the five lines, each asserted free of names, references and paths of the test catalog) | `LiveSenderCatalogTests.cs` |
| AC10 | manual: the Product Manager, on an image and a Companion built from the stamped head (*Approach*, *Manual check*) | — |
| AC11 | check: the Reviewer reads `README.md` and `docs/USER_GUIDE.md` (both languages) against AC11 in step 8 | — |
| AC12 | `LiveCatalogTests.LiveCatalogHoldsNoDocumentOrMessageBytes` (reflection over its instance fields: no `CatalogDocument`, `byte[]`, `ReadOnlyMemory<byte>`, `ContractMessage`), `LiveCatalogWriteIconWritesOneIconPerCall`; check: the Reviewer reads `WriteUpload` and the send loop against N2 | `LiveCatalogTests.cs` |
| AC13 | `LiveSenderCatalogTests.LiveSenderSendsLiveMessagesWhileCatalogKeepsFailing`, `LiveSenderAbandonsCatalogAfterThreeFailedConnections`, `LiveSenderResetsFailuresAfterCompletedUpload` | `LiveSenderCatalogTests.cs` |

## Approach

**Catalog state (`LiveCatalog`, new).** `Create` converts with `GameCatalogDocumentConverter.ToDocument`, writes the
document with `CatalogDocumentWriter`, takes the identity (`ContractCatalogUpload.ComputeIdentity` on those bytes) and
keeps only the identity, the catalog, the icons, the build and a map from icon reference to `GameIcon` (built with the
shared `GameCatalogDocumentConverter.ToIconReference`, the helper the converter's private `ToReference` now calls).
The document and its bytes are locals. A writer refusal gives `Identity` `null`. `WriteUpload` converts and writes
again, wraps the bytes in a `ContractCatalogUpload` with a new id and returns only the `ContractWriter` output. The
document model and bytes are out of scope before it returns (N2, AC12). `WriteIcon` writes one `iconImage` for a
reference and returns `null` for an unknown reference or a writer refusal.

**Offering a catalog (`LiveSender.SetCatalog`).** App subscribes to `catalogLoader.StatusChanged` and passes the status,
`Catalog`, `Icons` and the new `GameCatalogLoader.Build`. It calls once more right after `StartLiveSending` for a
catalog that was ready before the sender existed. Final statuses (`Ready`, `NotCached`, `IconsFailed`; `IconsFailed`
with icons `null`) create a `LiveCatalog`, unless the current one has the same identity, which keeps it and its state.
`NoGame` and `Failed` clear it. Every other status changes nothing. A new catalog resets the failure count and the
closed mark. A writer refusal logs `CatalogUploadNotWritten` once and closes the catalog. Each change wakes the send
loop.

**Send loop.** Per connection the sender keeps the server's identity (`IngestClient.ServerCatalogIdentity`, then the
identity of each accepted upload). Before each buffer message it checks whether a catalog is due: one exists, it is
not closed, its identity differs from the server's, and it is not deferred. A deferred catalog waits until as many
buffer messages as `LiveBuffer.Status.Waiting` held at the hello are acknowledged, or the buffer is empty. If the
identities are equal, it logs `CatalogAlreadyOnServer` once per connection and catalog.
- An upload goes through `IngestClient.SendCatalogUploadAsync`. A refusal logs `CatalogRefusedByServer` and closes the
  catalog for the run (E2).
- Otherwise each listed reference goes out through `WriteIcon` and `SendAsync`, one at a time; a refusal or `null`
  counts in `<r>`. Before each icon the loop checks for a newer catalog and drops the remaining icons when there is
  one (E6).
- After the last icon it logs `CatalogSentToServer(i, r)` and resets the failure count.
- A connection that fails while the upload or an icon is in flight counts one failure and defers the catalog. The
  third failure closes it and logs `CatalogUploadAbandoned(3)` (E11).
- The wait for the next buffer message (`NextOrFailureAsync`) also ends on the catalog signal, releasing the buffer's
  in-flight entry as on a failure.
- Catalog and icon bytes never reach `LiveBuffer` (AC8).

**Reply bound.** `HandleHelloReply` stores `ContractHelloAccepted.CatalogIdentity`. `SendCatalogUploadAsync` marks the
awaited acknowledgement as a catalog one. When the receive loop's message passes `MaxReplyBytes` and that mark is set
(read under `_stateLock`), it copies into a buffer of `MaxCatalogReplyBytes` + 1 bytes for that message only, then
returns to the 65,537-byte buffer. Every other case stays `InvalidReply`. The bound counts raw received bytes, before
parsing.

**Layout of the new lines:** exactly the texts of spec N7; no other user-visible output.

**Manual check (AC10).** The Product Manager builds the image (`docker build -f src/Censio.Server/Dockerfile -t
censio .`) and the Companion (the `dotnet publish` command of the user guide) from the stamped head on his PC. He starts
the image with an empty `/config` and the Companion in forward mode with the real game, and checks names and good icons
on `/regions` and an island page, and the log lines. Fallback if he cannot run `docker build`: the image
`networlddev/censio:0.4.0` (fact 13; the server is unchanged by this change); the Companion is always built from the
head. Logged as `7 Manual test` before step 8.

Early draft PR: no — the manual check uses local builds, and no criterion needs CI before the review.

## Options considered

1. **Send the catalog through the buffer as files** — lost: up to 24 MiB per file inside the buffer's limits would
   drop ticks, and the catalog is rebuilt from memory at every connection anyway.
2. **A second connection for the catalog** — lost: the server accepts one Companion at a time (record 0062).
3. **Raise the bound for every reply to about 1.1 MB** — lost: a hostile or broken server could make every connection,
   the replay's included, hold the larger buffer. Only the one reply kind that needs it gets it (record 0108).
4. **Catalog always after the buffered messages** — lost: after a long outage the names come last (spec, *Open
   questions*).
5. **Deferral without a cap, or a cap without deferral** (the spec challenge's two options) — lost: deferral alone
   reconnects every 30 seconds forever on a catalog the server never takes, and a cap alone starves ticks for three
   rounds; both together are E11 (record 0107).
6. **Replay uploads too** — out of scope (spec, *Out of scope*).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Companion.Core` | `LiveCatalog.cs`, `LiveCatalogMessage.cs` (new) | catalog state and its messages |
| `Censio.Companion.Core` | `LiveSender.cs` | `SetCatalog`, catalog phase, deferral and cap |
| `Censio.Companion.Core` | `IngestClient.cs` | hello identity, `SendCatalogUploadAsync`, catalog reply bound |
| `Censio.Companion.Core` | `GameCatalogLoader.cs` | `Build` |
| `Censio.Companion.Core` | `GameCatalogDocumentConverter.cs` | public `ToIconReference`, used by `ToReference` |
| `Censio.Companion.Core` | `CompanionLog.cs` | five `[LoggerMessage]` methods |
| `Censio.Companion` | `App.xaml.cs` | wire `StatusChanged` to `SetCatalog`, one call after `StartLiveSending` |

## Signatures (for the Dev's skeleton)

- The sealed class `LiveCatalog`: `public static LiveCatalog Create(GameCatalog catalog, GameIconSet? icons, string? build)`;
  the get-only nullable string property `LiveCatalog.Identity`; `public LiveCatalogMessage? WriteUpload()`;
  `public LiveCatalogMessage? WriteIcon(string reference)`.
- The readonly record struct `LiveCatalogMessage` with the required init properties `LiveCatalogMessage.Id` (a Guid)
  and `LiveCatalogMessage.Utf8Json` (read-only memory of bytes).
- `LiveSender`: the constant `LiveSender.MaxCatalogAttempts` (int, 3) and
  `public void SetCatalog(GameCatalogStatus status, GameCatalog? catalog, GameIconSet? icons, string? build)`.
- `IngestClient`: the constant `IngestClient.MaxCatalogReplyBytes` (int, 1179648), the get-only nullable string
  property `IngestClient.ServerCatalogIdentity`, and
  `public Task<ContractAcknowledgement?> SendCatalogUploadAsync(ReadOnlyMemory<byte> utf8Json, Guid id, CancellationToken cancellationToken)`.
- `GameCatalogLoader`: the get-only nullable string property `GameCatalogLoader.Build` (the build of the installation of
  the published catalog).
- `GameCatalogDocumentConverter`: `public static string ToIconReference(GameIcon icon)`.
- `CompanionLog`: `public static partial void CatalogSentToServer(ILogger logger, int iconCount, int refusedCount);`,
  `public static partial void CatalogAlreadyOnServer(ILogger logger);`,
  `public static partial void CatalogRefusedByServer(ILogger logger, ContractAcknowledgementRefusal refusal);`,
  `public static partial void CatalogUploadNotWritten(ILogger logger);`,
  `public static partial void CatalogUploadAbandoned(ILogger logger, int attempts);`

All checked against *Writing code*: at most four parameters, no nested type, no keyword names. The skeleton holds
nothing real: the App wiring is written in step 6.

## Test files

`tests/Censio.Companion.Core.Tests/LiveSenderCatalogTests.cs`, `LiveCatalogTests.cs`, `IngestClientTests.cs` (new),
`GameCatalogLoaderTests.cs` (extended). The Tester extends `ScriptedIngestServer`: a hello reply with a
`catalogIdentity` and an acknowledgement with `missingIcons` (already listed under *Test doubles*, no new entry).

Existing test call sites of a changed signature: none (every change adds members). Existing `LiveSenderTests` never
call `SetCatalog`, so no catalog is sent there and they stay unchanged.

Files no unit test can cover: none new; `App.xaml.cs` is the untested shell (`docs/UNIT_TESTS.md`).

## Areas

- `docs/areas/live-sending.md (+520 words)`: new section *Catalog* (spec S1–S7, E2–E11, the bound), *What is sent*,
  *Privacy*, *Log* (five rows), *Limits and errors* (the catalog reply bound), *Related decisions* (0107, 0108),
  *Not here*.
- `docs/areas/companion-log.md (+70 words)`: the five lines in the list of what is logged.
- `docs/areas/local-mode.md (+5 words)`, `docs/areas/game-catalog-format.md (+10 words)` (also *Scope*),
  `docs/areas/game-catalog-cache.md (+3 words)`, `docs/areas/contract.md (+3 words)`, `docs/areas/ingest.md (+3
  words)`, `docs/areas/replay-sending.md (+3 words)`: the "(#42)" and "not yet" pointers name live sending.
- `docs/areas/README.md`: the *Not here* cells of live-sending and game-catalog-format lose "(#42)".

## Documentation updates

All by the Dev, after the code is green:
- the area documents of *Areas*;
- `docs/ARCHITECTURE.md`: the catalog sentence of *Data flow* ("which nothing sends yet (#42)") names live sending, and
  the forward-mode sentence adds the catalog and icons, with records 0107 and 0108;
- `README.md` (English, then German): the Docker names-and-icons line and the forward-mode steps (upload, skip, the
  log lines);
- `docs/USER_GUIDE.md` (English, then German): *What is sent and what is lost* (the catalog and icons), the web
  interface's names paragraph, the replay section (a replay sends no catalog, spec AC11), and the log lines;
- `.squad/project.md (+4 words)`: in *Outbound calls* after `LiveSender.ForServer`, the text "with the catalog
  (`LiveCatalog`)". The Dev reports it and the orchestrator writes it.

## Architecture check

- Buffered ticks stay bounded and in order (record 0031): catalog messages never enter the buffer, and E11 keeps a
  failing catalog from holding the buffered messages back beyond three reconnects.
- Record 0023: the catalog goes only to the server live sending already sends to. The token and the hello are
  unchanged.
- One Companion per server (record 0062): the catalog uses the same connection. No guarantee of `.squad/project.md` is
  weakened.

## Security considerations

- [x] The new limit (`MaxCatalogReplyBytes`) counts raw WebSocket bytes before parsing, and applies only while a
  catalog acknowledgement is awaited; `ContractReader` then checks the count of 16,384 and the hex form.
- [x] Exceptions: none reach a user text. Failures end as the existing `ReplaySendProblemKind` reasons, and the new
  log lines take only counts and the refusal kind.

## Decision records

- `docs/decisions/0107-catalog-uploaded-first-on-each-live-connection-deferred-after-a-failure.md` (Proposed)
- `docs/decisions/0108-companion-reads-a-larger-reply-only-for-a-catalog-acknowledgement.md` (Proposed)

## Out of scope / follow-ups

Replay uploads (spec, *Out of scope*); a menu line for the upload.
