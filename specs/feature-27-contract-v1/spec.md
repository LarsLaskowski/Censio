# Spec: JSON contract v1 between Companion and server

Source: Issue #27
Status: Draft

## Problem

The Companion parses the pipe and the game files; the server must never see pipe frames, only messages of a contract of
Censio's own with an explicit version (record [0018](../../docs/decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md)).
That contract does not exist yet: `Censio.Contracts` holds only the catalog document of #26
([`game-catalog-format.md`](../../docs/areas/game-catalog-format.md)), and nothing is sent (README, *Status*). The ingest
endpoint (#28), forward mode (#40), the tick buffer (#41) and the catalog upload (#42) all wait for it. A Companion and a
server of different releases must refuse each other with a clear error instead of misreading each other.

This feature defines the messages, writes and reads them in memory and documents them. It sends, receives, stores and
shows nothing: that is #28, #40, #41 and #42.

Dependencies, checked in the code: #10 (`PipeTickAssembler`, `PipeTick` in `src/Censio.Pipe`) and #26
(`CatalogDocumentReader`, `CatalogDocumentWriter` in `src/Censio.Contracts`) are built and closed. A closed tick has a
real receive time and a game time (`PipeTick.ReceiveTime`, `GameTime`), as the issue says.

## Scenarios

- S1: Given a Companion of contract version 1, when it writes its hello with the contract version, the token, its
  product version and the selected profile, then a server of version 1 reads the same values back.
- S2: Given a hello the server accepts, when the server writes its reply, then the Companion reads that it was accepted,
  the server's product version and which catalog the server already holds (none, or the identity of one), so #42 can
  skip an upload the server does not need.
- S3: Given a closed tick, when the Companion writes it, then the server reads every island of it — key (session GUID,
  island ID, area index), session ID, name, every entry with all its values and both maps — with the tick's real
  receive time and game time, exactly as the Companion had them.
- S4: Given a session start or a session end of the pipe, when the Companion writes it, then the server reads which of
  the two it was; a session start carries no text, so the figure name never crosses the contract (record
  [0041](../../docs/decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md)).
- S5: Given ticks taken while profile A was selected and delivered after the user switched to profile B (a buffered tick,
  #41), when the server reads them, then they belong to profile A; the profile of a tick or session event is read from
  its own message, not from the hello of the connection.
- S6: Given the Companion's catalog document, when the Companion writes a catalog upload, then the server reads the same
  document; when the server confirms it, the confirmation names the icon references it lacks, and the Companion writes
  one icon image per missing reference, which the server reads with its reference and PNG data.
- S7: Given the Companion is connected, when it writes a status, then the server reads the Companion's pipe state (the
  states of [`pipe-connection.md`](../../docs/areas/pipe-connection.md), *States*); a status sent at an interval serves as
  heartbeat.
- S8: Given a Companion message the server must confirm (catalog upload, icon image, tick, session event), when the
  server writes an acknowledgement, then the Companion reads which message it confirms or refuses, and for a refusal
  its kind and text. A message written again from the same data, such as a tick resent after a lost acknowledgement,
  carries the same identity, so the server can tell a resend from a new message.
- S9: Given a Companion and a server whose contract versions differ, when the server reads the hello, then it refuses it
  as *unsupported version* and its reply names the version received and the versions it supports, which the Companion
  reads in either direction (newer Companion with older server and the reverse).

## Errors and edge cases

The refusal texts follow the catalog document's ([`game-catalog-format.md`](../../docs/areas/game-catalog-format.md),
*Limits and errors*). A refusal is a typed result with its kind and text, never an exception, and never holds message
content: no token, profile, island name, catalog content or image data.

- E1: A message of a contract version other than `1`, whatever else it holds, is refused as *unsupported version*:
  `Contract version <n> is not supported; supported: 1`.
- E2: A message that is not JSON, is truncated, has an unknown message type, an unknown, repeated or missing property or a
  wrong JSON type is refused as *malformed*: `Contract message is malformed`.
- E3: A message that breaks a value rule of version 1 (a tick without islands, an island key twice in one tick, an icon
  image whose PNG data does not hash to its reference, a string that is no well-formed Unicode text) is refused as
  *invalid*: `Contract message breaks a rule of contract version 1`. The writer refuses the same and writes nothing.
- E4: A message above a size or count bound is refused as *too large*: `Contract message exceeds a size bound`.
- E5: A catalog document inside a catalog upload that the catalog reader refuses refuses the upload with the catalog's
  own kind and text (e.g. `Catalog schema version 2 is not supported; supported: 1`); ticks are unaffected.
- E6: A float the game sent that is not a finite number (NaN or an infinity, [`pipe.md`](../../docs/areas/pipe.md),
  *Frame layout*) crosses as "no value" and is read back as no value, never as `0`; the rest of the tick is unchanged.
- E7: A product GUID twice in one island, an island with no entries, a tick whose islands all have no entries (*no data
  yet*), an empty island name, negative GUIDs and a game time at either end of the signed 64-bit range are carried as
  they are.
- E8: A hello without a token is a valid message; whether the server accepts it is #28's (record
  [0019](../../docs/decisions/0019-websocket-transport-with-shared-token.md)).

## Non-functional requirements

- N1: Reading back what was written gives an equal message, every value exact (float32 values bit for bit, the receive
  time to its full precision, 64-bit integers without rounding); the same message always gives the same bytes.
- N2: The bounds admit every tick the assembler can deliver ([`pipe.md`](../../docs/areas/pipe.md), *Limits and errors*:
  256 islands, 524,288 entries and map pairs, names of 255 bytes), every catalog document the catalog writer writes and
  every icon PNG the icon reader keeps; the plan states each bound and the memory a read of the largest message
  measured.
- N3: The reader runs on untrusted input and is strict like the catalog reader (record
  [0056](../../docs/decisions/0056-catalog-document-read-strictly-by-exact-schema-version.md)): it checks size before
  syntax, syntax before version, version before structure, structure before values, and returns a typed result.
- N4: The hello and its version refusal keep one form across every later contract version, so two sides of any versions
  can always tell each other their versions.
- N5: Privacy: the token, profile names and island names are private; no refusal text holds them, and the area document
  says that no log line may.

## Acceptance criteria

- AC1: Every message kind — hello, hello reply (accepted and refused), catalog upload, icon image, tick, session start,
  session end, status, acknowledgement (confirmed and refused) — written and read back gives an equal message, and
  writing it again gives the same bytes; a hello without a token among them (S1, S2, S6, S7, S8, E8, N1).
- AC2: A tick read back holds every island with key, session ID and name and every entry with its product GUID, its eleven values and both
  maps, the receive time and the game time, exact, including the cases of E7 (S3, E7, N1).
- AC3: A non-finite float in a tick is written as no value and read back as no value, never `0`, with the other values
  of the tick unchanged (E6).
- AC4: No message has a place for the figure name: a session start reads back with no text; the hello carries the
  selected profile; a tick and a session event read back with the profile they were written with, independent of any
  hello (S4, S5).
- AC5: A message of contract version other than `1`, also one that would be malformed under version 1, is refused as
  *unsupported version* with the text of E1; a version refusal reply names the received and the supported versions and
  is read by the reader whatever the version of the hello was (S9, E1, N4).
- AC6: The reader refuses each case of E2, E3 and E4 with its kind and exact text, throws for none, and no refusal text
  contains the token, a profile or island name, catalog content or image data; the writer refuses the cases of E3 and
  E4 and writes nothing (E2, E3, E4, N3, N5).
- AC7: A tick at the pipe's tick limits, a catalog document at the catalog's bounds and an icon PNG at the icon reader's
  bound are written and read back; one unit above a contract bound is refused as *too large* (E4, N2).
- AC8: A catalog upload whose catalog document the catalog reader refuses is refused with the catalog's kind and text
  (E5).

## Out of scope

- Sending and receiving over the WebSocket, the message order on a connection and its enforcement, the token check,
  a second connection (#28, #40); the buffer and the resend (#41); when the catalog and icons are uploaded (#42);
  storing, profiles as server entities and showing anything (#29, #30, #35, #43).
- Negotiating an older contract version: a side reads exactly the versions it lists (record 0056).
- The figure name, the pipe's version messages, read errors, tick-limit items, repeated session ends and a tick's close
  reason: they stay in the Companion.
- README and user guide: nothing a tester can try changes (README *Status*: "Nothing is sent from the Companion to the
  server"), as with the catalog format of #26 (PR #163); #28 and #40 document what testers can try.

## Open questions

- Is the profile of a tick taken from the hello or from the tick? Answered: from the
  message, because a buffered tick of profile A may be delivered on a connection opened under profile B (S5).
- How is a profile identified, and does a rename keep its history? Answered: the contract carries a stable profile
  identity apart from the name the user typed, so a later rename (#43) needs no new contract version; creating, naming and
  renaming profiles is #30 and #43.
- Must the contract carry a non-finite float? Answered: yes, as no value (E6), because the pipe delivers NaN as
  read and a whole tick lost for one value, or a value shown as `0`, would misreport the game (record 0031: gaps, never
  zeros).
- Does a catalog schema mismatch refuse the connection? Answered: no, only the upload (E5); names fall back to GUIDs as
  record 0056 foresees.

## Area documents

- New `docs/areas/contract.md` (row in `docs/areas/README.md`): the message kinds with an example of invented values
  for each, the properties and value rules, the versioning rules and the frozen form of hello and version refusal, the
  reader's check order, the bounds and refusal texts, the meaning of an acknowledgement and of a message identity, what
  never crosses (figure name), and privacy.
- `docs/areas/game-catalog-format.md`, *Not here*: the upload's messages are in `contract.md`.
- `docs/ARCHITECTURE.md`, *Data flow*: the contract exists in `Censio.Contracts`; nothing sends it yet (#28, #40).
