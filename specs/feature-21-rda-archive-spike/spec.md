# Spec: Spike — reading RDA archives (NuGet library or own reader)

Source: Issue #21
Status: Draft

## Problem

Names and icons for the pipe's numeric IDs live in the game's own archives (`config.rda`, `ui.rda`), to be extracted
locally ([0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md)). Record
[0024](../../docs/decisions/0024-rda-reading-with-an-existing-nuget-library.md) chose a NuGet library "where one fits"
but left open whether one does, and nobody has located the files that hold goods, buildings, workforce, regions and
their German and English texts in Anno 117. #23 (names) and #24 (icons) cannot start until both are known and
`Censio.GameData` (today only `GameDataAssembly`) can read an entry of such an archive.

## Scenarios

- S1: Given the spike is done, when a maintainer reads the new decision record, then it lists every candidate library
  found with its licence, maintenance (last release, activity), Anno 117 support and API fit, and the choice: a
  library, or an own reader with its effort estimate.
- S2: Given a developer starting #23 or #24, when they read the game-files area document, then for each of goods,
  buildings, workforce, regions, German texts, English texts and icons they find the archive (relative to the
  installation folder) and the path inside it, with the game version they were confirmed on.
- S3: Given an archive, when the reader lists it, then it returns each entry's path and size; when it reads one entry
  by path, then it returns that entry's content, decompressed.
- S4: Given a contributor with Anno 117 installed, when they point the prototype check at the installation folder and
  run it, then it reads one asset file from the configuration archive and one icon from the UI archive and reports for
  each only its path, its size and its format marker (the start of an XML document, the DDS header), plus the entry
  paths that match the data kinds of S2 — no other content.
- S5: Given no installation is configured (CI, the cloud session), when the tests run, then the prototype check is
  reported inconclusive, not failed, and every other test runs on a synthetic archive.

## Errors and edge cases

Each failure is a typed result with a kind, never an exception that escapes the reader
([0033](../../docs/decisions/0033-pipe-reader-strict-from-the-reference-source.md) style).

- E1: Not an RDA archive, or a format version the reader does not support → "unsupported archive"; nothing is read.
- E2: Truncated archive, or a directory, block or entry that points outside the file → "corrupt archive".
- E3: A declared size above the reader's bound, or a decompressed entry larger or smaller than declared → "corrupt
  entry"; the reader never allocates beyond the bound.
- E4: An entry with a feature the reader does not support (e.g. encryption) → "unsupported entry" for that entry;
  the other entries stay readable.
- E5: The requested path is not in the archive → "not found".
- E6: The archive file is missing or cannot be opened → "unreadable", naming the I/O problem, not the content.
- E7: The game is running and holds the archive open → the read succeeds and the game is not disturbed.

## Non-functional requirements

- N1: Reading one entry costs memory for the directory and that entry, not for the whole archive (the archives are
  large); the per-entry bound is set in the plan.
- N2: The installation is opened read-only, sharing read and write with the game; nothing is written into it.
- N3: No game content — no extracted entry, text or icon — enters the repository, a fixture, a log or test output
  ([0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md),
  [0029](../../docs/decisions/0029-anonymised-test-fixtures-only.md)); synthetic archives hold invented content.
  Entry paths and sizes are not content.
- N4: A chosen library has an MIT, BSD, Apache or Unlicense licence
  ([0028](../../docs/decisions/0028-mit-licence.md)), comes from NuGet through Central Package Management, and runs on
  `net10.0` on Linux, where `Censio.GameData` is tested ([0004](../../docs/decisions/0004-companion-shell-and-linux-build.md)).
- N5: Security: the reader parses external input (`.squad/project.md`, *Security areas*, "later game files"); a new
  dependency is a security area too.
- N6: Nothing changes for Companion or server users: the reader is not wired into the Companion yet.
- N7: The session cannot open the community's GitHub repositories or NuGet search (proxy 403; nuget.org package pages
  and `api.nuget.org` open); evidence from pages the session cannot open is confirmed by a person (`.squad/routing.md`,
  *Pages the session cannot open*).

## Acceptance criteria

- AC1: A new decision record amends 0024 (released in v0.1.0, so not edited) with the evaluation of S1 and the
  choice; each licence and maintenance claim names its source, and a source the session could not open is confirmed
  by a person (S1, N4, N7).
- AC2: The game-files area document states the locations of S2, each confirmed on a real installation by the run of
  AC5, with the game version (S2).
- AC3: On synthetic archives, the reader lists the entries and reads a stored and a compressed entry of every archive
  variant the record names for Anno 117 (S3, N1, N3).
- AC4: On synthetic malformed archives, the reader returns the kinds of E1–E6 and reads the other entries after E4
  (E1–E6, N1, N5).
- AC5: The prototype check of S4 passes on a real installation, also while the game runs, reporting only paths,
  sizes, counts and format markers; unset, it is inconclusive (S4, S5, E7, N2, N3).

## Out of scope

- Finding the installation (#22): the check takes the folder as given.
- Building the catalog (#23), converting DDS icons (#24), the cache (#25), the catalog format (#26).
- Wiring the reader into the Companion (N6); README and user guide stay unchanged.
- Writing or modifying archives; archive versions of other Anno games unless Anno 117 uses them.

## Open questions

- Who runs the real-installation part (the check of AC5, which also yields the locations of AC2 and confirms Anno 117
  support for AC1), and when — and may the reader be built from the format's public descriptions without a real
  sample archive in the repository (`.squad/routing.md`, *Escalation to the Product Manager*)? Answered (Product
  Manager, option a): yes — the Product Manager runs the check on a local build of the branch head before PR
  approval (step 9); the run is the real-sample check, only paths, sizes, counts and format markers are reported, and
  the residual risk is recorded in the record.
- Where do the file locations go, given the issue asks for them "in the ADR"? Answered: in the area document,
  linked from the record — they describe the installed game version and change with game updates, while a released
  record is append-only (`docs/decisions/README.md`).

## Area documents

- New area `game-files` (name and scope decided in the plan; index row in `docs/areas/README.md`): the archive
  versions and entry kinds read; the bounds; the failure kinds of E1–E6; read-only shared access; the locations of
  S2 with the game version; that no content leaves the machine (link 0023).
- `docs/UNIT_TESTS.md` (*Where tests live*): the environment variable that names the installation for the check.
