# 0016: C#, .NET 10 and Blazor Server only

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** —
- **Source:** Issue #8
- **Supersedes:** —

## Context

Companion and server share types (the contract, the catalog), and local mode
([0015](0015-two-deliverables-and-local-mode.md)) runs the server inside the Companion process. A second language or
runtime would mean a second toolchain, a second set of analyzers and tests, and a contract kept in step by hand.

## Options considered

1. **A JavaScript single-page application with a separate API** — a large ecosystem for charts; a second language,
   a build toolchain and an API layer between UI and data.
2. **Blazor WebAssembly** — C# in the browser; the browser downloads the runtime, and the live data still needs an
   API and a push channel.
3. **C# on .NET 10 everywhere, the web UI in Blazor Server** — one language, one SDK, shared assemblies; the UI runs
   on the server and pushes live updates over its circuit. Con: every open page holds a server connection.

## Decision

Option 3. All production code is C# on .NET 10 (the target frameworks in the project files), and the web UI is
Blazor Server with interactive server rendering; no JavaScript framework and no second runtime.

## Consequences

- One toolchain and one analyzer setup (`.squad/stack.md`) cover every project.
- A JavaScript library is used only through a Blazor wrapper package
  ([0025](0025-mudblazor-and-blazor-apexcharts-light-theme.md)).
- Each browser tab holds a circuit on the server; fine for one player's history, not for a public site.
- Moving to a later .NET version is a change of the target frameworks, the SDK in `global.json` and the image bases.
