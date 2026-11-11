# Censio user guide

Censio reads the statistics of a running Anno 117 game on Windows and shows them live and over time in a web application: a Windows Companion extracts the data, a Blazor Server application stores and displays it. The status and the quick start are in the [README](../README.md).

Deutsche Version: [Censio-Benutzerhandbuch](#censio-benutzerhandbuch)

## Installation

### Server (Docker)

The Censio server runs as the Docker image `networlddev/censio` (linux/amd64) on Docker Hub. Every release publishes the tag `<version>` (for example `0.1.0`) and moves the tag `latest` to the newest release. The versions are listed on the release page `https://github.com/LarsLaskowski/Censio/releases` and the tags on `https://hub.docker.com/r/networlddev/censio/tags`. You need Docker 23 or later (Linux Docker Engine, or Docker Desktop for Windows with the WSL 2 backend and Linux containers). The commands below use the version `0.1.0`; replace it with the version you want, or with `latest`. Run the commands one at a time: a command that fails makes the next one fail too. Not yet tried: pulling and running the published image `0.1.0`.

#### Pull

```text
docker pull networlddev/censio:0.1.0
```

`docker run` pulls a missing image itself, so this step only fetches it in advance. A failed pull leaves nothing half-installed.

#### Run

Start the container with a named volume for the data directory `/config`:

```text
docker run -d --name censio -p 8080:8080 -v censio-config:/config networlddev/censio:0.1.0
```

Check that it answers (the response is `Healthy`; not yet tried: in Windows PowerShell 5.1 `curl` is an alias of `Invoke-WebRequest`, which should show `StatusCode` `200` and `Content` `Healthy` and may ask for a confirmation first) and read its log:

```text
curl http://localhost:8080/health
```

```text
docker logs censio
```

Open `http://localhost:8080/` for the start page. After a few seconds `docker ps` shows the container as `(healthy)`: the image probes `/health` itself every 10 seconds. If a run failed, remove the container before starting it again, because its name is taken:

```text
docker rm -f censio
```

#### Port

Inside the container the server listens on port 8080. If port 8080 is taken on the host, Docker reports "port is already allocated"; map another host port:

```text
docker run -d --name censio -p 9000:8080 -v censio-config:/config networlddev/censio:0.1.0
```

To change the port the server itself listens on, set `ASPNETCORE_HTTP_PORTS` (the health probe follows the first port of the list; `ASPNETCORE_URLS` is not supported by the image):

```text
docker run -d --name censio -e ASPNETCORE_HTTP_PORTS=9090 -p 9090:9090 -v censio-config:/config networlddev/censio:0.1.0
```

#### Data directory and NAS hosts

`/config` is the directory for the server's data; it holds the data-protection key ring in `/config/data-protection-keys`. The image sets `CENSIO__DATADIRECTORY=/config`; point it to another absolute path with `-e CENSIO__DATADIRECTORY=<path>`. It cannot be switched off in the image: `docker run -e` cannot unset it and an empty value stops the start. The image declares no `VOLUME`, so every command above passes `-v`. A named volume (`censio-config`) is the default and the only form on Docker Desktop for Windows. The container never starts as root: it runs as user `1654`.

On a NAS whose shares belong to another user (Unraid `nobody:users` is `99:100`, a Synology user such as `1026:100`), start the container with that user and group; replace the path with your share:

```text
docker run -d --name censio --user 99:100 -p 8080:8080 -v /mnt/user/appdata/censio:/config networlddev/censio:0.1.0
```

Or hand the directory to the image's user and start without `--user`:

```text
chown -R 1654:1654 /mnt/user/appdata/censio
```

```text
docker run -d --name censio -p 8080:8080 -v /mnt/user/appdata/censio:/config networlddev/censio:0.1.0
```

A directory the container user cannot write stops the container at start with exit code 1; `docker logs censio` then shows `The data-protection key ring cannot be stored in '/config/data-protection-keys'; the server stops. Make the data directory writable for the user the server runs as.` A value of `CENSIO__DATADIRECTORY` that is not an absolute path also stops it with exit code 1; `docker logs censio` then shows `The data directory (Censio:DataDirectory) must be an absolute path, not '<value>'; the server stops.` Pull the image on the NAS itself; `docker run` does it when the image is missing:

```text
docker pull networlddev/censio:0.1.0
```

Building the image from source and starting it with the commands of this guide was tried on Docker Desktop for Windows in PowerShell 7. An automated check on Linux Docker Engine runs the `--user 99:100` command with a bind mount owned by `99:100`; Windows PowerShell 5.1, the other commands of this section (the `chown` remedy, the pull on the NAS), the stop at start for an unwritable directory on NAS hardware, and Unraid or Synology hardware have not been tried yet.

#### Network and TLS

Inside the image the server listens on all interfaces of the container, on plain HTTP, with no login on the start page and `/health`. The `-p` mapping decides who can reach it: `-p 8080:8080` publishes it on every interface of the host. To reach it from the host alone, bind the mapping to the loopback address:

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.1.0
```

On Linux a published port bypasses host firewalls such as ufw and firewalld, so restrict access with the mapping, not with the firewall. TLS is the job of a reverse proxy in front of the server; see [SECURITY.md](../SECURITY.md).

#### Limits

- Only linux/amd64; arm64 hosts (including ARM-based Synology models) and Windows containers are not supported.
- The image runs with invariant globalization and has no time zone data: `TZ` has no effect and log times are UTC.
- ASP.NET Core's data-protection keys lie unencrypted in `/config/data-protection-keys`; keep the directory and its backups private. ASP.NET Core logs the warning `No XML encryptor configured` when the server creates the first key.
- The server logs to the console; read it with `docker logs censio`.

### Companion

The Companion is the Windows program that reads the statistics of the running game. Download `Censio-<version>-win-x64.zip` from the release page `https://github.com/LarsLaskowski/Censio/releases` (under *Assets* of the release). It needs Windows 10 22H2 or Windows 11, 64-bit; it needs no installed .NET, no installer and no administrator rights. Windows other than these and arm64 are not supported. Not yet tried: the ZIP of `v0.1.0` on a machine without .NET.

1. Extract the ZIP to a folder of your choice. It holds one file, `Censio.Companion.exe`.
2. Start `Censio.Companion.exe`. A tray icon appears; its menu shows the status, the last tick, the game installation (`Game: …`, see *Game installation* below), the game catalog (`Catalog: …`, see *Game catalog* below), `Open diagnostics`, `Start recording`, `Open recordings folder`, `Open log folder`, `Start with Windows`, `Version <version>` and `Exit`.

The program is not signed, so Windows SmartScreen may show "Windows protected your PC" the first time. Choose "More info", then "Run anyway" (on a German Windows: "Weitere Informationen", then "Trotzdem ausführen"). Tip reported by the Product Manager from a German Windows, not tried against a real release download: open the properties of the extracted `Censio.Companion.exe` (or of the ZIP itself), tick *Zulassen* (on an English Windows probably *Unblock*; not yet tried) and apply; SmartScreen then does not ask.

#### Start with Windows

The Companion does not start with Windows unless you turn it on: choose `Start with Windows` in the tray menu. A check mark shows that it is on; choosing the entry again turns it off. Turning it on writes one value, `Censio Companion`, under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`: it applies to your Windows account only, and the entry needs no administrator rights (the maintainer tried it on Windows 11: no administrator prompt appeared). The value is the full path of the running `Censio.Companion.exe` in quotes, without arguments, so the Companion starts the way a double click starts it (it neither records nor replays).

- Extract the ZIP to a folder in your own profile that stays where it is, for example `%LOCALAPPDATA%\Programs\Censio`. Do not use a shared folder or the root of a drive that other users can write to: whoever can replace the file there runs it at your next sign-in.
- A folder path that contains `%` or is longer than 200 characters cannot be used: the entry stays unchecked, nothing is written and the log says `Could not change the autostart setting`.
- If you move the exe, or start a new version from another folder, the old entry still points to the old file and the menu shows the entry unchecked. To remove such a leftover entry, start any copy of `Censio.Companion.exe`, choose `Start with Windows` (it replaces the leftover entry with the path of that copy), then choose it again to turn it off; or delete the value `Censio Companion` in the Registry Editor at the key above. Turn the entry off before you delete the folder. Switching the old program off in the Windows startup apps list does not delete the value.
- A value with another path or with arguments counts as off; choosing the entry then replaces it.
- If you switch the Companion off in the Windows startup apps list (Settings, Apps, Startup), the menu does not show it and still shows the check mark; switch it on again in that list. A policy of your organization that blocks startup programs is not visible to the Companion either.
- The maintainer tried this on Windows 11: the Companion started with Windows after a restart with the entry on and did not start with it off; no administrator prompt appeared (under a standard account); folders with spaces, `&` or `,` in the path worked; a second Windows account was unaffected; and turning the entry off removed it. Not tried: whether SmartScreen asks at the first automatic start, which name the startup apps list shows, and a folder path with `%`.

#### Connecting to the game

The game creates its statistics pipe only when it is started with the launch argument `/pipe`. Without it the Companion cannot connect, and it looks the same as a game that is not running: the tooltip stays at `Censio Companion: Waiting for game` although the game runs. Start the Companion before or after the game; it needs no restart when the game is quit and started again. It only reads from the pipe and never writes to it.

**Adding `/pipe`.** The argument takes effect at the next start of the game: if the game runs, quit it, add the argument and start it again.

- *Ubisoft Connect (every copy of the game, also one bought on Steam, which starts Ubisoft Connect too):* open Ubisoft Connect, choose *Library*, click the three dots on Anno 117 to open its pop-up menu, choose *Manage*, then *Properties*, type `/pipe` into the field *Add launch arguments* and choose *Close*.
- *Steam (alternative for a Steam copy):* in the *Library* right-click Anno 117, choose *Properties...* in the context menu, then under *General* type `/pipe` into the field *Launch Options*. If the tooltip stays at `Waiting for game` with this option set, the argument may not reach the game through Ubisoft Connect: add `/pipe` in Ubisoft Connect as above.

The Ubisoft Connect path was tried by the Product Manager on Windows 11 (26H2) with Ubisoft Connect in German: *Bibliothek*, the three dots on the game (pop-up menu), *Verwalten*, *Eigenschaften*, field *Startargumente hinzufügen*, `/pipe` entered, *Schließen*. The Product Manager also confirmed the English labels of this path in an English Ubisoft Connect: *Library*, the three dots, *Manage*, *Properties*, field *Add launch arguments*, *Close*. The Product Manager confirmed the labels of the Steam path up to the field as well, in German (*Bibliothek*, right-click on the game, *Eigenschaften*, *Allgemein*, field *Startoptionen*) and in English (*Library*, right-click on the game, *Properties...*, *General*, field *Launch Options*). Not yet tried: whether the argument reached the game after saving, and whether `/pipe` set in Steam reaches the game through Ubisoft Connect.

Hover over the tray icon: the tooltip reads `Censio Companion: <state>`. As soon as the game has sent a complete tick, a second line shows `Last tick: <time>, <n> islands` (the time when it arrived, as `HH:mm:ss` of today, otherwise with the date). The icon shows the state too: a grey ring while waiting (`Waiting for game`, `Connected`, `Pipe busy`), a green circle while `Receiving`, a red triangle for `Protocol mismatch`, `Error` and an invalid command line. Right-click the icon for the menu, which shows the same status and last tick.

The menu entry `Open diagnostics` opens a window with the last tick: a header with `Status:`, `Last tick:` and `Game time:`, and a table with one row per good of each island (region, island, area, session ID, name, good, generation, consumption, delta, perfect generation, perfect consumption, buildings, maintenance, income, profit and the two productivity values). Once the catalog is ready (*Game catalog* below) the `Region` and `Good` cells show `<English name> / <German name> (<GUID>)`, or the one name there is followed by the GUID; where the catalog has no name, and before it is ready, they show the raw number (GUID) the game sent, as the islands and all other values always do; the same cells show the game's icon in front of the text where the game's files give one (a small image, *Game catalog* below); numbers use the decimal point on every Windows. The window follows each new tick, also in a replay, and keeps the last tick when the connection ends. Closing it (close button or Alt+F4) leaves the Companion running; `Open diagnostics` opens it again with the last tick, and while it is open it brings the same window to the front. While it is open it has a taskbar button.

| Tooltip | Meaning | What to do |
| ------- | ------- | ---------- |
| `Waiting for game` | The pipe does not exist: the game is not running or was not started with `/pipe`. The Companion keeps looking (pauses grow from 1 to 10 seconds) | Start the game. If the game runs, check first that `/pipe` is saved in the launcher's field for launch arguments (*Adding `/pipe`*) and restart the game: the argument takes effect only at the next start. If it stays at `Waiting for game` with Steam's launch option set, add `/pipe` in Ubisoft Connect |
| `Connected` | The pipe is open; the game has not yet sent its first message. The game sends it at once, so you will hardly ever see this state | Nothing |
| `Receiving` | The game is sending data | Nothing |
| `Pipe busy` | The pipe exists but another program holds it (for example another pipe tool); the game serves one client at a time, so close other pipe readers. The Companion keeps retrying (pauses grow from 1 to 10 seconds) and shows `Receiving` at the next attempt after the pipe is free. It can also stay at `Pipe busy` after you restart the Companion while the game is in the main menu, until you load a game | Close the other pipe tool, or load a save if you restarted the Companion in the game's main menu |
| `Protocol mismatch` | The pipe speaks another protocol version than 2 (for example after a game update); the Companion stays connected and continues when version 2 arrives | Wait for version 2; if it does not go away, report it (*Reporting problems*) with the version from the tray menu and the log of the day, without the recording |
| `Error` | Connecting failed or the connection broke off. When you quit the game, `Error` can show briefly before `Waiting for game`; that is normal | If it does not go away, report it as for `Protocol mismatch` |
| ` (recording)` after the state | A recording is on (the entry reads `Stop recording`) | Nothing; choose `Stop recording` when you are done |
| ` (recording failed)` after the state | Writing the recording failed (for example the disk is full or too slow); recording stopped, the Companion keeps working | Free disk space, then choose `Start recording`: it tries again with a new file |

When the game quits, the Companion returns to `Waiting for game` and connects again by itself when the game is back; loading another save and a return to the main menu keep the connection. `Exit` in the tray menu works in every state and ends the program within 2 seconds.

#### Game installation

The Companion looks for your Anno 117 installation once at every start, in the background; the tray menu and `Exit` work while it runs. The result is the greyed-out menu line `Game: …` directly below `Last tick:`, for example `Game: build 26.29905.4.51797 (Ubisoft Connect)`. The Companion only reads: it writes nothing to the game, the registry or your settings file. The line is not shown in the diagnostics window.

**Search order.**

1. The folder named in the settings file (below). If it is set it is used, and nothing else is searched.
2. Ubisoft Connect's records of installed games (the registry key `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs`, read only): the first valid record by number is used, and a record that points at a folder without the game is skipped. A record that points at a network (UNC) or device path is never followed; a mapped drive letter or a folder link still is.
3. Ubisoft Connect's default games folder, `C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games` (below the 32-bit Program Files folder).

An installation is a folder that holds `maindata\config.rda` and `bin\win64\anno117.exe`. Steam is not searched: with a Steam copy, or a game in another folder, use the settings file. A change counts after the next start of the Companion.

**The build** is the file version of `bin\win64\anno117.exe` as four numbers, for example `26.29905.4.51797`. It is not the version the game's main menu shows (`3.0.1733841` on the same installation), and Ubisoft Connect shows none. Not yet verified: that the build changes with every game update.

**The settings file.** Create `%LOCALAPPDATA%\Censio\settings.json` (type that path into the Explorer address bar; the folder `Censio` exists once the Companion has run) with your game folder, for example:

```json
{
  "gameFolder": "C:\\Games\\Anno 117"
}
```

- Write a full path and double every backslash, or use forward slashes (`"C:/Games/Anno 117"`). The folder is the one that holds `maindata` and `bin`, not `maindata` itself. Variables such as `%USERPROFILE%` and a bare drive letter (`"C:"`) are not full paths. A network path (`\\\\server\\share\\Anno 117`) you write here is used as written, and Windows then contacts that server.
- The key `gameFolder` is read ignoring case (`GameFolder` works); any other spelling, such as `game_folder`, is an unknown key, which is ignored: the automatic search runs and the menu then shows `(Ubisoft Connect)` or `Game: not found`, not `(settings file)`. Other keys are ignored. A blank `gameFolder` counts as not set.
- The file is UTF-8 (Notepad's byte order mark is fine), comments (`//`, `/* */`) and trailing commas are accepted, and it may be up to 1 MiB. Edit it with the Companion closed and start it again.
- A wrong folder in the file is never replaced by a searched one, so that you see the mistake.

**Menu texts.**

| Text | What to do |
| ---- | ---------- |
| `Game: searching` | Wait; it ends by itself. A slow or sleeping drive can take a while. |
| `Game: build <build> (Ubisoft Connect)` | Nothing; found through Ubisoft Connect. |
| `Game: build <build> (settings file)` | Nothing; the folder of your settings file is used. |
| `Game: build unknown (…)` | The installation is used, but its program file has no readable version. Nothing to do. |
| `Game: not found` | Neither Ubisoft Connect's records nor its default folder hold the game (for example a Steam copy). Name the folder in the settings file. |
| `Game: folder in settings not found` | The folder in `gameFolder` does not exist. Correct the path (a variable inside a path is not expanded, so it is not found either). |
| `Game: folder in settings is not a full path` | Write the full path, starting with the drive letter, for example `C:\\Games\\Anno 117`. |
| `Game: folder in settings is not an Anno 117 installation` | The folder lacks `maindata\config.rda` or `bin\win64\anno117.exe`. Name the folder that holds `maindata` and `bin`. |
| `Game: settings file invalid` | The file is not valid JSON or not an object, `gameFolder` is not a text, the key occurs twice, the file is larger than 1 MiB or it cannot be read. The log names the reason. |

The log (`Open log folder`) names the cause of every problem and, when the game is found, the source, the build and the folder.

#### Game catalog

After the game search, the Companion loads the names of regions, goods, workforce and buildings in English and German from its cache or, when there is none that fits, reads them from your own game installation (the file `maindata\config.rda`), in the background, at every start; the tray menu, the connection and `Exit` do not wait for it. It only reads the game's files: nothing is written into the installation, and the names stay on your computer (they are not sent anywhere, not written to the log and not part of any release). The only copy is the cache file described below. The greyed-out menu line `Catalog: …` directly below `Game: …` shows the state:

| Menu line | What to do |
| --------- | ---------- |
| `Catalog: reading` | Wait; it ends by itself. It also shows while the game search runs. |
| `Catalog: checking` | Wait; the Companion compares the game with the cache. |
| `Catalog: extracting names` | Wait; the names are read from the game files. The names shown before stay until the new ones are ready. |
| `Catalog: extracting icons, <n> entries` | Wait; the names are ready and shown, the icons follow. |
| `Catalog: <n> entries` | Nothing; the diagnostics window now shows names and icons. |
| `Catalog: <n> entries, icons failed` | The names work; the icons could not be read (see the icon paragraph below). Nothing is cached, so the next start reads the files again. |
| `Catalog: <n> entries, not cached` | Names and icons work, but the cache could not be written (disk full, no access to `%LOCALAPPDATA%\Censio\Cache`); the log line `Catalog cache not written` says why. The next start reads the game files again. |
| `Catalog: no game` | No installation was found (see *Game installation*); GUIDs stay numbers. |
| `Catalog: failed` | A game file could not be read or has an unexpected structure, for example after a game update. GUIDs stay numbers; the log line `Catalog failed: <kind>` names the kind (`ArchiveUnreadable`, `FileMissing`, `FileUnreadable` or `MalformedFile`); report it with the game build (*Reporting problems*). |

A GUID the catalog cannot name stays a number in the diagnostics window. For every such GUID of a tick the log holds, once for each catalog (so again after an extraction because of a game update), one line `Catalog cannot name <Region|Good|Workforce|Building> <GUID>: <Unknown|Incomplete|Nameless|Ambiguous>` (unknown: not in the game files; incomplete: only one language has a name; nameless: no name; ambiguous: the files define it more than once), never a name. The log also says how many entries were built, and names an archive that holds a second copy of one of the catalog's files (`Catalog: <archive> also lists <path>; the copy in config.rda is used`). If the game folder in the settings file is on a network drive written as `\\server\share`, the Companion lists the game archives over the network at every start and at every check, and reads them over it when it extracts.

Cache: after a complete extraction (names and icons) the Companion saves them in the file `%LOCALAPPDATA%\Censio\Cache\catalog.bin`, one file for the installation extracted last. The next start loads it in a moment without reading the game's archives, if the game folder, the game build and the name, size and modification time of every `.rda` file below `maindata` are the same, and the Companion itself was not updated; otherwise the cache is not used, the log line `Catalog cache not used: missing`, `game changed` or `Companion changed` says why, and the Companion extracts again and replaces the cache. A damaged or foreign file is never used in part: the warning `Catalog cache not used: invalid` is logged and the Companion extracts again. When the game is updated while the Companion runs, start it with the launch argument `/pipe` as usual: at each new connection the Companion checks the game, and if it changed, `Catalog: extracting names` shows again while the old names stay, and the cache is replaced; an unchanged game does nothing. Measured on the maintainer's installation (1,040 entries; your computer will differ): about 7 s to extract (names 2.5 s, icons 4.5 s) and about 2.4 s to load from the cache. The log lines `Catalog extracted in <t> s (names <a> s, icons <b> s)` and `Catalog loaded from cache in <t> s: …` give the times of your computer. The cache holds your game's names and icons, so it is private like a recording: it is not sent anywhere, not written to the log and not part of any release, you should not share it, and it stays on the computer when you remove the Companion, so delete the folder `%LOCALAPPDATA%\Censio\Cache` by hand then. If a game update went unnoticed (an archive rewritten with the same name, size and time), close the Companion, delete that folder and start again. A game started without `/pipe`, a replay and an invalid command line are only checked at the start.

Icons: once the names are shown, the Companion also reads the icons of goods and regions from the game's picture archives (`maindata\ui.rda` and the `zz_patchfiles_<n>.rda` files next to it, the highest number first), converts them in memory and shows each one at 20 by 20 pixels in front of the name in the `Region` and `Good` cells; region icons are shown on a dark round backdrop, because the game draws them as light glyphs. Where the game's files give no icon, or one that cannot be read, the cell shows the text alone, without backdrop; the names never wait for or depend on the icons. The icons are saved only in the cache file above: they are not sent, not written to the log and not part of any release. The log has one line per extraction, `Catalog icons: <g> goods and <r> regions with an icon; without one: <a> not named, <m> missing, <u> unreadable` (counts of entries), or the warning `Catalog icons failed: <kind> (<archive>)` with one of the kinds `ArchiveMissing`, `ArchiveUnreadable`, `DuplicateArchiveNumber` or `IconSetTooLarge`; then no icon is shown, and the names are unaffected. After a game update the counts show whether icons went missing; report such a line with the game build (*Reporting problems*). If the game folder is on a network drive, the picture archives are read over the network at every extraction as well.

#### Recording and replay

For testing and bug reports the Companion can keep what the game sends and play it back later. Both can be started with a switch on the command line (for example `Censio.Companion.exe --record` in a terminal or in a shortcut's target); a recording can also be started and stopped from the tray menu (`Start recording`, `Stop recording`), a replay only with its switch. Close a running Companion first (`Exit` in the tray menu): a second start ends at once without a message, and nothing replays.

- **Record:** `Censio.Companion.exe --record` starts with the recording on; without the switch choose `Start recording` in the tray menu at any time, also while the game is already sending. The Companion connects to the game as usual and writes every connection to its own file in `%LOCALAPPDATA%\Censio\Recordings` (the menu entry `Open recordings folder` opens it). A file `recording-<yyyyMMdd-HHmmss>Z.jsonl` (UTC time of the first data; a taken name gets `-2`, `-3`, … before `.jsonl`) is created when the game's first data arrives after the start. Until then the tooltip already ends with `(recording)`, but the folder may not exist and holds no file: that is no failure. When the game is quit and started again, the next connection writes a new file, and every new start writes a new file, so one session can span several files. While recording, the tooltip ends with `(recording)` and the menu entry reads `Stop recording`; `Stop recording` writes what is still waiting and closes the file. If writing fails (for example the disk is full or too slow), recording stops, the tooltip ends with `(recording failed)` and the Companion keeps working; `Start recording` tries again with a new file. `Exit` in the tray menu writes what is still waiting, for at most 1 second, and ends the program. If the Companion is killed or the computer loses power, the last lines of a file can be missing; whether Windows sign-out or shutdown writes them is not tried.
- **Replay:** `Censio.Companion.exe --replay <file> [--speed <factor>|max]` reads a recording from a local file instead of the game's pipe and plays it back as the game sent it. `--speed` is `max` (no pauses) or a number from `0.1` to `1000`, written with a decimal point also on a German Windows (`--speed 10` pauses a tenth as long); without it the replay runs at real speed. The Companion does not open the pipe while it replays, and the file is only read. The tooltip, the last tick, the icon and the window `Open diagnostics` show the progress, and the window shows the replayed ticks and keeps the last tick of the file (`Start recording` is greyed out during a replay): `Replay: Connected`, then `Replay: Receiving` (or `Replay: Protocol mismatch`), and after the last frame `Replay finished` until `Exit`. A problem with the file shows as `Replay error: <reason>` with `file not found`, `not a recording`, `unsupported version`, `invalid line <n>` or `read failed`.

  To check a finished recording, which needs no console:

  1. Choose `Exit` in the tray menu. A Companion that still runs makes the replay start end at once, without a message.
  2. In Explorer, copy the full path of `Censio.Companion.exe` and of the recording file, each with "Copy as path" (right-click; on Windows 10 Shift + right-click).
  3. Press the Windows key + R, and in the *Run* dialog enter the line `"<path of Censio.Companion.exe>" --replay "<path of the file>" --speed max`, pasting both quoted paths. The quotes stay, so paths with spaces and `%LOCALAPPDATA%` already expanded by Explorer work.
  4. The tooltip shows `Censio Companion: Replay: Receiving` and ends at `Censio Companion: Replay finished`. That means the file is readable.
  5. Choose `Exit`: it ends the replay. Start `Censio.Companion.exe` as usual to get the Companion back; `Start with Windows` starts it only at the next sign-in.

  Tried by the Product Manager on Windows 11 (26H2) with a Companion built from source with the documented publish command: this procedure with `Exit` first and the *Run* dialog, which works. Not reported separately: the exact quoting and path details of the line, and a replay started while the tray Companion still runs.
- **Invalid command line:** an unknown argument, `--speed` without `--replay`, a speed outside the range, `--replay` without a file, or `--record` together with `--replay` show `Censio Companion: Invalid command line`; the Companion then neither records nor replays, and `Exit` works.

**A recording is private.** It holds the name of the player figure (which can be your real name), island names and economic data exactly as the game sent them. It stays in the folder on your computer, the Companion never sends it anywhere and never deletes it. Do not attach or post a raw recording publicly (not in an issue either); only anonymised fixtures are meant to be shared (*Anonymising a recording (contributors)*). Keep a recording until a maintainer asks how to hand it in (*Reporting problems*). Delete the files yourself when you no longer need them.

##### Anonymising a recording (contributors)

A contributor who wants to commit a recording as a test fixture anonymises it first. The tool is a console program of the repository, not part of the release or of the Companion ZIP, so it needs a checkout of the repository and the .NET SDK (as in *Build from source*). Testers who are not contributors do not need it: they keep their raw recordings and hand them in privately when a maintainer asks.

1. Stop the recording (`Stop recording` or `Exit`). A file the Companion still writes cannot be opened and gives `file not found`.
2. In the root directory of the repository run `dotnet run --project src/Censio.Anonymiser -c Release -- <recording> <output>` with the recording file and a new output file name. The output must not exist; it is never overwritten.
3. On success the tool prints `Anonymised <n> frames to <output>` and exits with code 0. The original is not changed.

What changes: every island and figure name becomes `Island <n>` or `Player <n>` (one original name always the same invented one), the amounts of every entry change (generation, consumption, income and productivity by 1 % to 20 %; maintenance and workforce amounts by 1 % to 20 % and at least 1, below 5 by exactly 1, with 1 becoming 2; delta, profit and average productivity follow), and every time moves earlier by a random offset of 30 to 365 days. What stays: the frame types and order, the game times, the pauses, the island and product GUIDs, the building counts and the workforce and building GUIDs. A replay of the output shows the same ticks as the original.

Errors are one line on standard error, `Error: <reason>`, with exit code 1: `file not found`, `not a recording`, `unsupported version`, `invalid line <n>`, `read failed`, `frame cannot be anonymised in line <n>` (a frame the tool does not decode, or any frame but `Version` before a `Version 2` frame or after another version), `output file exists`, `output is the input`, `write failed` (for example a folder that does not exist) and `cancelled` (Ctrl+C). Without exactly two arguments the tool prints `Usage: Censio.Anonymiser <recording> <output>` and exits with code 2. After an error no output file is left.

Check the output before you commit it: a name or a number you know from the game must not appear in it. Do not commit or post the original recording.

### Build from source

To run a state of the repository that has not been released, build it from a checkout of this repository, with the commands run in its root directory. No edge builds are offered. In the other `docker run` commands of this guide, add `--pull never` and use the local name `censio` instead of `networlddev/censio:0.1.0`.

#### Build the server image

This builds the current state of the repository into a local image. It needs Docker 23 or later with BuildKit and access to `mcr.microsoft.com`. Run it in the root folder of the checkout (see [Getting Censio](../README.md#getting-censio)):

```text
docker build -f src/Censio.Server/Dockerfile -t censio .
```

The build stamps the version `0.0.0`; pass another one as a semantic version (`MAJOR.MINOR.PATCH`, optionally with pre-release and build parts), anything else fails the build with `VERSION is not a semantic version`:

```text
docker build -f src/Censio.Server/Dockerfile --build-arg VERSION=1.2.3 -t censio .
```

Start the local image. `--pull never` makes Docker refuse to start when the build failed and no local image `censio` exists, instead of pulling a foreign image of that name:

```text
docker run -d --name censio --pull never -p 8080:8080 -v censio-config:/config censio
```

Then check `http://localhost:8080/health` (`Healthy`) and `http://localhost:8080/` as above; the start page shows `Version 0.0.0`. The maintainer built and ran the image with these steps and `/health` answered `Healthy`. Not yet tried: the refusal of `--pull never` after a failed build.

#### Build the Companion

Building needs the .NET 11 SDK (version 11.0.100 or a later patch or feature band), access to `nuget.org` and the source (see [Getting Censio](../README.md#getting-censio)), on any operating system the SDK supports; only running the program needs Windows 10 22H2 or Windows 11, x64. Run the command in the root folder of the checkout. It uses the flags of the release build, except the version, and writes to the folder `publish`:

```text
dotnet publish src/Censio.Companion/Censio.Companion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -o publish
```

The folder `publish` then holds only `Censio.Companion.exe`. Start it on Windows (`publish\Censio.Companion.exe`); the tray menu shows `Version 0.0.0`. The command is written to run in PowerShell 7 and Windows PowerShell 5.1. The maintainer published the Companion with this command, started it, and its tray menu showed `Version 0.0.0`; which PowerShell was used was not recorded. Not yet tried: Windows PowerShell 5.1.

## First recording, step by step

This path leads from the download to a recording file and its replay, on Windows 10 22H2 or Windows 11 (64-bit) with Anno 117 installed. Each step ends with what you see next.

1. Download `Censio-<version>-win-x64.zip` from the release page `https://github.com/LarsLaskowski/Censio/releases` (under *Assets* of the release). If the page lists no release yet, there is no ZIP: build the Companion from source (*Build the Companion*), then continue at step 3 with `publish\Censio.Companion.exe`. You see the ZIP in your downloads folder.
2. Extract the ZIP to a folder first (right-click, *Extract All…*), best one in your own profile that stays where it is, for example `%LOCALAPPDATA%\Programs\Censio`. Do not start the exe from inside the ZIP: it then runs from a temporary folder, and `Start with Windows` would register a path Windows later removes. You see the file `Censio.Companion.exe`.
3. Start `Censio.Companion.exe`. If Windows SmartScreen shows "Windows protected your PC", choose "More info", then "Run anyway". No window opens; a tray icon appears.
4. Find the icon in the notification area at the right of the taskbar. Windows may hide a new icon behind the arrow `^` next to the clock; click the arrow and drag the icon out to keep it visible. Hover over it: the tooltip reads `Censio Companion: Waiting for game`. A second start of `Censio.Companion.exe` ends at once without a message and leaves the first running.
5. Add `/pipe` to the launch arguments of the game with the Ubisoft Connect steps of *Adding `/pipe`* (every copy, also a Steam copy; for a Steam copy there is the alternative described there). Quit the game first if it runs: the argument takes effect at the next game start.
6. Start the game from the launcher and load a save. The tooltip changes from `Censio Companion: Waiting for game` to `Censio Companion: Receiving`. If it stays at `Waiting for game`, see the table of states under *Connecting to the game*.
7. Right-click the tray icon and choose `Start recording`. The tooltip ends with `(recording)` at once; no folder or file exists until the game's first data.
8. Play for a while. When a tick has closed, the tooltip gets a second line `Last tick: <time>, <n> islands`.
9. Choose `Stop recording`. The tooltip loses `(recording)`.
10. Choose `Open recordings folder`. Explorer shows `%LOCALAPPDATA%\Censio\Recordings` with a file `recording-<yyyyMMdd-HHmmss>Z.jsonl`. If you quit and restarted the game during the recording, the session spans several files (*Recording and replay*). The file is private: do not post or attach it.
11. Choose `Exit`. The icon disappears.
12. Replay the file from the Windows *Run* dialog as described under *Recording and replay* (copy both paths with "Copy as path", then `"<path of Censio.Companion.exe>" --replay "<path of the file>" --speed max`).
13. The tooltip ends at `Censio Companion: Replay finished`: the file is readable. Choose `Exit`, then start `Censio.Companion.exe` as in step 3 for normal use.

Tried by the Product Manager on Windows 11 (26H2) with Ubisoft Connect in German and a Companion built from source with the documented publish command, with the real game: step 5 (entering `/pipe` and choosing *Close*), recording and its file, the tray icon in the overflow area, the tooltips and tray states, and the replay with `Exit` and the *Run* dialog (steps 7 to 13 in general). Not yet tried: the download from the release `v0.1.0` (steps 1 and 2 as written), the SmartScreen dialog of step 3, whether `/pipe` set in Steam reaches the game through Ubisoft Connect (the labels of the Steam path and the English labels of the Ubisoft Connect path, *Close* included, were confirmed by the Product Manager), and the SmartScreen tip under *Companion*.

## First start

Server: open `http://localhost:8080/`; the start page shows the heading `Censio` and the line `Version <version>`. `curl http://localhost:8080/health` answers `Healthy`, and after a few seconds `docker ps` shows the container as `(healthy)`. The first start creates the first data-protection key, and ASP.NET Core logs the warning `No XML encryptor configured` once (see *Limits*).

Companion: the program opens no window until you choose `Open diagnostics`, and shows a taskbar button only while that window is open. One icon appears in the notification area with the tooltip `Censio Companion: <state>` (the states are listed under *Connecting to the game* above); its menu holds the greyed-out status, last tick, game installation (`Game: …`, see *Game installation* above) and game catalog (`Catalog: …`, see *Game catalog* above), `Open diagnostics`, `Start recording` (or `Stop recording`), `Open recordings folder`, `Open log folder`, `Start with Windows` (checked while it is on), `Version <version>` (greyed out) and `Exit`, which removes the icon and ends the program within 2 seconds. The icon is a grey ring, a green circle or a red triangle by state. A second start in the same Windows session does nothing: it ends at once without a message and writes no log line, and the first keeps running. Windows SmartScreen asks once at the first start (see *Companion* above).

## Data and logs

Server: the data directory is `/config` in the Docker image. It holds the data-protection key ring in `/config/data-protection-keys`; the keys are unencrypted, so keep the directory and its backups private. The server writes its log to the console; read it with `docker logs censio`. Log times are UTC. The settings are listed in the [README](../README.md#configuration).

Companion: it stores game data only in the catalog cache `%LOCALAPPDATA%\Censio\Cache\catalog.bin` (the names and icons of your game, saved after a complete extraction, never sent, and kept when you remove the Companion, so delete the folder by hand; it holds temporary files only for the moment of a save, and the Companion deletes leftovers older than a day), and the diagnostics window stores nothing (it shows the last tick on screen only and writes nothing to disk or to the log). It writes recording files to `%LOCALAPPDATA%\Censio\Recordings` only while a recording runs (`--record` or `Start recording`); they hold the data of your game as it was sent, stay on your computer and are never deleted by the program, so delete them by hand (*Recording and replay*). It also writes a log to `%LOCALAPPDATA%\Censio\Logs`: one file per day, `companion-<yyyyMMdd>.log` (a file that grows past 10 MiB continues in `companion-<yyyyMMdd>-2.log` and so on). The Companion keeps the 10 newest of these files and deletes older ones; it deletes no other file in the folder. The menu entries `Open recordings folder` and `Open log folder` open both folders in Explorer. Only when you turn `Start with Windows` on does it write one value `Censio Companion` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` (your account only), and it deletes that value when you turn it off; the log then holds `Autostart enabled` or `Autostart disabled`, and a warning if the setting could not be read or changed (no file path). A line holds the local time, the level and the message: the start, every change of the connection state, recording started and stopped, the names of recording files, replay start and end, the result of the game installation search (the source, the build and the game folder, or the reason it failed; see *Game installation*), the result of the catalog (entry count, or the failure kind), whether the cache was used, loaded or written, the durations of an extraction or a cache load, the counts of its icons and the GUIDs it cannot name (see *Game catalog*), and problems. The log holds no game data, no game name and no icon, but the game folder at every start and an error text can contain a file path, and so your Windows user name; check a log before you share it. The Companion also reads, but never writes, the optional settings file `%LOCALAPPDATA%\Censio\settings.json` and Ubisoft Connect's install records in the registry.

## Reporting problems

Open a GitHub issue with the [Bug report](../.github/ISSUE_TEMPLATE/bug_report.md) template on `https://github.com/LarsLaskowski/Censio/issues/new/choose`. Include:

- the version: the start page (`Version <version>`) or the Companion's tray menu;
- how you run it: your `docker run` command (without secrets and private paths) or how you started the Companion, and your host operating system;
- the log: an excerpt of `docker logs censio` (redact tokens and other secrets); for the Companion, the log file of the day in `%LOCALAPPDATA%\Censio\Logs` (the tray menu entry `Open log folder` opens the folder). **Check the log for your Windows user name before you share it:** an error text can contain a file path such as `C:\Users\<your name>\...`, so replace the name or cut the line out. Never attach a raw recording (`--record`) to an issue or post it publicly, it holds private names and game data; describe the problem and the tooltip instead, and keep the file until a maintainer asks how to hand it in. A screenshot of the diagnostics window shows the island names of your savegame as well: hide them, or describe the problem instead, before you attach it. Never attach the catalog cache (`%LOCALAPPDATA%\Censio\Cache\catalog.bin`), it holds the names and icons of your game.

Report a security vulnerability privately as [SECURITY.md](../SECURITY.md) describes, never in a public issue.

# Censio-Benutzerhandbuch

Censio liest die Statistik eines laufenden Anno-117-Spiels unter Windows und zeigt sie live und im Verlauf in einer Webanwendung: Ein Windows-Companion extrahiert die Daten, eine Blazor-Server-Anwendung speichert und zeigt sie an. Status und Schnellstart stehen in der [README](../README.md#censio-deutsch).

English version: [Censio user guide](#censio-user-guide)

## Installation

### Server (Docker)

Der Censio-Server läuft als Docker-Image `networlddev/censio` (linux/amd64) auf Docker Hub. Jedes Release veröffentlicht den Tag `<version>` (zum Beispiel `0.1.0`) und setzt den Tag `latest` auf das neueste Release. Die Versionen stehen auf der Release-Seite `https://github.com/LarsLaskowski/Censio/releases`, die Tags auf `https://hub.docker.com/r/networlddev/censio/tags`. Voraussetzung ist Docker 23 oder neuer (Docker Engine unter Linux oder Docker Desktop für Windows mit WSL-2-Backend und Linux-Containern). Die Befehle unten verwenden die Version `0.1.0`; sie durch die gewünschte Version oder durch `latest` ersetzen. Die Befehle werden einzeln ausgeführt: Schlägt ein Befehl fehl, schlägt auch der nächste fehl. Noch nicht ausprobiert: das Laden und Starten des veröffentlichten Images `0.1.0`.

#### Image laden

```text
docker pull networlddev/censio:0.1.0
```

`docker run` lädt ein fehlendes Image selbst, dieser Schritt holt es nur vorab. Ein fehlgeschlagener Pull hinterlässt nichts Halbfertiges.

#### Starten

Den Container mit einem benannten Volume für das Datenverzeichnis `/config` starten:

```text
docker run -d --name censio -p 8080:8080 -v censio-config:/config networlddev/censio:0.1.0
```

Prüfen, ob er antwortet (die Antwort lautet `Healthy`; noch nicht ausprobiert: in Windows PowerShell 5.1 ist `curl` ein Alias für `Invoke-WebRequest`, das `StatusCode` `200` und `Content` `Healthy` anzeigen sollte und vorher eine Bestätigung verlangen kann), und sein Log lesen:

```text
curl http://localhost:8080/health
```

```text
docker logs censio
```

Die Startseite liegt unter `http://localhost:8080/`. Nach wenigen Sekunden zeigt `docker ps` den Container als `(healthy)`: Das Image fragt `/health` selbst alle 10 Sekunden ab. Ist ein Start fehlgeschlagen, den Container vor einem neuen Start entfernen, weil sein Name belegt ist:

```text
docker rm -f censio
```

#### Port

Im Container lauscht der Server auf Port 8080. Ist Port 8080 auf dem Host belegt, meldet Docker „port is already allocated“; dann einen anderen Host-Port abbilden:

```text
docker run -d --name censio -p 9000:8080 -v censio-config:/config networlddev/censio:0.1.0
```

Den Port, auf dem der Server selbst lauscht, ändert `ASPNETCORE_HTTP_PORTS` (der Health-Check folgt dem ersten Eintrag der Liste; `ASPNETCORE_URLS` unterstützt das Image nicht):

```text
docker run -d --name censio -e ASPNETCORE_HTTP_PORTS=9090 -p 9090:9090 -v censio-config:/config networlddev/censio:0.1.0
```

#### Datenverzeichnis und NAS-Hosts

`/config` ist das Verzeichnis für die Daten des Servers; es enthält den Data-Protection-Schlüsselring in `/config/data-protection-keys`. Das Image setzt `CENSIO__DATADIRECTORY=/config`; mit `-e CENSIO__DATADIRECTORY=<Pfad>` lässt es sich auf einen anderen absoluten Pfad setzen. Im Image abschalten lässt es sich nicht: `docker run -e` kann es nicht entfernen, und ein leerer Wert stoppt den Start. Das Image deklariert kein `VOLUME`, deshalb geben alle Befehle oben `-v` an. Ein benanntes Volume (`censio-config`) ist der Standard und unter Docker Desktop für Windows die einzige Form. Der Container startet nie als root: Er läuft als Benutzer `1654`.

Auf einem NAS, dessen Freigaben einem anderen Benutzer gehören (Unraid `nobody:users` ist `99:100`, ein Synology-Benutzer etwa `1026:100`), den Container mit diesem Benutzer und dieser Gruppe starten; den Pfad durch die eigene Freigabe ersetzen:

```text
docker run -d --name censio --user 99:100 -p 8080:8080 -v /mnt/user/appdata/censio:/config networlddev/censio:0.1.0
```

Oder das Verzeichnis dem Benutzer des Images übergeben und ohne `--user` starten:

```text
chown -R 1654:1654 /mnt/user/appdata/censio
```

```text
docker run -d --name censio -p 8080:8080 -v /mnt/user/appdata/censio:/config networlddev/censio:0.1.0
```

Ein Verzeichnis, das der Container-Benutzer nicht beschreiben kann, stoppt den Container beim Start mit Exit-Code 1; `docker logs censio` zeigt dann `The data-protection key ring cannot be stored in '/config/data-protection-keys'; the server stops. Make the data directory writable for the user the server runs as.` Ein Wert von `CENSIO__DATADIRECTORY`, der kein absoluter Pfad ist, stoppt ihn ebenfalls mit Exit-Code 1; `docker logs censio` zeigt dann `The data directory (Censio:DataDirectory) must be an absolute path, not '<value>'; the server stops.` Das Image auf dem NAS selbst laden; `docker run` tut das, wenn das Image fehlt:

```text
docker pull networlddev/censio:0.1.0
```

Das Bauen des Images aus dem Quellcode und sein Start mit den Befehlen dieser Anleitung wurde unter Docker Desktop für Windows in PowerShell 7 ausprobiert. Eine automatische Prüfung unter Docker Engine für Linux führt den Befehl mit `--user 99:100` und einem Bind-Mount aus, der `99:100` gehört; Windows PowerShell 5.1, die übrigen Befehle dieses Abschnitts (die `chown`-Abhilfe, das Laden auf dem NAS), der Stopp beim Start für ein nicht beschreibbares Verzeichnis auf NAS-Hardware sowie Unraid- oder Synology-Hardware wurden noch nicht ausprobiert.

#### Netzwerk und TLS

Im Image lauscht der Server auf allen Schnittstellen des Containers, über reines HTTP und ohne Login für Startseite und `/health`. Die `-p`-Abbildung entscheidet, wer ihn erreicht: `-p 8080:8080` veröffentlicht ihn auf jeder Schnittstelle des Hosts. Um ihn nur vom Host aus erreichbar zu machen, die Abbildung an die Loopback-Adresse binden:

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.1.0
```

Unter Linux umgeht ein veröffentlichter Port Host-Firewalls wie ufw und firewalld; den Zugriff deshalb über die Abbildung beschränken, nicht über die Firewall. TLS ist Aufgabe eines Reverse Proxys vor dem Server; siehe [SECURITY.md](../SECURITY.md).

#### Einschränkungen

- Nur linux/amd64; arm64-Hosts (auch ARM-basierte Synology-Modelle) und Windows-Container werden nicht unterstützt.
- Das Image läuft mit invarianter Globalisierung und ohne Zeitzonendaten: `TZ` hat keine Wirkung, Logzeiten sind UTC.
- Die Data-Protection-Schlüssel von ASP.NET Core liegen unverschlüsselt in `/config/data-protection-keys`; das Verzeichnis und seine Sicherungen privat halten. ASP.NET Core meldet die Warnung `No XML encryptor configured`, wenn der Server den ersten Schlüssel anlegt.
- Der Server schreibt sein Log auf die Konsole; Lesen mit `docker logs censio`.

### Companion

Der Companion ist das Windows-Programm, das die Statistik des laufenden Spiels liest. `Censio-<version>-win-x64.zip` von der Release-Seite `https://github.com/LarsLaskowski/Censio/releases` herunterladen (beim Release unter *Assets*). Er braucht Windows 10 22H2 oder Windows 11, 64 Bit; er braucht kein installiertes .NET, keinen Installer und keine Administratorrechte. Anderes Windows und arm64 werden nicht unterstützt. Noch nicht ausprobiert: das ZIP von `v0.1.0` auf einem Rechner ohne .NET.

1. Das ZIP in einen beliebigen Ordner entpacken. Es enthält eine Datei, `Censio.Companion.exe`.
2. `Censio.Companion.exe` starten. Ein Tray-Symbol erscheint; sein Menü zeigt den Status, den letzten Tick, die Spielinstallation (`Game: …`, siehe *Spielinstallation* unten), den Spielkatalog (`Catalog: …`, siehe *Spielkatalog* unten), `Open diagnostics`, `Start recording`, `Open recordings folder`, `Open log folder`, `Start with Windows`, `Version <version>` und `Exit`.

Das Programm ist nicht signiert, deshalb zeigt Windows SmartScreen beim ersten Start unter Umständen „Der Computer wurde durch Windows geschützt“. „Weitere Informationen“ wählen, dann „Trotzdem ausführen“ (unter englischem Windows: "More info", "Run anyway"). Tipp, vom Product Manager unter deutschem Windows gemeldet und noch nicht an einem echten Release-Download ausprobiert: die Eigenschaften der entpackten `Censio.Companion.exe` (oder des ZIP selbst) öffnen, *Zulassen* ankreuzen (unter englischem Windows vermutlich *Unblock*; noch nicht ausprobiert) und übernehmen; SmartScreen fragt dann nicht.

#### Start mit Windows

Der Companion startet nur dann mit Windows, wenn Sie es einschalten: im Tray-Menü `Start with Windows` wählen. Ein Häkchen zeigt, dass es eingeschaltet ist; den Eintrag erneut zu wählen schaltet es aus. Das Einschalten schreibt einen Wert, `Censio Companion`, unter `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`: Er gilt nur für Ihr Windows-Konto, und der Eintrag braucht keine Administratorrechte (der Maintainer hat es unter Windows 11 ausprobiert: keine Administrator-Abfrage erschien). Der Wert ist der vollständige Pfad der laufenden `Censio.Companion.exe` in Anführungszeichen, ohne Argumente; der Companion startet also wie bei einem Doppelklick (er zeichnet weder auf noch spielt er ab).

- Das ZIP in einen Ordner im eigenen Profil entpacken, der an seinem Platz bleibt, zum Beispiel `%LOCALAPPDATA%\Programs\Censio`. Keinen gemeinsamen Ordner und nicht das Stammverzeichnis eines Laufwerks verwenden, in das andere Benutzer schreiben können: Wer die Datei dort ersetzen kann, führt sie bei Ihrer nächsten Anmeldung aus.
- Ein Ordnerpfad mit `%` oder mit mehr als 200 Zeichen kann nicht verwendet werden: Der Eintrag bleibt ohne Häkchen, es wird nichts geschrieben, und das Log meldet `Could not change the autostart setting`.
- Wenn Sie die Exe verschieben oder eine neue Version aus einem anderen Ordner starten, zeigt der alte Eintrag noch auf die alte Datei, und das Menü zeigt den Eintrag ohne Häkchen. Um einen solchen übrig gebliebenen Eintrag zu entfernen, starten Sie eine beliebige Kopie von `Censio.Companion.exe`, wählen `Start with Windows` (der Eintrag wird durch den Pfad dieser Kopie ersetzt) und wählen es dann erneut, um es auszuschalten; oder löschen Sie den Wert `Censio Companion` im Registrierungs-Editor am oben genannten Schlüssel. Schalten Sie den Eintrag aus, bevor Sie den Ordner löschen. Das Ausschalten des alten Programms in der Windows-Liste der Autostart-Apps löscht den Wert nicht.
- Ein Wert mit anderem Pfad oder mit Argumenten gilt als ausgeschaltet; die Wahl des Eintrags ersetzt ihn dann.
- Wenn Sie den Companion in der Windows-Liste der Autostart-Apps ausschalten (Einstellungen, Apps, Autostart), zeigt das Menü das nicht und zeigt weiter das Häkchen; schalten Sie ihn in dieser Liste wieder ein. Eine Richtlinie Ihrer Organisation, die Startprogramme sperrt, sieht der Companion ebenfalls nicht.
- Der Maintainer hat dies unter Windows 11 ausprobiert: Der Companion startete nach einem Neustart mit Windows, wenn der Eintrag eingeschaltet war, und startete nicht, wenn er ausgeschaltet war; keine Administrator-Abfrage erschien (unter einem Standardkonto); Ordner mit Leerzeichen, `&` oder `,` im Pfad funktionierten; ein zweites Windows-Konto blieb unberührt; und das Ausschalten des Eintrags entfernte ihn. Nicht ausprobiert: ob SmartScreen beim ersten automatischen Start fragt, welchen Namen die Autostart-Liste zeigt, und ein Ordnerpfad mit `%`.

#### Verbindung zum Spiel

Das Spiel legt seine Statistik-Pipe nur an, wenn es mit dem Startargument `/pipe` gestartet wird. Ohne dieses Argument kann der Companion sich nicht verbinden; das sieht genauso aus wie ein Spiel, das nicht läuft: Der Tooltip bleibt bei `Censio Companion: Waiting for game`, obwohl das Spiel läuft. Den Companion vor oder nach dem Spiel starten; er braucht keinen Neustart, wenn das Spiel beendet und erneut gestartet wird. Er liest nur aus der Pipe und schreibt nie hinein.

**`/pipe` hinzufügen.** Das Argument wirkt beim nächsten Start des Spiels: Läuft das Spiel, beenden Sie es, fügen das Argument hinzu und starten es neu.

- *Ubisoft Connect (jede Kopie des Spiels, auch eine bei Steam gekaufte, die Ubisoft Connect mitstartet):* Ubisoft Connect öffnen, *Bibliothek* wählen, bei Anno 117 auf die drei Punkte klicken (Popupmenü), *Verwalten* wählen, dann *Eigenschaften*, `/pipe` in das Feld *Startargumente hinzufügen* eintragen und *Schließen* wählen.
- *Steam (Alternative für eine Steam-Kopie):* in der *Bibliothek* mit der rechten Maustaste auf Anno 117 klicken, im Kontextmenü *Eigenschaften* wählen, dann unter *Allgemein* `/pipe` in das Feld *Startoptionen* eintragen. Bleibt der Tooltip mit dieser Option bei `Waiting for game`, erreicht das Argument das Spiel über Ubisoft Connect vielleicht nicht: `/pipe` dann wie oben in Ubisoft Connect eintragen.

Der Weg in Ubisoft Connect wurde vom Product Manager unter Windows 11 (26H2) mit deutschem Ubisoft Connect ausprobiert: *Bibliothek*, die drei Punkte beim Spiel (Popupmenü), *Verwalten*, *Eigenschaften*, Feld *Startargumente hinzufügen*, `/pipe` eingetragen, *Schließen*. Der Product Manager hat außerdem die englischen Bezeichnungen dieses Wegs in einem englischen Ubisoft Connect bestätigt: *Library*, die drei Punkte, *Manage*, *Properties*, Feld *Add launch arguments*, *Close*. Ebenso hat er die Bezeichnungen des Steam-Wegs bis zum Feld bestätigt, auf Deutsch (*Bibliothek*, Rechtsklick auf das Spiel, *Eigenschaften*, *Allgemein*, Feld *Startoptionen*) und auf Englisch (*Library*, Rechtsklick auf das Spiel, *Properties...*, *General*, Feld *Launch Options*). Noch nicht ausprobiert: ob das Argument nach dem Speichern im Spiel ankam und ob ein in Steam gesetztes `/pipe` das Spiel über Ubisoft Connect erreicht.

Mit der Maus über das Tray-Symbol fahren: Der Tooltip lautet `Censio Companion: <Zustand>`. Sobald das Spiel einen vollständigen Tick gesendet hat, zeigt eine zweite Zeile `Last tick: <Zeit>, <n> islands` (die Ankunftszeit als `HH:mm:ss`, wenn sie heute war, sonst mit Datum). Auch das Symbol zeigt den Zustand: ein grauer Ring beim Warten (`Waiting for game`, `Connected`, `Pipe busy`), ein grüner Kreis bei `Receiving`, ein rotes Dreieck bei `Protocol mismatch`, `Error` und einer ungültigen Kommandozeile. Ein Rechtsklick auf das Symbol öffnet das Menü, das denselben Status und letzten Tick zeigt.

Der Menüeintrag `Open diagnostics` öffnet ein Fenster mit dem letzten Tick: einen Kopf mit `Status:`, `Last tick:` und `Game time:` und eine Tabelle mit einer Zeile je Ware jeder Insel (Region, Insel, Gebiet, Sitzungs-ID, Name, Ware, Erzeugung, Verbrauch, Delta, perfekte Erzeugung, perfekter Verbrauch, Gebäude, Unterhalt, Einkommen, Gewinn und die beiden Produktivitätswerte). Sobald der Katalog bereit ist (*Spielkatalog* unten), zeigen die Zellen `Region` und `Good` `<englischer Name> / <deutscher Name> (<GUID>)` oder den einen vorhandenen Namen mit der GUID dahinter; wo der Katalog keinen Namen hat und bevor er bereit ist, zeigen sie die rohe Zahl (GUID), die das Spiel gesendet hat, wie es Inseln und alle anderen Werte immer tun; dieselben Zellen zeigen vor dem Text das Symbol des Spiels, wo die Spieldateien eines liefern (ein kleines Bild, *Spielkatalog* unten); Zahlen haben unter jedem Windows den Dezimalpunkt. Das Fenster folgt jedem neuen Tick, auch beim Abspielen, und behält den letzten Tick, wenn die Verbindung endet. Schließen (Schließen-Schaltfläche oder Alt+F4) lässt den Companion weiterlaufen; `Open diagnostics` öffnet es wieder mit dem letzten Tick und holt ein offenes Fenster in den Vordergrund. Solange es offen ist, hat es eine Schaltfläche in der Taskleiste.

| Tooltip | Bedeutung | Was tun |
| ------- | --------- | ------- |
| `Waiting for game` | Die Pipe existiert nicht: Das Spiel läuft nicht oder wurde nicht mit `/pipe` gestartet. Der Companion sucht weiter (die Pausen wachsen von 1 auf 10 Sekunden) | Das Spiel starten. Läuft das Spiel, zuerst prüfen, dass `/pipe` im Feld für Startargumente des Launchers gespeichert ist (*`/pipe` hinzufügen*), und das Spiel neu starten: Das Argument wirkt erst beim nächsten Start. Bleibt es bei `Waiting for game`, obwohl die Startoption von Steam gesetzt ist, `/pipe` in Ubisoft Connect eintragen |
| `Connected` | Die Pipe ist geöffnet; das Spiel hat noch keine erste Nachricht gesendet. Das Spiel sendet sie sofort, dieser Zustand ist daher kaum zu sehen | Nichts |
| `Receiving` | Das Spiel sendet Daten | Nichts |
| `Pipe busy` | Die Pipe existiert, wird aber von einem anderen Programm gehalten (zum Beispiel einem anderen Pipe-Werkzeug); das Spiel bedient nur einen Client gleichzeitig, andere Pipe-Leser also schließen. Der Companion versucht es weiter (die Pausen wachsen von 1 auf 10 Sekunden) und zeigt beim nächsten Versuch nach dem Freiwerden der Pipe `Receiving`. Er kann auch nach einem Neustart des Companions bei `Pipe busy` bleiben, solange das Spiel im Hauptmenü ist, bis ein Spiel geladen wird | Das andere Pipe-Werkzeug schließen, oder einen Spielstand laden, wenn der Companion im Hauptmenü des Spiels neu gestartet wurde |
| `Protocol mismatch` | Die Pipe spricht eine andere Protokollversion als 2 (zum Beispiel nach einem Spiel-Update); der Companion bleibt verbunden und macht weiter, sobald Version 2 eintrifft | Auf Version 2 warten; verschwindet es nicht, melden (*Probleme melden*), mit der Version aus dem Tray-Menü und dem Log des Tages, ohne die Aufzeichnung |
| `Error` | Das Verbinden ist fehlgeschlagen oder die Verbindung ist abgebrochen. Beim Beenden des Spiels kann `Error` kurz vor `Waiting for game` erscheinen; das ist normal | Verschwindet es nicht, melden wie bei `Protocol mismatch` |
| ` (recording)` nach dem Zustand | Eine Aufzeichnung läuft (der Eintrag lautet `Stop recording`) | Nichts; am Ende `Stop recording` wählen |
| ` (recording failed)` nach dem Zustand | Das Schreiben der Aufzeichnung ist fehlgeschlagen (zum Beispiel weil die Platte voll oder zu langsam ist); die Aufzeichnung endete, der Companion arbeitet weiter | Speicherplatz schaffen, dann `Start recording` wählen: Es versucht es mit einer neuen Datei erneut |

Beendet man das Spiel, kehrt der Companion zu `Waiting for game` zurück und verbindet sich von selbst wieder, sobald das Spiel zurück ist; das Laden eines anderen Spielstands und der Wechsel ins Hauptmenü behalten die Verbindung. `Exit` im Tray-Menü funktioniert in jedem Zustand und beendet das Programm innerhalb von 2 Sekunden.

#### Spielinstallation

Der Companion sucht Ihre Anno-117-Installation bei jedem Start einmal im Hintergrund; das Tray-Menü und `Exit` funktionieren währenddessen. Das Ergebnis ist die ausgegraute Menüzeile `Game: …` direkt unter `Last tick:`, zum Beispiel `Game: build 26.29905.4.51797 (Ubisoft Connect)`. Der Companion liest nur: Er schreibt nichts ins Spiel, in die Registry oder in Ihre Einstellungsdatei. Das Diagnosefenster zeigt die Zeile nicht.

**Suchreihenfolge.**

1. Der in der Einstellungsdatei (unten) genannte Ordner. Ist er gesetzt, wird er verwendet, und sonst wird nichts gesucht.
2. Die Einträge von Ubisoft Connect zu installierten Spielen (der Registry-Schlüssel `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs`, nur lesend): Der erste gültige Eintrag nach Nummer wird verwendet; ein Eintrag, der auf einen Ordner ohne das Spiel zeigt, wird übersprungen. Ein Eintrag, der auf einen Netzwerkpfad (UNC) oder Gerätepfad zeigt, wird nie verfolgt; ein zugeordneter Laufwerksbuchstabe oder eine Ordnerverknüpfung dagegen schon.
3. Der Standard-Spieleordner von Ubisoft Connect, `C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games` (unter dem 32-Bit-Programmordner).

Eine Installation ist ein Ordner, der `maindata\config.rda` und `bin\win64\anno117.exe` enthält. Steam wird nicht durchsucht: Bei einer Steam-Kopie oder einem Spiel in einem anderen Ordner die Einstellungsdatei verwenden. Eine Änderung gilt nach dem nächsten Start des Companions.

**Der Build** ist die Dateiversion von `bin\win64\anno117.exe` als vier Zahlen, zum Beispiel `26.29905.4.51797`. Er ist nicht die Version, die das Hauptmenü des Spiels zeigt (`3.0.1733841` bei derselben Installation), und Ubisoft Connect zeigt keine. Noch nicht überprüft: dass sich der Build mit jedem Spiel-Update ändert.

**Die Einstellungsdatei.** `%LOCALAPPDATA%\Censio\settings.json` anlegen (den Pfad in die Adresszeile des Explorers eingeben; der Ordner `Censio` existiert, sobald der Companion einmal lief) mit Ihrem Spieleordner, zum Beispiel:

```json
{
  "gameFolder": "C:\\Games\\Anno 117"
}
```

- Einen vollständigen Pfad schreiben und jeden Backslash verdoppeln, oder Schrägstriche verwenden (`"C:/Games/Anno 117"`). Der Ordner ist der, der `maindata` und `bin` enthält, nicht `maindata` selbst. Variablen wie `%USERPROFILE%` und ein bloßer Laufwerksbuchstabe (`"C:"`) sind keine vollständigen Pfade. Einen Netzwerkpfad (`\\\\server\\share\\Anno 117`), den Sie hier schreiben, verwendet der Companion wie geschrieben, und Windows kontaktiert dann diesen Server.
- Der Schlüssel `gameFolder` wird ohne Beachtung der Groß- und Kleinschreibung gelesen (`GameFolder` funktioniert); jede andere Schreibweise, etwa `game_folder`, ist ein unbekannter Schlüssel und wird ignoriert: Die automatische Suche läuft, und das Menü zeigt dann `(Ubisoft Connect)` oder `Game: not found`, nicht `(settings file)`. Andere Schlüssel werden ignoriert. Ein leeres `gameFolder` gilt als nicht gesetzt.
- Die Datei ist UTF-8 (die Byte-Reihenfolge-Marke von Notepad ist in Ordnung), Kommentare (`//`, `/* */`) und nachgestellte Kommas sind erlaubt, und sie darf bis zu 1 MiB groß sein. Bearbeiten Sie sie bei geschlossenem Companion und starten Sie ihn danach neu.
- Ein falscher Ordner in der Datei wird nie durch einen gesuchten ersetzt, damit Sie den Fehler sehen.

**Menütexte.**

| Text | Was zu tun ist |
| ---- | -------------- |
| `Game: searching` | Warten; es endet von selbst. Ein langsames oder schlafendes Laufwerk kann dauern. |
| `Game: build <build> (Ubisoft Connect)` | Nichts; über Ubisoft Connect gefunden. |
| `Game: build <build> (settings file)` | Nichts; der Ordner Ihrer Einstellungsdatei wird verwendet. |
| `Game: build unknown (…)` | Die Installation wird verwendet, aber ihre Programmdatei hat keine lesbare Version. Nichts zu tun. |
| `Game: not found` | Weder die Einträge von Ubisoft Connect noch dessen Standardordner enthalten das Spiel (zum Beispiel eine Steam-Kopie). Den Ordner in der Einstellungsdatei nennen. |
| `Game: folder in settings not found` | Der Ordner in `gameFolder` existiert nicht. Den Pfad korrigieren (eine Variable in einem Pfad wird nicht aufgelöst und wird deshalb ebenfalls nicht gefunden). |
| `Game: folder in settings is not a full path` | Den vollständigen Pfad mit Laufwerksbuchstaben schreiben, zum Beispiel `C:\\Games\\Anno 117`. |
| `Game: folder in settings is not an Anno 117 installation` | Dem Ordner fehlt `maindata\config.rda` oder `bin\win64\anno117.exe`. Den Ordner nennen, der `maindata` und `bin` enthält. |
| `Game: settings file invalid` | Die Datei ist kein gültiges JSON oder kein Objekt, `gameFolder` ist kein Text, der Schlüssel kommt doppelt vor, die Datei ist größer als 1 MiB oder sie ist nicht lesbar. Das Log nennt den Grund. |

Das Log (`Open log folder`) nennt die Ursache jedes Problems und, wenn das Spiel gefunden wird, die Quelle, den Build und den Ordner.

#### Spielkatalog

Nach der Spielsuche lädt der Companion im Hintergrund bei jedem Start die Namen von Regionen, Waren, Arbeitskräften und Gebäuden auf Englisch und Deutsch aus seinem Cache oder, wenn keiner passt, liest sie aus Ihrer eigenen Spielinstallation (die Datei `maindata\config.rda`); das Tray-Menü, die Verbindung und `Exit` warten nicht darauf. Er liest die Dateien des Spiels nur: In die Installation wird nichts geschrieben, und die Namen bleiben auf Ihrem Rechner (sie werden nirgendwohin gesendet, nicht ins Log geschrieben und sind nicht Teil eines Release). Die einzige Kopie ist die unten beschriebene Cache-Datei. Die ausgegraute Menüzeile `Catalog: …` direkt unter `Game: …` zeigt den Zustand:

| Menüzeile | Was zu tun ist |
| --------- | -------------- |
| `Catalog: reading` | Warten; es endet von selbst. Sie erscheint auch, solange die Spielsuche läuft. |
| `Catalog: checking` | Warten; der Companion vergleicht das Spiel mit dem Cache. |
| `Catalog: extracting names` | Warten; die Namen werden aus den Spieldateien gelesen. Die bisher gezeigten Namen bleiben, bis die neuen bereit sind. |
| `Catalog: extracting icons, <n> entries` | Warten; die Namen sind bereit und werden gezeigt, die Symbole folgen. |
| `Catalog: <n> entries` | Nichts; das Diagnosefenster zeigt jetzt Namen und Symbole. |
| `Catalog: <n> entries, icons failed` | Die Namen funktionieren; die Symbole ließen sich nicht lesen (siehe den Absatz zu Symbolen unten). Es wird nichts im Cache gespeichert, der nächste Start liest die Dateien also erneut. |
| `Catalog: <n> entries, not cached` | Namen und Symbole funktionieren, aber der Cache ließ sich nicht schreiben (Platte voll, kein Zugriff auf `%LOCALAPPDATA%\Censio\Cache`); die Logzeile `Catalog cache not written` nennt den Grund. Der nächste Start liest die Spieldateien erneut. |
| `Catalog: no game` | Es wurde keine Installation gefunden (siehe *Spielinstallation*); GUIDs bleiben Zahlen. |
| `Catalog: failed` | Eine Spieldatei war nicht lesbar oder hat einen unerwarteten Aufbau, zum Beispiel nach einem Spielupdate. GUIDs bleiben Zahlen; die Logzeile `Catalog failed: <Art>` nennt die Art (`ArchiveUnreadable`, `FileMissing`, `FileUnreadable` oder `MalformedFile`); melden Sie sie mit dem Spiel-Build (*Probleme melden*). |

Eine GUID, die der Katalog nicht benennen kann, bleibt im Diagnosefenster eine Zahl. Für jede solche GUID eines Ticks steht im Log, einmal je Katalog (also nach einer Extraktion wegen eines Spielupdates erneut), eine Zeile `Catalog cannot name <Region|Good|Workforce|Building> <GUID>: <Unknown|Incomplete|Nameless|Ambiguous>` (unknown: nicht in den Spieldateien; incomplete: nur eine Sprache hat einen Namen; nameless: kein Name; ambiguous: die Dateien definieren sie mehrfach), nie ein Name. Das Log nennt außerdem, wie viele Einträge gebaut wurden, und benennt ein Archiv, das eine zweite Kopie einer Datei des Katalogs enthält (`Catalog: <Archiv> also lists <Pfad>; the copy in config.rda is used`). Liegt der Spieleordner der Einstellungsdatei auf einem Netzlaufwerk in der Schreibweise `\\Server\Freigabe`, listet der Companion die Spielarchive bei jedem Start und bei jeder Prüfung über das Netzwerk und liest sie darüber, wenn er extrahiert.

Cache: Nach einer vollständigen Extraktion (Namen und Symbole) speichert der Companion sie in der Datei `%LOCALAPPDATA%\Censio\Cache\catalog.bin`, einer Datei für die zuletzt extrahierte Installation. Der nächste Start lädt sie in einem Augenblick, ohne die Archive des Spiels zu lesen, wenn der Spieleordner, der Spiel-Build sowie Name, Größe und Änderungszeit jeder `.rda`-Datei unter `maindata` dieselben sind und der Companion selbst nicht aktualisiert wurde; sonst wird der Cache nicht benutzt, die Logzeile `Catalog cache not used: missing`, `game changed` oder `Companion changed` nennt den Grund, und der Companion extrahiert erneut und ersetzt den Cache. Eine beschädigte oder fremde Datei wird nie teilweise benutzt: Die Warnung `Catalog cache not used: invalid` wird geschrieben, und der Companion extrahiert erneut. Wird das Spiel aktualisiert, während der Companion läuft, starten Sie es wie üblich mit dem Startargument `/pipe`: Bei jeder neuen Verbindung prüft der Companion das Spiel, und hat es sich geändert, zeigt `Catalog: extracting names` wieder an, während die alten Namen bleiben, und der Cache wird ersetzt; ein unverändertes Spiel bewirkt nichts. Gemessen auf der Installation des Maintainers (1.040 Einträge; Ihr Rechner weicht ab): etwa 7 s zum Extrahieren (Namen 2,5 s, Symbole 4,5 s) und etwa 2,4 s zum Laden aus dem Cache. Die Logzeilen `Catalog extracted in <t> s (names <a> s, icons <b> s)` und `Catalog loaded from cache in <t> s: …` nennen die Zeiten Ihres Rechners. Der Cache enthält die Namen und Symbole Ihres Spiels und ist deshalb privat wie eine Aufzeichnung: Er wird nirgendwohin gesendet, nicht ins Log geschrieben und ist nicht Teil eines Release, Sie sollten ihn nicht weitergeben, und er bleibt auf dem Rechner, wenn Sie den Companion entfernen; löschen Sie dann den Ordner `%LOCALAPPDATA%\Censio\Cache` von Hand. Blieb ein Spielupdate unbemerkt (ein Archiv mit gleichem Namen, gleicher Größe und gleicher Zeit neu geschrieben), den Companion schließen, den Ordner löschen und neu starten. Ein Spiel, das ohne `/pipe` gestartet wurde, ein Abspielen und eine ungültige Befehlszeile werden nur beim Start geprüft.

Symbole: Sobald die Namen angezeigt werden, liest der Companion auch die Symbole der Waren und Regionen aus den Bildarchiven des Spiels (`maindata\ui.rda` und die danebenliegenden Dateien `zz_patchfiles_<n>.rda`, die höchste Nummer zuerst), wandelt sie im Speicher um und zeigt jedes mit 20 mal 20 Pixeln vor dem Namen in den Zellen `Region` und `Good`; Regionssymbole stehen auf einem dunklen runden Hintergrund, weil das Spiel sie als helle Zeichen zeichnet. Liefern die Spieldateien kein Symbol oder eines, das sich nicht lesen lässt, zeigt die Zelle nur den Text, ohne Hintergrund; die Namen warten nie auf die Symbole und hängen nicht von ihnen ab. Die Symbole werden nur in der Cache-Datei oben gespeichert: Sie werden nicht gesendet, nicht ins Log geschrieben und sind nicht Teil eines Release. Das Log enthält je Extraktion eine Zeile, `Catalog icons: <g> goods and <r> regions with an icon; without one: <a> not named, <m> missing, <u> unreadable` (Anzahl der Einträge), oder die Warnung `Catalog icons failed: <Art> (<Archiv>)` mit einer der Arten `ArchiveMissing`, `ArchiveUnreadable`, `DuplicateArchiveNumber` oder `IconSetTooLarge`; dann wird kein Symbol gezeigt, und die Namen bleiben unberührt. Nach einem Spielupdate zeigen die Zahlen, ob Symbole fehlen; melden Sie eine solche Zeile mit dem Spiel-Build (*Probleme melden*). Liegt der Spieleordner auf einem Netzlaufwerk, werden auch die Bildarchive bei jeder Extraktion über das Netzwerk gelesen.

#### Aufzeichnen und Abspielen

Für Tests und Fehlermeldungen kann der Companion aufzeichnen, was das Spiel sendet, und es später wieder abspielen. Beides kann mit einem Schalter in der Kommandozeile gestartet werden (zum Beispiel `Censio.Companion.exe --record` in einem Terminal oder im Ziel einer Verknüpfung); eine Aufzeichnung lässt sich auch im Tray-Menü starten und beenden (`Start recording`, `Stop recording`), ein Abspielen nur mit seinem Schalter. Einen laufenden Companion vorher schließen (`Exit` im Tray-Menü): Ein zweiter Start endet sofort ohne Meldung, und es wird nichts abgespielt.

- **Aufzeichnen:** `Censio.Companion.exe --record` startet mit eingeschalteter Aufzeichnung; ohne den Schalter im Tray-Menü jederzeit `Start recording` wählen, auch wenn das Spiel schon sendet. Der Companion verbindet sich wie gewohnt mit dem Spiel und schreibt jede Verbindung in eine eigene Datei in `%LOCALAPPDATA%\Censio\Recordings` (der Menüeintrag `Open recordings folder` öffnet ihn). Eine Datei `recording-<yyyyMMdd-HHmmss>Z.jsonl` (UTC-Zeit der ersten Daten; ein vergebener Name bekommt `-2`, `-3`, … vor `.jsonl`) entsteht, sobald nach dem Start die ersten Daten des Spiels eintreffen. Bis dahin endet der Tooltip schon mit `(recording)`, aber der Ordner existiert unter Umständen nicht und enthält keine Datei: Das ist kein Fehler. Wird das Spiel beendet und neu gestartet, schreibt die nächste Verbindung eine neue Datei, und jeder neue Start schreibt eine neue Datei; eine Sitzung kann also aus mehreren Dateien bestehen. Während der Aufzeichnung endet der Tooltip mit `(recording)`, und der Menüeintrag lautet `Stop recording`; `Stop recording` schreibt noch Wartendes und schließt die Datei. Schlägt das Schreiben fehl (zum Beispiel weil die Platte voll oder zu langsam ist), endet die Aufzeichnung, der Tooltip endet mit `(recording failed)`, und der Companion arbeitet weiter; `Start recording` versucht es mit einer neuen Datei erneut. `Exit` im Tray-Menü schreibt noch Wartendes, höchstens 1 Sekunde lang, und beendet das Programm. Wird der Companion beendet („abgeschossen“) oder fällt der Strom aus, können die letzten Zeilen einer Datei fehlen; ob Windows beim Abmelden oder Herunterfahren sie schreibt, ist nicht ausprobiert.
- **Abspielen:** `Censio.Companion.exe --replay <Datei> [--speed <Faktor>|max]` liest eine Aufzeichnung aus einer lokalen Datei statt aus der Pipe des Spiels und spielt sie so ab, wie das Spiel sie gesendet hat. `--speed` ist `max` (keine Pausen) oder eine Zahl von `0.1` bis `1000`, auch unter deutschem Windows mit Dezimalpunkt geschrieben (`--speed 10` macht die Pausen auf ein Zehntel kürzer); ohne Angabe läuft das Abspielen in Echtzeit. Der Companion öffnet beim Abspielen die Pipe nicht, und die Datei wird nur gelesen. Der Tooltip, der letzte Tick, das Symbol und das Fenster `Open diagnostics` zeigen den Fortschritt, und das Fenster zeigt die abgespielten Ticks und behält den letzten Tick der Datei (`Start recording` ist beim Abspielen ausgegraut): `Replay: Connected`, dann `Replay: Receiving` (oder `Replay: Protocol mismatch`) und nach dem letzten Frame `Replay finished` bis `Exit`. Ein Problem mit der Datei erscheint als `Replay error: <Grund>` mit `file not found`, `not a recording`, `unsupported version`, `invalid line <n>` oder `read failed`.

  Eine fertige Aufzeichnung prüfen, ohne Konsole:

  1. Im Tray-Menü `Exit` wählen. Läuft noch ein Companion, endet der Start des Abspielens sofort und ohne Meldung.
  2. Im Explorer den vollständigen Pfad von `Censio.Companion.exe` und der Aufzeichnungsdatei jeweils mit „Als Pfad kopieren“ kopieren (Rechtsklick; unter Windows 10 Umschalt + Rechtsklick).
  3. Windows-Taste + R drücken und im Dialog *Ausführen* die Zeile `"<Pfad der Censio.Companion.exe>" --replay "<Pfad der Datei>" --speed max` eingeben, beide Pfade samt Anführungszeichen eingefügt. Die Anführungszeichen bleiben, damit gehen auch Pfade mit Leerzeichen und ein vom Explorer bereits aufgelöstes `%LOCALAPPDATA%`.
  4. Der Tooltip zeigt `Censio Companion: Replay: Receiving` und endet bei `Censio Companion: Replay finished`. Das heißt, die Datei ist lesbar.
  5. `Exit` wählen: Das beendet das Abspielen. `Censio.Companion.exe` wie gewohnt starten, um den Companion zurückzubekommen; `Start with Windows` startet ihn erst bei der nächsten Anmeldung.

  Vom Product Manager unter Windows 11 (26H2) mit einem Companion ausprobiert, aus dem Quellcode mit dem dokumentierten Publish-Befehl gebaut: dieses Verfahren mit `Exit` zuerst und dem Dialog *Ausführen*, es funktioniert. Nicht gesondert gemeldet: die Einzelheiten der Zeile zu Anführungszeichen und Pfaden und ein Abspielen, das gestartet wird, während der Tray-Companion noch läuft.
- **Ungültige Kommandozeile:** ein unbekanntes Argument, `--speed` ohne `--replay`, eine Geschwindigkeit außerhalb des Bereichs, `--replay` ohne Datei oder `--record` zusammen mit `--replay` zeigen `Censio Companion: Invalid command line`; der Companion zeichnet dann weder auf noch spielt er ab, und `Exit` funktioniert.

**Eine Aufzeichnung ist privat.** Sie enthält den Namen der Spielfigur (der Ihr echter Name sein kann), Inselnamen und Wirtschaftsdaten genau so, wie das Spiel sie gesendet hat. Sie bleibt im Ordner auf dem eigenen Rechner, der Companion sendet sie nirgendwohin und löscht sie nie. Eine unbearbeitete Aufzeichnung nie öffentlich anhängen oder posten (auch nicht in einem Issue); geteilt werden sollen nur anonymisierte Testdaten (*Eine Aufzeichnung anonymisieren (für Mitwirkende)*). Eine Aufzeichnung aufheben, bis ein Maintainer fragt, wie sie übergeben werden soll (*Probleme melden*). Die Dateien selbst löschen, wenn sie nicht mehr gebraucht werden.

##### Eine Aufzeichnung anonymisieren (für Mitwirkende)

Wer eine Aufzeichnung als Testdatei einchecken möchte, anonymisiert sie vorher. Das Werkzeug ist ein Konsolenprogramm des Repositorys, nicht Teil des Releases oder des Companion-ZIPs; es braucht einen Checkout des Repositorys und das .NET SDK (wie in *Aus dem Quellcode bauen*). Tester, die nicht mitwirken, brauchen es nicht: Sie behalten ihre unbearbeiteten Aufzeichnungen und geben sie privat weiter, wenn ein Maintainer fragt.

1. Die Aufzeichnung beenden (`Stop recording` oder `Exit`). Eine Datei, die der Companion noch schreibt, lässt sich nicht öffnen und ergibt `file not found`.
2. Im Hauptverzeichnis des Repositorys `dotnet run --project src/Censio.Anonymiser -c Release -- <Aufzeichnung> <Ausgabe>` mit der Aufzeichnungsdatei und einem neuen Namen für die Ausgabedatei ausführen. Die Ausgabe darf nicht existieren; sie wird nie überschrieben.
3. Bei Erfolg gibt das Werkzeug `Anonymised <n> frames to <Ausgabe>` aus und endet mit Code 0. Das Original bleibt unverändert.

Was sich ändert: Jeder Insel- und Figurenname wird zu `Island <n>` oder `Player <n>` (derselbe Originalname immer derselbe erfundene), die Beträge jedes Eintrags ändern sich (Erzeugung, Verbrauch, Einkommen und Produktivität um 1 % bis 20 %; Unterhalt und Arbeitskräftemengen um 1 % bis 20 % und mindestens 1, unter 5 um genau 1, wobei 1 zu 2 wird; Delta, Gewinn und durchschnittliche Produktivität folgen), und jede Zeit wird um einen zufälligen Versatz von 30 bis 365 Tagen nach früher verschoben. Was bleibt: Frame-Typen und Reihenfolge, Spielzeiten, Pausen, die GUIDs von Inseln und Produkten, die Gebäudezahlen und die GUIDs der Arbeitskräfte und Gebäude. Ein Abspielen der Ausgabe zeigt dieselben Ticks wie das Original.

Fehler sind eine Zeile auf der Standardfehlerausgabe, `Error: <Grund>`, mit Code 1: `file not found`, `not a recording`, `unsupported version`, `invalid line <n>`, `read failed`, `frame cannot be anonymised in line <n>` (ein Frame, den das Werkzeug nicht dekodiert, oder jeder Frame außer `Version` vor einem `Version 2`-Frame oder nach einer anderen Version), `output file exists`, `output is the input`, `write failed` (zum Beispiel ein Ordner, der nicht existiert) und `cancelled` (Strg+C). Ohne genau zwei Argumente gibt das Werkzeug `Usage: Censio.Anonymiser <recording> <output>` aus und endet mit Code 2. Nach einem Fehler bleibt keine Ausgabedatei zurück.

Die Ausgabe vor dem Einchecken prüfen: Ein Name oder eine Zahl, die Sie aus dem Spiel kennen, darf darin nicht vorkommen. Die ursprüngliche Aufzeichnung nie einchecken oder posten.

### Aus dem Quellcode bauen

Um einen Stand des Repositorys auszuführen, der nicht veröffentlicht ist, ihn aus einem Checkout dieses Repositorys bauen; die Befehle laufen in dessen Hauptverzeichnis. Edge-Builds werden nicht angeboten. In den übrigen `docker run`-Befehlen dieser Anleitung `--pull never` ergänzen und den lokalen Namen `censio` statt `networlddev/censio:0.1.0` verwenden.

#### Server-Image bauen

Das baut den aktuellen Stand des Repositorys zu einem lokalen Image. Es braucht Docker 23 oder neuer mit BuildKit und Zugriff auf `mcr.microsoft.com`. Im Hauptordner des Checkouts ausführen (siehe [Censio beziehen](../README.md#censio-beziehen)):

```text
docker build -f src/Censio.Server/Dockerfile -t censio .
```

Der Build setzt die Version `0.0.0`; eine andere wird als Semantic Version übergeben (`MAJOR.MINOR.PATCH`, optional mit Pre-Release- und Build-Teil), alles andere lässt den Build mit `VERSION is not a semantic version` scheitern:

```text
docker build -f src/Censio.Server/Dockerfile --build-arg VERSION=1.2.3 -t censio .
```

Das lokale Image starten. `--pull never` lässt Docker den Start verweigern, wenn der Build fehlgeschlagen ist und kein lokales Image `censio` existiert, statt ein fremdes Image dieses Namens zu laden:

```text
docker run -d --name censio --pull never -p 8080:8080 -v censio-config:/config censio
```

Dann `http://localhost:8080/health` (`Healthy`) und `http://localhost:8080/` wie oben prüfen; die Startseite zeigt `Version 0.0.0`. Der Maintainer hat das Image mit diesen Schritten gebaut und gestartet, und `/health` antwortete mit `Healthy`. Noch nicht ausprobiert: die Ablehnung durch `--pull never` nach einem fehlgeschlagenen Build.

#### Companion bauen

Das Bauen braucht das .NET-11-SDK (Version 11.0.100 oder ein späterer Patch oder ein späteres Feature Band), Zugriff auf `nuget.org` und den Quellcode (siehe [Censio beziehen](../README.md#censio-beziehen)), auf jedem Betriebssystem, das das SDK unterstützt; nur das Ausführen des Programms braucht Windows 10 22H2 oder Windows 11, x64. Den Befehl im Hauptordner des Checkouts ausführen. Er verwendet die Flags des Release-Builds, außer der Version, und schreibt in den Ordner `publish`:

```text
dotnet publish src/Censio.Companion/Censio.Companion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -o publish
```

Der Ordner `publish` enthält danach nur `Censio.Companion.exe`. Unter Windows starten (`publish\Censio.Companion.exe`); das Tray-Menü zeigt `Version 0.0.0`. Der Befehl ist so geschrieben, dass er in PowerShell 7 und Windows PowerShell 5.1 läuft. Der Maintainer hat den Companion mit diesem Befehl veröffentlicht und gestartet, und sein Tray-Menü zeigte `Version 0.0.0`; welche PowerShell verwendet wurde, wurde nicht festgehalten. Noch nicht ausprobiert: Windows PowerShell 5.1.

## Erste Aufzeichnung Schritt für Schritt

Dieser Weg führt vom Download bis zu einer Aufzeichnungsdatei und ihrem Abspielen, unter Windows 10 22H2 oder Windows 11 (64 Bit) mit installiertem Anno 117. Jeder Schritt endet mit dem, was Sie als Nächstes sehen.

1. `Censio-<version>-win-x64.zip` von der Release-Seite `https://github.com/LarsLaskowski/Censio/releases` herunterladen (beim Release unter *Assets*). Zeigt die Seite noch kein Release, gibt es kein ZIP: den Companion aus dem Quellcode bauen (*Companion bauen*) und bei Schritt 3 mit `publish\Censio.Companion.exe` weitermachen. Sie sehen das ZIP im Ordner Downloads.
2. Das ZIP zuerst in einen Ordner entpacken (Rechtsklick, *Alle extrahieren…*), am besten in einen Ordner im eigenen Profil, der an seinem Platz bleibt, zum Beispiel `%LOCALAPPDATA%\Programs\Censio`. Die Exe nicht aus dem ZIP heraus starten: Sie läuft dann aus einem temporären Ordner, und `Start with Windows` würde einen Pfad eintragen, den Windows später entfernt. Sie sehen die Datei `Censio.Companion.exe`.
3. `Censio.Companion.exe` starten. Zeigt Windows SmartScreen „Der Computer wurde durch Windows geschützt“, „Weitere Informationen“ wählen, dann „Trotzdem ausführen“. Es öffnet sich kein Fenster; ein Tray-Symbol erscheint.
4. Das Symbol im Infobereich rechts der Taskleiste suchen. Windows kann ein neues Symbol hinter dem Pfeil `^` neben der Uhr verbergen; auf den Pfeil klicken und das Symbol herausziehen, damit es sichtbar bleibt. Mit der Maus darüber fahren: Der Tooltip lautet `Censio Companion: Waiting for game`. Ein zweiter Start von `Censio.Companion.exe` endet sofort ohne Meldung und lässt den ersten weiterlaufen.
5. `/pipe` mit den Ubisoft-Connect-Schritten aus *`/pipe` hinzufügen* zu den Startargumenten des Spiels hinzufügen (jede Kopie, auch eine Steam-Kopie; für eine Steam-Kopie gibt es dort die Alternative). Läuft das Spiel, es vorher beenden: Das Argument wirkt beim nächsten Start des Spiels.
6. Das Spiel aus dem Launcher starten und einen Spielstand laden. Der Tooltip wechselt von `Censio Companion: Waiting for game` zu `Censio Companion: Receiving`. Bleibt er bei `Waiting for game`, siehe die Tabelle der Zustände unter *Verbindung zum Spiel*.
7. Mit der rechten Maustaste auf das Tray-Symbol klicken und `Start recording` wählen. Der Tooltip endet sofort mit `(recording)`; Ordner und Datei entstehen erst mit den ersten Daten des Spiels.
8. Eine Weile spielen. Sobald ein Tick abgeschlossen ist, bekommt der Tooltip eine zweite Zeile `Last tick: <Zeit>, <n> islands`.
9. `Stop recording` wählen. Der Tooltip verliert `(recording)`.
10. `Open recordings folder` wählen. Der Explorer zeigt `%LOCALAPPDATA%\Censio\Recordings` mit einer Datei `recording-<yyyyMMdd-HHmmss>Z.jsonl`. Haben Sie das Spiel während der Aufzeichnung beendet und neu gestartet, besteht die Sitzung aus mehreren Dateien (*Aufzeichnen und Abspielen*). Die Datei ist privat: nicht posten oder anhängen.
11. `Exit` wählen. Das Symbol verschwindet.
12. Die Datei im Windows-Dialog *Ausführen* abspielen, wie unter *Aufzeichnen und Abspielen* beschrieben (beide Pfade mit „Als Pfad kopieren“ kopieren, dann `"<Pfad der Censio.Companion.exe>" --replay "<Pfad der Datei>" --speed max`).
13. Der Tooltip endet bei `Censio Companion: Replay finished`: Die Datei ist lesbar. `Exit` wählen und `Censio.Companion.exe` wie in Schritt 3 für den normalen Betrieb starten.

Vom Product Manager unter Windows 11 (26H2) mit deutschem Ubisoft Connect und einem Companion, aus dem Quellcode mit dem dokumentierten Publish-Befehl gebaut, mit dem echten Spiel ausprobiert: Schritt 5 (Eintragen von `/pipe` und *Schließen*), die Aufzeichnung und ihre Datei, das Tray-Symbol im Überlaufbereich, die Tooltips und Tray-Zustände und das Abspielen mit `Exit` und dem Dialog *Ausführen* (Schritte 7 bis 13 im Allgemeinen). Noch nicht ausprobiert: der Download vom Release `v0.1.0` (die Schritte 1 und 2 wie beschrieben), der SmartScreen-Dialog von Schritt 3, ob ein in Steam gesetztes `/pipe` das Spiel über Ubisoft Connect erreicht (die Bezeichnungen des Steam-Wegs und die englischen Bezeichnungen des Ubisoft-Connect-Wegs, *Close* eingeschlossen, hat der Product Manager bestätigt) und der SmartScreen-Tipp unter *Companion*.

## Erster Start

Server: `http://localhost:8080/` öffnen; die Startseite zeigt die Überschrift `Censio` und die Zeile `Version <version>`. `curl http://localhost:8080/health` antwortet mit `Healthy`, und nach wenigen Sekunden zeigt `docker ps` den Container als `(healthy)`. Beim ersten Start wird der erste Data-Protection-Schlüssel angelegt, und ASP.NET Core meldet einmal die Warnung `No XML encryptor configured` (siehe *Einschränkungen*).

Companion: Das Programm öffnet kein Fenster, bis Sie `Open diagnostics` wählen, und zeigt nur solange dieses Fenster offen ist eine Schaltfläche in der Taskleiste. Im Infobereich erscheint ein Symbol mit dem Tooltip `Censio Companion: <Zustand>` (die Zustände stehen oben unter *Verbindung zum Spiel*); sein Menü enthält den ausgegrauten Status, letzten Tick, die Spielinstallation (`Game: …`, siehe *Spielinstallation* oben) und den Spielkatalog (`Catalog: …`, siehe *Spielkatalog* oben), `Open diagnostics`, `Start recording` (oder `Stop recording`), `Open recordings folder`, `Open log folder`, `Start with Windows` (mit Häkchen, solange es eingeschaltet ist), `Version <version>` (ausgegraut) und `Exit`, das das Symbol entfernt und das Programm innerhalb von 2 Sekunden beendet. Das Symbol ist je nach Zustand ein grauer Ring, ein grüner Kreis oder ein rotes Dreieck. Ein zweiter Start in derselben Windows-Sitzung tut nichts: Er endet sofort ohne Meldung und schreibt keine Logzeile, und der erste läuft weiter. Windows SmartScreen fragt beim ersten Start einmal nach (siehe *Companion* oben).

## Daten und Logs

Server: Das Datenverzeichnis ist im Docker-Image `/config`. Es enthält den Data-Protection-Schlüsselring in `/config/data-protection-keys`; die Schlüssel sind unverschlüsselt, deshalb das Verzeichnis und seine Sicherungen privat halten. Der Server schreibt sein Log auf die Konsole; Lesen mit `docker logs censio`. Die Logzeiten sind UTC. Die Einstellungen stehen in der [README](../README.md#konfiguration).

Companion: Er speichert Spieldaten nur im Katalog-Cache `%LOCALAPPDATA%\Censio\Cache\catalog.bin` (die Namen und Symbole Ihres Spiels, nach einer vollständigen Extraktion gespeichert, nie gesendet und beim Entfernen des Companions behalten, also den Ordner von Hand löschen; er enthält temporäre Dateien nur für den Augenblick eines Speicherns, und der Companion löscht Reste, die älter als ein Tag sind), und das Diagnosefenster speichert nichts (es zeigt den letzten Tick nur auf dem Bildschirm und schreibt nichts auf die Platte oder ins Log). Aufzeichnungsdateien schreibt er nach `%LOCALAPPDATA%\Censio\Recordings` nur, solange eine Aufzeichnung läuft (`--record` oder `Start recording`); sie enthalten die Daten des eigenen Spiels, wie sie gesendet wurden, bleiben auf dem Rechner und werden vom Programm nie gelöscht, also von Hand löschen (*Aufzeichnen und Abspielen*). Außerdem schreibt er ein Log nach `%LOCALAPPDATA%\Censio\Logs`: eine Datei pro Tag, `companion-<yyyyMMdd>.log` (wird eine Datei über 10 MiB groß, wird das Log in `companion-<yyyyMMdd>-2.log` und weiteren Dateien fortgesetzt). Der Companion behält die 10 neuesten dieser Dateien und löscht ältere; andere Dateien im Ordner löscht er nicht. Die Menüeinträge `Open recordings folder` und `Open log folder` öffnen beide Ordner im Explorer. Nur wenn Sie `Start with Windows` einschalten, schreibt er einen Wert `Censio Companion` unter `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` (nur Ihr Konto), und er löscht diesen Wert beim Ausschalten; das Log enthält dann `Autostart enabled` oder `Autostart disabled` und eine Warnung, wenn die Einstellung nicht gelesen oder geändert werden konnte (ohne Dateipfad). Eine Zeile enthält die lokale Zeit, die Stufe und die Meldung: den Start, jede Änderung des Verbindungszustands, Aufzeichnung gestartet und beendet, die Namen der Aufzeichnungsdateien, Beginn und Ende eines Abspielens, das Ergebnis der Suche nach der Spielinstallation (die Quelle, der Build und der Spieleordner oder der Grund des Scheiterns; siehe *Spielinstallation*), das Ergebnis des Katalogs (Anzahl der Einträge oder die Fehlerart), ob der Cache benutzt, geladen oder geschrieben wurde, die Dauer einer Extraktion oder eines Cache-Ladens, die Anzahlen seiner Symbole und die GUIDs, die er nicht benennen kann (siehe *Spielkatalog*), und Probleme. Das Log enthält keine Spieldaten, keine Spielnamen und keine Symbole, aber der Spieleordner bei jedem Start und ein Fehlertext können einen Dateipfad und damit den Windows-Benutzernamen enthalten; prüfen Sie ein Log, bevor Sie es weitergeben. Der Companion liest außerdem die optionale Einstellungsdatei `%LOCALAPPDATA%\Censio\settings.json` und die Installationseinträge von Ubisoft Connect in der Registry, schreibt sie aber nie.

## Probleme melden

Ein GitHub-Issue mit der Vorlage [Bug report](../.github/ISSUE_TEMPLATE/bug_report.md) auf `https://github.com/LarsLaskowski/Censio/issues/new/choose` öffnen. Angeben:

- die Version: die Startseite (`Version <version>`) oder das Tray-Menü des Companions;
- wie er läuft: den `docker run`-Befehl (ohne Geheimnisse und private Pfade) oder wie der Companion gestartet wurde, und das Host-Betriebssystem;
- das Log: einen Auszug aus `docker logs censio` (Tokens und andere Geheimnisse schwärzen); beim Companion die Logdatei des Tages in `%LOCALAPPDATA%\Censio\Logs` (der Tray-Menüeintrag `Open log folder` öffnet den Ordner). **Das Log vor dem Weitergeben auf den Windows-Benutzernamen prüfen:** ein Fehlertext kann einen Dateipfad wie `C:\Users\<Ihr Name>\...` enthalten, deshalb den Namen ersetzen oder die Zeile entfernen. Eine unbearbeitete Aufzeichnung (`--record`) nie an ein Issue anhängen oder öffentlich posten, sie enthält private Namen und Spieldaten; stattdessen das Problem und den Tooltip beschreiben und die Datei aufheben, bis ein Maintainer sagt, wie sie übergeben werden kann. Ein Screenshot des Diagnosefensters zeigt ebenfalls die Inselnamen Ihres Spielstands: Verdecken Sie sie oder beschreiben Sie das Problem stattdessen, bevor Sie ihn anhängen. Den Katalog-Cache (`%LOCALAPPDATA%\Censio\Cache\catalog.bin`) nie anhängen, er enthält die Namen und Symbole Ihres Spiels.

Eine Sicherheitslücke privat melden, wie [SECURITY.md](../SECURITY.md) es beschreibt, nie in einem öffentlichen Issue.
