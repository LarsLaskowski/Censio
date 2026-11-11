# Companion log

## Scope

The Companion's log file: where it lies, its daily files, size limit and pruning, the line format, what is logged
(the catalog, cache and icon lines included) and what is never logged, how texts are sanitized and cut, and the
background writer with its bounds. A user who checks a log before sharing it, and the operator who reads it, rely on it.

## Behavior


- The Companion writes a log to `%LOCALAPPDATA%\Censio\Logs\companion-<yyyyMMdd>.log` (the local date). The folder
  is fixed. Information and above is logged; there is no setting.
- A file that would pass 10 MiB (10,485,760 bytes) with the next line is continued in
  `companion-<yyyyMMdd>-<n>.log` with `n` from 2 up to 9999 (beyond that lines are dropped); a new local day starts a
  new file, and a Companion started again on the same day continues the day's highest-numbered file. When a new file
  is created, only the 10 newest log files are kept, newest by the date and then `n` in the name. Only top-level files
  whose whole name is exactly `companion-<yyyyMMdd>.log` or `companion-<yyyyMMdd>-<n>.log` (`n` from 2, no leading
  zero, ASCII digits only, case-insensitive) are ever deleted, other files in the folder never; a symbolic link is
  deleted as a link, not its target.
- A line is `<yyyy-MM-ddTHH:mm:ss.fffzzz> <level> <message>`: local time with UTC offset, the level `INF`, `WRN`, `ERR`
  or `CRT`, and the message; an exception follows on the lines after it, each indented by two spaces.
- It logs: the start with the version and the mode (`Normal`, `Record`, `Replay`, `Invalid`); each change of the
  connection state with the error kind, the Windows error code and the received version (only changes, never a line
  per skipped frame); recording started and stopped at the click, its file name (the name only) when the file is
  created, a recording failure and the failure of a stopped recording; the result of the game installation search at every start (found with its source, build and folder, or the warning of its failure; [`game-installation.md`](game-installation.md), *Log*), where the folder and the settings file path are logged as written and may hold the Windows user name; the catalog (below); a replay's start (the file name only), its
  end and its problem reason; a folder that could not be opened; the autostart turned on (`Autostart enabled`) or off (`Autostart disabled`) and, as warnings, `Could not read the autostart setting` (once until a read succeeds again, so a blocked registry does not log at every menu opening) and `Could not change the autostart setting`, with the exception where there is one and never the exe path; an unexpected error of a background task; and `Exit`.
- **Catalog lines** ([`game-catalog.md`](game-catalog.md)), none with a name or text, only GUIDs, kinds, archive paths and
  counts: `Catalog built: <n> entries; <i> inherited assets left out; <d> unknown region types dropped`
  (Information); `Catalog failed: <kind>` (Warning; the kinds `ArchiveUnreadable`, `FileMissing`, `FileUnreadable`,
  `MalformedFile`); for each catalog file that another archive also lists, `Catalog: <archive> also lists <path>; the
  copy in config.rda is used` (Warning), and `Catalog: <c> other archives could not be checked for copies` (Warning,
  only when `c` is above 0); and for every closed tick once a catalog is published, `Catalog cannot name <role> <GUID>:
  <problem>` (Information) with the role `Region` (the session GUID), `Good`, `Workforce` or `Building` and the problem
  `Unknown`, `Incomplete`, `Nameless` or `Ambiguous`. Each (role, GUID) is logged once per published catalog, GUID `0` never, live or
  replayed, whether or not the diagnostics window is open; after 1,000 such lines one line, `Catalog: more than 1000
  GUIDs cannot be named; further ones are not logged for this catalog`, ends them. A catalog that replaces another (a
  re-extraction after a game update) starts its own set and its own limit. Ticks closed before a catalog is published
  are not checked, except the latest one at that moment, which is checked unless no connection or replay has started
  yet.
- **Cache lines** ([`game-catalog.md`](game-catalog.md), *Cache*), none with a name or an image:
  `Catalog cache not used: <missing|game changed|Companion changed>` (Information, at the start before the extraction);
  `Catalog cache not used: invalid` and `Catalog check failed; the current catalog is kept` (Warnings);
  `Catalog cache not written` (Warning, with the exception, which may hold the cache path and so the Windows user name);
  `Catalog extracted in <t> s (names <a> s, icons <b> s)` (Information, after the icon line of the extraction, also when the
  icons failed, not when the catalog failed); and `Catalog loaded from cache in <t> s: <n> entries, <g> goods and <r> regions
  with an icon` (Information; the only catalog line of a cache load). Durations have one decimal and a decimal point. A
  check that finds the game unchanged writes nothing.
- **Icon lines** ([`game-catalog.md`](game-catalog.md), *Icons*), written after the catalog line of the same extraction, none
  with a name, an entry path or image data: `Catalog icons: <g> goods and <r> regions with an icon; without one: <a> not
  named, <m> missing, <u> unreadable` (Information; the counts are of entries) and `Catalog icons failed: <kind>
  (<archive>)` (Warning; the kinds `ArchiveMissing`, `ArchiveUnreadable`, `DuplicateArchiveNumber` and `IconSetTooLarge`,
  the archive as a path relative to `maindata`, `maindata` itself when the listing failed). One of them is written per extraction, and
  none while the catalog fails, no game is found or the catalog is loaded from the cache.
- The log holds no pipe content (figure name, island names, values): recording files and folders are logged by name only.
- Text that came from outside (a replay file name, exception texts) is sanitized: each control character, format
  character, line or paragraph separator and unpaired surrogate becomes one `?`, so a text cannot add a line or hide
  characters. A message is cut at 2,048 characters and an exception text at 16,384, each with `…` appended when cut.
- **Exception texts are logged as given and may contain the profile path, and so the Windows user name.** A user
  checks a log for it before sharing it.
- The writer works in the background: a line waits in a queue of at most 1,048,576 characters (counted per line) and a
  line that does not fit is dropped. If the log folder or file cannot be created or written, or the folder is not a
  full path, the lines are lost and nothing else changes, with no message. At `Exit` the queue gets 0.5 second.
- A second Companion in the same session ends at once and writes no log line.

## Limits and errors

- A log that cannot be written changes nothing else and shows no message (see the writer above).

## Related decisions

- [`decisions/0037-companion-log-through-the-logging-abstraction-to-own-rotating-file.md`](../decisions/0037-companion-log-through-the-logging-abstraction-to-own-rotating-file.md) — the Companion logs through .NET's logging abstraction into its own rotating text file.
- [`decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md`](../decisions/0048-catalog-names-shown-in-the-diagnostics-window-unresolved-guids-logged-once.md) — names in the diagnostics window, unresolved GUIDs logged once.

## Not here

- The menu entry `Open log folder` and the other folders: [`companion.md`](companion.md) (*Folders*).
- The tray icon, the menu, the settings file and the other Companion behavior: [`companion.md`](companion.md).
- The diagnostics window: [`companion-diagnostics.md`](companion-diagnostics.md).
- The catalog, its cache and icons that the lines report on: [`game-catalog.md`](game-catalog.md).
