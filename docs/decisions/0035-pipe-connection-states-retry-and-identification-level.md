# 0035: The pipe connection is a platform-neutral state machine over a Windows connector that checks the pipe exists first and connects at identification level

- **Status:** Proposed
- **Date:** 2026-11-01
- **Area:** pipe
- **Source:** Issue #11
- **Supersedes:** —

## Context

The Companion must find the game's pipe `\\.\pipe\anno117`, survive game restarts, report a pipe held by another
client as a status, and never hang on `Exit` (issue #11, `specs/feature-11-pipe-connection/spec.md` on the work
branch). The reference reader waits with `WaitNamedPipe(NMPWAIT_WAIT_FOREVER)` and can block without end. The
player must tell "game not running" from "pipe busy", but .NET's `NamedPipeClientStream.ConnectAsync(timeout)`
retries a missing pipe until its timeout and then throws the same `TimeoutException` as for a busy one
(`dotnet/runtime` `release/10.0`, `NamedPipeClientStream.cs`, `ConnectInternal`; `NamedPipeClientStream.Windows.cs`,
`TryConnect`). With its default impersonation level `None` it sets no `SECURITY_SQOS_PRESENT`, so the pipe's server
— the game, or any program that created the name first — may impersonate the Companion's user at
`SecurityImpersonation`, Windows' default for a named pipe server (Microsoft documentation,
`impersonating-a-named-pipe-client.md` and `impersonation-levels.md`). The logic must be tested on Linux (record [0004](0004-companion-shell-and-linux-build.md)).

## Options considered

1. **`NamedPipeClientStream`, preceded by a check that the pipe exists in the `\\.\pipe\` listing** (chosen) — a
   missing pipe is decided at once without opening anything; the whole entry is compared, since a pipe name may hold
   a backslash and `x\anno117` would otherwise match; a timed-out connect is busy when the pipe still
   exists. Managed code only. Con: a small race between the check and the connect, and the listing's behavior is
   confirmed only on Windows.
2. **P/Invoke `CreateFileW` and `WaitNamedPipeW`, as the reference** — exact Windows error codes, no race. Con:
   interop code in the untested shell, the handle wrapped by hand, analyzer and build settings for interop; the
   same Windows-only verification.
3. **`NamedPipeClientStream.ConnectAsync(timeout)` alone** — simplest. Rejected: a missing game shows as `Pipe busy`
   after 2 seconds (spec AC15).
4. **`File.Exists` on the pipe path as the check** — short. Rejected: querying a pipe path's attributes is reported to
   open the pipe and so take a server instance (not measured); the listing opens nothing.
5. **Impersonation level `Anonymous`** instead of `Identification` — the server learns nothing. Rejected for now: a
   server that calls `ImpersonateNamedPipeClient` fails against an anonymous client, and whether the game does is
   unknown; `Identification` already keeps the server from acting as the user.
6. **`PipeOptions.CurrentUserOnly`** — refuses a pipe whose owner is another user. Rejected: it would also refuse
   a game run as administrator, and a foreign pipe is already read within the reader's limits.
7. **Reset the retry delay on every connection that reached `Receiving`** — simpler. Rejected (spec challenge m5):
   a pipe that sends `Version 2` and then breaks would be retried every second forever. The reset needs an
   uninterrupted 30 seconds in `Receiving` up to the end of the connection.
8. **One tooltip update per status change** — simplest. Rejected (Security plan review): a pipe alternating
   `Version 2` and `Version 3` on one connection would queue updates without bound; at most one update is pending
   and it shows the latest status.

## Decision

Options 1 and `Identification`: the Windows shell's connector checks the pipe listing, then connects read-only and
asynchronously at impersonation level `Identification` with a 2-second timeout; the result is classified by a
platform-neutral rule, and a platform-neutral state machine on an injected clock decides the states, the retry
delays and their reset after 30 seconds in `Receiving`. The rules are in [`areas/pipe.md`](../areas/pipe.md).

## Consequences

- The impersonation level holds only for the local server name `.`: Windows ignores the flags on a remote
  connection, so the server name stays a constant.
- An unexpected exception of an attempt or a read ends it as an error and the connection retries; only the stop's
  own cancellation ends the loop.
- The states and delays are tested on Linux with a fake connector and stream; the connector itself is verified only
  by the manual test on Windows.
- A pipe that appears between the check and the connect is found by the next attempt; one that vanishes during the
  connect is reported as not found after the timeout.
- If the game turns out to impersonate its client, the level stays `Identification`; if a game update refuses it,
  the connection reports `Error` with the Windows code.
