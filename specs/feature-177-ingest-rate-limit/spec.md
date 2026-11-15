# Spec: Limit ingest connections per host

Source: Issue #177
Status: Draft

## Problem

The ingest endpoint bounds connections waiting for their hello only in total (16, record 0064), and a refused hello
writes a warning line each time. Any one host that reaches the port can therefore keep the real Companion out by
refilling the 16 places, and a client that reconnects in a loop with a wrong token or an old contract version grows the
server's log without bound; Docker's default `json-file` log driver does not rotate it. Both are accepted risks today
(`docs/areas/ingest.md`, *Accepted risks*; `SECURITY.md`; the user guide's *Limits* item). The operator's only remedy is
restricting who reaches the port.

Claims of the issue checked against the code (base `origin/main`):

- Confirmed: the bound is global (`IngestConnections.MaxPendingHellos`, `503` in `IngestEndpoint.HandleAsync`).
- Refuted in part: a place is held until the hello is decided, at most 10 seconds (`IngestSession.RunHelloPhaseAsync`
  and `OnHelloTimeout` free it before the close), not 10 plus the 2-second close wait. The connection itself lasts up to
  2 seconds longer outside the count, so a host that sends bad hellos at once can hold any number of connections in
  their close wait, each with its 32,769-byte buffer: a related defect this feature bounds per host (E3).
- Confirmed in part: a refused hello with a reply writes one warning line; one closed without a reply writes none.
- Confirmed: the server's sends use no timeout of their own (`IngestSession.SendAsync`, `CancellationToken.None`). The
  keep-alive measurements in `ingest.md` cover peers that send nothing, not a peer that sends but never reads.
- Confirmed: the server reads no forwarded header (no forwarded-headers middleware in `src/`); the address of a
  connection is the TCP peer's.

## Scenarios

- S1: Given a server reachable by the Companion's host and by a hostile host, when the hostile host keeps opening
  connections that never send a hello, then it holds at most 4 of the 16 places, and the Companion's next hello is
  accepted.
- S2: Given a host that sends hellos with a wrong token in a loop, when its 10th hello within 60 seconds fails, then the
  server logs `A host was refused 10 times within 60 seconds; its connections are refused for 60 seconds.` once, answers
  every upgrade from that host `429` for 60 seconds without a log line, and at the end of the block logs
  `A host's block ended; <n> connections were refused while it lasted.`
- S3: Given a blocked host, when its block has ended and it sends a hello with the right token, then the hello is
  accepted as today.
- S4: Given a Companion whose hello was accepted, when it stops reading what the server sends (acknowledgements and
  pings), then the server ends its connection at most 60 seconds after the last frame it read from it, logs
  `Companion disconnected; <a> messages accepted, <r> refused.`, and the next hello is accepted.
- S5: Given a server behind a reverse proxy, when connections arrive through the proxy, then all of them count as one
  host, whatever forwarded headers they carry.

## Errors and edge cases

- E1: What counts as a failed hello: every connection that got a place and whose hello was not accepted — refused
  `unsupportedVersion` or `tokenRefused`, closed without a reply (unreadable, binary, over 32,768 bytes, no hello, not
  complete within 10 seconds), or ended by the peer before its hello. A refusal `companionConnected` does not count (it
  needs the token, and the real Companion meets it while its earlier connection is still being stored); neither does a
  hello whose profile could not be stored (a server fault).
- E2: An accepted hello sets the host's count of failed hellos back to zero; a block already running is not shortened.
- E3: Upgrades refused before a place is taken (`400`, `403`, `429`, `503`) are not failed hellos; only refused upgrades
  of a blocked host are counted, for the end line of S2. A block starts with the failure that reaches 10 and is not
  extended by the upgrades refused during it; after it the count starts at zero.
- E4: The order of the HTTP refusals: not an upgrade `400`; an `Origin` header `403`; a blocked host `429`; a host with 4
  connections waiting for their hello `429`; 16 connections waiting `503`. All have an empty body; none is logged.
- E5: A connection of a host that becomes blocked while it waits for its hello keeps its place and is decided as today;
  the block applies to new upgrades.
- E6: Host is the remote IP address: an IPv4 address, or an IPv4-mapped IPv6 address as its IPv4 address, is one host;
  IPv6 addresses sharing their first 64 bits are one host. A connection without a remote address (a test host, a Unix
  socket) belongs to one shared host.
- E7: Where every connection arrives from one address (a reverse proxy, a Docker setup that does not keep the client's
  address), all clients are one host: a client through it that fails 10 hellos blocks the real Companion behind the
  same address for 60 seconds, and 4 waiting connections take all its places.
- E8: A restart forgets every count and block.
- E9: The refusal lines of today are written unchanged for a host until it is blocked; during a block none is written,
  since no hello is read.

## Non-functional requirements

- N1: Values: 4 connections waiting for their hello per host (the total of 16 stays); 10 failed hellos within 60 seconds
  start a block of 60 seconds; an accepted Companion that reads nothing loses its place within 60 seconds of the last
  frame read from it. No new setting: the values are fixed like the bounds of today.
- N2: Memory: the server tracks at most 1,024 hosts at a time; a host is forgotten 60 seconds after its last failed hello
  or the end of its block, whichever is later. While 1,024 are tracked, every further host shares one entry, so a
  flood of addresses blocks new hosts together rather than escaping the limits.
- N3: Times come from the server's injected clock, so blocks and windows are testable on a fake clock.
- N4: Privacy: no line names a remote address or a host; the rule of `ingest.md`, *Log lines*, stays. The new lines are
  `Warning` (block start) and `Information` (block end), category `Censio.Server.Ingest`.
- N5: Compatibility: a Companion with the right token that connects one connection at a time sees no change. The
  Companion's replay reports a `429` as it reports any HTTP answer, `server unreachable` (`replay-sending.md`).
- N6: Residual log volume: per host at most 10 refusal lines and 2 block lines in each block cycle; many hosts can still
  grow the log, which stays an accepted risk with Docker log rotation (`--log-opt max-size`) as the operator's remedy.

## Acceptance criteria

- AC1: A host with 4 connections waiting for their hello is answered `429` with an empty body on a further upgrade, while
  another host's upgrade is accepted; the total bound of 16 with `503` is unchanged (S1, E4, E6, N1, N5).
- AC2: The refusals are decided in the order of E4, and none writes a log line (E4).
- AC3: The 10th failed hello of a host within 60 seconds blocks the host for 60 seconds: every upgrade from it is answered
  `429` with an empty body, the block-start line of S2 is logged once, and after 60 seconds the host's upgrade is
  accepted again (S2, S3, E3, E5, E8, N1, N3).
- AC4: Exactly the cases of E1 count as failed hellos: `companionConnected` and a failed profile store do not, and an
  accepted hello sets the count back to zero without shortening a running block (E1, E2).
- AC5: At the end of a block the line `A host's block ended; <n> connections were refused while it lasted.` is logged
  with the number of its upgrades refused during the block, `0` included; no new line names an address or host (S2, E3,
  E9, N4).
- AC6: Hosts are told apart as E6 states: two IPv4 addresses, two IPv6 addresses differing within the first 64 bits, and
  a connection without remote address are separate hosts; an IPv4-mapped IPv6 address and its IPv4 address, and two IPv6
  addresses of one `/64`, are the same host. Forwarded headers change nothing (S5, E6, E7).
- AC7: With 1,024 hosts tracked, a further host shares one entry with every other untracked host, and a host is forgotten
  60 seconds after its last failed hello or block end (N2).
- AC8: An accepted Companion that reads nothing — idle, or sending while the server's replies stay unread — has its
  connection ended at most 60 seconds after the last frame the server read from it, `Companion disconnected; …` is
  logged and a new hello is accepted (S4, N1).

## Out of scope

- Trusting forwarded headers (`X-Forwarded-For`, `Forwarded`) from a configured proxy: S5 and E7 hold instead; a
  follow-up issue may add it.
- Logging the remote address of a refused or blocked host.
- A setting for any value of N1; a `Retry-After` header on `429`.
- A bound across hosts on log lines (N6), and the Companion's reaction to `429` in live sending (#40).

## Open questions

- Behind a reverse proxy, are forwarded headers trusted, and from whom? Answered: no, from no one (S5, E7). The server
  reads none today; a header any client can set would let a client escape its limits or get another host blocked, so it
  could only be trusted from proxies the operator lists — configuration to get wrong, which record 0064 rejected for
  origins on the same ground. Behind a proxy the limits act on the proxy as one host, which keeps today's lock-out risk
  for that deployment and no worse; the operator's remedy there is the proxy's own limits, or letting the Companion
  connect past the proxy.
- Does the keep-alive bound a Companion that never reads? Answered: the requirement is AC8, whatever delivers it. The
  sends have no timeout (`IngestSession.SendAsync`), and whether .NET's keep-alive aborts while a send is blocked is
  library behavior no measurement in `ingest.md` covers; the plan names that measurement on the real server, and adds a
  bound of its own if the keep-alive does not meet AC8.
- Which failures count, which values hold, and how are refusals summarised without addresses? Answered: E1 to E3, N1
  and N4, decided by the Lead.

## Area documents

- `docs/areas/ingest.md`: *Route and HTTP refusals* with the `429` cases and the order of E4; a subsection *Limits per
  host* (what a host is, failed hellos, the block, the tracked hosts, forwarded headers not trusted); the values in
  *Bounds and times*; the two lines in *Log lines*; the no-read bound beside the keep-alive and its measurement in
  *Measurements*; *Accepted risks* rewritten: lock-out and log volume now need many hosts, and a proxy makes all clients
  one host.
- `SECURITY.md` and the user guide's ingest *Limits* item, English and German: the limits per host, the residual risks,
  Docker log rotation.
- Record 0064 (unreleased, not in `v0.2.0`) or a new record links to `ingest.md` for the values; which one is the plan's.
