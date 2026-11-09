# Game installation samples

Two samples the Product Manager took on 2026-11-09 from his own Anno 117 installation through Ubisoft Connect, on
Windows 11 Pro 26H2. The search for the game installation is built from them
([`game-installation.md`](../../../../docs/areas/game-installation.md); spec of feature #22, N8). Both are kept exactly as
he supplied them; do not reformat, re-encode or translate them.

Neither holds a private value: the drive letter `E:`, the games' folder names, the language `de-DE`, Ubisoft's game
ids (`4553`, `921`), the game's versions and the Windows build name no person, account or machine.

## Samples

| Sample | What it is |
| ------ | ---------- |
| `ubisoft-connect-installs.reg` | `reg export` of the whole key `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs`: UTF-16 LE with a byte order mark and CRLF line ends. Git treats it as binary (it holds NUL bytes), so no line-end conversion touches it. |
| `game-installation.md` | The Product Manager's note, in German, with the facts he read on his machine; read by no test. |

## `ubisoft-connect-installs.reg`

- The header line `Windows Registry Editor Version 5.00`, then the key itself without values.
- One subkey per Ubisoft game, named by Ubisoft's game id, each with the string values `Language` (`de-DE`) and
  `InstallDir`:
  - `4553` (Anno 1800): `"InstallDir"="E:/Anno 1800/"`
  - `921` (Anno 117 - Pax Romana): `"InstallDir"="E:/Anno 117 - Pax Romana/"`
- `InstallDir` is written with forward slashes and a trailing slash.
- The names compare differently as text and as numbers: by text `4553` sorts before `921`, by number after. The file
  lists `4553` first.

Read through the test double `RegExportUbisoftInstallRecords` (byte order mark detected, one record per subkey, `InstallDir` with `\\` and `\"` unescaped, other values ignored) by:

- `RegExportUbisoftInstallRecordsReadsSample` (in `RegExportUbisoftInstallRecordsTests`), which expects exactly the two
  records above, in file order, with `InstallDir` verbatim.
- `GameInstallationSearchSampleRecordsTakeNumericFirst` (in `GameInstallationSearchTests`), which keeps the record names
  and replaces each `InstallDir` by a temporary folder named after the record: with both installations present the
  search takes `921`'s folder, with only `4553`'s it takes that one.

## `game-installation.md`

The note states, in German:

- The `InstallDir` value of Anno 117 (`E:/Anno 117 - Pax Romana/`), on its first line.
- Ubisoft Connect's default games folder `C:/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/`, which the user
  can change (his own is `E:\`).
- The game's version is shown only in the game's main menu, not in Ubisoft Connect.
- The program file lies in `bin\win64` (`anno117.exe`); its file version is `26.29905.4.51797`, followed by the
  PowerShell `VersionInfo` output of the file: the four file and product version parts set, every version string
  empty.
- The last line: the date `09.11.2026`, the main-menu version `3.0.1733841`, and the Windows version
  (`Windows 11 Pro 26H2`, build `26300.9818`).

No test reads it; the plan of feature #22 cites it in *Verified facts* (facts 2, 4, 5 and 6).
