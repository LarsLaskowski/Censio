# Companion

## Scope

The Companion process on Windows: its tray icon, tooltip and menu, how it exits, how many instances run, the
version it shows, and the release ZIP it ships in. The user who starts the Companion relies on it.

## Behavior

### Tray icon and menu

- Started, the Companion shows one icon in the notification area with the tooltip `Censio Companion`. It opens
  no window and shows no taskbar button.
- The context menu of the icon holds exactly three entries: `Version <version>` (greyed out, nothing happens on
  click), a separator and `Exit`.
- `<version>` is the informational version the Companion was built with: `0.0.0` without a version, otherwise
  the build property `Version` exactly as passed.
- `Exit` removes the icon and ends the process with exit code 0.
- When Windows Explorer restarts, the icon appears again once the notification area is back. Sign-out and
  shutdown end the Companion without a dialog.

### Single instance

- Only one Companion runs per Windows user session. A second start in the same session ends at once with exit
  code 0, without a message and without a second icon; the first keeps running.
- A start in another user session (another signed-in user) is not blocked and has its own icon.

### Platform

- The Companion runs on Windows x64. Every build writes `Censio.Companion.exe`, on Linux too; it runs only on
  Windows, and a local build needs the .NET 10 Desktop Runtime (the release ZIP below does not).
- Every release attaches the ZIP `Censio-<version>-win-x64.zip`. It holds exactly one file at its root, the
  self-contained single-file `Censio.Companion.exe` with the native libraries embedded, and needs no installed .NET,
  installer or administrator rights, on Windows 10 22H2 and Windows 11, x64. The exe is not signed, so SmartScreen
  asks once ("More info", "Run anyway"). Its tray menu shows `Version <version>`; the exe's file properties show no
  product version.

## Limits and errors

- The Companion shows no error message and logs nothing yet.

## Related decisions

- [`decisions/0004-companion-shell-and-linux-build.md`](../decisions/0004-companion-shell-and-linux-build.md) — logic in a platform-neutral project, the Windows shell builds on Linux.
- [`decisions/0005-product-version-from-one-build-property.md`](../decisions/0005-product-version-from-one-build-property.md) — the version comes from the build property `Version`.
- [`decisions/0012-release-pre-release-first-and-secret-free-builds.md`](../decisions/0012-release-pre-release-first-and-secret-free-builds.md) — the release ZIP is built on Linux without secrets and checked before it is attached.
- [`decisions/0027-companion-zip-self-contained-single-file-unsigned.md`](../decisions/0027-companion-zip-self-contained-single-file-unsigned.md) — self-contained single-file exe in a ZIP, not signed.

## Not here

- Reading the statistics pipe and the game files, the connection to the server, settings and autostart (later issues).
