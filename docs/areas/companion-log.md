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
  end and its problem reason; for a replay sent to a server the four lines of [`replay-sending.md`](replay-sending.md) (*Log*): `Replay connected to the server`, `Replay refused by the server: <reason>`, `Replay sending failed: <reason>` and `Replay sent <n> ticks, <r> refused`, never with the token, the address, a name or an exception text; a folder that could not be opened; the lines of local mode and of live sending (below); the autostart turned on (`Autostart enabled`) or off (`Autostart disabled`) and, as warnings, `Could not read the autostart setting` (once until a read succeeds again, so a blocked registry does not log at every window opening) and `Could not change the autostart setting`, with the exception where there is one and never the exe path; an unexpected error of a background task; and `Exit`.
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
- **Cache lines** ([`game-catalog-cache.md`](game-catalog-cache.md)), none with a name or an image:
  `Catalog cache not used: <missing|game changed|Companion changed>` (Information, at the start before the extraction);
  `Catalog cache not used: invalid` and `Catalog check failed; the current catalog is kept` (Warnings);
  `Catalog cache not written` (Warning, with the exception, which may hold the cache path and so the Windows user name);
  `Catalog extracted in <t> s (names <a> s, icons <b> s)` (Information, after the icon line of the extraction, also when the
  icons failed, not when the catalog failed); and `Catalog loaded from cache in <t> s: <n> entries, <g> goods and <r> regions
  with an icon` (Information; the only catalog line of a cache load). Durations have one decimal and a decimal point. A
  check that finds the game unchanged writes nothing.
- **Icon lines** ([`game-catalog-icons.md`](game-catalog-icons.md)), written after the catalog line of the same extraction, none
  with a name, an entry path or image data: `Catalog icons: <g> goods and <r> regions with an icon; without one: <a> not
  named, <m> missing, <u> unreadable` (Information; the counts are of entries) and `Catalog icons failed: <kind>
  (<archive>)` (Warning; the kinds `ArchiveMissing`, `ArchiveUnreadable`, `DuplicateArchiveNumber` and `IconSetTooLarge`,
  the archive as a path relative to `maindata`, `maindata` itself when the listing failed). One of them is written per extraction, and
  none while the catalog fails, no game is found or the catalog is loaded from the cache.
- **Settings, profile and restart lines** ([`companion-settings.md`](companion-settings.md), [`companion-profiles.md`](companion-profiles.md)),
  none with a value, a name, a path or the token: `Settings saved` (Information, after the settings window wrote the
  file), `Could not save the settings: <exception type>` (Warning, for a failed write of the settings file or of the
  profiles file; only the type's name), `Profile switched` (Information, when the active profile changed),
  `Profiles file invalid: <problem>` (Warning; the problem is `cannot be read`, `larger than 1 MiB` or `not of its
  format`), `Language setting invalid; the Windows display language is used` (Warning, at start) and, for a restart,
  `Restart failed: the Companion could not be started: <exception type>` and `Restart failed: the previous Companion did
  not end` (Warnings). The figure name and its hash are never logged. The log stays English when the Companion's texts
  are German ([`companion-texts.md`](companion-texts.md)).
- **Local mode lines** ([`local-mode.md`](local-mode.md), *Log*): `Server settings invalid: <problem>` (Warning; the
  problem is one of `mode is not local or forward`, `mode occurs more than once`, `localPort is not a port from 1 to 65535`,
  `localPort occurs more than once`, or a problem of forward mode, [`live-sending.md`](live-sending.md), *Switching on*), `Local server running on port <port>` (Information), `Local server port <port> in
  use` (Warning), `Local server failed` (Error, with the exception), `Local server stopped unexpectedly` (Error) and `Could
  not open the browser` (Warning, with the exception). Of the hosted server itself, lines of level Warning and above are
  written, under the rules of this document (sanitized and cut), among them the `Critical` lines of its start checks and
  `The ingest token file could not be read back; the server stops.`; lower levels are not. The ingest token is never
  logged.
- **Live sending lines** ([`live-sending.md`](live-sending.md), *Log*), written when the state changes and never per retry,
  none with the token, the address, a name or an exception text: `Live sending connected to the server` (Information),
  `Live sending failed: <reason>; retrying` (Warning, when the reason differs from the last one logged since the last
  accepted hello), `Live sending refused by the server: <reason>; not retried` (Warning), `Server refused a live message:
  <kind>` (Warning), `Live message not sent: the contract writer refused it` (Warning), `Live messages dropped: <n>`
  (Warning), `Live buffer holds <n> messages from an earlier run` (Information), `Live buffer cannot be written; waiting
  messages are kept in memory until Exit` (Warning), `Live buffer entries unreadable: <n>; discarded` (Warning) and the five catalog lines `Catalog sent to the server: <i> icons
  sent, <r> refused` (Information; `<i>` the icons the server accepted, `<r>` those it or the contract writer refused),
  `Catalog already on the server` (Information), `Server refused the catalog: <kind>` (Warning), `Catalog not sent: the
  contract writer refused it` (Warning) and `Catalog not sent: the connection failed 3 times during its upload` (Warning),
  none with a name, an icon reference or a path; no
  line holds a path or a message's content; the reasons are those of [`replay-sending.md`](replay-sending.md) (*Limits and errors*).
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
- The hosted server and the meaning of its lines: [`local-mode.md`](local-mode.md), [`server.md`](server.md); the meaning of the live sending lines: [`live-sending.md`](live-sending.md).
- The catalog, its icons and its cache that the lines report on: [`game-catalog.md`](game-catalog.md), [`game-catalog-icons.md`](game-catalog-icons.md), [`game-catalog-cache.md`](game-catalog-cache.md).
