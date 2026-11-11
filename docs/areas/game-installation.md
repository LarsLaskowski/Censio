# Game installation

## Scope

Finding the Anno 117 installation folder and its build: the sources and their order, what counts as an installation,
the build, and the failures. The Companion does this once per start, and the player relies on the result shown in
the tray menu; the catalog work (#23 to #25) takes the folder and the build from it. The settings file itself is in
[`companion.md`](companion.md) (*Settings file*), the archives in [`game-files.md`](game-files.md).

## Behavior

### What counts as an installation

A folder is an installation when `maindata\config.rda` and `bin\win64\anno117.exe` below it exist as files; a folder
of that name does not count. A trailing separator and forward slashes in the folder are accepted.

### Search order

The search runs once per start, in every mode (the invalid command line included), in the background; the tray, the
pipe connection and `Exit` do not wait for it. Until it ends the menu line reads `Game: searching`. A change while the
Companion runs counts only after a restart, except the build (*Build*).

1. **The settings file.** A `gameFolder` that is a text (not blank) is used and nothing else is searched:
   - not a full path (relative, a bare drive letter, a variable such as `%USERPROFILE%`; variables are never
     expanded): `SettingsFolderNotFullPath`;
   - the folder does not exist: `SettingsFolderNotFound`;
   - the folder exists but is not an installation: `SettingsFolderNotInstallation`;
   - otherwise it is found, with the source *settings file*. A wrong folder is reported and never replaced by a
     searched one. An invalid settings file is reported in the same way (`SettingsFileInvalid`). No file, a blank
     value or no `gameFolder` key means no folder, and the search goes on.
2. **Ubisoft Connect's records.** The subkeys of `HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs` (read in the
   32-bit view, read-only), one per Ubisoft game, each named by a number with a plain string value (`REG_SZ`) `InstallDir` (for
   example `E:/Anno 117 - Pax Romana/`, forward slashes and a trailing slash as Ubisoft Connect writes it). A value
   of any other kind (an expandable string included), a missing value or one that cannot be read counts as no folder. The records are
   tried in the order of their numbers compared as numbers (`921` before `4553`), names that are not only ASCII digits
   after them in ordinal order. The first record whose `InstallDir` is a full path, is no UNC or device path and is an
   installation is found, with the source *Ubisoft Connect*; any other record is skipped. A registry that cannot be
   read counts as no records.
3. **Ubisoft Connect's default games folder**, `<Program Files (x86)>\Ubisoft\Ubisoft Game Launcher\games`: its
   top-level folders in ordinal order ignoring case; the first installation is found, with the source *Ubisoft
   Connect*. A folder that cannot be listed counts as none.
4. Otherwise `Game: not found`.

Steam is not searched. A player with another copy (Steam) names the folder in the settings file.

### Folders from outside

- **UNC and device paths:** no UNC or device path written in a record is followed (a path whose first character is `\`
  or `/` and whose second is `\`, `/` or `?`, both counted as separators); a mapped drive letter and a directory
  symlink are still followed. A UNC folder the player writes in the settings file is the player's own choice and is
  probed as written, over the network. With such a folder found, the Companion lists every archive below `maindata` with its size and modification time over the network (SMB) at the start and at each check, and reads `config.rda` and opens the archives over it only when it extracts the catalog ([`game-catalog.md`](game-catalog.md), *Limits and errors*).
- The Companion only reads: it checks that the two files exist, asks Windows for the file version of the program file,
  and writes nothing to the installation, the registry or the settings file. It reads no whole drive.

### Build

The build is the file version of `bin\win64\anno117.exe` as four numbers `Major.Minor.Build.Revision`, for example
`26.29905.4.51797`, not the version the game's main menu shows (`3.0.1733841` on the same installation). A file that
cannot be read, has no version or has all four numbers zero gives the build *unknown*; the installation is still
used. The same file version gives the same text at every start. Not verified: that the file version changes with
every game update.

The build is read again at each later check of the catalog, at a new pipe connection ([`game-catalog.md`](game-catalog.md),
*Cache*): only the file version of the program file of the installation found at the start, not a new search. The
`Game:` line then shows the build read last, the next time the menu is opened; no log line is written. A build that cannot be read
then is *unknown*. The build is part of the key of the catalog cache.

### Menu line

A greyed-out entry directly below `Last tick:` and above the first separator, not shown in the diagnostics window:

| Text | Meaning |
| ---- | ------- |
| `Game: searching` | the search has not ended |
| `Game: build <build> (Ubisoft Connect)` or `(settings file)` | found; `<build>` is `unknown` when there is none |
| `Game: not found` | nothing found in the records and the default folder |
| `Game: folder in settings not found` | `gameFolder` names a folder that does not exist |
| `Game: folder in settings is not a full path` | `gameFolder` is not a full path |
| `Game: folder in settings is not an Anno 117 installation` | the folder lacks the two files |
| `Game: settings file invalid` | the settings file cannot be used |

### Log

One line per search, with the folder as written (it may hold the Windows user name), sanitized and cut like every log
line ([`companion-log.md`](companion-log.md)); no game content is logged.

| Level | Message |
| ----- | ------- |
| Information | `Game installation found through <Ubisoft Connect or settings file>, build <build or unknown>, folder <folder>` |
| Warning | `Game installation not found; checked the Ubisoft Connect records and its default games folder` |
| Warning | `The game folder <folder> in the settings file does not exist` |
| Warning | `The game folder <folder> in the settings file is not a full path` |
| Warning | `The game folder <folder> in the settings file is not an Anno 117 installation; expected the folder that holds maindata\config.rda and bin\win64\anno117.exe` |
| Warning | `The settings file <file> is invalid: <problem>`, with the exception where there is one |

## Limits and errors

- The search always ends in a final status: an unexpected error is logged as `Game installation not found...` with its
  exception, and the menu shows `Game: not found`. Records and the settings file are untrusted input, read into
  results with a kind and failing closed; a damaged one never ends the Companion.
- No dialog is shown; the menu line and the log are the only messages.

## Related decisions

- [`decisions/0044-game-installation-found-through-ubisoft-connect-or-the-settings-file.md`](../decisions/0044-game-installation-found-through-ubisoft-connect-or-the-settings-file.md) — Ubisoft Connect's records and default folder, the settings file overrides, Steam not searched.
- [`decisions/0045-companion-settings-in-a-json-file-under-local-app-data.md`](../decisions/0045-companion-settings-in-a-json-file-under-local-app-data.md) — the override is a hand-edited JSON file.
- [`decisions/0046-game-build-is-the-program-files-version.md`](../decisions/0046-game-build-is-the-program-files-version.md) — the build is the program file's version.

## Not here

- The settings file's format and reading rules: [`companion.md`](companion.md) (*Settings file*).
- The archives below the installation and their reading: [`game-files.md`](game-files.md).
- The catalog built from the installation: [`game-catalog.md`](game-catalog.md); the cache keyed by the build: [`game-catalog.md`](game-catalog.md), *Cache*.
