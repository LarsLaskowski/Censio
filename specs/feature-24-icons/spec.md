# Spec: Extract and convert icons

Source: Issue #24
Status: Draft

## Problem

The Companion's catalog ([`game-catalog.md`](../../docs/areas/game-catalog.md)) names goods and regions, but has no
pictures: the diagnostics window shows text only ([`companion.md`](../../docs/areas/companion.md), *Diagnostics
window*, "icons do not exist yet"), and the later web UI (#26, #42) has nothing to show beside a name. The icons are in
the player's own installation as DDS files in `ui.rda` and the `zz_patchfiles_*` archives
([`game-files.md`](../../docs/areas/game-files.md), *Locations*), which no browser shows, and may only be extracted on
the player's machine (record [0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md)).

## Scenarios

- S1: Given a ready catalog, when the icon step that follows it ends, then every good entry whose asset names an icon
  (`IconFilename`) the icon archives hold has that icon as a PNG of the full size the archive holds, kept in memory
  with the catalog; nothing is written to disk. The asset names `data/ui/fhd/<rest>.png`, the archives hold
  `data/ui/4k/<rest>_0.dds`, both compared ignoring case (the Product Manager's sample).
- S2: Given icons, when the player opens `Open diagnostics`, then the `Region` and `Good` cells of a GUID with an icon
  show it before the text they show today; a cell without one looks as today. Names appear as soon as the catalog is
  ready, as today; icons follow when the icon step ends.
- S3: A region entry gets the icon its own session asset names, without one that of its region asset (game sessions
  name none).
- S4: The icon archives are `maindata/ui.rda` and every file below `maindata` named `zz_patchfiles_<n>.rda`, searched
  as the survey searches ([`game-files.md`](../../docs/areas/game-files.md), *Installation survey*), the name compared
  ignoring case, `<n>` one or more decimal digits read as a number (leading zeros allowed). When several hold an icon
  path, the archive with the highest `<n>` wins, `ui.rda` being the oldest.
- S5: When the icon step ends, the log holds one information line with the good and region entries with an icon and,
  per reason (E2 to E4), the number without one — counts only.

## Errors and edge cases

- E1: `ui.rda` or any patch archive is missing (`ui.rda` only), cannot be opened or listed, the listing fails, or two
  patch archives have the same `<n>`: no entry has an icon; names, entries and the `Catalog:` line are unchanged; a
  warning log line names the kind and the archive path. A file named like a patch archive whose `<n>` is no number or
  exceeds 32 bits is no icon archive.
- E2: The asset (for a region entry also its region asset) names no icon: counted as *not named*.
- E3: The path lacks the form of S1, or no icon archive holds its entry: counted as *missing*.
- E4: The entry is not a two-dimensional DDS image with a `DX10` header of format BC7 (DXGI 98 or 99), contradicts its
  own size, exceeds a bound (N2), cannot be read, or one archive lists it twice ignoring case: counted as *unreadable*;
  other icons and names are unaffected. Only the largest image is used. A width or height that is no multiple of 4 is
  decoded from whole blocks and cropped. A block of the reserved mode (first byte 0) decodes to transparent black.
- E5: An icon path holding `..`, a rooted path or a drive letter is only compared with archive entry paths, never
  turned into a file system path.
- E6: Several entries name the same icon: each gets it.
- E7: The catalog fails or finds no game: no icons.
- E8: An ambiguous or unknown GUID has no entry and so no icon; an incomplete or nameless entry can have one.

## Non-functional requirements

- N1: Privacy: no icon, neither as DDS nor as PNG, enters the repository, a test fixture, a release artifact, the log or
  test output; tests generate their DDS images in code (records 0023 and
  [0029](../../docs/decisions/0029-anonymised-test-fixtures-only.md)). Logs hold GUIDs, archive paths, kinds and counts.
- N2: Bounds against hostile input: a per-entry bound for icon entries below the archive's 512 MiB, checked before the
  entry is read, and a bound on width and height checked from the header before any pixel is decoded; the plan sets
  both above the real icons (512 by 512 pixels, 10 levels, 349,700 bytes) and the largest one AC4 reports.
- N3: Memory: the catalog holds one PNG per distinct icon path and at most one icon's decoded pixels at a time; the
  window decodes each distinct icon once, at cell size, and shares it across cells and ticks.
- N4: Decoding and conversion run in the platform-neutral game data library, tested on Linux; only the display is
  Windows code.
- N5: Own code or a library under MIT, BSD, Apache 2.0 or the Unlicense (record
  [0028](../../docs/decisions/0028-mit-licence.md)); not ImageSharp (Product Manager). The plan checks a licence from
  its text.
- N6: The icon step runs in the background after the catalog and delays neither names, tray menu, connection nor
  `Exit`.
- N7: Archives are opened read-only, shared with the running game; with a UNC game folder they are read over SMB.
- N8: Pixels: RGBA with alpha kept; DXGI 98 and 99 give the same bytes; the PNG carries no colour-space chunk.

## Acceptance criteria

- AC1: For synthetic BC7 blocks of each mode 0 to 7, covering partitions and, in modes 4 and 5, rotation and index
  selection, and for the reserved mode, the PNG's RGBA pixels equal expected values derived without the code under
  test (worked from the BC7 format description, or checked once against a reference decoder the plan names), for DXGI
  98 and 99 alike, cropped for a size that is no multiple of 4 (E4, N8).
- AC2: When several icon archives hold a path, the highest `<n>` wins, `ui.rda` only without a patch archive; archives
  are recognised and ordered as S4 says (S4, E1).
- AC3: Each case of E1 to E5 and E7 leaves the affected entries without an icon, counted by reason in S5's line (E1: a
  warning with kind and archive), and every name, entry and the `Catalog:` line unchanged (S5, E1, E2, E3, E4, E5, E7, N2).
- AC4: On the Product Manager's installation, the opt-in test with `CENSIO_ANNO_INSTALL` finds an icon for every good
  GUID of `long-session.jsonl` the catalog names, and fails when a region entry naming a path of S1's form is
  *unreadable*; it reports per region entry its icon or reason, the icon paths held by more than one archive and
  whether their sizes differ, the largest icon, the total PNG bytes, duration and peak working set, never a name or
  image (issue: "every good in the fixtures has an icon"; N2, N3, N6).
- AC5: On a Windows build, the window shows icons in the `Region` and `Good` cells, names before icons arrive, and
  cells without one as today (manual, S2).
- AC6: The repository and release artifacts hold no extracted icon: no DDS or PNG is committed for the tests, the
  Companion writes no icon to disk, and a new dependency meets N5 (issue; N1, N5).
- AC7: From a synthetic archive set, each good and region entry gets the icon its asset or region asset names by S1's
  rule, entries naming one path share one icon, and the lookup ignores case (S1, S3, E6, E8, N3, N4, N7).

## Out of scope

- Icons of workforce and buildings (the window shows neither; the web UI adds them when it needs them).
- Caching icons on disk and re-extracting after updates (#25); catalog format (#26); upload (#42); WebP; other sizes.

## Open questions

- How does an asset name its icon, and which entry and format hold it? Answered: by the Product Manager (sample kept
  outside the repository): goods and region assets name `data/ui/fhd/<category>/.../<name>.png` in
  `Values/Standard/IconFilename` (four goods of `base` and `dlc03`, one region asset); sessions name none. `ui.rda`
  and `zz_patchfiles_300` hold `data/ui/4k/<same rest>_0.dds`; the `dlc03` one only `zz_patchfiles_300`. The four
  files: `DX10` header, BC7 sRGB, 512 by 512, 10 levels, 349,700 bytes, all eight BC7 modes used. No region icon file:
  AC4 checks region icons.
- Which copy wins? Answered: by the Lead from the Product Manager's unverified statement in `game-files.md`; AC4
  reports the copies, and the residual risk goes into the plan's record.
- PNG or WebP? Answered: PNG, which browsers and WPF show without an extra codec.
- Where are icons stored? Answered: in memory; the disk cache is #25.
- How do testers see icons? Answered: in the window's `Region` and `Good` cells, beside the names (record
  [0048](../../docs/decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md)).

## Area documents

- A new area `game-icons` (the plan decides; `game-catalog.md` the alternative): entries with icons, path rule and
  lookup, archives and order, format, PNG, bounds, reasons, privacy, the opt-in test; its index row; the *Not here*
  lines of `game-catalog.md` and `game-files.md` and the latter's *Icons* row; `game-catalog.md` *Network* (icon
  entries over SMB); `game-files.md` *Paths* ("the icon cache of #24" no longer holds).
- [`companion.md`](../../docs/areas/companion.md): icons in the cells, icons after names, the log lines.
- `.squad/project.md`: the DDS decoder under *Parsing of external input*, the icon reads under *Outbound calls* and
  file paths (orchestrator, from the Dev's text).
- `README.md` and `docs/USER_GUIDE.md` (English, then German).

## Challenge

- M1 accepted: AC1 now covers modes 0 to 7, partitions, rotation and index selection, and the reserved mode, with
  expected pixels derived independently; E4 and N8 fix the reserved mode (transparent black, the format's rule),
  cropping, DXGI 98 and 99 alike, alpha, no colour-space chunk. The path rule moved to AC7.
- M2 accepted: S4 defines the patch archives, their name, number and order; E1 makes any unopenable icon archive and a
  duplicate `<n>` withhold all icons with a warning, so no stale copy is used silently; AC2 and AC3 cover it.
- Minor 1 accepted: AC4 reports copies and per-region reasons and fails on an *unreadable* region icon.
- Minor 2 accepted: lookup ignores case (S1); two matches in one archive are *unreadable* (E4); AC7 tests it.
- Minor 3 accepted: N2 sets a lower per-entry bound for icons.
- Minor 4 accepted: N3 bounds the window's decoding.
- Minor 5 accepted: names appear as today, icons later (S2, N6); AC4 reports total PNG bytes.
- Minor 6 accepted: *Area documents* lists the three places.
