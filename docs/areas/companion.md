# Companion

## Scope

The Companion process on Windows: its tray icon, tooltip and menu, how it exits, how many instances run, the
version it shows, its command line (recording the pipe and replaying a recording), and the release ZIP it ships in. The user who starts the Companion relies on it.

## Behavior

### Tray icon and menu

- Started, the Companion shows one icon in the notification area with the tooltip `Censio Companion: <state>`, where `<state>` is the state of the pipe connection ([`pipe.md`](pipe.md)): `Waiting for game`, `Connected`, `Receiving`, `Pipe busy`, `Protocol mismatch` or `Error`. Before the first attempt it reads `Censio Companion: Waiting for game`. It opens
  no window and shows no taskbar button.
- The tooltip follows the state without any action by the player. At most one tooltip update waits for the
  tray at a time and it shows the latest state. The tooltip shows no cause.
- The tooltip ends with ` (recording)` while the Companion records and with ` (recording failed)` after the recording
  failed (*Recording*); a Companion started without `--record` shows neither.
- The context menu of the icon holds exactly three entries: `Version <version>` (greyed out, nothing happens on
  click), a separator and `Exit`.
- `<version>` is the informational version the Companion was built with: `0.0.0` without a version, otherwise
  the build property `Version` exactly as passed.
- `Exit` removes the icon at once and ends the process with exit code 0 within 2 seconds in every connection state;
  the tray never waits for a pending pipe call.
- When Windows Explorer restarts, the icon appears again once the notification area is back. Sign-out and
  shutdown end the Companion without a dialog.

### Command line

The Companion knows three switches; the case of a switch does not matter. A start without a switch behaves as
described in the other sections.

| Switch | Meaning |
| ------ | ------- |
| `--record` | Record every connection to the pipe to a file until `Exit` (*Recording*) |
| `--replay <file>` | Do not open the pipe; feed the recording `<file>` through the pipe's reader and tick assembler (*Replay*) |
| `--speed <value>` | With `--replay` only: `max` (no pauses) or a factor from `0.1` to `1000`, written with a decimal point (`.`) also on a German Windows; default `1` (real speed) |

- Any other command line is invalid: an unknown argument, a switch given twice, `--speed` without `--replay`, a speed
  that is not `max` or a factor in that range, `--replay` without a file or with an empty one or with a switch in
  the place of the file, or `--record` together with `--replay`. The tooltip reads `Censio Companion: Invalid command
  line`, the Companion neither records, replays nor opens the pipe, and the menu and `Exit` work as usual.
- The menu is the same with every switch: `Version <version>`, a separator and `Exit`.
- A start with a switch obeys *Single instance*: if a Companion already runs in the user session, the new one ends
  at once, so close the running one first.

### Recording

- With `--record` the Companion reads the pipe as usual (the states and their tooltips are unchanged) and writes the
  frames of each connection to its own file in `%LOCALAPPDATA%\Censio\Recordings`. The folder is fixed; no part of
  it comes from the pipe or the command line. The file format, the queue and the failure rules are in
  [`pipe.md`](pipe.md) (*Recording and replay*).
- The file of a connection is created at its first frame; no folder or file is created without `--record` or while no
  frame arrives. When the game quits and is started again, the next connection writes a new file.
- `Exit` ends the Companion as without `--record`, within 2 seconds with exit code 0. On `Exit` the frames still
  waiting to be written are written for at most 1 second. Whether Windows sign-out and shutdown do the same is not
  verified (*Limits and errors*).
- If the recording fails (the folder cannot be created, a write fails, the disk is too slow for the pipe), it stops
  for the rest of the run with the lines written so far complete, and the tooltip reads
  `Censio Companion: <state> (recording failed)` until `Exit`. The connection is not affected.
- Recordings are never deleted by the Companion; the user deletes them. A recording holds the savegame headline,
  island names and economic data as the game sent them and stays on the machine: it is never sent anywhere, and
  only anonymised fixtures may be shared publicly.

### Replay

- With `--replay <file>` the Companion reads the file instead of the pipe, at the recorded pace divided by the speed
  of `--speed`. The file is only read.
- The tooltip reads `Censio Companion: Replay: Connected` from the start until a replayed frame changes the state,
  then `Censio Companion: Replay: Receiving` or `Censio Companion: Replay: Protocol mismatch` by the same rules as the
  live connection ([`pipe.md`](pipe.md), *States*), and after the last frame `Censio Companion: Replay finished`
  until `Exit`. A replay never shows `Error`.
- A problem with the file ends the replay with `Censio Companion: Replay error: <reason>` after the frames before it
  were replayed. The reasons are `file not found`, `not a recording`, `unsupported version`, `invalid line <n>` and
  `read failed` ([`pipe.md`](pipe.md), *Replay problems*).
- The replayed ticks have no consumer yet: only the tooltip shows the progress.

### Single instance

- Only one Companion runs per Windows user session. A second start in the same session ends at once with exit
  code 0, without a message and without a second icon; the first keeps running.
- A start in another user session (another signed-in user) is not blocked and has its own icon.

### Platform

- The Companion runs on Windows x64. Every build writes `Censio.Companion.exe`, on Linux too; it runs only on
  Windows, and a local build needs the .NET 10 Desktop Runtime (the release ZIP below does not).
- Every release attaches the ZIP `Censio-<version>-win-x64.zip`. It holds exactly one file at its root, the
  self-contained single-file `Censio.Companion.exe` with the native libraries embedded, and needs no installed .NET,
  installer or administrator rights, on Windows 10 22H2 and Windows 11, x64. The exe is not signed, so SmartScreen
  asks once ("More info", "Run anyway"). Its tray menu shows `Version <version>`; the exe's file properties show no
  product version.

## Limits and errors

- The Companion shows no error message or dialog and logs nothing yet; a connection problem shows only as a tooltip
  state, a recording problem as `(recording failed)`, a replay problem as `Replay error`.
- Recording and replay cannot be started or stopped while the Companion runs, and there is no tray entry for them.
- The 1 second for writing the queue is given on `Exit`. That Windows sign-out or shutdown gives it too is not
  verified.

## Related decisions

- [`decisions/0004-companion-shell-and-linux-build.md`](../decisions/0004-companion-shell-and-linux-build.md) — logic in a platform-neutral project, the Windows shell builds on Linux.
- [`decisions/0005-product-version-from-one-build-property.md`](../decisions/0005-product-version-from-one-build-property.md) — the version comes from the build property `Version`.
- [`decisions/0012-release-pre-release-first-and-secret-free-builds.md`](../decisions/0012-release-pre-release-first-and-secret-free-builds.md) — the release ZIP is built on Linux without secrets and checked before it is attached.
- [`decisions/0027-companion-zip-self-contained-single-file-unsigned.md`](../decisions/0027-companion-zip-self-contained-single-file-unsigned.md) — self-contained single-file exe in a ZIP, not signed.
- [`decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md`](../decisions/0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md) — recordings hold the raw frames, written off the read path; a replay feeds them back on a recorded clock.

## Not here

- Opening and reading the statistics pipe, the connection states and the retry rules, the recording file format and the replay rules: see [`pipe.md`](pipe.md).
- Reading the game files, the connection to the server, settings and autostart (later issues).
