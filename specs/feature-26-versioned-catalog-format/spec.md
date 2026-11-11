# Spec: Versioned catalog format

Source: Issue #26
Status: Draft

## Problem

The server cannot read game files, so it will get its names and icons from the Companion (record
[0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md); upload #42, contract message #27). The
Companion's catalog ([`game-catalog.md`](../../docs/areas/game-catalog.md)) and its icons
([`game-catalog-icons.md`](../../docs/areas/game-catalog-icons.md)) exist only as in-memory types of `Censio.GameData`
and as the Companion's private binary cache; `Censio.Contracts` holds only the product version today. There is no
format both sides share, no version a reader can check, and no rule for what an older or newer reader does with it.
Companion and server are not always updated together (record
[0018](../../docs/decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md)), so a mismatch must be refused,
never misread.

## Scenarios

- S1: Given the catalog the Companion built, its icons and the game build, when the Companion converts it, then it gets
  a catalog document of schema version 1 with the game version (the build, or none when it is *unknown*) and one entry
  per catalog entry, in the catalog's order: GUID, kind (`region`, `good`, `workforce`, `building`), the names per
  language it has (`en`, `de`), the region GUID of a region entry, the region types (*not stated*, *none* or the list
  in its order) and, for an entry with an icon, the icon reference: the SHA-256 of its PNG data in lowercase hex, the
  same for every entry that shares the image.
- S2: Given the icons failed or are not built yet, when the Companion converts the catalog, then the document has the
  same entries with no icon reference.
- S3: Given a catalog document, when it is written as JSON and read back, then the reader returns the same catalog,
  and writing that again gives the same bytes.
- S4: Given a document of a schema version the reader does not support, when it is read, then it is refused as
  *unsupported version*, naming the version found and the versions supported, and nothing of it is used.
- S5: Given a document that is not valid for its version, when it is read, then it is refused with its kind and a
  fixed text, and nothing of it is used.

## Errors and edge cases

Each refusal is a result with a kind and its fixed text, never an exception and never content of the document (no
name, region type or reference):

| Kind | Text |
| ---- | ---- |
| Malformed | `Catalog document is malformed` |
| Unsupported version | `Catalog schema version <n> is not supported; supported: 1` |
| Invalid | `Catalog document breaks a rule of schema version 1` |
| Too large | `Catalog document exceeds a size bound` |

- E1: Not JSON, truncated, invalid UTF-8 or a lone surrogate, content after the document: *malformed*.
- E2: A property unknown to the version, a property given twice (also a language in one entry's names), a missing
  property, a property name in another case, a value of the wrong JSON type, `null` where the version allows none, a
  `schemaVersion` that is missing or no integer: *malformed*.
- E3: A `schemaVersion` that is an integer other than 1: *unsupported version*, whatever else the document holds
  (properties of a later version included); it is decided before any other rule of the version.
- E4: GUID `0`, a GUID on two entries, a kind not listed in S1, a region entry without a region GUID or with `0`, a
  region GUID on another kind, an empty name, a language that is no well-formed BCP 47 tag of at most 35 characters,
  an empty region type, an icon reference that is not 64 lowercase hex digits, a game version that is not four
  numbers of 0 to 65,535 without leading zeros joined by `.` and not all zero: *invalid*.
- E5: A document above 64 MiB, or a string above 65,536 bytes of UTF-8 (the cache's string bound,
  [`game-catalog-cache.md`](../../docs/areas/game-catalog-cache.md)): *too large*, checked before the content above the
  bound is held.
- E6: A catalog with no entries, an entry with no name in any language (*nameless*) or in one only (*incomplete*):
  valid; the names object then lists only the languages with a name.
- E7: The writer given a catalog that breaks a rule of E4 or a bound of E5 refuses it with that kind and writes
  nothing: the writer never produces a document the reader refuses.

## Non-functional requirements

- N1: Strict and fail closed (record [0033](../../docs/decisions/0033-pipe-reader-strict-from-the-reference-source.md)):
  a document is used whole or not at all; a refusal returns a typed result, not an exception.
- N2: Compatibility rules for later versions: one integer `schemaVersion`; every change of what a document may hold or
  what a value means (a property, a kind, a value range) raises it; a new language, a new game version or a new entry
  does not. A reader accepts exactly the versions it lists and reads each by that version's rules; an unknown version
  is refused (E3), never read by the nearest known rules.
- N3: Serialization through System.Text.Json source generation, with no reflection-based serialization (issue #26).
- N4: Deterministic: the same catalog gives the same bytes (entries and region types in their order, languages in
  ordinal order of their tag, every property always written, `null` where an entry has no value).
- N5: Privacy: the document holds the player's names, so it is private like the cache; this feature only converts,
  writes and reads it in memory, sends and stores nothing, and no refusal text or log holds its content.

## Acceptance criteria

- AC1: Converting a catalog with its icon set and build gives the document of S1: schema version 1, the game version
  (`null` for *unknown*), the entries in catalog order with every field of S1, the SHA-256 reference of each entry's
  icon, one reference for a shared image, and none without an icon or without an icon set (S1, S2).
- AC2: A document written and read back equals the original in every field and order, and writing it again gives
  identical bytes, with reflection-based serialization switched off; the cases include names in two, one and no
  language, non-ASCII names, region types *not stated*, *none* and a list, a game version `null`, an entry with and
  without an icon reference, and no entries (S3, E6, N3, N4).
- AC3: The catalog built from the six fixture files of `tests/Censio.GameData.Tests/Fixtures/GameCatalog/`, with icons
  of invented pixels for its goods and regions, converts, writes and reads back valid and equal, every entry included
  (S1, S3, issue #26).
- AC4: A document with `schemaVersion` 0, 2 or -1 is refused as *unsupported version* with its text naming that
  number, also when it holds a property unknown to version 1; a missing or non-integer `schemaVersion` is *malformed*
  (S4, E2, E3, N2).
- AC5: Each case of E1 and E2 is refused as *malformed* (S5, E1, E2, N1).
- AC6: Each case of E4 is refused as *invalid* (S5, E4, N1).
- AC7: A document of 64 MiB plus one byte and a string of 65,537 bytes are refused as *too large*, documents exactly at
  the bounds are not; the writer refuses a catalog of E4 or E5 with that kind and writes nothing (E5, E7).
- AC8: Every refusal returns its kind and the fixed text of the table, never an exception and never a name, region
  type or reference of the document (S5, N1, N5).

## Out of scope

- Sending, receiving, storing or showing the catalog, and choosing which version to send to an older server (#27,
  #42); the icon images and how they travel (#27, #42): the document carries only their references.
- The ambiguous GUIDs, the counts of the Companion's catalog, the icon problems and the icons' file names: the server
  needs names and icons only; a GUID without an entry is unknown to it. Adding one later raises the version (N2).
- Replacing the Companion's binary cache with this format.

## Open questions

- Does the document hold the PNG data? Answered: no; it holds a content reference per icon (the issue names "icon
  references"), so an upload can skip images the server already has (#42), and the image data stays out of a
  document of names. The plan's decision record states the options.
- Accept unknown properties for forward compatibility? Answered: no; strict reading with a version raised on every
  change (records 0018, 0033); a mismatched pair is refused (N2).
- README and user guide (issue's *Documentation*)? Answered: no change; nothing a tester can try changes (the README's
  "Nothing is sent from the Companion to the server" stays true). The format is described in its area document.

## Area documents

- New `docs/areas/game-catalog-format.md`: the document's properties with an example of invented values, the value
  rules, the bounds, the refusal kinds and texts, the compatibility rules and the privacy rule.
- `docs/areas/game-catalog.md` and `docs/areas/game-catalog-cache.md`: *Not here* points to the new area instead of
  #26; the areas index gets its row.
