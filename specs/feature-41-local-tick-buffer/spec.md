# Spec: Bounded local tick buffer and gaps

Source: Issue #41
Status: Draft

## Problem

A player whose server runs elsewhere (forward mode, [`live-sending.md`](../../docs/areas/live-sending.md)) loses data
whenever the server is out of reach for longer than a short while: today the waiting messages are held in memory only,
at most 64 messages or 16 MiB (`LiveSender.MaxWaitingMessages`, `MaxWaitingBytes`), so a NAS restart of a few hours, a
night with the server off, or a Companion restart or `Exit` during an outage leaves a gap in the history. Record
[0031](../../docs/decisions/0031-bounded-local-tick-buffer-gaps-accepted.md) and the guarantee in `.squad/project.md`
promise a persistent buffer bounded by age and size, delivered in order, with the dropped rest shown as a gap, never as
zeros. External testers need the behavior described in `README.md` and `docs/USER_GUIDE.md` to try it.

## Scenarios

The buffer holds every message live sending waits to send: ticks, trade-routes messages and session events (today's
waiting messages, `live-sending.md`, *What is sent*); `status` messages stay unbuffered.

- S1: Given forward mode and a running game, when the server is unreachable for eight hours (more than 64 messages and
  more than 16 MiB of written JSON, today's memory bound) and then returns, then every message taken in that time is sent after the next `helloAccepted`, oldest first, before the messages taken after it,
  and the pages show the eight hours without a break. Meanwhile the greyed menu line `Buffer: <n> waiting` counts the
  messages that wait, and it reads `Buffer: 0 waiting` once all are acknowledged.
- S2: Given messages waiting during an outage, when the player chooses `Exit` (or the PC restarts) and starts the
  Companion later, then the waiting messages are sent after the next `helloAccepted`, in order, before any message the
  new run takes, and the log reads `Live buffer holds <n> messages from an earlier run`.
- S3: Given an outage longer than the age limit (default 24 hours), then every waiting message older than the limit is
  removed, oldest first, never sent and counted: the menu line reads `Buffer: <n> waiting, <d> dropped`, the log
  `Live messages dropped: <d>` (today's line and timing), and the history holds no values for the dropped span, which the
  charts draw as a break, never as zeros.
- S4: Given an outage in which the waiting messages reach the size limit (default 100 MB) before the age limit, then the
  oldest are removed and counted as in S3 until the rest fits.
- S5: Given `"bufferHours": 48` and `"bufferMegabytes": 500` in `settings.json`, when the Companion starts, then the
  limits are 48 hours and 500 MB; without the keys they are 24 and 100.
- S6: Given local mode and a hosted server that did not start (`Server: port <port> in use`), then the messages taken
  wait in the buffer, and the next start with a running hosted server sends them as in S2.
- S7: Given `Server: token refused` or `Server: does not support contract version 2`, then the Companion keeps taking
  messages into the buffer within its limits until `Exit`, and the next start, with the setting or the server fixed,
  sends them as in S2.
- S8: Given a start without live sending (no `mode`, an invalid server setting, `--replay`, an invalid command line),
  then the buffer is neither read, sent, changed nor removed, and the menu has no `Buffer:` line.

## Errors and edge cases

- E1: `bufferHours` or `bufferMegabytes` that is not an integer literal in its range (`24.0`, `2.4e1`, `"24"`, `null`,
  0, a negative number; hours 1 to 168, megabytes 1 to 10,240) or occurs more than once (also in different case) is an
  invalid server setting: `Server: settings invalid`, nothing sent, and the log names one of `bufferHours is not a whole
  number from 1 to 168`, `bufferHours occurs more than once`, `bufferMegabytes is not a whole number from 1 to 10240`,
  `bufferMegabytes occurs more than once` (`live-sending.md`, *Switching on*). The keys are read with `mode` `forward`
  and `local`.
- E2: The buffer folder cannot be created or a message cannot be written (disk full, access denied, a file of that
  name): what is already on disk is still sent first, the messages taken afterwards wait in memory for the rest of the
  run within today's bound (64 messages, 16 MiB; oldest dropped and counted), the menu line gets `, not saved to disk`
  appended, and `Live buffer cannot be written; waiting messages are kept in memory until Exit` is logged once.
- E3: A buffer that is partly unreadable at start (a torn end after a power cut, a file changed by hand or written by
  another release): the messages before the first unreadable part are sent in order, the rest is discarded, and
  `Live buffer partly unreadable; the rest was discarded` is logged. The discarded part is not in the dropped count,
  because a torn part has no countable messages: a gap only the log reports, an accepted risk of `live-buffer.md`.
- E4: A single message larger than the size limit: the newest message always stays, as today, and is sent.
- E5: The age is the time since the Companion took the message, by the PC's clock: a clock set back keeps messages
  longer, a clock set forward drops them earlier.
- E6: A dropped span shorter than 15 minutes between two kept ticks is drawn joined, as any pause of that length
  ([`web-charts.md`](../../docs/areas/web-charts.md), *Points, gaps and hourly means*); the dropped count still shows it.
- E7: After a long outage the backlog is sent one message at a time before the new messages; until it is through, the
  pages show older data (their `As of …` line moves forward), and a waiting message that passes the age limit while
  it waits is dropped and counted.
- E8: The message in flight at `Exit`, at a crash or at a lost connection is sent again with the same identity; a
  server that stored it acknowledges it without storing it twice ([`ingest.md`](../../docs/areas/ingest.md)). A message
  acknowledged with a refusal leaves the buffer and is not sent again.
- E9: The buffer is sent to the server of the start that reads it: after a change of `serverAddress`, or from
  `forward` to `local`, the waiting messages go to the new server under the profile `Live`.
- E10: Another Windows user has a buffer of their own; removing the Companion keeps the buffer folder.

## Non-functional requirements

- N1: Defaults 24 hours and 100 MB (1 MB = 1,048,576 bytes, counted as the messages' written JSON); whichever is
  reached first removes the oldest messages.
- N2: Disk use: the buffer's files stay within the size limit plus the newest message plus a bookkeeping overhead the
  plan states.
- N3: Memory does not grow with the buffer: at most 16 MiB of waiting messages plus the newest message and the message
  in flight are in memory at once, whatever the buffer holds.
- N4: Reading the buffer at start does not delay the tray icon, the menu or the pipe, and the menu never waits for the
  disk.
- N5: A message is in the buffer before it is first sent; `Exit` and a crash of the process lose no message written
  to it. `Exit` keeps its bounds (2 seconds, 5 in local mode; `companion.md`).
- N6: Privacy and security: the buffer lies in the fixed folder `%LOCALAPPDATA%\Censio\Buffer`, like the recordings,
  logs and cache folders, never in a path from the settings file or from received data; a new record of this change
  supersedes the path consequence of the released record 0031 ("a path from configuration") with this fixed folder; it holds the messages as sent (island names, the economy, trade
  routes), never the token; no log line holds a message's content. Reading it back is external input: read into typed
  results, failing closed (record 0033).
- N7: Compatibility: the contract (version 2) and the server are unchanged; a server of release 0.4.0 receives the
  buffer; a settings file without the new keys keeps working.

## Acceptance criteria

- AC1: Messages taken while no connection is accepted, within both limits, are sent after the next `helloAccepted`
  in the order taken, before messages taken later, each acknowledged message once; more than 64 messages and more than
  16,777,216 bytes of written JSON are buffered this way and all sent oldest first, none dropped while both configured
  limits hold (S1, S6, E7).
- AC2: Messages waiting at `Exit` or a process crash are read at the next start with live sending and sent in order
  before that run's messages, the message in flight with its identity; `Live buffer holds <n> messages from an earlier
  run` is logged for n > 0 (S2, E8, E9, N5).
- AC3: No message older than `bufferHours` is sent; such messages are removed oldest first and counted (S3, E5, E7).
- AC4: The waiting messages' written JSON never exceeds `bufferMegabytes` × 1,048,576 bytes except by the newest
  message, which always stays; the oldest are removed and counted first; the buffer's files stay within N2 (S4, E4, N1,
  N2).
- AC5: Where live sending runs, the menu shows below the `Server:` line `Buffer: <n> waiting`, with `, <d> dropped`
  appended once this run dropped messages and `, not saved to disk` after a failed write; drops are logged as
  `Live messages dropped: <n>` (S1, S3).
- AC6: `bufferHours` and `bufferMegabytes` follow E1, defaulting to 24 and 100 (S5, E1).
- AC7: A dropped tick leaves no stored values, and the charts break where more than 15 minutes pass, never drawing
  zeros (S3, E6).
- AC8: A buffer that cannot be written, or is partly unreadable, behaves as E2 and E3, and live sending goes on
  (E2, E3, N6).
- AC9: After a final refusal the buffer keeps taking messages within its limits, and a start without live sending
  leaves the buffer untouched and shows no `Buffer:` line (S7, S8, E10).
- AC10: Memory and start behave as N3 and N4 with a full buffer of the default size (N3, N4, N7).
- AC11: `README.md` and `docs/USER_GUIDE.md`, English then German, describe the buffer, its keys and defaults, the
  menu line and the log lines so that a tester can try S1 to S3 (N1, N6).

## Out of scope

- A gap marker message or a server-side display of dropped spans (`contract.md`: silence is the gap, #41 needs no
  message).
- Buffering for a replay with `--server` ([`replay-sending.md`](../../docs/areas/replay-sending.md), *No buffer*).
- A menu entry to open or clear the buffer; a per-server or per-profile buffer (#43 chooses profiles).
- Surviving a power cut beyond E3: the messages taken in the last moments may be lost.

## Open questions

- Is the buffer for ticks only? Answered: no — it replaces today's in-memory waiting messages (`LiveSender._queue`
  holds ticks, trade-routes messages and session events; record 0104: "#41 replaces the bound by its persistent
  buffer"); a dropped trade-routes or session-event message counts as a dropped message, as the log line does today.
- How are the limits configured? Answered: as keys of the settings file, like `serverAddress` (record 0103); the
  ranges cap the age at the 7 days the history keeps ticks at full resolution (record 0021).
- How does the server show the gap? Answered: by the absence of the ticks: the history keeps no value for them
  (`history.md`) and the charts break after 15 minutes (record 0088); `contract.md` already states that #41 needs no
  message for it. The server needs no change; late ticks in order are stored as any tick (`history.md`).
- Does local mode use the buffer? Answered: yes — live sending in local mode is the same sender and the same profile
  (`live-sending.md`, *Local mode*).

## Area documents

- New `docs/areas/live-buffer.md`: what is buffered, the folder, the limits and their keys, order and resend, drops and
  their count, the menu line, the log lines, the failure cases E2 and E3, privacy and the accepted risks (among them a
  torn buffer's gap that only the log reports).
- `live-sending.md`: *Waiting messages* and the accepted risk *Memory only* replaced by a pointer to `live-buffer.md`;
  *Switching on* gains the two keys and their problem texts.
- `companion.md`: the `Buffer:` menu line, the two keys under *Settings file*, the folder under *Folders*.
- `companion-log.md`: the new log lines. `docs/areas/README.md`: the index row of `live-buffer.md` and the rows that
  name "#41".

## Challenge

- Major 1 (N6 cites record 0031 for a fixed folder it does not state): accepted. The folder stays fixed, as every
  folder the Companion writes (`companion.md`, *Folders*): a path from the settings file would add a parsed path to a
  security area for no user need. Record 0031 is released (in `v0.4.0`), so the plan writes a new record that supersedes
  its path consequence; N6 now says so instead of citing 0031.
- Major 2 (no criterion fails on a build that persists only today's queue): accepted. S1 is an outage of eight hours,
  beyond 64 messages and 16 MiB, and AC1 requires more than 64 messages and more than 16,777,216 bytes to be buffered
  and all sent oldest first, none dropped while both configured limits hold.
- Minor 3 (N3 cannot hold for a newest message over 16 MiB): accepted. N3 now bounds memory by 16 MiB plus the newest
  message and the message in flight.
- Minor 4 (E3's discarded part not in the tray count): accepted as the second option. A torn or foreign part has no
  countable messages, so a count would be invented; E3 states the gap only the log reports, and `live-buffer.md` lists
  it among its accepted risks.
