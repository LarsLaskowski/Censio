# Spec: Limit ingest connections per host

Source: Issue #177
Status: Draft

## Problem

The ingest endpoint bounds connections waiting for their hello only in total (16, record 0064), and each refused hello
writes a warning line. One host can keep the real Companion out by refilling the 16 places, and a client reconnecting in
a loop with a wrong token or an old contract version grows the log without bound (Docker's default `json-file` driver
does not rotate). Both are accepted risks today (`docs/areas/ingest.md`, *Accepted risks*); the operator's only remedy is
restricting who reaches the port.

The issue's claims against `origin/main`: the bound is global (`IngestEndpoint.HandleAsync`, `503`) and the sends have
no timeout (`IngestSession.SendAsync`) — confirmed; a place is held 10 plus 2 seconds — refuted: it is freed when the
hello is decided, at most 10 seconds (`RunHelloPhaseAsync`, `OnHelloTimeout`). The close wait then runs outside the
count, so a host that sends bad hellos at once can hold any number of connections in it, each with a 32,769-byte buffer:
a related defect the block per host bounds (E3). Only a refused hello with a reply writes a line.

## Scenarios

- S1: Given the Companion's host and a hostile host, when the hostile host keeps opening connections that never send a
  hello, then it holds at most 4 of the 16 places and the Companion's next hello is accepted.
- S2: Given a host that sends hellos with a wrong token in a loop, when its 10th failed hello within 60 seconds arrives,
  then the server logs `A host was refused 10 times within 60 seconds; its connections are refused for 60 seconds.`,
  answers every upgrade from that host `429` for 60 seconds, and then logs
  `A host's block ended; <n> connections were refused while it lasted.`
- S3: Given a block that has ended, when the host sends a hello with the right token, then it is accepted.
- S4: Given an accepted Companion, when it stops reading (acknowledgements and pings), then the server ends its
  connection at most 60 seconds after the last frame it read from it, logs `Companion disconnected; …`, and the next
  hello is accepted.
- S5: Given a reverse proxy in front of the server, then every connection through it counts as one host, whatever
  forwarded headers it carries, also with `ASPNETCORE_FORWARDEDHEADERS_ENABLED` `true`.

## Errors and edge cases

- E1: A failed hello is a connection that got a place and whose hello was not accepted: refused `unsupportedVersion` or
  `tokenRefused`, closed without a reply (*The hello*, step 1 and 3, the 10-second timeout), or ended by the peer first.
  Not counted: `companionConnected` (it needs the token, and the real Companion meets it while its earlier connection is
  still stored) and a profile that could not be stored (a server fault).
- E2: An accepted hello sets the host's count to zero; a running block is not shortened.
- E3: The window slides: a host is blocked when a failed hello makes 10 of its failed hellos lie within the last 60
  seconds. Refused upgrades are no failed hellos and do not extend a block; those of a blocked host are counted for the
  end line. After a block the count starts at zero.
- E4: Order of the HTTP refusals: not an upgrade `400`; an `Origin` header `403`; a blocked host `429`; a host with 4
  connections waiting `429`; 16 waiting `503`. Empty bodies; none is logged.
- E5: A connection already waiting when its host is blocked is decided as today.
- E6: A host is the TCP peer's IP address: IPv4, an IPv4-mapped IPv6 address as its IPv4 address, every full IPv6
  address on its own; connections without an address (a test host, a Unix socket) share one host. The address the
  forwarded-headers middleware substitutes (`ASPNETCORE_FORWARDEDHEADERS_ENABLED` `true` trusts any client's
  `X-Forwarded-For`) is not used.
- E7: Where every client arrives from one address (a reverse proxy, a Docker setup that does not keep the client's
  address) all are one host: one client's 10 failures block the Companion behind it for 60 seconds. A device that picks
  many IPv6 addresses of its `/64` escapes the limits per host; only the total of 16 holds against it, as today.
- E8: A restart forgets every count and block.
- E9: The refusal lines of today stay; a blocked host writes none, as no hello is read.

## Non-functional requirements

- N1: Fixed values, no setting: 4 waiting connections per host (total 16 stays); 10 failed hellos within 60 seconds
  block for 60 seconds; an accepted Companion that reads nothing is ended within 60 seconds of its last frame read.
- N2: At most 1,024 hosts are tracked; a host is forgotten 60 seconds after its last failed hello or block end, never
  while a connection of it waits (it counts then). Beyond 1,024, further hosts share one entry, so a flood blocks new
  hosts together rather than escaping. The host of the last accepted hello keeps an entry of its own outside the 1,024
  until another host's hello is accepted; a Companion on a new address (an IPv6 temporary address) may be refused `429`
  during a flood, an accepted risk.
- N3: Times come from the injected clock.
- N4: No line names an address or a host (`ingest.md`, *Log lines*). The block lines are `Warning` (start) and
  `Information` (end), category `Censio.Server.Ingest`.
- N5: A Companion with the right token, one connection at a time, sees no change; the replay reports `429` as
  `server unreachable` (`replay-sending.md`).
- N6: Per host at most 12 lines in any 60 seconds; a host below the threshold writes up to 9 for as long as it goes on.
  Many hosts can still grow the log: an accepted risk, Docker log rotation (`--log-opt max-size`) the remedy.

## Acceptance criteria

- AC1: A host with 4 connections waiting gets `429` with an empty body on a further upgrade while another host's upgrade
  is accepted; the total of 16 with `503` is unchanged (S1, E4, N1, N5).
- AC2: The refusals are decided in the order of E4, and none writes a log line (E4).
- AC3: The 10th failed hello of a host within 60 seconds blocks it for 60 seconds: every upgrade from it gets `429` with
  an empty body, the start line of S2 is logged once, and afterwards its upgrade is accepted; 10 failed hellos spread
  over more than 60 seconds block nothing (S2, S3, E3, E5, E8, N1, N3).
- AC4: Exactly the cases of E1 count as failed hellos, and an accepted hello sets the count to zero without shortening a
  running block (E1, E2).
- AC5: At the end of a block the end line of S2 is logged with the number of its refused upgrades, `0` included; no new
  line names an address or host (S2, E9, N4).
- AC6: Two IPv4 addresses, two IPv6 addresses of one `/64`, and a connection without address are separate hosts; an
  IPv4-mapped IPv6 address and its IPv4 address are one host (E6, E7).
- AC7: With 1,024 hosts tracked a further host shares one entry with every untracked host, the host of the last accepted
  hello keeps its own, and a host is forgotten 60 seconds after its last failed hello or block end, not while a
  connection of it waits (N2).
- AC8: An accepted Companion that reads nothing — idle and answering no ping, or sending while the replies stay unread —
  is ended at most 60 seconds after the last frame read from it, `Companion disconnected; …` is logged and a new hello
  is accepted; one with a receive pending that sends nothing stays connected past 60 seconds (S4, N1).
- AC9: An upgrade with `X-Forwarded-For` or `Forwarded` counts for its TCP peer, with
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` unset and `true` alike (S5, E6).

## Out of scope

- Trusting forwarded headers from a configured proxy (a possible follow-up); logging addresses; a setting for a value
  of N1; `Retry-After`; a bound on log lines across hosts (N6); the Companion's reaction to `429` (#40).

## Open questions

- Are forwarded headers trusted behind a reverse proxy, and from whom? Answered: from no one, whatever
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` says (S5, E6). A header any client sets would let it escape its limits or get
  another host blocked; trusting listed proxies is configuration to get wrong, the ground on which record 0064 rejected
  an origin list. Behind a proxy the limits act on the proxy, which keeps today's lock-out risk there and no worse.
- Does the keep-alive bound a Companion that never reads? Answered: AC8 is the requirement. Whether .NET's keep-alive
  aborts while a send is blocked is library behavior no measurement in `ingest.md` covers; the plan names that
  measurement and adds a bound of its own if the keep-alive misses AC8.
- Which failures count, which values hold, how are refusals summarised without addresses? Answered: E1 to E3, N1, N4
  (Lead).

## Area documents

- `docs/areas/ingest.md`: the `429` cases in *Route and HTTP refusals*; a subsection *Limits per host* (host, failed
  hellos, window, block, tracked hosts, forwarded headers); *Bounds and times*; the block lines in *Log lines*; the
  no-read bound and its measurement; *Accepted risks* rewritten (many hosts or IPv6 addresses, a proxy as one host, a
  flood refusing a Companion on a new address).
- `SECURITY.md` and the user guide's ingest *Limits* item, English and German.
- Record 0064 (unreleased, not in `v0.2.0`) or a new record; the plan decides.

## Challenge

Spec challenge by the Devil's Advocate, 3 major and 4 minor objections.

1. Major — `ASPNETCORE_FORWARDEDHEADERS_ENABLED` `true` makes any client's `X-Forwarded-For` the remote address
   (`WebApplication.CreateBuilder`, `Program.cs:8`). Accepted: the limits key on the TCP peer whatever the setting says
   (S5, E6, Open questions), covered by the new AC9. How the endpoint reaches the TCP peer is the plan's, marked
   *unverified* until its test passes; if the plan finds no way, the Lead amends AC9 to declare the setting unsupported
   for the limits in `ingest.md`, `SECURITY.md` and the user guide, rather than leave it unstated.
2. Major — `/64` grouping merges every device of an IPv6 network. Accepted: a host is the full IPv6 address (E6,
   AC6). Trade-off recorded: `/64` would let one device's wrong token block the Companion of the same network, a full
   address lets a host that picks many addresses escape the limits per host; the second leaves such a host with
   today's total of 16 and harms no innocent device, so it is taken. The IPv6 case is in E7 and the *Accepted risks*
   outline.
3. Major — a flood of hosts drives the shared entry into a block. Accepted: the host of the last accepted hello keeps an
   entry of its own outside the 1,024 (N2, AC7); a Companion on a new address during a flood is refused, stated as an
   accepted risk in N2 and the *Accepted risks* outline.
4. Minor — the window was undefined. Accepted: a sliding window (E3), AC3 adds that 10 failures over more than 60
   seconds block nothing, N6 corrected.
5. Minor — "idle" in AC8. Accepted: "idle and answering no ping", and a Companion with a receive pending that sends
   nothing stays connected past 60 seconds (AC8, `ingest.md`, *One Companion and silent connections*).
6. Minor — a host with a waiting connection could be forgotten. Accepted: it is not forgotten and counts against the
   1,024 (N2, AC7).
7. Minor — under `TestServer` every connection has no address, so E6 makes them one host and
   `IngestEndpointSeventeenthPendingConnectionReturns503` (`tests/Censio.Server.Tests/IngestEndpointTests.cs`) would get
   `429` on the 5th. Accepted for the plan: it gives test connections client addresses and has the Tester rewrite that
   test with at least 4 hosts, not delete it. The spec keeps E6, which describes the product.
