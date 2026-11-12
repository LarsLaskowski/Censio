# 0059: Contract messages carry an identity the Companion assigns and the profile they were taken under

- **Status:** Proposed
- **Date:** 2026-11-12
- **Area:** contract
- **Source:** Issue #27
- **Supersedes:** —

## Context

The server acknowledges what it took, and the Companion's buffer (#41, record
[0031](0031-bounded-local-tick-buffer-gaps-accepted.md)) resends what was not acknowledged, so a resend must be told from
a new message. Session events carry no data at all, so two session ends are equal in content. The profile is selected in
the Companion and can change while ticks wait in the buffer
([0041](0041-profile-selected-in-the-companion-figure-name-only-a-hint.md)).

## Options considered

1. **Identity from the content (a hash of the message)** — no state; two session ends collide, and so would two ticks of
   equal content.
2. **A sequence number per Companion** — ordered and short; it restarts with a new installation or a lost settings file
   and then repeats numbers the server has seen.
3. **A random UUID assigned when the Companion takes the message and kept with it** (chosen) — unique without state;
   carries no order, which the receive and game times give.
4. **Profile per connection, in the hello only** — smaller ticks; a buffered tick of profile A delivered after a switch
   to B lands in B, or the Companion must reconnect per profile.
5. **Profile identity in each tick and session event, the name only in the hello** (chosen) — the profile survives the
   buffer and a rename needs no new version.

## Decision

Options 3 and 5. A tick, session event, catalog upload and icon image carry a UUID the Companion assigns when it takes
the message, kept for every resend; ticks and session events carry the identity of their profile, and session events
their receive time. The forms are in [`contract.md`](../areas/contract.md).

## Consequences

- #41 stores the identity with each buffered message; the server (#30, #35) treats a known identity as a resend.
- The profile name crosses only in the hello, never the figure name (0041).
