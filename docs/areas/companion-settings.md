# Companion settings

## Scope

The Companion's settings window: the menu entry that opens it, its fields and their defaults, the validation texts, how
the settings file is written (unknown keys kept, comments dropped, replaced as a whole, the check for a change from
outside), what applies at once and what by a restart, the restart itself and its failures, and the log lines of the
window. A player who sets up local mode or forward mode without editing a file relies on it. The file's keys and how
they are read are in [`companion.md`](companion.md) (*Settings file*); the texts of the window in both languages in
[`companion-texts.md`](companion-texts.md); the profile list and the notice in
[`companion-profiles.md`](companion-profiles.md).

## Behavior

### Opening

- The menu entry `Settings…` stands directly above `Open diagnostics` and is enabled in every mode (live, `--record`,
  replay, an invalid command line). It opens one window titled `Censio Companion settings`; a second click brings the
  open window to the front (a minimized one is restored) and opens no second window.
- The window reads the file and checks a typed game folder off the tray thread and appears when both are done; the
  tray, the pipe, the menu and `Exit` never wait for it, its write or its folder check. Closing the window with its
  close button or `Cancel` changes nothing.
- A notification of the tray icon that opens this window on a click is in [`companion-profiles.md`](companion-profiles.md)
  (*Notice*).

### Fields

The window shows, top to bottom: a banner (*Warnings*), `Mode` (`Off`, `Local`, `Forward`), `Local port`, `Server
address`, `Server token` (a masked box), `Buffer hours`, `Buffer megabytes`, `Game folder` with `Browse…` and the hint
`Empty: find automatically`, `Profile` (a drop-down with `New profile…`), `Start with Windows`, `Language` (`English`,
`Deutsch`), the outcome line, `Save` and `Cancel`; a message stands below the field it belongs to.

| Field | Shows | Default | Enabled |
| ----- | ----- | ------- | ------- |
| `Mode` | `mode` of the file | `Off` (no `mode`, or a blank one) | always |
| `Local port` | `localPort` | 5000 | with `Local` |
| `Server address` | `serverAddress` | empty | with `Forward` |
| `Server token` | `serverToken`, masked | empty (no token) | with `Forward` |
| `Buffer hours` | `bufferHours` | 24 | with `Local` and `Forward` |
| `Buffer megabytes` | `bufferMegabytes` | 100 | with `Local` and `Forward` |
| `Game folder` | `gameFolder` | empty: the automatic search ([`game-installation.md`](game-installation.md)) | always |
| `Profile` | the profiles of [`companion-profiles.md`](companion-profiles.md), the active one selected | `Live` | always |
| `Start with Windows` | the state the registry shows now ([`companion.md`](companion.md), *Start with Windows*) | off | always |
| `Language` | the language of the Companion's texts | the saved choice, else the Windows display language | always |

- A field that has a problem of the file (*Errors from the file*) is enabled in every mode, so that it can be edited
  away. A disabled field keeps its value and is written as shown.
- `Browse…` opens a folder dialog and puts the picked folder's full path in the game folder field; the field can be typed in too.
- `New profile…` shows a name box below the drop-down; saving creates the profile and selects it ([`companion-profiles.md`](companion-profiles.md), *Creating and selecting*).
- The window shows the file's values and, for a missing key, the default. A mode that is `Off` writes no `mode`.

### Validation

A message stands below its field, the typed values stay, and `Save` writes nothing while one is shown. Every field's
non-empty text is checked whatever the mode; only an empty address is a problem in `Forward` alone, and an empty port,
hours, megabytes, token or game folder is no problem (the default, no token, the automatic search).

| Field | Rule | Message (English) |
| ----- | ---- | ----------------- |
| `Local port` | a whole number from 1 to 65535 | `Enter a whole number from 1 to 65535.` |
| `Server address` | with `Forward`, not empty | `Enter the server address.` |
| `Server address` | an absolute `ws://` or `wss://` address with a host, without user name, password or fragment ([`replay-sending.md`](replay-sending.md), *Switches*) | `Enter a ws:// or wss:// address with a host, without user name, password or fragment.` |
| `Server token` | after trimming 32 to 1,024 visible ASCII characters (`!` to `~`), a non-ASCII character included in the rule ([`ingest-token.md`](ingest-token.md), *Token*) | `The token must be 32 to 1024 visible ASCII characters (! to ~).` |
| `Buffer hours` | a whole number from 1 to 168 | `Enter a whole number from 1 to 168.` |
| `Buffer megabytes` | a whole number from 1 to 10240 | `Enter a whole number from 1 to 10240.` |
| `Game folder` | a full path | `Enter a full path, such as C:\Games\Anno 117.` |
| `Game folder` | an existing folder | `This folder does not exist.` |
| `Game folder` | an installation by the search's check ([`game-installation.md`](game-installation.md), *What counts as an installation*) | `This folder is not an Anno 117 installation.` |
| new profile name | not blank | `Enter a name.` |
| new profile name | at most 64 characters | `Use at most 64 characters.` |
| new profile name | not equal to another profile's name ignoring case | `A profile of this name exists.` |
| new profile name | well-formed text without a control character | `The name holds characters that cannot be used.` |
| new profile name | fewer than 1,000 profiles | `No more profiles can be created.` |

- A whole number is written as decimal digits only (`5`, `05`); `5.0`, `"5"`, a sign and white space are refused.
- The folder checks run on `Save` and when the window opens, off the tray thread; a typed UNC folder is probed over the
  network as written, as the search does ([`game-installation.md`](game-installation.md), *Folders from outside*).
- The address, token and number rules are those the readers apply ([`local-mode.md`](local-mode.md) and
  [`live-sending.md`](live-sending.md), *Switching on*).

### Errors from the file

- A file that cannot be parsed (larger than 1 MiB, not JSON, not an object, unreadable, holding a name or a text value that is not valid UTF-8,
  also under an unknown key) shows the defaults and the banner `The settings file could
  not be read (<reason>). Saving replaces it with the values shown.`; the reason is the one the log gets. `Save` replaces
  such a file; this is the only time a file the Companion cannot read is replaced.
- A key with a wrong value, or written twice (also in another letter case), of any mode (`"localPort": "abc"`,
  `"gameFolder": 5`, `"mode": "remote"`, two `mode`): its field is empty, with the message of its rule, `Choose a mode.`
  for a `mode` that is not a text or a text other than empty, `local` or `forward`, or `This setting is written more than
  once in the file.`; the other fields show the file's values. `Save` stays blocked until the player edits that field.
- The key `language` is no such key: a wrong or repeated value counts as not set, as the Companion's start reads it
  ([`companion-texts.md`](companion-texts.md), *Language*).

### Writing the file

- The window writes `%LOCALAPPDATA%\Censio\settings.json` new and replaces the old file by a rename: it writes
  `settings.json.tmp` in the same folder, flushes it to disk and moves it over the file, so a crash leaves the old or the
  new file. It writes UTF-8 without a byte order mark, indented.
- Keys, once each and in this order and spelling: `mode` (left out for `Off`), `localPort`, `serverAddress` (left out
  when empty), `serverToken` (left out when empty, trimmed), `bufferHours`, `bufferMegabytes`, `gameFolder` (left out
  when empty), `language` (`en` or `de`, only when the file had a valid one or the player changed the field), then the
  unknown keys with their values as read. Numbers are written as the parsed whole number (`05` as `5`). Every other
  spelling of a known key (`MODE`, an escaped `"mode"`) is left out, so the file holds each known key once.
- Switching the mode keeps the other keys: switching from `Forward` back to `Local` keeps `serverAddress` and `serverToken`
  in the file, and `Off` is written by leaving `mode` out, so a switch back needs no new entry.
- Unknown keys stay with their values; comments, the key order and the spelling of numbers of a hand-edited file are not kept.
- A file written by the window is read back by the Companion's reader as saved; a hand-edited file keeps working
  ([`companion.md`](companion.md), *Settings file*).
- Before writing, the window compares the file with the bytes it read when it opened (their SHA-256); a file changed
  since, or created since for a missing one, writes nothing and shows `The settings file was changed outside this
  window. Close the window and open it again.`
- A file that cannot be written (access denied, a locked file, a full disk) writes nothing, deletes the `.tmp` file,
  leaves the old file whole, restarts nothing and shows `The settings could not be saved. Details are in the log.`; the
  log gets the exception's type only.
- The file holds the ingest token in plain text (*Privacy*). The replaced file takes the access rules its folder passes
  on, so rules set on `settings.json` alone are lost at a save. A crash between writing and replacing leaves
  `settings.json.tmp`, which holds the token, in the same folder until the next save overwrites it.

### What applies when

- At once, in the running Companion: the profile ([`companion-profiles.md`](companion-profiles.md)), `Start with
  Windows` (switched as the menu entry does, only when the box differs from the registry's state) and the language
  ([`companion-texts.md`](companion-texts.md)), also while the window stays open.
- By a restart: a changed mode, port, address, token, buffer limit or game folder, compared with the values the running
  Companion started with (not with the file as it was when the window opened), whole numbers and the trimmed token
  compared as the Companion reads them (`05` equals `5`, a blank port equals 5000). A save without such a change restarts nothing.
- A saved change that needs a restart shows `Saved. The Companion restarts to apply the changes.`; a save that needs none
  closes the window without a text.
- `Start with Windows` that cannot be written is logged as the menu entry logs it ([`companion-log.md`](companion-log.md))
  and shows `Start with Windows could not be changed.`; the other values are saved and the window stays open.

### Warnings

The banner at the top of the window shows, one line each and in this order, only what applies when the window opens:
the file problem (*Errors from the file*), `The profiles file could not be read. Saving a profile replaces it.`
([`companion-profiles.md`](companion-profiles.md)), `Saving ends the running replay.`, `Saving ends the running
recording.` and, while the live buffer is memory only ([`live-sending.md`](live-sending.md), *Buffer*), `Messages that
could not be saved to disk are lost.` The last three are true for a save that restarts the Companion: the restarted
Companion neither records nor replays, and a memory-only buffer is lost.

### Restart

- The running Companion starts its own exe (the full path it runs from, no shell) with the one argument `--restarted`
  and then exits as `Exit` does. The exit ends the single instance only after the hosted server stopped and the profiles
  file was written; if one of them did not finish within its wait ([`companion.md`](companion.md), *Tray icon and
  menu*, `Exit`), the single instance is left to the process's end.
- A Companion started with `--restarted` waits up to 10 seconds for the single instance of the old one; every other
  second start ends at once ([`companion.md`](companion.md), *Single instance*). In local mode it also retries a port that is in use every 500 ms
  for the same 10 seconds, with the menu line `Server: starting`, and shows `Server: port <port> in use` only after that
  time ([`local-mode.md`](local-mode.md), *Limits and errors*).
- A restarted Companion starts as without a switch: it neither records nor replays. `--restarted` is accepted alone only.
  Started by hand while the old Companion runs, it waits for the old one and then ends like any second start.
- If the exe cannot be started (moved or deleted, a start refused), the old Companion keeps running with its old values,
  the file holds the saved values and the window shows `The Companion could not restart. Exit and start it again to
  apply the changes.`; the log gets `Restart failed: the Companion could not be started: <exception type>`.
- A restarted Companion whose 10 seconds run out ends without an icon and logs `Restart failed: the previous Companion
  did not end`; the player starts it again.

### Log

`Settings saved` (Information) after a save, `Could not save the settings: <exception type>` (Warning) for a write that
failed, `Profile switched` (Information) when the active profile changed, `Language setting invalid; the Windows
display language is used` (Warning, at start) and the two restart lines above (Warning). None holds a value, a name, a
path or the token ([`companion-log.md`](companion-log.md)).

### Privacy

- The token is shown masked, is cleared from the box when the window closes, is held by no generated text form of the
  window's values and is in no log line, notification or message.
- The window writes the token only into the settings file, as before a plain text in the player's private folder; the
  file is not shared.

## Limits and errors

- The settings file: at most 1 MiB, judged on its raw bytes ([`companion.md`](companion.md), *Settings file*).
- A refused save shows the messages of *Validation*, `The settings file was changed outside this window…`, `The settings
  could not be saved…` or `The Companion could not restart…`; no message holds a value, a path or an exception text.
- A second `Save` while one runs is ignored.

## Related decisions

- [`decisions/0110-settings-window-writes-the-settings-file-keeping-unknown-keys.md`](../decisions/0110-settings-window-writes-the-settings-file-keeping-unknown-keys.md) — the window writes the file, keeps unknown keys, drops comments, replaces it as a whole.
- [`decisions/0112-connection-settings-applied-by-a-restart-that-waits-for-the-old-companion.md`](../decisions/0112-connection-settings-applied-by-a-restart-that-waits-for-the-old-companion.md) — connection and game settings take effect by a restart that waits for the old Companion.
- [`decisions/0113-companion-texts-in-english-and-german-from-one-table.md`](../decisions/0113-companion-texts-in-english-and-german-from-one-table.md) — the window's texts come in English and German.
- [`decisions/0045-companion-settings-in-a-json-file-under-local-app-data.md`](../decisions/0045-companion-settings-in-a-json-file-under-local-app-data.md) — the file's place and reading rules (writing superseded by 0110).

## Not here

- The keys' reading rules and the file's size limit: [`companion.md`](companion.md) (*Settings file*); local mode's and forward mode's rules for each key: [`local-mode.md`](local-mode.md), [`live-sending.md`](live-sending.md).
- Profiles and the notice: [`companion-profiles.md`](companion-profiles.md); every text in both languages: [`companion-texts.md`](companion-texts.md).
- The hosted server and its port: [`local-mode.md`](local-mode.md).
