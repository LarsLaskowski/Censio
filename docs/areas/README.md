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
| [companion](companion.md) | The Companion process on Windows: tray icon, tooltip, `Exit`, single instance per user session, connection state in the tooltip, menu (diagnostics window, recording toggle, last tick, folder entries, start with Windows), icon looks, command line (`--record`, `--replay`, `--speed`), recordings and log folders, the optional autostart entry, the log file, replay tooltips, platform, release ZIP | Pipe and game-file reading, server connection |
| [pipe](pipe.md) | The statistics pipe as the Companion reads it: frame layout, the message types of protocol version 2, the 1 MiB limit, error kinds, skip and stop rules, the version gate, ticks and session events, the tick limits | The connection, recording and replay, anonymising recordings (see pipe-recording) |
| [pipe-connection](pipe-connection.md) | The Companion's connection to the game's pipe: discovery, opening at identification level, states, retry, the latest tick kept across connections, stop | The tooltip texts and icon looks |
| [pipe-recording](pipe-recording.md) | Recording the frames of a connection to a file and replaying it: what is recorded, file format version 1, the write queue and its failures, replay pacing and clock, line rules, replay problems, privacy, anonymising a recording with the contributor tool (command, what changes and is kept, refusals, fixtures) | The menu entries and switches |
<!-- project:end area-index -->
