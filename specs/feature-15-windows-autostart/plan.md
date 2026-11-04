# Plan: Optional Windows autostart for the Companion

Source: [spec.md](spec.md) (Issue #15)
Status: Draft
Tier: security — the change writes a command line that Windows runs at every sign-in, and its quoting and the match rule are the guard that decides which file Windows starts (output escaping, *Tiers*).
Security areas touched: new — the per-user autostart value `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Censio Companion` and its `Explorer\StartupApproved\Run` value, written and removed by `CompanionAutostart` through `RegistryStartupEntryStore` (`src/Censio.Companion.Core/CompanionAutostart.cs`, `src/Censio.Companion/RegistryStartupEntryStore.cs`); logging of the autostart lines (`CompanionLog`). `.squad/project.md` (*Security areas*) gains the entry (Dev).

## Problem / root cause

New feature: [spec.md](spec.md), *Problem*. Nothing exists today (fact 1).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The Companion has no autostart entry or code | `TrayMenuEntryKind.cs` has no such kind; `grep -rn -i "registry\|autostart"` over `src/Censio.Companion*` finds nothing | confirmed |
| 2 | The menu is built from `TrayMenu.Create(version, snapshot, clock)` each time it opens, on the UI thread | `TrayIcon.cs:70-74` (`Opening` → `BuildItems`), `TrayIcon.cs:126` | confirmed |
| 3 | `TrayMenu.Create` has nine call sites in tests and one in the shell | `TrayMenuTests.cs:91,115,136,155,178,207,226,242,258`; `TrayIcon.cs:126` | confirmed |
| 4 | Menu clicks reach `App.Dispatch(TrayMenuEntryKind)`; only listed kinds get a click handler | `TrayIcon.cs:139-142`, `App.xaml.cs:252-288` | confirmed |
| 5 | `src/Censio.Companion/**` is outside both coverage measures | `.squad/stack.md` *Coverage exclusions*; `ci.yml:71`; record 0004 | confirmed |
| 6 | `Microsoft.Win32.Registry` is in the shared framework, no package needed | `/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/10.0.14/ref/net10.0/Microsoft.Win32.Registry.dll` | confirmed |
| 7 | No release tag exists, so record 0017 is unreleased and edited in place | `git ls-remote --tags origin` prints nothing | confirmed |
| 8 | `Environment.ProcessPath` returns the full path of `Censio.Companion.exe` in the single-file publish | to verify: the maintainer, step 7 manual run (AC6), the value written to the Run key equals the exe's path (source: .NET API documentation) | unverified |
| 9 | Explorer starts a REG_SZ Run value at sign-in, the quoted path as one file, without `cmd`, and `&`, `^`, `,`, `;`, `%` in the quoted path are taken literally | to verify: the maintainer, AC6, an exe in a folder named `Censio & Co, 100%` (source: Microsoft documentation of the Run key; CreateProcess quoting rules) | unverified |
| 10 | Task Manager and Settings write the off switch as a 12-byte REG_BINARY under `HKCU\…\Explorer\StartupApproved\Run` with first byte `0x03` (off) and `0x02` (on); deleting the value makes the entry count as on | to verify: the maintainer, AC6, read the value with `reg query` after switching off and on in Settings and in Task Manager, and after a menu click (source: not documented by Microsoft; widely observed) | unverified |
| 11 | The startup apps list shows a Run value under the exe's file description or file name, not the value name | to verify: the maintainer, AC6; the documents name what is shown (source: observed Windows behavior) | unverified |
| 12 | A standard user can write `HKCU\…\Run` and `StartupApproved\Run` without UAC | to verify: the maintainer, AC6, standard account (source: per-user hive owned by the user) | unverified |
| 13 | `RegistryKey.GetValue` returns `null` for an absent value, `string` for REG_SZ and REG_EXPAND_SZ (unexpanded with `DoNotExpandEnvironmentNames`), `byte[]` for REG_BINARY, `int` for REG_DWORD; `DeleteValue(name, false)` does not throw when absent | to verify: the maintainer, AC6 (each path of `RegistryStartupEntryStore` runs once); the fake models exactly this (source: .NET API documentation) | unverified |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `TrayMenuCreateHasAllEntriesInOrder` (adapted: 13 entries, `Start with Windows` between separators after `Open log folder`), `TrayMenuCreateAutostartEnabledInEveryMode` (live, recording, replay, invalid), `TrayMenuCreateAutostartCheckedWhenEnabled`, `TrayMenuCreateAutostartUncheckedWhenDisabled`; `CompanionAutostartRefreshMatchingEntryReturnsTrue`, `CompanionAutostartRefreshAbsentEntryReturnsFalse`, `CompanionAutostartRefreshOtherPathReturnsFalse`, `CompanionAutostartRefreshEntryWithArgumentsReturnsFalse`, `CompanionAutostartRefreshTurnedOffReturnsFalse`, `CompanionAutostartRefreshUnknownOffRecordReturnsFalse`, `CompanionAutostartRefreshNoExePathReturnsFalse`; `CompanionAutostartMatchesCases` (data rows of *Approach*, guard table), `CompanionAutostartIsTurnedOffCases` (data rows) | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs`, `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |
| AC2 | `CompanionAutostartToggleWhenOffWritesQuotedCommand` (expected value written as a raw literal `"""<path>"""` plus first and last character `"`), `CompanionAutostartToggleWhenOffRemovesOffSwitch`, `CompanionAutostartToggleWhenOffReplacesOtherCommand`, `CompanionAutostartToggleWhenOnRemovesEntryAndOffSwitch`, `CompanionAutostartToggleUsesStateOfLastRefresh`, `CompanionAutostartFormatCommandCases` (path with spaces, comma, `&`, non-ASCII; relative path, empty, `null`, path holding `"` give `null`) | `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |
| AC3 | `CompanionAutostartConstructAndRefreshWriteNothing` (the fake counts writes and removals: zero after construction and any number of `Refresh` calls) | `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |
| AC4 | `CompanionAutostartRefreshReadFailsReturnsFalseAndLogsWarning`, `CompanionAutostartToggleWriteFailsLogsWarning`, `CompanionAutostartToggleRemoveFailsLogsWarning`, `CompanionAutostartToggleNoExePathLogsWarningAndWritesNothing`, `CompanionAutostartToggleLogsEnabledWithoutPath`, `CompanionAutostartToggleLogsDisabledWithoutPath` (messages exactly as in *Approach*, no line contains the exe path) | `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |
| AC5 | `CompanionAutostartToggleTouchesOnlyItsEntryName` (the fake records every name; only `Censio Companion` is written or removed); the HKCU-only store is checked by the Reviewer and by hand in AC6 | `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |

AC6, AC7 and AC8 are verified without tests (below).

## Verification without tests

The change has production and test code; this section only covers the criteria no unit test can check.

- AC6: by the maintainer (Product Manager) on Windows 10 22H2 or 11 x64 in step 7, before step 9, on an exe published from the branch head with the command of README *Build the Companion from source*, under a standard account. Cases: S2–S4 with a restart each; E4 off in Settings and in Task Manager, then a click; E5 a folder with spaces and `Censio & Co, 100%` (fact 9); turning off leaves neither value (`reg query`, fact 10); no UAC prompt; S6 a second account; E9 SmartScreen; facts 8, 10–13. The orchestrator logs the result; a failed case goes back to the Dev.
- AC7, AC8: the Reviewer reads README, `docs/USER_GUIDE.md` and `docs/areas/companion.md` against the spec and the code.

## Approach

**Core (`Censio.Companion.Core`, tested on Linux).** `CompanionAutostart` holds every rule; `IStartupEntryStore` is a dumb raw-value store, so the shell decides nothing.

- `Refresh()` reads the Run value and the off-switch value of `EntryName` and returns, and keeps as `IsEnabled`, `Matches(command, exePath) && IsTurnedOff(approval) == false`. Without a usable exe path (`FormatCommand` gives `null`) it returns `false` without reading or logging. An exception of the store: log `AutostartReadFailed`, return `false`.
- `Toggle()` acts on the state of the last `Refresh` (E7). From off: `FormatCommand(exePath)`; `null` → log `AutostartChangeFailed` with no exception, write nothing; else `WriteCommand(EntryName, command)`, then `RemoveApproval(EntryName)`, then log `AutostartEnabled`. From on: `RemoveCommand(EntryName)`, then `RemoveApproval(EntryName)`, then log `AutostartDisabled`. An exception: log `AutostartChangeFailed` with it and stop; the next `Refresh` shows what holds (writing first and clearing second fails closed: a write that lands without the clear reads as off).
- `FormatCommand(exePath)`: `null` when the path is `null`, empty, not `Path.IsPathFullyQualified`, or holds `"`; else `"` + path + `"`.
- `Matches(command, exePath)` — the guard, with every form the Run processing accepts:

| Stored value | Result | Why |
| ------------ | ------ | --- |
| absent or not a `string` (`int`, `byte[]`) | no match | not a command |
| `"<exe path>"`, any case | match | the form the Companion writes |
| `"<exe path>" --record`, or anything after the closing quote | no match | holds arguments (E10) |
| `<exe path>` unquoted, path without whitespace | match | Windows starts the same file |
| `<exe path>` unquoted, path with whitespace | no match | Windows may start `C:\Program.exe` first; the click replaces it with the quoted form |
| quotes inside, or only one quote | no match | not the Companion's form |
| `%VAR%\…` (REG_EXPAND_SZ or REG_SZ) | no match unless the literal text equals the path | compared unexpanded |
| another program's path | no match | E10 |

  Comparison: `string.Equals(inner, exePath, StringComparison.OrdinalIgnoreCase)` on the full text, no trimming.
- `IsTurnedOff(approval)`: `null` → `false`; a `byte[]` of exactly 12 bytes whose first byte is `0x02` or `0x06` → `false`; everything else (`0x03`, `0x07`, another length, another type) → `true` (fail closed, E4).
- Logs (`CompanionLog`): Information `Autostart enabled`, `Autostart disabled`; Warning `Could not read the autostart setting`, `Could not change the autostart setting`, each with the exception or `null`; no message holds a path.
- Menu: `TrayMenu.Create(version, snapshot, isAutostartEnabled, clock)` yields, top to bottom: Status, LastTick, separator, OpenDiagnostics, ToggleRecording, OpenRecordingsFolder, OpenLogFolder, separator, **ToggleAutostart** (`Start with Windows`, enabled in every mode, `IsChecked = isAutostartEnabled`), separator, Version, separator, Exit. `TrayMenuEntry` gains `IsChecked` (the three-argument constructor sets `false`).

**Shell (`Censio.Companion`).** `RegistryStartupEntryStore` opens only `Registry.CurrentUser`: `Software\Microsoft\Windows\CurrentVersion\Run` (read with `DoNotExpandEnvironmentNames`; write with `CreateSubKey` and `SetValue(name, command, RegistryValueKind.String)`; remove with `DeleteValue(name, false)` on the opened key, nothing when the key is absent) and `Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` (read; remove the same way; never written). `App.OnStartup` creates `CompanionAutostart(new RegistryStartupEntryStore(), Environment.ProcessPath, _logger)` after the logger, in every mode. `TrayIcon` gets `Func<bool> isAutostartEnabled`, called in `BuildItems` (so on each opening), sets `ToolStripMenuItem.Checked = entry.IsChecked`, and wires a click for `ToggleAutostart`; `App.Dispatch` calls `_autostart?.Toggle()` on the UI thread. Nothing else calls `Toggle` (AC3).

Layout of the documents is free for the Dev, approved in step 9.

## Options considered

- **Storage**: Run value vs Startup-folder shortcut vs scheduled task — Run value; the reasons are in record 0017.
- **Re-enabling an entry the player switched off**: delete the off-switch value (chosen; leaves nothing behind, fact 10) vs write `0x02` and twelve bytes (keeps a value after turning off, m4 of the spec challenge).
- **Where the rules live**: in Core behind a raw store (chosen; testable on Linux, record 0004) vs a store that returns parsed states (moves the guard into untested shell code).
- **When the state is read**: at each menu opening (chosen; the startup apps list can change it any time) vs once at start (stale after E4).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Companion.Core | `IStartupEntryStore.cs` | new interface |
| Censio.Companion.Core | `CompanionAutostart.cs` | new class |
| Censio.Companion.Core | `CompanionLog.cs` | four log methods |
| Censio.Companion.Core | `TrayMenu.cs` | constant, `Create` with `isAutostartEnabled`, the new entry |
| Censio.Companion.Core | `TrayMenuEntry.cs` | `IsChecked`, four-argument constructor |
| Censio.Companion.Core | `TrayMenuEntryKind.cs` | `ToggleAutostart` after `OpenDiagnostics` |
| Censio.Companion | `RegistryStartupEntryStore.cs` | new class |
| Censio.Companion | `TrayIcon.cs` | constructor parameter, check mark, click wiring |
| Censio.Companion | `App.xaml.cs` | field `_autostart`, creation, `CreateTrayIcon`, `Dispatch` |

## Signatures (for the Dev's skeleton)

- `public interface IStartupEntryStore`
  - `object? ReadCommand(string name);`
  - `void WriteCommand(string name, string command);`
  - `void RemoveCommand(string name);`
  - `object? ReadApproval(string name);`
  - `void RemoveApproval(string name);`
- `CompanionAutostart` (public sealed class)
  - `EntryName` (public const string, value "Censio Companion")
  - `public CompanionAutostart(IStartupEntryStore store, string? exePath, ILogger logger)`
  - `IsEnabled` (public bool, get and private set)
  - `public bool Refresh()`
  - `public void Toggle()`
  - `public static string? FormatCommand(string? exePath)`
  - `public static bool Matches(object? command, string exePath)`
  - `public static bool IsTurnedOff(object? approval)`
- `CompanionLog`: `public static partial void AutostartEnabled(ILogger logger);`, `public static partial void AutostartDisabled(ILogger logger);`, `public static partial void AutostartReadFailed(ILogger logger, Exception? exception);`, `public static partial void AutostartChangeFailed(ILogger logger, Exception? exception);`
- `TrayMenu`: `StartWithWindowsText` (public const string, value "Start with Windows"), `public static IReadOnlyList<TrayMenuEntry> Create(string version, TraySnapshot snapshot, bool isAutostartEnabled, TimeProvider clock)` (replaces the three-parameter overload)
- `TrayMenuEntry`: `public TrayMenuEntry(TrayMenuEntryKind kind, string text, bool isEnabled, bool isChecked)`, `IsChecked` (public bool, get only)
- `TrayMenuEntryKind`: `ToggleAutostart`
- `RegistryStartupEntryStore` (internal sealed class implementing `IStartupEntryStore`, shell)
- `TrayIcon`: `public TrayIcon(string version, TrayIconImages images, Func<TraySnapshot> snapshot, Func<bool> isAutostartEnabled, Action<TrayMenuEntryKind> dispatch)`

Files the skeleton rewrites: `TrayMenu.cs`, `TrayMenuEntry.cs`, `TrayMenuEntryKind.cs`, `CompanionLog.cs`, `TrayIcon.cs`, `App.xaml.cs` (all read).

## Test files

- `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` (new)
- `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` (adapted and extended)
- `tests/Censio.Companion.Core.Tests/FakeStartupEntryStore.cs` (new fake: a dictionary of raw values per name for each of the two lists, a switch per operation to throw, the names and count of every write and removal; models fact 13, marked there as unverified)

Existing call sites: the nine `TrayMenu.Create` calls in `TrayMenuTests.cs` take `isAutostartEnabled: false` and `TrayMenuCreateHasAllEntriesInOrder` gets the 13 entries — the Tester (*Loop limits*). `TrayIcon.cs:126` and `App.xaml.cs:244` — the Dev in the skeleton.

Files no unit test can cover: `src/Censio.Companion/RegistryStartupEntryStore.cs`, `TrayIcon.cs`, `App.xaml.cs` — the Windows shell, which no test project loads; already excluded in both measures (fact 5, record 0004); no new exclusion.

## Areas

- `docs/areas/companion.md`: as the spec's *Area documents* says — a section *Start with Windows* (the rules of *Approach* in user terms: the entry, checked state, what a click writes and clears, E2–E7, E10), the menu enumeration, the four log lines in *Log*, *Limits and errors* (E11, the undocumented off record, what AC6 verified or not), *Scope*, *Not here*, *Related decisions* (0017).

## Documentation updates

All by the Dev once the code is green, English first, then German in the same file:

- `README.md`: *Status* bullet (line 14) and *Companion* step 2 (line 103) name `Start with Windows` in the menu; a short paragraph under *Companion*: off by default, per-user, extract the ZIP to a folder that stays, moving or a new version in another folder needs a click again, turn it off before deleting, how to remove a stale entry (AC7); the German counterparts.
- `docs/USER_GUIDE.md`: *Companion* step 2 (line 109) and *First start* (line 182) menu lists; a subsection *Start with Windows* under *Companion* with the AC7 content and the E9 SmartScreen result from AC6; *Data and logs* (line 188): the Companion writes one per-user autostart value only when turned on, and the log lines; the German counterparts (lines 308, 381, 387 and the new subsection).
- `docs/areas/companion.md`: *Areas* above.
- `docs/areas/README.md` (line 32): the companion row's scope names the autostart entry.
- `docs/ARCHITECTURE.md` (line 24): the Core list adds "autostart rules"; line 25: the shell adds "the registry store of the autostart value".
- `.squad/project.md` *Security areas*: a bullet "Windows autostart: `CompanionAutostart` writes only `HKCU\…\Run\Censio Companion` as the quoted `Environment.ProcessPath` without arguments, and removes only that value and its `StartupApproved\Run` value (`RegistryStartupEntryStore`, record 0017)"; *Test doubles*: `FakeStartupEntryStore` (the Tester).

## Architecture check

No guarantee in `.squad/project.md` (none recorded). The Core/shell split of record 0004 holds: rules in Core, Windows calls in the shell. No network, no new dependency, no new configuration option.

## Security considerations

- [x] No limits: the store values are read whole; the registry bounds them.
- [x] No exception reaches the user: every store exception is caught in `CompanionAutostart` and only logged (the exception text may name the registry key, never the exe path from this code).
- The value is written in the user's own hive; any process of the same user could write it already, so no privilege boundary moves. The guard is the quoting and the match rule above: the Companion never writes an unquoted or argument-carrying command and reports anything else as off.

## Decision records

- `docs/decisions/0017-companion-tray-app-autostart-and-connect.md` (Accepted, unreleased, extended in place): the storage options (Run value, Startup-folder shortcut, scheduled task), the choice, and the consequences (follows the last exe, undocumented off switch read fail-closed, policy invisible); `Area:` set to companion. The orchestrator sets it to the final state in step 9; no index change (the title stands).

## Out of scope / follow-ups

As the spec's *Out of scope*. None to open.
