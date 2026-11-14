# 0079: Recordings are sent to a server by switches of the Companion's replay, one message at a time, without reconnect

- **Status:** Proposed
- **Date:** 2026-11-14
- **Area:** replay-sending
- **Source:** Issue #34
- **Supersedes:** —

## Context

Testers want to fill a server without the game, through the real contract (#34), before forward mode (#40) exists.
The issue allows a command-line tool or a Companion developer option. Record
[0015](0015-two-deliverables-and-local-mode.md) ships two deliverables, the Companion ZIP and the server image; the
Companion already replays a recording through the live reader and tick assembler on a recorded clock
([0036](0036-pipe-recording-raw-frames-and-replay-on-a-recorded-clock.md)). The server pings a silent connection and
aborts it when no pong comes ([`ingest.md`](../areas/ingest.md)), and .NET answers a ping only inside a pending
receive. This is the Companion's first outbound connection.

## Options considered

1. **Companion switches `--server` and `--token-file` beside `--replay`** — ships in the ZIP, no .NET SDK for testers,
   reuses the replay, its pacing and its tray states; Windows only.
2. **A separate console tool like `Censio.Anonymiser`** — runs on Linux and in scripts; testers need the SDK and a
   checkout, or it becomes a third released deliverable against record 0015.
3. **A server-side import of a recording file** — no client at all; bypasses the WebSocket, the hello, the token and
   the acknowledgements, which are what the issue wants exercised, and puts a pipe reader into the server against
   record [0018](0018-pipe-parsed-in-companion-versioned-json-contract.md).
4. **Reconnect and resend as in #40** — survives a server restart; duplicates #40's buffer and backoff design before
   it is made.

## Decision

Option 1 without option 4. The replay sends each tick and session event only after the previous one is
acknowledged, keeps a receive pending from the accepted hello to the close, sends under a new profile per run with
recorded receive times, and ends with an error instead of reconnecting. The rules are in
[`replay-sending.md`](../areas/replay-sending.md).

## Consequences

- The client code (connection, hello, acknowledgements) lives in `Censio.Companion.Core` and can be reused by #40.
- One message in flight bounds memory and lets a slow server slow the replay; it costs one round trip per message.
- Every run adds a profile `Replay <time>`; deleting it waits for #35.
- A recording leaves the machine as contract messages when the user asks for it; the privacy text of
  [`pipe-recording.md`](../areas/pipe-recording.md) says so.
