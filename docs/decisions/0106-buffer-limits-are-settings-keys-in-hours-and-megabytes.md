# 0106: The buffer's limits are keys of the settings file, in whole hours and megabytes

- **Status:** Proposed
- **Date:** 2026-11-23
- **Area:** live-sending
- **Source:** Issue #41
- **Supersedes:** —

## Context

Issue #41 asks for a limit by age and by size, 24 hours or 100 MB by default, both configurable; record
[0031](0031-bounded-local-tick-buffer-gaps-accepted.md) leaves the bounds to #41. Forward mode's other settings are
keys of `settings.json` ([0103](0103-forward-server-address-and-token-are-keys-of-the-settings-file.md)); the history
keeps ticks at full resolution for 7 days ([0021](0021-sqlite-history-with-retention.md)). The Companion's clock is
the only clock it has while the server is away.

## Options considered

1. **Keys `bufferHours` (1 to 168) and `bufferMegabytes` (1 to 10,240, 1 MB = 1,048,576 bytes of written JSON), integer
   literals like `localPort`, read with `mode` `forward` and `local`; a wrong value is an invalid server setting; the
   age is the time since the Companion took the message** (chosen) — one file for every setting, one validation rule,
   a cap at the full-resolution retention. Against: a typo stops live sending until it is fixed.
2. **Command-line switches** — visible in the shortcut; autostart writes the command without arguments
   ([`companion.md`](../areas/companion.md)), so they would be lost.
3. **A wrong value falls back to the default with a warning** — sending goes on; the player believes a limit that does
   not hold, unlike every other server setting.
4. **Fixed limits** — no setting to validate; the issue asks for configurable ones.
5. **Age by the message's own receive time from the pipe** — the same clock; no difference worth a second name.

## Decision

Option 1. The values, texts and the drop order are in [`live-sending.md`](../areas/live-sending.md), *Switching on* and
*Buffer*.

## Consequences

- A clock set forward drops messages early, one set back keeps them longer.
- A limit above 7 days is refused; a late tick of a folded hour would count on top of its hourly point
  ([`history.md`](../areas/history.md)).
- An invalid key stops local mode's server as an invalid `localPort` does.
