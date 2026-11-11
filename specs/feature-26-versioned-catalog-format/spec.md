# Spec: Versioned catalog format

Source: Issue #26
Status: Draft

## Problem

The server cannot read game files, so it will get its names and icons from the Companion (record
[0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md); upload #42, contract message #27). The
Companion's catalog ([`game-catalog.md`](../../docs/areas/game-catalog.md)) and its icons
([`game-catalog-icons.md`](../../docs/areas/game-catalog-icons.md)) exist only in memory and in its private cache;
there is no shared, versioned format. Companion and server are not always updated together (record
[0018](../../docs/decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md)), so a mismatch must be refused,
never misread.

## Scenarios

- S1: Given the catalog the Companion built, its icons and the game build, when the Companion converts it, then it gets
  a catalog document of schema version 1 with the game version (the build, or none when it is *unknown*) and one entry
  per catalog entry the catalog answers a lookup with, in the catalog's order: GUID, kind (`region`, `good`,
  `workforce`, `building`), the names per language it has (`en`, `de`), the region GUID of a region entry, the region
  types (*not stated*, *none* or the list in its order) and, for an entry with an icon, the icon reference: the SHA-256
  of its PNG data in lowercase hex, the same for every entry that shares the image. An entry of GUID `0` ("none", never looked up,
  [`game-catalog.md`](../../docs/areas/game-catalog.md)) is left out, and of two entries of one GUID the first is kept.
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

The reader takes the document as UTF-8 bytes and checks in this order; the first failing step decides the kind: (1)
the document's length (E5); (2) the whole document is well-formed JSON, each string's length checked before it is
decoded (E1, E5); (3) `schemaVersion` (E2, E3); (4) the structure of version 1 (E2); (5) the value rules of version 1
(E4).

- E1: Not JSON, truncated, invalid UTF-8 or a lone surrogate, content after the document: *malformed*.
- E2: A property unknown to the version, a property given twice (also an identical language tag in one entry's names),
  a missing property, a property name in another case, a value of the wrong JSON type, `null` where the version allows
  none, a number with a fraction or an exponent or outside the signed 32-bit range (GUIDs, region GUIDs and
  `schemaVersion` alike, so `1.0` and `1e0` are no version 1), a `schemaVersion` that is missing or given twice:
  *malformed*.
- E3: A `schemaVersion` that is a signed 32-bit integer other than 1: *unsupported version*, whatever else the
  document holds (properties of a later version included), as long as steps 1 and 2 pass.
- E4: GUID `0`, a GUID on two entries, a kind not listed in S1, a region entry without a region GUID or with `0`, a
  region GUID on another kind, an empty name, a language tag that is not a lowercase BCP 47 tag of at most 35
  characters (a primary subtag of 2 or 3 letters `a`–`z`, then subtags of 1 to 8 letters `a`–`z` or digits, each after
  one `-`; `pt-br` is valid, `pt-BR` and `EN` are not), an empty region type, an icon reference that is not 64
  lowercase hex digits, a game version that is not four numbers of 0 to 65,535 without leading zeros joined by `.` and
  not all zero: *invalid*. Any other non-zero signed 32-bit GUID, negative ones included, is valid (the fixture has
  them).
- E5: A document above 64 MiB (67,108,864 bytes), or a string whose UTF-8 form is above 65,536 bytes (the cache's
  string bound, [`game-catalog-cache.md`](../../docs/areas/game-catalog-cache.md)): *too large*.
- E6: A catalog with no entries, an entry with no name in any language (*nameless*) or in one only (*incomplete*):
  valid; the names object then lists only the languages with a name.
- E7: The writer given a document that breaks a rule of E4 or a bound of E5 refuses it with that kind and writes
  nothing: the writer never produces a document the reader refuses. The conversion of S1 never gives such a document
  for GUIDs (it leaves out `0` and repeats).

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
- N6: `Censio.Contracts` keeps referencing no other project ([`ARCHITECTURE.md`](../../docs/ARCHITECTURE.md)): the
  document, its writer and its reader are there; the conversion from the Companion's catalog is in the Companion's
  platform-neutral logic, which already references `Censio.GameData`. The conversion gets no caller in this feature
  (#42 adds it).

## Acceptance criteria

- AC1: Converting a catalog with its icon set and build gives the document of S1: schema version 1, the game version
  (`null` for *unknown*), the entries in catalog order with every field of S1, the SHA-256 reference of each entry's
  icon, one reference for a shared image, and none without an icon or without an icon set; an entry of GUID `0` is
  left out and the second entry of a repeated GUID too, and the result is accepted by the writer (S1, S2, E7).
- AC2: A document written and read back equals the original in every field and order, and writing it again gives
  identical bytes, with reflection-based serialization switched off; the cases include names in two, one and no
  language, non-ASCII names, negative GUIDs and region GUIDs, region types *not stated*, *none* and a list, a game
  version `null`, an entry with and without an icon reference, and no entries (S3, E6, N3, N4).
- AC3: The catalog built from the six fixture files of `tests/Censio.GameData.Tests/Fixtures/GameCatalog/` (linked into
  the Companion's tests, not copied), with icons of invented pixels for its goods and regions, converts, writes and
  reads back valid and equal, every entry included (S1, S3, N6, issue #26).
- AC4: A document with `schemaVersion` 0, 2 or -1 is refused as *unsupported version* with its text naming that
  number, also when it holds a property unknown to version 1; a missing `schemaVersion`, `1.0`, `1e0`, `"1"`,
  2147483648 or a repeated `schemaVersion` is *malformed*, and so is a truncated document with `"schemaVersion": 2`
  (step 2 before step 3) (S4, E2, E3, N2).
- AC5: Each case of E1 and E2 is refused as *malformed*, a GUID of 2147483648 and of `5.0` included (S5, E1, E2, N1).
- AC6: Each case of E4 is refused as *invalid*, the language tags `EN`, `pt-BR`, `e` and one of 36 characters
  included, while `en`, `de` and `pt-br` are accepted (S5, E4, N1).
- AC7: A document of 67,108,865 bytes and one with a string of 65,537 bytes of UTF-8 are refused as *too large*; a
  document of exactly 67,108,864 bytes and a string of exactly 65,536 bytes are not refused for their size; only the
  kinds are asserted. The writer refuses a document of E4 or E5 with that kind and writes nothing (E5, E7).
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

- New `docs/areas/game-catalog-format.md`: everything above as rules, with an example of invented values.
- `docs/areas/game-catalog.md` and `docs/areas/game-catalog-cache.md`: *Not here* points to the new area instead of
  #26; the areas index gets its row.

## Challenge

- 1 (major, GUID `0`): accepted. Confirmed in `GameCatalogAssets.Add` and `GameCatalogBuilder` (lines 36, 90): `0` is
  never filtered. The conversion leaves out GUID `0` and keeps the first of a repeated GUID (as `GameCatalog.Find`);
  `0` stays *invalid* in the document. S1, E7, AC1 changed.
- 2 (minor, negative GUIDs): accepted; E2, E4, AC2, AC5.
- 3 (minor, version form, check order): accepted; the five-step order, E2, AC4 (truncated, version 2: *malformed*).
- 4 (minor, language tag case): accepted; lowercase only, other case *invalid*; E4, AC6.
- 5 (minor, testable bounds): accepted; bytes in, length before parsing, string length before decoding; AC7 asserts
  kinds only.
- 6 (minor, placement): accepted; N6, AC3. The Companion's tests copy only their own fixtures today, so the plan adds
  the link.
