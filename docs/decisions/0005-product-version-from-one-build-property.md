# 0005: The product version comes from one build property; the first release is 0.1.0, without edge builds

- **Status:** Accepted
- **Date:** 2026-10-30
- **Area:** —
- **Source:** Issue #2, Issue #8
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

For the version line (issue #8): **start at `1.0.0`**, against **`0.x` releases from `0.1.0`, `1.0.0` at milestone M7**;
the second is chosen, because the contract and the storage may still change incompatibly before 1.0. For builds
between releases: **edge builds on every merge to `main`** (#6), against **none, an unreleased state is built from
source** ([0013](0013-readme-quick-start-user-guide-reference.md)); the second is chosen, because #6 was closed as
not planned.

## Decision

Option 2. `Directory.Build.props` sets `Version` to `0.0.0` and turns off the source-revision suffix; every
assembly carries the build's `Version` as its informational version, and the start page and tray menu show it
unchanged. How a build is given its version is described in `docs/CONTRIBUTING.md` (*Versioning and
releases*); what each component shows is in [`areas/server.md`](../areas/server.md) and
[`areas/companion.md`](../areas/companion.md). The first release is `v0.1.0`; no edge builds are published.

## Consequences

- A local build always shows `0.0.0`; only the release workflow of #5 produces other versions (the edge builds of
  #6 were closed as not planned).
- A workflow that forgets `-p:Version` ships `0.0.0`; #5 checks the version of its artifacts.
