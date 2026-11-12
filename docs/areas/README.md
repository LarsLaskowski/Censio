# Areas

What the software does, area by area. Each area has one document here that states the behavior a user, an
operator or another component can rely on — formats, limits, error behavior, guarantees. The
[decision records](../decisions/README.md) explain *why* individual choices were made; the area documents say
*what holds today*. `ARCHITECTURE.md` shows how the parts fit together and links the areas instead of repeating them.

## Rules

- **One document per area**, `docs/areas/<slug>.md`, created from [`_template.md`](_template.md) and listed in the
  index below. The squad keeps it in step with the code (the Lead plans the update, the Dev writes it against the code as
  built): a change in behavior updates the area document in the same pull request.
- **Normative and stable.** Write what must hold, not how one language or library does it: the test is whether
  the document would still be true if the component were rewritten in another language. No class, package or
  function names as a contract (mention them only as a pointer to where it is implemented).
- **One home per behavior.** A cross-cutting topic (security, offline behavior) lives in the area that owns it; the
  others link there.
- **Size.** One readable document per area: split one that grows past `AREA_MAX_WORDS` words
  (`.squad/tools/squad_settings.py`; `decision-check.py` warns and names the split) into narrower areas — a Lead
  decision like any new area (below) — and fold one that says only a few sentences into a neighbor. Development and
  release process are not areas (see `CONTRIBUTING.md`).
- **A new area is a Lead decision** made in the plan (`plan.md`, *Areas*), with its name and scope; the Dev writes
  the document and its index row with the plan's documentation updates.
- Every decision record names its area in its `Area:` field (`—` for a record about the process). Area documents are
  not frozen by a release; their history is Git.

## Index

<!-- project:begin area-index -->
| Area | Scope | Not here |
| ---- | ----- | -------- |
| [server](server.md) | The server's HTTP surface: listen address, start page, `/health`, plain HTTP, unknown paths, the version shown, the data directory with its data-protection key ring and the place of the ingest token file, the Docker image | The ingest (see ingest), release workflows, web theming |
| [companion](companion.md) | The Companion process on Windows: tray icon, tooltip, `Exit`, single instance per user session, connection state in the tooltip, menu (diagnostics window, recording toggle, last tick, game line, folder entries, start with Windows), icon looks, command line (`--record`, `--replay`, `--speed`), recordings and log folders, the optional autostart entry, the settings file, replay tooltips, platform, release ZIP | Pipe and game-file reading, the catalog, server connection, the log file (see companion-log), the diagnostics window (see companion-diagnostics) |
| [companion-log](companion-log.md) | The Companion's log file: path and daily files, size limit and continuation, pruning, line format, what is logged (catalog, cache and icon lines included), what is never logged, sanitizing and length cuts, exception texts, the background writer and its bounds | The log folder entry (see companion), the catalog and its lines' meaning (see game-catalog) |
| [companion-diagnostics](companion-diagnostics.md) | The diagnostics window: header, table, numbers, catalog names, icons, area names, update, kept tick, single window, read path, memory, privacy | The menu entry (see companion), building the catalog and icons (see game-catalog) |
| [pipe](pipe.md) | The statistics pipe as the Companion reads it: frame layout, the message types of protocol version 2, the 1 MiB limit, error kinds, skip and stop rules, the version gate, ticks and session events, the tick limits | The connection, recording and replay, anonymising recordings (see pipe-recording) |
| [pipe-connection](pipe-connection.md) | The Companion's connection to the game's pipe: discovery, opening at identification level, states, retry, the latest tick kept across connections, stop | The tooltip texts and icon looks |
| [pipe-recording](pipe-recording.md) | Recording the frames of a connection to a file and replaying it: what is recorded, file format version 1, the write queue and its failures, replay pacing and clock, line rules, replay problems, privacy, anonymising a recording with the contributor tool (command, what changes and is kept, refusals, fixtures) | The menu entries and switches |
| [game-files](game-files.md) | Reading the game's archives: the archive format version read (V2.2), block and entry kinds, bounds, failure kinds, read-only access shared with the game, where the game keeps its names, texts and icons, the installation survey | Finding the installation (see game-installation), the catalog (see game-catalog), the icons (see game-catalog-icons) |
| [game-installation](game-installation.md) | Finding the Anno 117 installation folder and its build: the sources and their order (settings file, Ubisoft Connect's records and default games folder), what counts as an installation, the build, the menu line and log lines, the failures | The settings file's format (see companion), the archives (see game-files), the catalog (see game-catalog) |
| [game-catalog](game-catalog.md) | Building the catalog of regions, goods, workforce and buildings from the asset, data set, text, template and property default files of `config.rda`: the files and nodes read, kinds, English and German names, region types, the problem of a GUID (unknown, incomplete, nameless, ambiguous), inherited assets, zero GUIDs, copies in other archives, bounds, failure kinds, privacy | The archives (see game-files), the icons (see game-catalog-icons), the cache (see game-catalog-cache), the tray line, the diagnostics cells and icons (see companion-diagnostics), the cache folder (see companion) and the log lines (see companion-log), the catalog format for the server (see game-catalog-format) |
| [game-catalog-icons](game-catalog-icons.md) | The icons of the goods and regions of the catalog: which entries get one, the path rule from the asset's `IconFilename` to the archive entry, the archives and their order, DDS/BC7 to PNG, the reasons for a missing icon, the failure kinds, the bounds, the test with the real files, privacy | The archives (see game-files), the diagnostics cells (see companion-diagnostics) and the log lines (see companion-log), the cache (see game-catalog-cache) |
| [game-catalog-cache](game-catalog-cache.md) | The cache of the catalog and its icons: the key (Companion identity and game state), the check at start and at each pipe connection, what is cached, the file layout, bounds, validation, writes and pruning, privacy | The cache folder (see companion) and the log lines (see companion-log), the build (see game-installation), the catalog format for the server (see game-catalog-format) |
| [game-catalog-format](game-catalog-format.md) | The versioned catalog document for the server: properties, value rules, the reader's five checks in order, the size and count bounds, refusal kinds and texts, compatibility rules, privacy | Building the catalog (see game-catalog), the icons (see game-catalog-icons), the cache (see game-catalog-cache), sending and storing it (#42) |
| [contract](contract.md) | The JSON contract between Companion and server: messages, versions and the frozen hello, identities, the reader's checks, bounds, refusal texts, privacy | Sending, receiving and the endpoint (#28, #40), the catalog document itself (see game-catalog-format) |
| [ingest](ingest.md) | The Companion's WebSocket ingest on the server: the endpoint and its HTTP refusals, the hello and its checks in order, the token (file, generation, rotation, switch, comparison), one Companion and silent connections, acknowledgements and closes, bounds and times, log lines, privacy, the measured keep-alive and upload memory | The contract's messages and reader (see contract), storing and showing data (#30, #32), the server's HTTP surface (see server) |
<!-- project:end area-index -->
