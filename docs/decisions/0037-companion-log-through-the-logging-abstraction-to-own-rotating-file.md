# 0037: The Companion logs through .NET's logging abstraction into its own rotating text file

- **Status:** Proposed
- **Date:** 2026-11-02
- **Area:** companion
- **Source:** Issue #13
- **Supersedes:** —

## Context

Issue #13 asks for "structured logging to a rotating file in the local app data folder", so that an external tester
of release 0.1.0 has something to look at and to hand in (`specs/feature-13-tray-app/spec.md` on the work branch). The
Companion ships as one self-contained file ([0027](0027-companion-zip-self-contained-single-file-unsigned.md)), its
logic is tested on Linux ([0004](0004-companion-shell-and-linux-build.md)), and the server already logs through
`Microsoft.Extensions.Logging`. The log may receive external text (a replay file name, exception texts), and the
project's security areas require such text to be logged only truncated and sanitized; the rotation deletes files in
a folder of the user's profile.

## Options considered

1. **Serilog with its file sink (and its async sink)** — mature rolling files; three third-party packages to review
   and embed, file names and retention by the library's rules (continuation `_001`), and the deletion code outside
   this repository's tests.
2. **`Microsoft.Extensions.Logging.Abstractions` with a rotating file writer of the Companion's own** (chosen) — the
   server's logging API and its message templates and source-generated log methods, one Microsoft package of the
   runtime's release line; naming, rotation, pruning and sanitizing are own code with unit tests on Linux. Con: about
   one more component to maintain.
3. **An own log interface without any package** — no dependency; no message templates and a second logging API
   beside the server's.
4. **JSON lines instead of text lines** — machine-readable; a tester reads the log in Notepad, and the templates already
   keep the values named in code.

## Decision

Option 2: the Companion's components log through `ILogger` with message templates, and a provider of the Companion's
own writes them as sanitized text lines, off the caller's thread, into daily files that roll by size and are pruned
to a fixed count by an exact name pattern. The names, limits, line layout and what is logged are in
[`areas/companion.md`](../areas/companion.md).

## Consequences

- One new package, `Microsoft.Extensions.Logging.Abstractions`, in `Censio.Companion.Core`, embedded in the single
  file.
- The writer never blocks the tray or the pipe: a full queue or a failing disk loses lines, never the run.
- Only names of the log's own pattern are deleted; other files in the folder are never touched.
- Exception texts are logged as given and may contain the profile path, and so the Windows user name; the user guide
  asks testers to check a log before sharing it.
- A log level setting and other sinks come with the Companion's settings, not here.
