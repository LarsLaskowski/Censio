# Spec: Game catalog of goods, buildings, workforce and regions with German and English names

Source: Issue #23
Status: Draft

## Problem

The pipe identifies regions, goods, workforce and buildings by GUIDs only
([`pipe.md`](../../docs/areas/pipe.md), *Message types*); the diagnostics window shows them raw
([`companion.md`](../../docs/areas/companion.md), *Diagnostics window*), and the later web UI needs German and English
names. The names live in the player's installation (`config.rda`, [`game-files.md`](../../docs/areas/game-files.md),
*Locations*) and may never be shipped (record 0023).

## Scenarios

- S1: Given the Companion found the installation (`Game: build …`), when it starts, then it reads the game's assets,
  data sets and German and English texts in the background and builds a catalog: each GUID with its kind (region,
  good, building, workforce), its English and German name, and its regions. The tray menu shows `Catalog: reading`,
  then `Catalog: <n> entries`.
- S2: Given the catalog is ready, when the player opens `Open diagnostics`, then the `Region` and `Good` cells show
  `<English name> / <German name> (<GUID>)` instead of the bare number.
- S3: Given a closed tick, live or replayed, whether or not the diagnostics window is open, when it names a GUID in any
  role (region, good, workforce, building) that the catalog cannot name, then the log names that GUID, its role and
  the reason (unknown, incomplete, nameless, ambiguous) once per run, without any name; a shown cell keeps the bare
  number or the name it has; the Companion goes on.
- S4: Given the game data defines a region the code has never seen, when the catalog is built, then it resolves like
  the others: regions, region types and their names are read from the data, none is written into the code.
- S5: Given a contributor sets `CENSIO_ANNO_INSTALL` to an installation, when the opt-in catalog test runs, then it
  checks every non-zero GUID of `long-session.jsonl` against the catalog, reporting counts and unresolved GUIDs, never
  a name.

Regions, as the data states them:

- A **region entry** is every session asset whose `Session/Region` names a region asset (template not restricted, so
  `SessionModerate` and `SessionModerateRandom` both count); it carries that region asset's GUID and, where the region
  asset states a `RegionID`, that region type.
- A good or workforce entry carries the region types of its `AssociatedRegion`, a building entry those of its
  `AssociatedRegions` (`;`-separated names of the `Region` data set in `datasets.xml`). `ProductionRegions` is not
  used. A missing list is "not stated", an explicit empty one "none"; the two stay distinct.

## Errors and edge cases

- E1: GUID `0` means "none" (Product Manager, Q3): never looked up, shown as `0`, never reported.
- E2: An entry with a name in one language only: the cell shows the name it has; reported as incomplete.
- E3: No installation: `Catalog: no game`; every GUID stays a number.
- E4: A missing, unreadable, malformed or oversized file, or one with a document type declaration: no catalog is used
  (fails closed, record 0033); `Catalog: failed`, the log names the failure kind, never content; GUIDs stay numbers.
- E5: Names with control, format or separator characters, of any length, are shown and logged by the rule of the log
  ([`companion.md`](../../docs/areas/companion.md), *Log*).
- E7: A GUID defined by two assets, or one text identifier with two different texts in one language: the GUID is
  treated as unknown and reported as ambiguous. A text identifier repeated with the same text, or shared by several
  assets (a session and its region asset share one), is no ambiguity.
- E8: An asset or text file also listed in another archive: the catalog uses `config.rda` and the log names the other
  archive and path (Q2).
- E9: An asset of a catalog kind without a text identifier, or whose texts are both empty: it is an entry without
  names; its cell shows the GUID, and it is reported as nameless.
- E10: An asset that inherits from a base asset (`BaseAssetGUID`, no `Template` of its own): left out of the catalog
  and counted in the log; seen in a tick, it is reported as unknown. None is in the fixture.
- E11: A region type name not in the `Region` data set: dropped from the entry and counted in the log; a region asset
  without a `RegionID` gives its region entries no region type (today the base game's first region).

## Non-functional requirements

- N1: Privacy: no name, text or extracted file enters the repository, a fixture, a release, a log or test output; test
  data is synthetic or an anonymised fixture built from the Product Manager's sample, which stays outside the
  repository (records 0023, 0029). Logs and reports hold GUIDs, kinds, archive paths and counts only.
- N2: The catalog build writes no file anywhere (the cache is #25) and nothing into the installation.
- N3: Untrusted input: the XML is read forward-only, one asset or text element at a time, never as a tree of the
  whole file; without document type processing, external entities or network access; each file within the archive's
  per-entry bound ([`game-files.md`](../../docs/areas/game-files.md), *Limits*); no byte sequence makes the Companion
  throw or exit.
- N4: The build runs in the background at every start until #25 caches it; tray, pipe connection, diagnostics window
  and `Exit` never wait for it.
- N5: The catalog is built in `Censio.GameData` (platform-neutral, tested on Linux); another language needs only
  another text file (record 0023).

## Acceptance criteria

- AC1: On the Product Manager's installation every non-zero GUID of `long-session.jsonl` — 3 regions, 124 goods, 146
  buildings, 11 workforce — resolves to an entry of the expected kind with a non-empty English and German name; the
  opt-in test reports counts and unresolved GUIDs only, and names each other archive listing an asset or text file of
  `config.rda` (S1, S5, E1, E8, N1).
- AC2: In test data with GUIDs, names and region types no code contains, a session of either session template resolves
  as a region entry with its region asset and stated region type; goods, workforce and buildings carry the region
  types of their association list, "not stated" for a missing list and "none" for an empty one; an unknown type name
  is dropped and counted; this holds in `Censio.GameData` on Linux (S1, S4, E11, N5).
- AC3: For closed ticks with the diagnostics window closed, an unknown building GUID and an unknown workforce GUID, an
  unknown region and good, an incomplete, a nameless and an ambiguous GUID, and an inherited asset are each reported
  once per run with GUID, role and reason and without a name, never fatal; GUID `0` is never looked up or reported
  (S3, E1, E2, E7, E9, E10).
- AC4: The diagnostics window shows `<English name> / <German name> (<GUID>)` in `Region` and `Good` for a known GUID,
  the available name for an incomplete one, and the bare number otherwise or while no catalog is ready; names follow
  the rule of the log (S2, S3, E2, E5, E9).
- AC5: The `Catalog:` menu line reads `reading`, `<n> entries`, `no game` or `failed` by the state of the build, and
  the tray, connection and `Exit` do not wait for it (S1, E3, E4, N4).
- AC6: A missing, unreadable or malformed asset, data set or text file, one above the per-entry bound (lowered by the
  test), or one with a document type declaration gives `Catalog: failed` and a log line with the failure kind and no
  content, and no catalog is used; the files are read forward-only (E4, N3).
- AC7: Building the catalog writes no file; the repository's test data holds no extracted name or text (N1, N2).

## Out of scope

- Icons (#24), the cache, re-extraction and the measured duration (#25), the catalog format (#26), the upload (#42),
  any web UI, languages other than German and English.
- Building and workforce names in the diagnostics window: its table has no such column.
- Reading a patch archive's copy of the files (E8 only reports one); resolving inherited assets (E10); a region type
  for a region asset without a `RegionID` (E11).

## Open questions

- Q1: How do testers see the catalog? Answered: by the Product Manager, option A — names in the diagnostics window's
  `Region` and `Good` cells, a `Catalog:` menu line, unreported GUIDs in the log (GUIDs only).
- Q2: Which nodes hold the data; does a patch archive hold copies? Answered: the Product Manager supplied
  `config.rda`'s files (kept outside the repository), which define all 284 non-zero fixture GUIDs with both texts,
  and saw no copies in the patch archives; E8 is the precedence should one appear.
- Q3: May AC1 exclude GUID `0`? Answered: yes, by the Product Manager — it means "none".
- Q4: Where, and how often? Answered: in the Companion (record 0023) at every start until #25; `assets.xml`
  (46,440,056 bytes) fits the 512 MiB per-entry bound.

## Area documents

- `docs/areas/game-catalog.md` (new): files and nodes read, kinds, regions, the reported cases, bounds, failures,
  privacy.
- `docs/areas/game-files.md`: *Locations* states what this feature confirmed; *Not here* links the new document.
- `docs/areas/companion.md`: the `Catalog:` menu line, the named cells, the reported GUIDs and archives (*Log*).
- `README.md` (*Status*) and `docs/USER_GUIDE.md`, English then German: what testers see.

## Challenge

1. Regions undefined (major) — accepted: defined under *Scenarios* (sessions with `Session/Region`, the association
   lists, the `Region` data set, "not stated" against "none"; not `ProductionRegions`, which says where a good is
   produced). No default is written in: region asset 3225 states no `RegionID`, so its sessions get no type (E11);
   proposed follow-up: find the template default in the data. AC2 tests it.
2. Building and workforce GUIDs never reported (major) — accepted: S3 and AC3 cover every role of every closed tick,
   window open or not.
3. Asset without names — accepted: E9.
4. Inherited assets — accepted: E10; a risk for the decision record.
5. Memory bound — accepted: N3 forward-only, AC6 names the per-entry bound.
6. Structural facts — accepted: E7 on shared text identifiers; the asset-definition rule goes to the plan's
   *Verified facts*.
7. Session templates — accepted: any template with `Session/Region` naming a region asset.
8. Counts as values — accepted: removed from E2; no test asserts sample counts.
