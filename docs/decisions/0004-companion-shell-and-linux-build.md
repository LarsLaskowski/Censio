# 0004: Companion logic in a platform-neutral project; the Windows shell builds on Linux

- **Status:** Proposed
- **Date:** 2026-10-30
- **Area:** companion
- **Source:** Issue #2
- **Supersedes:** —

## Context

The Companion is a WPF tray application (`net10.0-windows`). The squad session and the CI job (`ubuntu-latest`)
are Linux, and every gate of `.squad/stack.md` builds and tests the whole solution there. A `net10.0-windows`
project with WPF or Windows Forms compiles on Linux with `EnableWindowsTargeting`, but its runtime configuration
names `Microsoft.WindowsDesktop.App`, which Linux does not have, so a test project for it cannot start there. The
*Coverage gate* counts only files that appear in a coverage report, so Companion code that no Linux test loads
would drop out of the measure silently. The coverage exclusion list of the gate (`COVERAGE_EXCLUDES`) is a squad
file that a product pull request may not change.

## Options considered

1. **A Windows CI job for the Companion and its tests** — runs the WPF code for real; the session's gates and
   the existing Linux job stay blind to it, a second job to maintain, and the squad cannot run the tests it writes.
2. **`EnableWindowsTargeting` and a test project that compiles the Companion's logic as linked sources** — one
   Companion project; coverlet does not instrument the test assembly the sources compile into, so the files are
   still not measured, and the logic cannot use the Companion's own types.
3. **`EnableWindowsTargeting`, the logic in a platform-neutral `Censio.Companion.Core` (`net10.0`) with its
   test project, and `Censio.Companion` as a thin Windows shell without a test project** — everything builds and
   is tested on Linux; one more project; the shell's glue is verified only by a run on Windows.

For the shell's coverage exclusion the gate's list (`COVERAGE_EXCLUDES`) is out of reach of a product pull request,
and `[ExcludeFromCodeCoverage]` is redundant locally (the assembly is never instrumented) and not what SonarQube
Cloud reads; `sonar.coverage.exclusions` plus the local exclusion by construction remain. For the tray icon the
Windows Forms `NotifyIcon` ships with the SDK and re-adds its icon after an Explorer restart; a WPF tray package
(H.NotifyIcon) would be a third-party dependency for three menu items.

## Decision

Option 3. `Censio.Companion` sets `EnableWindowsTargeting` and holds only startup and tray glue (WPF
application, Windows Forms tray icon) and sets `RuntimeIdentifier` `win-x64` with `SelfContained` false, so
every build, on Linux too, writes the Windows `Censio.Companion.exe` (without a runtime identifier the apphost
follows the build machine and a Linux build writes no `.exe`); everything a test can reach lives in
`Censio.Companion.Core`. The shell has
no test project, and its files are excluded from the coverage measure by construction locally (no test loads the
assembly) and in CI by `sonar.coverage.exclusions`.

## Consequences

- Later Companion features (pipe connection, forward mode, buffer, settings) put their logic in
  `Censio.Companion.Core` and only their Windows UI in the shell.
- The shell's behavior (tray icon, exit, Explorer restart, sign-out) is verified by a manual run on Windows,
  not by CI; a Windows job can be added later without changing this split.
- *Restore* also fetches the `win-x64` apphost pack; the self-contained release publish (#5) passes its own
  runtime options.
- This is the one exception to "one test project per production project".
- If the gate's exclusion list becomes changeable by product pull requests, the shell can be listed there
  explicitly instead of relying on "no report contains it".
