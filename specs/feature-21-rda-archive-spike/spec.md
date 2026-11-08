# Spec: Spike — reading RDA archives (NuGet library or own reader)

Source: Issue #21
Status: Approved by Lead

## Problem

Record [0024](../../docs/decisions/0024-rda-reading-with-an-existing-nuget-library.md) chose a NuGet library for the
game's RDA archives "where one fits" but left open whether one does, and nobody has located where Anno 117 keeps
goods, buildings, workforce, regions, their German and English texts, and icons. #23 and #24 wait for both and for a
`Censio.GameData` that can read an archive entry.

## Scenarios

- S1: Given the spike is done, when a maintainer reads the new decision record, then it says how candidate libraries
  were searched (terms, where) and lists each candidate with its licence, maintenance (last release, activity), Anno
  117 support and API fit, and the choice: a library, or an own reader with its effort estimate.
- S2: Given a developer starting #23 or #24, when they read the game-files area document, then they find the entry
  paths and every archive (relative to the installation folder) of the asset, data set and German and English text
  files and of the icon folders by kind (goods, buildings, workforce, regions where present); which of those files
  and which nodes in them hold goods, buildings, workforce and regions, the order the game loads the archives in and
  which copy wins (each with its source, or "unknown" as a stated risk for #23); and the game version this was found
  on.
- S3: Given an archive, when the reader lists it, then it returns each entry's path and size; when it reads one entry
  by path, then it returns that entry's content, decompressed.
- S4: Given a contributor with Anno 117 installed, when they point the prototype check at the installation folder and
  run it, then it reads one asset file and one icon and reports for each only its path, its size and its format
  marker (the start of an XML document, the DDS header), plus, for each entry path matching a data kind of S2, every
  archive it occurs in — no other content.
- S5: Given no installation is configured (CI, the cloud session), when the tests run, then the prototype check is
  reported inconclusive, not failed, and every other test runs on a synthetic archive.
- S6: Given the plan is not written yet, when the Product Manager runs the discovery listing on the installation,
  then the log records for every `.rda` file its path, size and header format marker, and — where a community tool
  can list it — the block flags and the entry paths with sizes, no content.

## Errors and edge cases

Each failure is a typed result with a kind, never an exception that escapes the reader
([0033](../../docs/decisions/0033-pipe-reader-strict-from-the-reference-source.md) style).

- E1: Not an RDA archive, or a format version the reader does not support → "unsupported archive"; nothing is read.
- E2: Truncated archive; a directory, block or entry that points outside the file; a block chain that revisits an
  offset; more entries or a larger directory than the reader's bounds → "corrupt archive", returned without hanging.
- E3: A declared size above the reader's bound, or a decompressed block or entry larger or smaller than declared →
  "corrupt entry"; the reader never allocates beyond the bound.
- E4: An entry with a feature the reader does not support (e.g. encryption) → "unsupported entry" for that entry;
  the other entries stay readable.
- E5: The requested path is not in the archive → "not found".
- E6: The archive file is missing or cannot be opened → "unreadable", naming the I/O problem, not the content.
- E7: The game is running and holds the archive open → the read succeeds and the game is not disturbed.
- E8: The same entry path twice in one archive → listing shows both; a read by that path returns "duplicate entry",
  neither copy. The same path in several archives is no reader error; S2 and S4 report each archive.

## Non-functional requirements

- N1: Reading one entry costs memory for the directory and the decompressed block that holds it (a block may pack
  many files), never the whole archive; the bounds on block size, entry count and directory size are set in the plan.
- N2: The installation is opened read-only, sharing read and write with the game; nothing is written into it.
- N3: No game content — no extracted entry, text or icon — enters the repository, a fixture, a log or test output
  ([0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md),
  [0029](../../docs/decisions/0029-anonymised-test-fixtures-only.md)); synthetic archives hold invented content.
  Entry paths, sizes, flags and header markers are not content.
- N4: A chosen library has an MIT, BSD, Apache or Unlicense licence
  ([0028](../../docs/decisions/0028-mit-licence.md)), comes from NuGet through Central Package Management, and runs on
  `net10.0` on Linux, where `Censio.GameData` is tested ([0004](../../docs/decisions/0004-companion-shell-and-linux-build.md)).
- N5: Security: the reader parses external input (`.squad/project.md`, *Security areas*, "later game files"); a new
  dependency is a security area too.
- N6: Users see no change (not wired in).
- N7: The session searches NuGet only through the nuget.org website (the search API answers 403) and cannot open
  the community's GitHub repositories; a person confirms the search and every page the session could not open
  (`.squad/routing.md`, *Pages the session cannot open*).

## Acceptance criteria

- AC1: A new decision record amends 0024 (released in v0.1.0, so not edited) with the search and evaluation of S1
  and the choice, resting on the discovery listing; each claim names its source, and the search and each source the
  session could not open are confirmed by a person (S1, N4, N7).
- AC2: The game-files area document states the file and icon-folder locations of S2 from the Product Manager's run
  of the S4 check (the listing named only archives), confirmed on the final head by the run of AC5, with the game
  version; which file and node holds goods, buildings, workforce and regions, load order and winning copy stay
  "unknown" (for #23) unless sourced (S2).
- AC3: On synthetic archives, the reader lists the entries and reads a stored and a compressed entry, and an entry
  of a block packing several files, of every archive variant the discovery listing found (S3, S6, N1, N3).
- AC4: On synthetic malformed archives, the reader returns the kinds of E1–E6 and E8 — for E2 one test each for a
  truncated file, an out-of-file offset, a revisited block offset and the entry-count and directory-size bounds —
  and reads the other entries after E4 (E1–E6, E8, N1, N5).
- AC5: The prototype check of S4 passes on a real installation, also while the game runs, reporting only paths,
  sizes, counts and format markers; unset, it is inconclusive (S4, S5, E7, N2, N3).
- AC6: Before the plan, the discovery listing of S6 is in the log, reported only as paths, sizes, flags and header
  markers (S6, N3).

## Out of scope

- Finding the installation (#22): the check takes the folder as given.
- Building the catalog (#23), converting DDS icons (#24), the cache (#25), the catalog format (#26).
- Resolving a path across archives by load order: that is #23's, from the order S2 states.
- Locating goods, buildings, workforce and regions inside the asset and data set files (which file, which nodes):
  the survey reads no content (N3), so that is #23's.
- Wiring the reader into the Companion (N6); README and user guide stay unchanged.
- Writing or modifying archives; archive versions of other Anno games unless Anno 117 uses them.

## Open questions

- Who runs the AC5 check, and when? Answered: the Product Manager (option a), on a local build of the branch head
  before step 9; it is the real-sample check, and the residual risk goes into the record.
- Does the Product Manager also run the discovery listing of S6 before the plan? Answered: yes (option a), with a
  command the Lead supplies and a community RDA tool where it opens the archives.
- Where do the file locations go (the issue says "in the ADR")? Answered: in the area document, linked from the
  record — they change with game updates, a released record does not.

## Area documents

- New area `game-files` (name and scope decided in the plan; index row in `docs/areas/README.md`): the archive
  versions and entry kinds read; the bounds; the failure kinds of E1–E6 and E8; read-only shared access; the locations of
  S2 with the load order and the game version; that no content leaves the machine (link 0023).
- `docs/UNIT_TESTS.md` (*Where tests live*): the environment variable that names the installation for the check.

## Challenge

- M1 (facts arrive only at step 9): accepted — S6/AC6 add a discovery listing before the plan; AC1–AC3 rest on it,
  AC5 confirms on the final head. The Product Manager agreed to the extra run (option a).
- M2 (one archive per data kind assumed): accepted — S2/AC2 give every archive, the load order and the winning copy;
  S4 reports every archive; E8 defines a duplicate within one archive.
- m1: accepted — N1 and E3 per decompressed block; AC3 covers a packed block.
- m2: accepted — E2 bounds entry count and directory size and catches a revisited offset; AC4 tests each.
- m3: accepted — S1/AC1 state the search; N7 a person confirms it.
- m4: accepted — the new record's *Supersedes* line also amends 0024's "in this record": locations live in the
  area document it links.

## Amendments

| Criterion | Change | Reason | Step | Decided by |
| --------- | ------ | ------ | ---- | ---------- |
| AC2 | Locations at file and icon-folder level; which file and node holds goods, buildings, workforce and regions stays "unknown" for #23, like load order | The survey reports no content (N3), so it shows where the asset, data set and text files and icon folders are (`log.md` row "G3 task 9 PM survey run"), not which nodes hold each data kind; S2 and *Out of scope* changed with it | G3 task 15 | Lead |
