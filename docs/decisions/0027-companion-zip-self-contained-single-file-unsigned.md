# 0027: The Companion ships as a self-contained single-file exe in a ZIP, without code signing

- **Status:** Accepted
- **Date:** 2026-11-01
- **Area:** companion
- **Source:** Issue #8; Issue #5 (release)
- **Supersedes:** —

## Context

Players should start the Companion without installing .NET, running an installer or having administrator rights. A
code-signing certificate costs money every year, and an Authenticode signature needs the private key in the release
pipeline, whose build jobs run without secrets ([0012](0012-release-pre-release-first-and-secret-free-builds.md)).

## Options considered

1. **An installer (MSI, MSIX) or a framework-dependent build** — familiar install; administrator rights or an
   installed .NET Desktop runtime, more packaging.
2. **A self-contained single-file exe in a ZIP** — extract and start; a large file that carries the runtime.
3. **Code signing** — no SmartScreen warning once reputation builds; a yearly certificate and a key in the release.

## Decision

Option 2 without option 3. Each release attaches a ZIP holding one self-contained single-file `Censio.Companion.exe`,
which is not signed. What the ZIP holds and needs is in [`areas/companion.md`](../areas/companion.md) (*Platform*).

## Consequences

- Windows SmartScreen warns on the first start; README and user guide say how to continue.
- Each release carries the whole runtime; a .NET security fix needs a new Companion release.
- Code signing would need a certificate and a signing job that holds the key apart from the build jobs; that
  revisits this record and 0012.
