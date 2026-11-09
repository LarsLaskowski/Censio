# Spec: Game catalog of goods, buildings, workforce and regions with German and English names

Source: Issue #23
Status: Draft

## Problem

The game's statistics pipe identifies regions, goods, workforce and buildings by numeric GUIDs only
([`pipe.md`](../../docs/areas/pipe.md), *Message types*). Today the diagnostics window shows those numbers raw
([`companion.md`](../../docs/areas/companion.md), *Diagnostics window*), and the later web UI needs names in German
and English. The names live in the player's own installation (`config.rda`: `assets.xml`, `texts_<language>.xml`,
[`game-files.md`](../../docs/areas/game-files.md), *Locations*) and may never be shipped (record 0023). Players and
external testers cannot tell what a GUID is, and every later catalog step (#24 icons, #25 cache, #26 format, #42
upload) needs a catalog to start from.

## Scenarios

- S1: Given the Companion found the installation (`Game: build …`), when it starts, then it reads the game's assets and
  German and English texts in the background and builds a catalog: each GUID with its kind (region, good, building,
  workforce), its English name, its German name, and the regions the game data assigns it (one or more, or none).
  The tray menu shows `Catalog: reading` meanwhile and then `Catalog: <n> entries`.
- S2: Given the catalog is ready, when the player opens `Open diagnostics`, then the `Region` and `Good` cells show
  `<English name> / <German name> (<GUID>)` instead of the bare number.
- S3: Given a tick names a GUID the catalog does not hold, when it is shown, then its cell keeps the bare number as
  today, and the log names that GUID and its role (region, good, workforce, building) once per run, without any name;
  the Companion goes on.
- S4: Given the game data defines a region the code has never seen (a new one after an update), when the catalog is
  built, then that region resolves like the others: regions are read from the data, none is written into the code.
- S5: Given a contributor sets `CENSIO_ANNO_INSTALL` to an installation, when the opt-in catalog test runs, then it
  builds the catalog and checks every non-zero GUID of the fixture `long-session.jsonl` against it, reporting counts
  and the unresolved GUIDs, never a name.

## Errors and edge cases

- E1: GUID `0` means "none" (Product Manager, Q3): the fixture carries it as a product and a workforce GUID (310 of
  21,091 entries); it is never looked up, is shown as `0` and is not reported as unknown.
- E2: An asset with a name in only one of the two languages (the sample has 3,255 empty German and 72 empty English
  texts): the cell shows the name it has, and the log reports the GUID as incomplete once per run.
- E3: No installation (`Game: not found` or a settings error): `Catalog: no game`; every GUID stays a number.
- E4: An archive or file that cannot be read, is missing, malformed, carries a document type declaration or exceeds a
  bound: no catalog is used (the reader fails closed, record 0033); the menu shows `Catalog: failed`, the log names the
  failure kind, never file content; every GUID stays a number and the Companion goes on.
- E5: A name with control, format or separator characters, or of any length, is shown and logged by the rule of the
  log ([`companion.md`](../../docs/areas/companion.md), *Log*); one cell cannot reorder another.
- E6: The game runs while the catalog is read: reading is shared read-only and succeeds
  ([`game-files.md`](../../docs/areas/game-files.md), *Access*).
- E7: A GUID defined by two assets, or a text identifier with two different texts in one language: that GUID is
  treated as unknown and reported as ambiguous; the rest of the catalog is used. A text identifier repeated with the
  same text is no ambiguity (the sample repeats 130 English identifiers, each with one text).
- E8: An asset or text file also listed in an archive other than `config.rda` (a patch archive): the catalog is built
  from `config.rda`, and the log names each other archive that lists the same path (archive and path only), so a
  player's report shows when a patch carries its own copy (Q2).

## Non-functional requirements

- N1: Privacy: no name, text or extracted file enters the repository, a fixture, a release, a log or test output;
  test data is synthetic or an anonymised fixture the Tester builds from the Product Manager's sample, which stays
  outside the repository (records 0023, 0029; *Escalation to the Product Manager* in `.squad/routing.md`). Logs and
  test reports hold GUIDs, kinds, archive paths and counts only.
- N2: The catalog build writes no file anywhere (the cache is #25) and nothing into the installation.
- N3: Untrusted input: the XML is read without document type processing, without external entities and without any
  network access; memory stays bounded by the archive bounds plus the catalog itself, and no byte sequence makes the
  Companion throw or exit.
- N4: The build runs in the background at every start until #25 caches it; the tray, the pipe connection, the
  diagnostics window and `Exit` never wait for it.
- N5: Platform: the catalog is built in `Censio.GameData` (platform-neutral, tested on Linux); German and English
  only, another language needs only another text file (record 0023).

## Acceptance criteria

- AC1: On the Product Manager's installation every non-zero GUID of `long-session.jsonl` — 3 regions, 124 goods, 146
  buildings, 11 workforce — resolves to an entry of the expected kind with a non-empty English and German name; the
  opt-in test reports counts and unresolved GUIDs only, and names each other archive that lists an asset or text file
  of `config.rda` (S1, S5, E1, E8, N1).
- AC2: A region, good, building or workforce defined only in the test data, with a GUID and names no code contains,
  resolves to its kind and both names; entries of kind good, building and workforce carry every region the data
  assigns, and none where it assigns none; this holds in `Censio.GameData` on Linux (S1, S4, N5).
- AC3: A GUID not in the catalog, one with a name in one language only, and one defined ambiguously are each reported
  once per run with GUID and role and without a name, never fatal; GUID `0` is never looked up or reported (S3, E1, E2,
  E7).
- AC4: The diagnostics window shows `<English name> / <German name> (<GUID>)` in `Region` and `Good` for a known GUID,
  the available name for an incomplete one, and the bare number otherwise or while no catalog is ready; names follow
  the rule of the log (S2, S3, E2, E5).
- AC5: The `Catalog:` menu line reads `reading`, `<n> entries`, `no game` or `failed` by the state of the build, and
  the tray, connection and `Exit` do not wait for it (S1, E3, E4, N4).
- AC6: A missing, unreadable, malformed or oversized asset or text file, or one with a document type declaration,
  gives `Catalog: failed` and a log line with the failure kind and no content, and no catalog is used (E4, N3).
- AC7: Building the catalog writes no file; the repository's test data holds no extracted name or text (N1, N2).

## Out of scope

- Icons (#24), the cache and re-extraction after updates and the measured duration (#25), the catalog format and its
  serialisation (#26), the upload to the server (#42) and any web UI.
- Building and workforce names in the diagnostics window: its table has no building or workforce column.
- Languages other than German and English.
- Reading a patch archive's copy of the asset or text files (E8 only reports one).
- E6 needs no criterion: it is the existing shared read of [`game-files.md`](../../docs/areas/game-files.md).

## Open questions

- Q1: How do external testers see the catalog in #23? Answered: by the Product Manager, option A — English and German
  names in the diagnostics window's `Region` and `Good` cells, a `Catalog:` menu line, unknown GUIDs in the log
  (GUIDs only, no names).
- Q2: Which nodes hold the GUID, kind, names and regions, and does a patch archive hold its own copy? Answered: the
  Product Manager supplied the real files of `config.rda` (`assets.xml`, `datasets.xml`, both text files; kept outside
  the repository) and the hint that an asset's text identifier leads to its texts. The Lead checked them against the
  fixture: all 284 non-zero GUIDs are assets of `assets.xml` — the 3 regions are session assets that point to a
  region asset, the goods and workforce are product assets (workforce marked as such), the buildings are assets with a
  building section — and every one has a text identifier with a non-empty English and German text. No asset GUID is
  defined twice and no file has a document type declaration. Patch archives: the Product Manager saw no asset or
  `texts_*.xml` file in them, everything seems to be in `config.rda`. Should a later installation carry a copy, the
  Lead's documented precedence is E8 (build from `config.rda`, report the other copy); the plan records this and the
  remaining unverified layout points as residual risk in its decision record.
- Q3: May AC1 exclude GUID `0`? Answered: yes, by the Product Manager — `0` means "none", is never looked up and never
  reported as unknown.
- Q4: Where is the catalog built? Answered: in the Companion from the local installation (record 0023), with the
  folder from #22 ([`game-installation.md`](../../docs/areas/game-installation.md)).
- Q5: Does the per-entry bound of 512 MiB admit the files? Answered: `assets.xml` is 46,440,056 bytes, the German texts
  7,071,912 ([`game-files.md`](../../docs/areas/game-files.md), *Locations*), confirmed by the sample.
- Q6: Extract at every start? Answered: yes until the cache of #25.

## Area documents

- `docs/areas/game-catalog.md` (new): the files and nodes read, the kinds, how regions are found, the rules for
  unknown, incomplete, ambiguous and zero GUIDs, other archives' copies, the bounds, the failure kinds and the privacy
  rule.
- `docs/areas/game-files.md`: *Locations* states what this feature confirmed about the asset and text files; *Not here*
  links the new document.
- `docs/areas/companion.md`: the `Catalog:` menu line (*Tray icon and menu*), the named cells (*Diagnostics window*),
  the reported GUIDs and archives (*Log*).
- `README.md` (*Status*) and `docs/USER_GUIDE.md`, English then German: what testers see and how to read it.
