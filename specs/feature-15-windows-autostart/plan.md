# Plan: Optional Windows autostart for the Companion

Source: [spec.md](spec.md) (Issue #15)
Status: Revised (1)
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
| 9 | Explorer starts a quoted REG_SZ Run value at sign-in as one file, with spaces, `&` and `,` in the path taken literally. `%` and paths over 200 characters are refused by `FormatCommand`, so neither expansion nor cutting is relied on | to verify: the maintainer, AC6 case E5 (a folder `Censio & Co, 1`); a failure only means nothing starts (source: Microsoft documentation of the Run key; CreateProcess quoting rules) | unverified |
| 10 | The off switch Settings and Task Manager write is a REG_BINARY of some length under `HKCU\…\Explorer\StartupApproved\Run`, whose first byte tells off from on; deleting the value makes Windows list the entry as on and start it | to verify: the Product Manager, *Measurement before tests* (M1, B1), before step 5; the output is pasted into this row and the Tester's `IsTurnedOff` rows use only the bytes it shows (source: not documented by Microsoft) | unverified |
| 11 | The startup apps list shows a Run value under the exe's file description or file name, not the value name | to verify: the maintainer, AC6; the documents name what is shown (source: observed Windows behavior) | unverified |
| 12 | A standard user can write `HKCU\…\Run` and delete a value under `StartupApproved\Run` without a UAC prompt | to verify: the Product Manager, *Measurement before tests* step 1 and 5 (source: per-user hive owned by the user) | unverified |
| 13 | `RegistryKey.GetValue` returns `null` for an absent value and also for any read error or unknown kind; `string[]` for REG_MULTI_SZ, `long` for REG_QWORD; `DeleteValue(name, false)` ignores errors besides a missing value; `OpenSubKey` throws `SecurityException` when access is denied | Security's reading of the .NET 10 `RegistryKey` source (plan review round 1, N1); the store's contract below does not rely on `GetValue`'s `null` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `TrayMenuCreateHasAllEntriesInOrder` (adapted: 13 entries, `Start with Windows` between separators after `Open log folder`), `TrayMenuCreateAutostartEnabledInEveryMode` (live, recording, replay, invalid), `TrayMenuCreateAutostartCheckedWhenEnabled`, `TrayMenuCreateAutostartUncheckedWhenDisabled`; `CompanionAutostartRefreshMatchingEntryReturnsTrue`, `CompanionAutostartRefreshAbsentEntryReturnsFalse`, `CompanionAutostartRefreshOtherPathReturnsFalse`, `CompanionAutostartRefreshEntryWithArgumentsReturnsFalse`, `CompanionAutostartRefreshTurnedOffReturnsFalse`, `CompanionAutostartRefreshUnknownOffRecordReturnsFalse`, `CompanionAutostartRefreshNoExePathReturnsFalse`; `CompanionAutostartMatchesCases` (one data row per line of the guard table in *Approach*), `CompanionAutostartIsTurnedOffCases` (the measured values of fact 10 as rows, each citing its source line, plus `null`, empty, another length and an unseen first byte) | `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs`, `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |
| AC2 | `CompanionAutostartToggleWhenOffWritesQuotedCommand` (expected value written as a raw literal `"""<path>"""` plus first and last character `"`), `CompanionAutostartToggleWhenOffRemovesOffSwitch`, `CompanionAutostartToggleWhenOffReplacesOtherCommand`, `CompanionAutostartToggleWhenOnRemovesEntryAndOffSwitch`, `CompanionAutostartToggleUsesStateOfLastRefresh`, `CompanionAutostartFormatCommandCases` (path with spaces, comma, `&`, non-ASCII, exactly 200 characters; relative path, empty, `null`, a path holding `"`, a path holding `%`, 201 characters give `null`), `CompanionAutostartToggleRefusedPathLogsWarningAndWritesNothing` | `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |
| AC3 | `CompanionAutostartConstructAndRefreshWriteNothing` (the fake counts writes and removals: zero after construction and any number of `Refresh` calls) | `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |
| AC4 | `CompanionAutostartRefreshReadFailsReturnsFalseAndLogsWarning`, `CompanionAutostartRefreshRepeatedReadFailureLogsOnce`, `CompanionAutostartRefreshFailureAfterSuccessLogsAgain`, `CompanionAutostartToggleWriteFailsLogsWarning`, `CompanionAutostartToggleRemoveFailsLogsWarning`, `CompanionAutostartToggleNoExePathLogsWarningAndWritesNothing`, `CompanionAutostartToggleLogsEnabledWithoutPath`, `CompanionAutostartToggleLogsDisabledWithoutPath` (messages exactly as in *Approach*, no line contains the exe path) | `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |
| AC5 | `CompanionAutostartToggleTouchesOnlyItsEntryName` (the fake records every name; only `Censio Companion` is written or removed); the HKCU-only store is checked by the Reviewer and by hand in AC6 | `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` |

AC6, AC7 and AC8 are verified without tests (below).

## Verification without tests

The change has production and test code; this section only covers the criteria no unit test can check.

- AC6: by the maintainer (Product Manager) on Windows 10 22H2 or 11 x64 in step 7, before step 9, on an exe published from the branch head with the command of README *Build the Companion from source*, under a standard account. Cases: S2–S4 with a restart each; E4 off in Settings and in Task Manager, then a click; E5 a folder `Censio & Co, 1` with spaces (fact 9), and a folder holding `%` (the entry stays unchecked, a warning in the log); turning off leaves neither value (`reg query`); no UAC prompt; S6 a second account; E9 SmartScreen; facts 8, 11. The orchestrator logs the result; a failed case goes back to the Dev.
- AC7, AC8: the Reviewer reads README, `docs/USER_GUIDE.md` and `docs/areas/companion.md` against the spec and the code.

## Measurement before tests

Facts 10 and 12 decide the off-switch rule and are measured by the Product Manager before the Tester writes the `IsTurnedOff` rows (step 5); everything else is designed to fail closed without a measurement. On Windows 11 and, if at hand, Windows 10 22H2, x64, signed in with a standard account, in PowerShell, with any `Censio.Companion.exe` (a `main` build is enough) at `$exe = "$env:LOCALAPPDATA\Programs\Censio\Censio.Companion.exe"`:

1. `New-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Censio Companion' -Value ('"' + $exe + '"') -PropertyType String -Force` — report whether a UAC prompt appeared.
2. Settings > Apps > Startup: report the name shown (fact 11), switch it off, then run `reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run /v "Censio Companion"` and paste the output.
3. Switch it on in Settings; paste the same `reg query` output.
4. Task Manager > Startup apps: *Disable*, paste the output; *Enable*, paste the output.
5. *Disable* again in Task Manager, then `Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run' -Name 'Censio Companion'` (UAC prompt?); reopen Settings and Task Manager and report whether the entry shows as enabled; sign out and in and paste `Get-Process Censio.Companion | Select-Object Path`.
6. Clean up: `Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Censio Companion'`; report the Windows version (`winver`).

The Lead writes the result into fact 10 and the `IsTurnedOff` rule (only first bytes and lengths seen as on count as on); if step 5 shows that deleting does not re-enable, the Lead revises E4 and AC2 before step 5.

## Approach

**Core (`Censio.Companion.Core`, tested on Linux).** `CompanionAutostart` holds every rule; `IStartupEntryStore` returns a value or `null` for absent and throws for anything else, so the shell decides nothing.

- `Refresh()` reads the Run value and the off-switch value of `EntryName` and returns, and keeps as `IsEnabled`, `Matches(command, exePath) && IsTurnedOff(approval) == false`. Without a usable exe path (`FormatCommand` gives `null`) it returns `false` without reading or logging. An exception of the store: return `false` and log `AutostartReadFailed` with it, once until a read succeeds again (a field remembers it; a successful read clears it), so a blocked read does not log at every opening (m3; written in *Limits and errors* of the area document).
- `Toggle()` acts on the state of the last `Refresh` (E7). From off: `FormatCommand(exePath)`; `null` → log `AutostartChangeFailed` with no exception, write nothing; else `WriteCommand(EntryName, command)`, then `RemoveApproval(EntryName)`, then log `AutostartEnabled`. From on: `RemoveCommand(EntryName)`, then `RemoveApproval(EntryName)`, then log `AutostartDisabled`. An exception: log `AutostartChangeFailed` with it and stop; the next `Refresh` shows what holds (writing first and clearing second fails closed: a write that lands without the clear reads as off).
- `FormatCommand(exePath)`: `null` when the path is `null`, empty, not `Path.IsPathFullyQualified`, longer than 200 characters, or holds `"` or `%`; else `"` + path + `"`. Refusing `%` and long paths outright keeps the design free of the measurements B1 (a) and (b) would need: whether Explorer expands `%NAME%` in a REG_SZ Run value and where it cuts a long command.
- `Matches(command, exePath)` — the guard: a match is only the exact form the Companion writes (m2):

| Stored value | Result | Why |
| ------------ | ------ | --- |
| `null` (absent) | no match | no entry |
| `"<exe path>"`, any case | match | the form the Companion writes |
| `"<exe path>" --record`, or anything after the closing quote, or leading or trailing spaces | no match | holds arguments or is not the written form (E10) |
| `<exe path>` unquoted, with or without whitespace | no match | not the written form; the click replaces it with the quoted form |
| quotes inside, or only one quote | no match | not the Companion's form |
| `%VAR%\…` | no match | compared unexpanded, and the exe path never holds `%` |
| another program's path | no match | E10 |

  Comparison: `string.Equals(command, FormatCommand(exePath), StringComparison.OrdinalIgnoreCase)`; `FormatCommand` giving `null` is no match.
- `IsTurnedOff(approval)`: `null` (absent) → `false`; a `byte[]` whose length and first byte are a combination the measurement (fact 10) showed for *on* → `false`; everything else → `true` (fail closed, E4). Expected from public reports: 12 bytes, first byte `0x02`; no other value counts as on unless measured.
- Logs (`CompanionLog`): Information `Autostart enabled`, `Autostart disabled`; Warning `Could not read the autostart setting`, `Could not change the autostart setting`, each with the exception or `null`; no message holds a path.
- Menu: `TrayMenu.Create(version, snapshot, isAutostartEnabled, clock)` yields, top to bottom: Status, LastTick, separator, OpenDiagnostics, ToggleRecording, OpenRecordingsFolder, OpenLogFolder, separator, **ToggleAutostart** (`Start with Windows`, enabled in every mode, `IsChecked = isAutostartEnabled`), separator, Version, separator, Exit. `TrayMenuEntry` gains `IsChecked` (the three-argument constructor stays and chains to the four-argument one with `isChecked: false`, m4).

**Shell (`Censio.Companion`).** `RegistryStartupEntryStore` opens only `Registry.CurrentUser`, the keys `Software\Microsoft\Windows\CurrentVersion\Run` and `Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` (the second is never written). Its contract (N1), which `FakeStartupEntryStore` copies:

- Read: `OpenSubKey(path, writable: false)`; a missing key, or a name not in `GetValueNames()` (case-insensitive), is `null`. Otherwise `GetValueKind(name)`: `ReadCommand` accepts `String` and `ExpandString` (read with `DoNotExpandEnvironmentNames`), `ReadApproval` only `Binary`; any other kind throws `InvalidDataException`, and a `null` from `GetValue` for a listed name throws `IOException` — both reach `Refresh` as a read failure (unchecked, E1, E4).
- Write (`WriteCommand` only): `CreateSubKey(path, writable: true)`, `SetValue(name, command, RegistryValueKind.String)`, then read back; a different value throws `IOException`.
- Remove: `OpenSubKey(path, writable: true)`; a missing key does nothing; `DeleteValue(name, false)`, then if `GetValueNames()` still lists the name, throw `IOException`.
- `SecurityException` and `UnauthorizedAccessException` (access denied) pass through to `CompanionAutostart` (E1).

`App.OnStartup` creates `CompanionAutostart(new RegistryStartupEntryStore(), Environment.ProcessPath, _logger)` after the logger, in every mode. `CreateTrayIcon` (`App.xaml.cs:244`) passes `() => _autostart?.Refresh() ?? false` to `TrayIcon` (m1), which calls it in `BuildItems`, so the registry is read at construction and at each opening; sets `ToolStripMenuItem.Checked = entry.IsChecked`, and wires a click for `ToggleAutostart`; `App.Dispatch` calls `_autostart?.Toggle()` on the UI thread. Nothing else calls `Toggle` (AC3).

Layout of the documents is free for the Dev, approved in step 9.

## Options considered

- **Storage**: Run value vs Startup-folder shortcut vs scheduled task — Run value; the reasons are in record 0017.
- **Re-enabling an entry the player switched off**: delete the off-switch value (chosen; leaves nothing behind, fact 10 step 5) vs write the measured *on* bytes (keeps a value after turning off, m4 of the spec challenge).
- **`%` and long paths**: refuse them (chosen; fails closed with no measurement) vs measure Explorer's expansion and cut-off (B1 a, b) and allow what it shows (more paths work, two more Windows facts to keep true).
- **Unquoted match**: none (chosen, m2) vs accepting an unquoted path without whitespace (a form the Companion never writes).
- **Where the rules live**: in Core behind a raw store (chosen; testable on Linux, record 0004) vs a store that returns parsed states (moves the guard into untested shell code).
- **When the state is read**: at each menu opening (chosen; the startup apps list can change it any time) vs once at start (stale after E4).

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Companion.Core | `IStartupEntryStore.cs` | new interface |
| Censio.Companion.Core | `CompanionAutostart.cs` | new class |
| Censio.Companion.Core | `CompanionLog.cs` | four log methods |
| Censio.Companion.Core | `TrayMenu.cs` | constant, `Create` with `isAutostartEnabled`, the new entry |
| Censio.Companion.Core | `TrayMenuEntry.cs` | `IsChecked`, four-argument constructor; the three-argument one chains to it with `false` |
| Censio.Companion.Core | `TrayMenuEntryKind.cs` | `ToggleAutostart` as the last member |
| Censio.Companion | `RegistryStartupEntryStore.cs` | new class |
| Censio.Companion | `TrayIcon.cs` | constructor parameter, check mark, click wiring |
| Censio.Companion | `App.xaml.cs` | field `_autostart`, creation, `CreateTrayIcon`, `Dispatch` |

## Signatures (for the Dev's skeleton)

- `public interface IStartupEntryStore`
  - `string? ReadCommand(string name);`
  - `void WriteCommand(string name, string command);`
  - `void RemoveCommand(string name);`
  - `byte[]? ReadApproval(string name);`
  - `void RemoveApproval(string name);`
- `CompanionAutostart` (public sealed class)
  - `EntryName` (public const string, value "Censio Companion")
  - `public CompanionAutostart(IStartupEntryStore store, string? exePath, ILogger logger)`
  - `IsEnabled` (public bool, get and private set)
  - `public bool Refresh()`
  - `public void Toggle()`
  - `public static string? FormatCommand(string? exePath)`
  - `public static bool Matches(string? command, string? exePath)`
  - `public static bool IsTurnedOff(byte[]? approval)`
- `CompanionLog`: `public static partial void AutostartEnabled(ILogger logger);`, `public static partial void AutostartDisabled(ILogger logger);`, `public static partial void AutostartReadFailed(ILogger logger, Exception? exception);`, `public static partial void AutostartChangeFailed(ILogger logger, Exception? exception);`
- `TrayMenu`: `StartWithWindowsText` (public const string, value "Start with Windows"), `public static IReadOnlyList<TrayMenuEntry> Create(string version, TraySnapshot snapshot, bool isAutostartEnabled, TimeProvider clock)` (replaces the three-parameter overload)
- `TrayMenuEntry`: `public TrayMenuEntry(TrayMenuEntryKind kind, string text, bool isEnabled, bool isChecked)` (new), the existing three-argument constructor unchanged in signature, `IsChecked` (public bool, get only)
- `TrayMenuEntryKind`: `ToggleAutostart` (last member)
- `RegistryStartupEntryStore` (internal sealed class implementing `IStartupEntryStore`, shell)
- `TrayIcon`: `public TrayIcon(string version, TrayIconImages images, Func<TraySnapshot> snapshot, Func<bool> isAutostartEnabled, Action<TrayMenuEntryKind> dispatch)`

Files the skeleton rewrites: `TrayMenu.cs`, `TrayMenuEntry.cs`, `TrayMenuEntryKind.cs`, `CompanionLog.cs`, `TrayIcon.cs`, `App.xaml.cs` (all read).

## Test files

- `tests/Censio.Companion.Core.Tests/CompanionAutostartTests.cs` (new)
- `tests/Censio.Companion.Core.Tests/TrayMenuTests.cs` (adapted and extended)
- `tests/Censio.Companion.Core.Tests/FakeStartupEntryStore.cs` (new fake of the store's contract in *Approach*: per list a dictionary of values by name, case-insensitive; a configurable exception per operation, including `SecurityException`, `InvalidDataException` and `IOException`; the names and count of every write and removal. It models the store's contract, not the registry; the registry facts it rests on are fact 13)

Existing call sites: the nine `TrayMenu.Create` calls in `TrayMenuTests.cs` take `isAutostartEnabled: false` and `TrayMenuCreateHasAllEntriesInOrder` gets the 13 entries — the Tester (*Loop limits*). `TrayIcon.cs:126` and `App.xaml.cs:244` — the Dev in the skeleton.

Files no unit test can cover: `src/Censio.Companion/RegistryStartupEntryStore.cs`, `TrayIcon.cs`, `App.xaml.cs` — the Windows shell, which no test project loads; already excluded in both measures (fact 5, record 0004); no new exclusion.

## Areas

- `docs/areas/companion.md`: as the spec's *Area documents* says — a section *Start with Windows* (the rules of *Approach* in user terms: the entry, checked state, what a click writes and clears, E2–E7, E10), the menu enumeration, the four log lines in *Log*, *Limits and errors* (E11, the undocumented off record, what AC6 verified or not), *Scope*, *Not here*, *Related decisions* (0017).

## Documentation updates

All by the Dev once the code is green, English first, then German in the same file:

- `README.md`: *Status* bullet (line 14) and *Companion* step 2 (line 103) name `Start with Windows` in the menu; a short paragraph under *Companion*: off by default, per-user, extract the ZIP to a folder of your own profile that stays (for example `%LOCALAPPDATA%\Programs\Censio`), not a shared or root-level folder others can write (N2), a folder path holding `%` or longer than 200 characters cannot be used for autostart, moving or a new version in another folder needs a click again, turn it off before deleting, how to remove a stale entry (AC7); the German counterparts.
- `docs/USER_GUIDE.md`: *Companion* step 2 (line 109) and *First start* (line 182) menu lists; a subsection *Start with Windows* under *Companion* with the AC7 content and the E9 SmartScreen result from AC6; *Data and logs* (line 188): the Companion writes one per-user autostart value only when turned on, and the log lines; the German counterparts (lines 308, 381, 387 and the new subsection).
- `docs/areas/companion.md`: *Areas* above.
- `docs/areas/README.md` (line 32): the companion row's scope names the autostart entry.
- `docs/ARCHITECTURE.md` (line 24): the Core list adds "autostart rules"; line 25: the shell adds "the registry store of the autostart value".
- `.squad/project.md` *Security areas*: a bullet "Windows autostart: `CompanionAutostart` writes only `HKCU\…\Run\Censio Companion` as the quoted `Environment.ProcessPath` without arguments, and removes only that value and its `StartupApproved\Run` value (`RegistryStartupEntryStore`, record 0017)"; *Test doubles*: `FakeStartupEntryStore` (the Tester).

## Architecture check

No guarantee in `.squad/project.md` (none recorded). The Core/shell split of record 0004 holds: rules in Core, Windows calls in the shell. No network, no new dependency, no new configuration option.

## Security considerations

- [x] Limits: the exe path is at most 200 characters and holds no `%` or `"`, checked on the path before quoting; the store values are read whole, the registry bounds them.
- [x] No exception reaches the user: every store exception is caught in `CompanionAutostart` and only logged (the exception text may name the registry key, never the exe path from this code).
- The value is written in the user's own hive; any process of the same user could write it already, so no privilege boundary moves. The guard is the quoting and the match rule above: the Companion never writes an unquoted or argument-carrying command and reports anything else as off.

## Decision records

- `docs/decisions/0017-companion-tray-app-autostart-and-connect.md` (Accepted, unreleased, extended in place): the storage options (Run value, Startup-folder shortcut, scheduled task), the choice, and the consequences (follows the last exe, undocumented off switch read fail-closed, policy invisible); `Area:` set to companion. The orchestrator sets it to the final state in step 9; no index change (the title stands).

## Challenge

- M1 (off-switch bytes without a real sample): accepted. Fact 10 is now measured by the Product Manager before step 5 (*Measurement before tests*); `IsTurnedOff` counts as on only what the measurement shows, `0x06`/`0x07` dropped unless seen; the Tester's rows cite the pasted output.
- m1 (the refresh delegate): accepted; *Approach* names `() => _autostart?.Refresh() ?? false` at `App.xaml.cs:244`.
- m2 (unquoted match): accepted; only the exact quoted form matches, OrdinalIgnoreCase; the unquoted rows are now no-match rows.
- m3 (warning per opening): decided: once until a read succeeds; tests `…RepeatedReadFailureLogsOnce` and `…FailureAfterSuccessLogsAgain`; the area document's *Limits and errors* says so.
- m4 (constructor, enum, contradictions): accepted; the three-argument constructor chains with `isChecked: false`, `ToggleAutostart` is the last enum member in *Affected projects* and *Signatures*.

## Security review

- B1 (facts 9, 10, 12 unmeasured): resolved by design and measurement. (a) and (b): `FormatCommand` refuses a path holding `%` or longer than 200 characters, so no test or record relies on expansion or cut-off (fact 9 keeps only spaces, `&` and `,`, which fail harmlessly and are checked in AC6). (c) and (d): measured by the Product Manager before step 5 (*Measurement before tests*); the Lead escalates for it. The guard list was re-checked whole: only the exact quoted form matches (guard table).
- N1 (`RegistryKey` behavior): accepted; the store's contract in *Approach* lists names via `GetValueNames`, checks the kind with `GetValueKind`, throws for another kind or a `null` read of a listed name, opens keys writable for writes and removals, reads back after `DeleteValue` and `SetValue`, and passes `SecurityException` through; the fake copies the contract; fact 13 rewritten.
- N2 (extract into the player's own profile): accepted; README and user guide say so (AC7, *Documentation updates*).

## Out of scope / follow-ups

As the spec's *Out of scope*. None to open.
