# 0054: Move to .NET 11 (STS) now, not wait for .NET 12

- **Status:** Accepted
- **Date:** 2026-11-11
- **Area:** —
- **Source:** feature `dotnet-11-upgrade` (the user's request to check the move from .NET 10 to .NET 11)
- **Supersedes:** 0016 — amends only its .NET version: the code is C# on .NET 11 instead of .NET 10, and the choice of
  C# and Blazor Server stands

## Context

[0016](0016-csharp-dotnet-10-blazor-server-only.md) put every project on .NET 10, an LTS release. .NET 11 was
released on 2026-11-10 as an STS release, and both releases reach end of support on 2028-11-14 (release index of
`builds.dotnet.microsoft.com`, read 2026-11-11). .NET 12, the next LTS release, is expected in November 2027. So
moving now gains no support time, and it means two migrations before 2028-11-14 (10 to 11, then 11 to 12) instead of
one.

A trial build on .NET 11 found these breaking changes:
- The logging source generator now escapes message templates.
- The runtime converts `double` to `decimal` exactly.
- `TextWriter.WriteLineAsync` has a new overload that takes a cancellation token.
- `Microsoft.Extensions.Logging.Abstractions` is now part of the shared framework.
- A test race shows up more often.

The players and the operators get nothing new from the move: the product's behavior stays the same.

## Options considered

1. **Stay on .NET 10 until .NET 12** — one migration, and the findings carry over to it. Con: all the breaking changes
   of two majors arrive in one change, in 2027. This was the Lead's recommendation.
2. **Move to .NET 11 now** (chosen by the Product Manager) — breaking changes are met one major at a time, and the
   runtime stays current. Con: two migrations, and the STS release gives no longer support.

## Decision

Option 2. Every project targets .NET 11. `global.json`, the image bases and the Microsoft.Extensions and ASP.NET Core
packages follow (`.squad/stack.md`, *Toolchain*), and user-visible behavior does not change.

## Consequences

- The move to .NET 12 is due before 2028-11-14 and is again one change of its own.
- The Windows 10 support list of .NET 11 is the same as that of .NET 10: LTSC 1607, 1809 and 21H2. Neither release
  lists Windows 10 22H2, which the README names, so it is untested on .NET 11 too.
- The replay speed divides by the factor as written (parsed as a `decimal` from the text), so the result does not
  depend on how the runtime converts `double` to `decimal` ([pipe-recording](../areas/pipe-recording.md), *Replay*).
- A script path that names the target framework (`release-check.sh`) is not checked by `dotnet_major.py`, and it
  changes with every move.
