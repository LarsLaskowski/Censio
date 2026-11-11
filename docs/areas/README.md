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
| [server](server.md) | The server's HTTP surface: listen address, start page, `/health`, plain HTTP, unknown paths, the version shown, the data directory and its data-protection key ring, the Docker image | Data ingest, release workflows, web theming |
| [companion](companion.md) | The Companion process on Windows: tray icon, tooltip, `Exit`, single instance per user session, connection state in the tooltip, menu (diagnostics window, recording toggle, last tick, game line, folder entries, start with Windows), icon looks, command line (`--record`, `--replay`, `--speed`), recordings and log folders, the optional autostart entry, the settings file, the log file, replay tooltips, platform, release ZIP | Pipe and game-file reading, the catalog, server connection |
| [pipe](pipe.md) | The statistics pipe as the Companion reads it: frame layout, the message types of protocol version 2, the 1 MiB limit, error kinds, skip and stop rules, the version gate, ticks and session events, the tick limits | The connection, recording and replay, anonymising recordings (see pipe-recording) |
| [pipe-connection](pipe-connection.md) | The Companion's connection to the game's pipe: discovery, opening at identification level, states, retry, the latest tick kept across connections, stop | The tooltip texts and icon looks |
| [pipe-recording](pipe-recording.md) | Recording the frames of a connection to a file and replaying it: what is recorded, file format version 1, the write queue and its failures, replay pacing and clock, line rules, replay problems, privacy, anonymising a recording with the contributor tool (command, what changes and is kept, refusals, fixtures) | The menu entries and switches |
| [game-files](game-files.md) | Reading the game's archives: the archive format version read (V2.2), block and entry kinds, bounds, failure kinds, read-only access shared with the game, where the game keeps its names, texts and icons, the installation survey | Finding the installation (see game-installation), the catalog and the icons (see game-catalog) |
| [game-installation](game-installation.md) | Finding the Anno 117 installation folder and its build: the sources and their order (settings file, Ubisoft Connect's records and default games folder), what counts as an installation, the build, the menu line and log lines, the failures | The settings file's format (see companion), the archives (see game-files), the catalog (see game-catalog) |
| [game-catalog](game-catalog.md) | Building the catalog of regions, goods, workforce and buildings from the asset, data set, text, template and property default files of `config.rda`: the files and nodes read, kinds, English and German names, region types, the problem of a GUID (unknown, incomplete, nameless, ambiguous), inherited assets, zero GUIDs, copies in other archives, the icons of goods and regions (path rule, archives and order, DDS/BC7 to PNG, bounds, reasons, failure kinds), the cache (key, checks at start and at each pipe connection, what is cached, file layout, bounds, validation, writes, pruning, failures), bounds, failure kinds, privacy | The archives (see game-files), the tray line, the diagnostics cells and icons, the cache folder and the log lines (see companion), the catalog format for the server (#26) |
<!-- project:end area-index -->
