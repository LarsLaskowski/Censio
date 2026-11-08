# Spec: Locate the game installation

Source: Issue #22
Status: Draft

## Problem

The catalog of names and icons (#23 to #26) is built from the game's archives below `maindata` of the installation
folder ([`game-files.md`](../../docs/areas/game-files.md)), but nothing finds that folder today: only the survey test
reads it, from the environment variable `CENSIO_ANNO_INSTALL`. A player should not have to look up and type the
folder, and the cache of #25 needs the game version to tell an updated game from an unchanged one. When the folder
cannot be found or a typed folder is wrong, the player must see that and why; today the Companion shows no error
message at all ([`companion.md`](../../docs/areas/companion.md), *Limits and errors*).

## Scenarios

- S1: Given Anno 117 installed through Ubisoft Connect and no game folder in the settings file, when the Companion
  starts, then the tray menu shows `Game: <version> (Ubisoft Connect)`, for example `Game: 3.0.1733841 (Ubisoft
  Connect)`, and the log has an information line with the source, the version and the folder.
- S2: Given the game installed through Steam in any of Steam's library folders, not recorded by Ubisoft Connect, and no
  game folder in the settings file, when the Companion starts, then the menu shows `Game: <version> (Steam)`.
- S3: Given the game in the default game folder of Ubisoft Connect or Steam while that store has no record of it (for
  example after Windows was reinstalled), when the Companion starts, then it is found there, with that store as the
  source.
- S4: Given the settings file `%LOCALAPPDATA%\Censio\settings.json` with `"gameFolder"` naming an installation, when the
  Companion starts, then that folder is used without any automatic search and the menu shows `Game: <version>
  (settings file)`.
- S5: Given installations found through both stores and no game folder in the settings file, when the Companion starts,
  then the one Ubisoft Connect records is used; the player picks another one through the settings file.
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
- E4: The settings file is not valid JSON, `gameFolder` is not a text, the file is larger than 1 MiB, or it cannot be
  read (locked, access denied): `Game: settings file invalid`.
- E5: In E1 to E4 the Companion does not fall back to the automatic search: the player meant the settings file, and a
  silently found other folder would hide the mistake.
- E6: No settings file, an empty or blank `gameFolder`, or no `gameFolder` key: the automatic search runs (S1 to S3).
- E7: A store record that cannot be read, is damaged, or points at a folder that is not an installation (a leftover of
  an uninstalled game) is skipped and the next source is tried.
- E8: Nothing is found: `Game: not found`; the warning names the sources that were checked (not their folders).
- E9: An installation is found but its version cannot be determined: `Game: version unknown (<source>)`; the
  installation is still used.
- E10: The settings file is edited, the game installed or moved while the Companion runs: nothing changes until the
  Companion is started again.

## Non-functional requirements

- N1: The search never blocks the tray: the icon, the menu and the pipe connection work while it runs, and `Exit`
  keeps its bound of 2 seconds ([`companion.md`](../../docs/areas/companion.md), *Tray icon and menu*).
- N2: Read-only: the Companion writes nothing into the installation, the registry or a store's files, and neither
  creates nor writes the settings file. It searches only the stores' records and default folders, never a whole drive.
- N3: The settings file is UTF-8 JSON; keys other than `gameFolder` are ignored, so the settings window (#43) can add
  its own; it is read up to 1 MiB (E4). Without the file the Companion behaves as today plus the automatic search.
- N4: Store records and the settings file are untrusted input, read into results with a kind and failing closed
  (record 0033's rule for external input); a damaged one never ends the Companion.
- N5: The log follows *Log* of [`companion.md`](../../docs/areas/companion.md): text from the settings file and the
  stores is sanitized and cut. The folder in the log may hold the Windows user name, like exception texts already do;
  the user guide's warning to check a log before sharing it covers it. No game content is logged.
- N6: The search runs once at each start, in every mode (live, `--record`, replay and an invalid command line).
- N7: `README.md` and `docs/USER_GUIDE.md` (English first, then German, in one file each) describe the search, its
  order, the settings file with an example (backslashes doubled in JSON), and every menu text above with what to do.

## Acceptance criteria

- AC1: Without a game folder in the settings file, the Companion finds an installation recorded by Ubisoft Connect,
  one in any Steam library, or one in either store's default game folder, in the order Ubisoft Connect, Steam,
  default folders, and the menu shows `Game: <version> (<source>)` (S1, S2, S3, S5, E6).
- AC2: A `gameFolder` that names an installation is used instead of the automatic search, with the source `settings
  file` (S4, N3).
- AC3: A wrong `gameFolder` or settings file shows the menu text of its case, logs a warning naming the cause, and runs
  no automatic search (E1, E2, E3, E4, E5).
- AC4: A store record that cannot be read or points at no installation is skipped; when nothing is found the menu
  shows `Game: not found` and a warning names the sources checked (E7, E8, N4).
- AC5: The version of a found installation is shown; the same installation gives the same version text at every
  start, and a changed game version gives a changed text; an undeterminable version shows `Game: version unknown
  (<source>)` (S1, E9).
- AC6: Until the search ends the menu shows `Game: searching`, and the tray, the pipe connection and `Exit` work as
  before; the search runs once per start in every mode and nothing is written (S6, E10, N1, N2, N6).
- AC7: The log lines of the search are sanitized and cut by the Companion's log rules and hold no game content (N5).
- AC8: On the Product Manager's Windows machine with the real game, the Companion finds the installation without a
  settings file, and a wrong `gameFolder` shows its menu text (S1 or S2, E1).
- AC9: README and user guide describe the new behavior, English first, then German (N7).

## Out of scope

- The settings window (#43), and any way to write the settings file other than by hand.
- Noticing changes while the Companion runs, and searching again when the game starts (#25) (E10 only says what
  happens).
- Extracting the catalog and the cache keyed by the version (#23 to #25); this feature only provides the folder and
  the version to them.
- Other stores (Epic, Game Pass) and other systems than Windows.
- Showing the folder in the menu or opening it from there.

## Open questions

- Where does the manual path override live before the settings window exists? Answered: in a JSON file, as the issue
  says ("config file"); the Lead places it at `%LOCALAPPDATA%\Censio\settings.json` beside the fixed `Logs` and
  `Recordings` folders ([`companion.md`](../../docs/areas/companion.md), *Folders*), under the key `gameFolder`, so #43
  can extend the same file.
- Where does the player see the "clear message", given that the Companion shows no dialog? Answered: in a greyed-out
  menu line `Game: …` beside `Last tick:`, with the detail in the log; no dialog or notification, as
  [`companion.md`](../../docs/areas/companion.md) (*Limits and errors*) holds for every other problem. The icon and the
  tooltip stay the pipe's.
- Which installation wins when several are found, and does an invalid override fall back? Answered: settings file,
  then Ubisoft Connect, then Steam, then the default folders (S5); no fallback (E5). Steam copies start through Ubisoft
  Connect (`docs/USER_GUIDE.md`, the `/pipe` advice), so Ubisoft Connect may record a Steam installation too; either
  way one folder is used.
- Where does the game version come from? Answered: the spec fixes only what the player sees and what #25 needs (AC5);
  the source (the program file, a store record) is chosen in the plan from a real installation, under the plan's rule
  for readers of an external format. The version `3.0.1733841` the Product Manager reported (game-files.md,
  *Locations*) is the example of the expected form.
- On which store is AC8 checked? Answered: on the Product Manager's installation, whose store is not recorded
  (game-files.md, *Locations*); the other store is covered by unit tests only and stated as not tried in the user
  guide until someone tries it.

## Area documents

- New `docs/areas/game-installation.md`: the sources and their order, what counts as an installation (`maindata\config.rda`),
  the settings file key and its rules, the version and when it is unknown, the failure cases and that nothing is
  written; what #23 to #25 receive.
- `docs/areas/companion.md`: the menu line `Game: …` with all its texts in *Tray icon and menu*, a *Settings file*
  section (path, format, size bound), the new log lines in *Log*, and *Not here* no longer listing settings and game
  files as later issues for the search.
- `docs/areas/game-files.md`: *Scope* and *Not here* point to `game-installation.md` instead of "#22".
