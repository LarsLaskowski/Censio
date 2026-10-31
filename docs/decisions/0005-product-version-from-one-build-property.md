# 0005: The product version comes from one build property

- **Status:** Accepted
- **Date:** 2026-10-30
- **Area:** —
- **Source:** Issue #2
- **Supersedes:** —

## Context

The server's start page and the Companion's tray menu show the version, and the release (#5) and edge (#6)
workflows must be able to set it (`1.2.3`, `0.0.0-edge+<short sha>`). The .NET SDK defaults to `1.0.0` and,
with Source Link, appends `+<commit>` to the informational version, so a build would show a version nobody
chose.

## Options considered

1. **A Git-based versioning package (MinVer, Nerdbank.GitVersioning)** — derives the version from tags; a new
   build dependency, needs full history in every build (Docker build context included), and the edge format of
   #6 would have to be bent into its scheme.
2. **One MSBuild property for all assemblies** — `Version` in `Directory.Build.props`, default `0.0.0`,
   overridden by the workflows with `-p:Version=…`; no dependency, the workflows decide the text.

## Decision

Option 2. `Directory.Build.props` sets `Version` to `0.0.0` and turns off the source-revision suffix; every
assembly carries the build's `Version` as its informational version, and the start page and tray menu show it
unchanged. How a build is given its version is described in `docs/CONTRIBUTING.md` (*Versioning and
releases*); what each component shows is in [`areas/server.md`](../areas/server.md) and
[`areas/companion.md`](../areas/companion.md).

## Consequences

- A local build always shows `0.0.0`; only the workflows of #5 and #6 produce other versions.
- A workflow that forgets `-p:Version` ships `0.0.0`; #5 checks the version of its artifacts.
