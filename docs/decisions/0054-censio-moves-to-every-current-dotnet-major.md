# 0054: Censio moves to every current .NET major, STS and LTS alike

- **Status:** Accepted
- **Date:** 2026-11-11
- **Area:** —
- **Source:** feature `dotnet-11-upgrade` (the user's request to check the move from .NET 10 to .NET 11)
- **Supersedes:** 0016 — amends only its .NET version: the code is C# on the current .NET major instead of .NET 10, and
  the choice of C# and Blazor Server stands

## Context

[0016](0016-csharp-dotnet-10-blazor-server-only.md) put every project on .NET 10, an LTS release. Microsoft releases a
new .NET major every November. Even majors are LTS and odd majors STS, and since .NET 9 an STS release is supported for
24 months. .NET 11 was released on 2026-11-10 as STS. It reaches end of support on 2028-11-14, the same day as .NET 10
(release index of `builds.dotnet.microsoft.com`, read 2026-11-11). Moving to every major therefore loses no support
time compared with staying on LTS. It does mean one migration a year instead of one every two years.

The first move, from .NET 10 to .NET 11, changed no behavior users can see. It taught two things:
- A major can change behavior the code relied on, without any compile error: the logging source generator's escaping,
  and the runtime's `double` to `decimal` conversion.
- A major can add API that makes analyzers report new diagnostics.

One major apart, each of these was small and was found by the existing tests and the *Analyzer gate*.

## Options considered

1. **LTS releases only (skip the STS majors), moving every two years** — half the migrations. Con: every move takes in
   the breaking changes of two majors at once, and the code stays up to a year behind the runtime and its performance
   work. For .NET 11 this was the Lead's recommendation.
2. **Every current major, STS or LTS** (chosen by the Product Manager) — each move meets the changes of one major,
   while they are new and documented, and the code is never more than one release behind. Con: one migration a year.

## Decision

Option 2. Censio moves to the current .NET major once it is released: .NET 11 in 2026, .NET 12 in 2027, .NET 13 in 2028,
STS and LTS alike. Each move takes every target framework, `global.json`, the image bases and the Microsoft.Extensions
and ASP.NET Core packages together (`.squad/stack.md`, *Toolchain*), with no change in behavior users can see.

## Consequences

- Each move is one change of its own, never part of a Dependabot update. It is due after the new major is released, and
  it is made while the current major is still supported.
- This record only describes the policy. No tooling, workflow or Dependabot rule starts a move, and Dependabot keeps
  ignoring the next major until the move.
- The move to .NET 11 rests on these findings:
  - The Windows 10 support list of .NET 11 is the same as that of .NET 10: LTSC 1607, 1809 and 21H2. Neither release
    lists Windows 10 22H2, which the README names, so it is untested on .NET 11 too.
  - The replay speed divides by the factor as written (parsed as a `decimal` from the text), so the result does not
    depend on how the runtime converts `double` to `decimal` ([pipe-recording](../areas/pipe-recording.md), *Replay*).
- `dotnet_major.py` does not check a script path that names the target framework (`release-check.sh`). That path
  changes with every move.
