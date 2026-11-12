# Spec: JSON contract v1 between Companion and server

Source: Issue #27
Status: Approved by Lead

## Problem

The Companion parses the pipe and the game files; the server must never see pipe frames, only messages of a contract of
Censio's own with an explicit version (record [0018](../../docs/decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md)).
That contract does not exist yet: `Censio.Contracts` holds only the catalog document of #26
([`game-catalog-format.md`](../../docs/areas/game-catalog-format.md)), and nothing is sent (README, *Status*). #28, #40, #41 and
#42 wait for it. Companion and server of different releases must refuse each other clearly, never misread each other.

Dependencies, checked in the code: #10 (`PipeTickAssembler`, `PipeTick` in `src/Censio.Pipe`) and #26
(`CatalogDocumentReader`, `CatalogDocumentWriter` in `src/Censio.Contracts`) are built and closed. A closed tick has a
real receive time and a game time (`PipeTick.ReceiveTime`, `GameTime`), as the issue says.

## Scenarios

- S1: Given a Companion of contract version 1, when it writes its hello with the contract version, the token, its
  product version and the selected profile (its identity and the name the user typed), then a server of version 1 reads the same values back.
- S2: Given a hello the server accepts, when the server writes its reply, then the Companion reads that it was accepted,
  the server's product version and which catalog the server already holds (none, or the SHA-256 of its catalog
  document's bytes in lowercase hex, which the deterministic catalog writer makes equal for an equal catalog), so #42
  can skip an upload the server does not need.
- S3: Given a closed tick, when the Companion writes it, then the server reads every island of it — key (session GUID,
  island ID, area index), session ID, name, every entry with all its values and both maps — with the tick's real
  receive time and game time, exactly as the Companion had them.
- S4: Given a session start or a session end of the pipe, when the Companion writes it, then the server reads which of
  the two it was and the real time the Companion received it, so a buffered event can be placed in history; a session
  start carries no text, so the figure name never crosses the contract (record
  [0041](../../docs/decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md)).
- S5: Given ticks taken while profile A was selected and delivered after the user switched to profile B (a buffered tick,
  #41), when the server reads them, then they belong to profile A; a tick or session event carries the identity (not the
  name) of its profile, read from its own message, not from the hello of the connection.
- S6: Given the Companion's catalog document, when the Companion writes a catalog upload, then the server reads the same
  document; when the server confirms it, the confirmation names the icon references it lacks, and the Companion writes
  one icon image per missing reference, which the server reads with its reference and PNG data.
- S7: Given the Companion is connected, when it writes a status, then the server reads the Companion's pipe state (the
  states of [`pipe-connection.md`](../../docs/areas/pipe-connection.md), *States*); a status sent at an interval serves as
  heartbeat.
- S8: Given a Companion message the server must confirm (catalog upload, icon image, tick, session event), when the
  server writes an acknowledgement, then the Companion reads which message it confirms or refuses, and for a refusal
  its kind and text. Every such message carries an identity the Companion assigns when it takes the message and keeps
  with it (#41 keeps it with a buffered message): a resend after a lost acknowledgement carries the same identity, two
  messages of equal content taken separately (two session ends) carry different ones.
- S9: Given a Companion and a server whose contract versions differ, when the server reads the hello, then it refuses it
  as *unsupported version* and its reply names the version received and the versions it supports, which the Companion
  reads in either direction (newer Companion with older server and the reverse).
- S10: Given a hello the server refuses for another reason, when it writes its reply, then the Companion reads the
  kind: *token missing or wrong*, or *another Companion connected* — so #28 and #40 need no new contract version.

## Errors and edge cases

A refusal is a typed result with its kind and text, never an exception, and never holds message
content: no token, profile, island name, catalog content or image data.

- E1: A message of a contract version other than `1`, whatever else it holds, is refused as *unsupported version*:
  `Contract version <n> is not supported; supported: 1`. Exempt are the hello and the refused hello reply, whose form is
  frozen (N4): a hello of any version is read for its version, then refused as above; a refused reply of any version
  is read with its kind, received version and supported versions.
- E2: A message that is not JSON, is truncated, has an unknown message type, an unknown, repeated or missing property or a
  wrong JSON type is refused as *malformed*: `Contract message is malformed`.
- E3: A message that breaks a value rule of version 1 (a tick without islands, an island key twice in one tick, an icon
  image whose PNG data does not hash to its reference, a string that is no well-formed Unicode text (the reader finds
  an escaped lone surrogate already in its syntax pass: *malformed*, as the catalog reader does), an empty profile
  name, a profile or message identity that is not a UUID or is the all-zero UUID, a catalog identity that is not 64
  lowercase hex digits) is refused as
  *invalid*: `Contract message breaks a rule of contract version 1`. The writer refuses the same and writes nothing.
- E4: A message above a size or count bound is refused as *too large*: `Contract message exceeds a size bound`. A tick
  whose message would exceed the byte bound is refused so by the writer and not sent; reporting it is #40's and #41's.
- E5: A catalog document inside a catalog upload that the catalog reader refuses refuses the upload with the catalog's
  own kind and text (e.g. `Catalog schema version 2 is not supported; supported: 1`); ticks are unaffected.
- E6: A float the game sent that is not a finite number (NaN or an infinity, [`pipe.md`](../../docs/areas/pipe.md),
  *Frame layout*) crosses as "no value" and is read back as no value, never as `0`; the rest of the tick is unchanged.
- E7: A product GUID twice in one island, islands without entries (also all of a tick), an empty island name, negative GUIDs and a game time at either end of the signed 64-bit range are carried as
  they are.
- E8: A hello without a token is a valid message; whether the server accepts it is #28's (record
  [0019](../../docs/decisions/0019-websocket-transport-with-shared-token.md)).

## Non-functional requirements

- N1: Reading back what was written gives an equal message, every value exact (float32 values bit for bit, the receive
  time to its full precision, 64-bit integers without rounding); the same message always gives the same bytes.
- N2: The bounds admit every catalog document the catalog writer writes, every icon PNG the icon reader keeps, and
  every tick within the pipe's island, entry-and-pair and map limits ([`pipe.md`](../../docs/areas/pipe.md), *Limits and
  errors*; names of 255 wire bytes, which U+FFFD replacement makes up to 765 UTF-8 bytes) whose message stays within one
  byte bound per message. That bound is far above the largest recorded tick, but below a tick at the pipe's entry limit
  (about 200 MB): the plan states it and the memory a read of the largest message measured, for Security to weigh.
- N3: The reader runs on untrusted input and is strict like the catalog reader (record
  [0056](../../docs/decisions/0056-catalog-document-read-strictly-by-exact-schema-version.md)): it checks size before
  syntax, syntax before version, version before structure, structure before values, and returns a typed result.
- N4: The hello and the refused hello reply keep one form across every later contract version, so two sides of any
  versions can always tell each other their versions.
- N5: Privacy: the token, profile names and island names are private; no refusal text holds them, and the area document
  says that no log line may.

## Acceptance criteria

- AC1: Every message kind — hello, hello reply (accepted and refused), catalog upload, icon image, tick, session start,
  session end, status, acknowledgement (confirmed and refused) — written and read back gives an equal message, and
  writing it again gives the same bytes; a hello without a token among them (S1, S2, S6, S7, S8, E8, N1).
- AC2: A tick read back holds every island with key, session ID and name and every entry with its product GUID, its eleven values and both
  maps, the receive time and the game time, exact, including the cases of E7 (S3, E7, N1).
- AC3: A pipe tick with a non-finite float, converted in `Censio.Companion.Core` and written, reads back with no value
  there, never `0`, the other values unchanged (E6).
- AC4: No message has a place for the figure name; a tick and a session event read back with the profile identity they
  were written with, independent of any hello (S4, S5).
- AC5: A message of contract version other than `1`, also one that would be malformed under version 1, is refused as
  *unsupported version* with the text of E1; a hello of version 2 is read with its version and refused; a fixture
  refusal written as a future server would (version 2, supported `[2]`) reads back with received version 1 and
  supported versions `[2]`; each refusal kind of S10 reads back (S9, S10, E1, N4).
- AC6: The reader refuses each case of E2, E3 and E4 with its kind and exact text, throws for none, and no refusal text
  contains the token, a profile or island name, catalog content or image data; the writer refuses the cases of E3 and
  E4 and writes nothing (E2, E3, E4, N3, N5).
- AC7: A tick at the pipe's island and entry-and-pair limits whose message fits the byte bound, a message at exactly the
  byte bound, a catalog document at the catalog's bounds and an icon PNG at the icon reader's
  bound, and an island name of 255 U+FFFD characters, are written and read back; one unit above a contract bound is
  refused as *too large*, by the reader and the writer (E4, N2).
- AC8: A catalog upload whose catalog document the catalog reader refuses is refused with the catalog's kind and text
  (E5).
- AC9: Two session ends taken separately are written with different identities and their own receive times; writing a
  taken message again keeps its identity (S4, S8).

## Out of scope

- Sending and receiving over the WebSocket (the conversion from `Censio.Pipe`'s ticks and session events to contract
  messages in `Censio.Companion.Core` is in scope, as #26's catalog conversion was; `Censio.Contracts` references no
  other project), the message order on a connection and its enforcement, the token check,
  a second connection (#28, #40); the buffer and the resend (#41); when the catalog and icons are uploaded (#42);
  storing, profiles as server entities and showing anything (#29, #30, #35, #43).
- Negotiating an older contract version: a side reads exactly the versions it lists (record 0056).
- A message for ticks the buffer dropped (#41): silence is the gap, so #41 needs no contract change (record 0031).
- How the Companion learns that another Companion replaced it, if #28 chooses replacement: the connection's close.
- The figure name, the pipe's version messages, read errors, tick-limit items, repeated session ends and a tick's close
  reason: they stay in the Companion.
- README and user guide: nothing a tester can try changes (README *Status*: "Nothing is sent from the Companion to the
  server"), as with the catalog format of #26 (PR #163); #28 and #40 document what testers can try.

## Open questions

- How is a profile identified, and does a rename keep its history? Answered: the contract carries a stable profile
  identity apart from the name the user typed, so a later rename (#43) needs no new contract version; creating, naming and
  renaming profiles is #30 and #43.
- Must the contract carry a non-finite float? Answered: yes, as no value (E6), because the pipe delivers NaN as
  read and a whole tick lost for one value, or a value shown as `0`, would misreport the game (record 0031: gaps, never
  zeros).

## Area documents

- New `docs/areas/contract.md` (row in `docs/areas/README.md`): each message with an invented example, value rules,
  versioning and the frozen forms, check order, bounds and refusal texts, acknowledgements and identities, privacy.
- `docs/areas/game-catalog-format.md`, *Not here*: the upload's messages are in `contract.md`.
- `docs/ARCHITECTURE.md`, *Data flow*: the contract exists in `Censio.Contracts`; nothing sends it yet (#28, #40).

## Challenge

1. (major) E1 contradicted S9/N4. Accepted: E1 exempts the frozen hello and refused reply; AC5 adds the future-server
   fixture.
2. (major) Message identity undefined, session events without time. Accepted: the Companion assigns the identity and
   keeps it with the message (S8, UUID by E3); session events carry their receive time (S4); AC9 added.
3. (major) Only a version refusal. Accepted: the refused reply lists *token missing or wrong* and *another Companion
   connected* (S10, AC5); a replacement, should #28 choose it, ends the connection (*Out of scope*).
4. (minor) Catalog identity undefined. Accepted: SHA-256 of the document bytes (S2, E3).
5. (minor) Profile name vs identity. Accepted: hello carries both, ticks and session events the identity (S1, S5);
   value rules in E3.
6. (minor) Name bound and U+FFFD. Accepted: 765 bytes in N2, such a name in AC7.
7. (minor) Where the conversion lives. Accepted: in scope, in `Censio.Companion.Core` (*Out of scope*, AC3).
8. (minor) Tick byte bound and memory. Accepted: N2 makes the plan state both for Security.
9. (minor) #41's gap. Accepted: silence is the gap, no message (*Out of scope*).
10. (Lead, while planning) N2 asked bounds for a tick at the pipe's entry limit: such a message is about 200 MB (scratch
    measurement, [plan.md](plan.md), *Verified facts*), more than a server on a NAS should read from one message. N2, E4
    and AC7 now cap a tick by the message byte bound, over 30 times the largest recorded tick; record 0060 holds the
    options.
