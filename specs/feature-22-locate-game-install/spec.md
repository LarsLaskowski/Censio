# Spec: Locate the game installation

Source: Issue #22
Status: Draft

## Problem

The catalog (#23 to #26) is built from the archives below the installation folder
([`game-files.md`](../../docs/areas/game-files.md)), but nothing finds that folder: only the survey test reads it from
`CENSIO_ANNO_INSTALL`. The player should not have to type it, #25 needs the game version, and a missing or wrong folder
must be visible with its reason, while the Companion today shows no error message at all.

## Scenarios

- S1: Given Anno 117 installed through Ubisoft Connect and no game folder in the settings file, when the Companion
  starts, then the tray menu shows, greyed out and directly below `Last tick:` (above the first separator), `Game:
  <version> (Ubisoft Connect)`, for example `Game: 3.0.1733841 (Ubisoft Connect)`, and the log has an information
  line with the source, the version and the folder. The diagnostics window does not show the line.
- S2: Given the game installed through Steam in any of Steam's library folders, not recorded by Ubisoft Connect, and no
  game folder in the settings file, when the Companion starts, then the menu shows `Game: <version> (Steam)`.
- S3: Given the game in the default game folder of Ubisoft Connect or Steam while that store has no record of it (for
  example after Windows was reinstalled), when the Companion starts, then it is found there, with that store as the
  source.
- S4: Given the settings file `%LOCALAPPDATA%\Censio\settings.json` with `"gameFolder"` naming an installation, when the
  Companion starts, then that folder is used without any automatic search and the menu shows `Game: <version>
  (settings file)`.
- S5: Given installations found through both stores and no game folder in the settings file, when the Companion starts,
  then the one Ubisoft Connect records is used; the player picks another one through the settings file. Within one
  store the first valid installation in the store's own order is used (Steam: its libraries in the order of their
  number in Steam's library list; Ubisoft Connect: its records by ascending number), checked against the real samples
  (N8).
- S6: Given the search has not ended yet (a slow or sleeping drive), when the player opens the menu, then it shows
  `Game: searching`, and every other entry and `Exit` work as usual.

## Errors and edge cases

The menu line is greyed out like `Last tick:`. Each case below also logs one warning naming the cause, and for the
settings file the folder or the file as written there.

- E1: The folder in the settings file does not exist: `Game: folder in settings not found`.
- E2: The folder in the settings file is not a full path (relative, a bare drive letter, or with environment variables
  such as `%USERPROFILE%`, which are not expanded): `Game: folder in settings is not a full path`.
- E3: The folder in the settings file exists but is not an installation (for example the player named `maindata`
  itself or the folder of the game's program file): `Game: folder in settings is not an Anno 117 installation`. The
  warning names what is expected: the folder that holds `maindata\config.rda`.
- E4: The settings file is not valid JSON, `gameFolder` is not a text, the key occurs twice (also in different case),
  the file is larger than 1 MiB, or it cannot be read (locked, access denied): `Game: settings file invalid`. A
  leading UTF-8 byte order mark, `//` and `/* */` comments and trailing commas are accepted, since the file is edited
  by hand (Notepad may save the mark).
- E5: In E1 to E4 no automatic search runs: a silently found other folder would hide the mistake.
- E6: No settings file, an empty or blank `gameFolder`, or no `gameFolder` key: the automatic search runs (S1 to S3).
- E7: A store record that cannot be read, is damaged, or points at a folder that is not an installation (a leftover of
  an uninstalled game) is skipped and the next source is tried.
- E8: Nothing is found: `Game: not found`; the warning names the sources that were checked (not their folders).
- E9: An installation is found but its version cannot be determined: `Game: version unknown (<source>)`; the
  installation is still used.
- E10: A change while the Companion runs (settings edited, game installed or moved) counts only after a restart.
- E11: The key differs from `gameFolder` only in case (`GameFolder`): it counts as `gameFolder`. Any other spelling
  is an unknown key (N3), so the automatic search runs; the menu then shows the store, not `(settings file)`, which
  the user guide points out.
- E12: A version text that holds control or format characters or is longer than 64 characters is shown sanitized as in
  the log (each such character becomes `?`) and cut to 64 characters with `…`.

## Non-functional requirements

- N1: The search never blocks the tray: the icon, the menu and the pipe connection work while it runs, and `Exit`
  keeps its bound of 2 seconds ([`companion.md`](../../docs/areas/companion.md), *Tray icon and menu*).
- N2: Read-only: nothing is written to the installation, the registry, a store's files or the settings file. It
  searches only the stores' records and default folders, never a whole drive.
- N3: The settings file is UTF-8 JSON (E4); the key is matched ignoring case (E11); other keys are ignored, so the settings window (#43) can add
  its own; it is read up to 1 MiB (E4). Without the file the Companion behaves as today plus the automatic search.
- N4: Store records and the settings file are untrusted input, read into results with a kind and failing closed
  (record 0033's rule for external input); a damaged one never ends the Companion.
- N5: The log follows *Log* of [`companion.md`](../../docs/areas/companion.md): text from the settings file and the
  stores is sanitized and cut. The folder in the log may hold the Windows user name, like exception texts already do;
  the user guide's warning to check a log before sharing it covers it. No game content is logged.
- N6: The search runs once per start, in every mode, the invalid command line included.
- N7: `README.md` and `docs/USER_GUIDE.md` (English first, then German, in one file each) describe the search, its
  order, the settings file with an example (backslashes doubled in JSON), and every menu text above with what to do;
  the menu lists of `README.md` and the user guide (*Companion*, *First start*) gain the line. The user guide's
  privacy paragraph (*Data and logs*, English and German) and the list in *Log* of `companion.md` say that the log now
  holds the game folder at every start.
- N8: Every store record and default folder the search reads is built from a real sample, named in the plan's
  *Verified facts*, before the plan is written: Ubisoft Connect's install record (registry export), the name of its
  default game folder and the source of the version from the Product Manager's installation (Ubisoft Connect,
  `docs/USER_GUIDE.md`, *First recording, step by step*); Steam's library list and the game's app manifest from a Steam installation,
  anonymized, and Anno 117's Steam app id from its store page. A store without such a sample when the plan is
  written leaves AC1 (an amendment) and goes to a follow-up issue; a store built from a sample but never run with the
  real game is marked "Not yet tried" in the README and the user guide (record 0039).

## Acceptance criteria

- AC1: Without a game folder in the settings file, the Companion finds an installation recorded by Ubisoft Connect,
  one in any Steam library, or one in either store's default game folder, in the order Ubisoft Connect, Steam,
  default folders, taking the first valid installation within a store, and the menu shows `Game: <version>
  (<source>)` directly below `Last tick:`; the store readers' tests read the real samples (S1, S2, S3, S5, E6, N8).
- AC2: A `gameFolder` that names an installation is used instead of the automatic search, with the source `settings
  file`; the key matches ignoring case, and a byte order mark, comments and trailing commas are accepted (S4, N3,
  E11).
- AC3: A wrong `gameFolder` or settings file shows the menu text of its case, logs a warning naming the cause, and runs
  no automatic search (E1, E2, E3, E4, E5).
- AC4: A store record that cannot be read or points at no installation is skipped; when nothing is found the menu
  shows `Game: not found` and a warning names the sources checked (E7, E8, N4).
- AC5: The version of a found installation is shown; the same installation gives the same version text at every
  start, and a changed game version gives a changed text; an undeterminable version shows `Game: version unknown
  (<source>)`; the menu shows the version sanitized and cut to 64 characters (S1, E9, E12).
- AC6: Until the search ends the menu shows `Game: searching`, and the tray, the pipe connection and `Exit` work as
  before; the search runs once per start in every mode and nothing is written (S6, E10, N1, N2, N6).
- AC7: The log lines of the search are sanitized and cut by the Companion's log rules and hold no game content (N5).
- AC8: On the Product Manager's Windows machine with the real game installed through Ubisoft Connect, the Companion
  finds the installation without a settings file, and a wrong `gameFolder` shows its menu text (S1, E1).
- AC9: README and user guide describe the new behavior, English first, then German (N7).

## Out of scope

- The settings window (#43), and any way to write the settings file other than by hand.
- Noticing changes while the Companion runs, and searching again when the game starts (#25) (E10 only says what
  happens).
- Extracting the catalog and the cache keyed by the version (#23 to #25); this feature only provides the folder and
  the version to them.
- Other stores (Epic, Game Pass) and other systems than Windows.
- Showing the folder in the menu or opening it from there; the line `Game: …` in the diagnostics window.

## Open questions

- Where does the override live before the settings window exists? Answered: a JSON file, as the issue says, at
  `%LOCALAPPDATA%\Censio\settings.json` beside the fixed `Logs` and `Recordings` folders, which #43 can extend.
- Where does the player see the "clear message"? Answered: in the menu line `Game: …` with the detail in the log; no
  dialog, as for every other problem (`companion.md`, *Limits and errors*). The icon and tooltip stay the pipe's.
- Which installation wins when several are found, and does an invalid override fall back? Answered: settings file,
  then Ubisoft Connect, then Steam, then the default folders (S5); no fallback (E5). Ubisoft Connect may also record a Steam
  copy, which starts through it (`docs/USER_GUIDE.md`, the `/pipe` advice); either way one folder is used.
- Where does the game version come from? Answered: the spec fixes what the player sees and what #25 needs (AC5); the
  plan chooses the source from the real sample (N8). `3.0.1733841` (game-files.md, *Locations*) shows the form.
- On which store is AC8 checked? Answered: Ubisoft Connect, the store of the Product Manager's run recorded in
  `docs/USER_GUIDE.md` (*First recording, step by step*). Steam is not tried with the real game unless someone does; N8 says what follows.

## Area documents

- New `docs/areas/game-installation.md`: sources and order, what counts as an installation, the settings key and
  its rules, the version, the failure cases, read-only access; what #23 to #25 receive.
- `docs/areas/companion.md`: the menu line `Game: …` with all its texts in *Tray icon and menu*, a *Settings file*
  section, the new log lines in *Log*, and *Not here* updated.
- `docs/areas/game-files.md`: *Scope* and *Not here* point to `game-installation.md` instead of "#22".

## Challenge

Devil's Advocate, spec challenge (1 major, 6 minor):

- M1 (store formats unseen, the Product Manager's store): accepted. N8 requires and sources a real sample of each
  store record, default folder and the version source before the plan; a store without one leaves AC1 for a
  follow-up issue, one not run with the game is marked "Not yet tried". AC1's tests read the samples; AC8 names
  Ubisoft Connect. The samples are asked of the Product Manager by escalation now.
- m1 (case of the key): accepted (E11, N3, AC2).
- m2 (byte order mark, comments, duplicates): accepted; a duplicate key is invalid (E4, AC2, AC3).
- m3 (several installations in one store): accepted (S5, AC1).
- m4 (menu position, diagnostics window): accepted (S1, AC1, *Out of scope*).
- m5 (version in the menu): accepted (E12, AC5).
- m6 (privacy text): accepted (N7).
