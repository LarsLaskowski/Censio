# Censio

Censio reads the statistics of a running Anno 117 game on Windows and shows them live and over time in a web application: a Windows Companion extracts the data, a Blazor Server application stores and displays it.

Deutsche Version: [Censio (Deutsch)](#censio-deutsch)

## Status

Censio is in development. The release `v0.4.0` completes milestone M5 (after `v0.3.0`, which completed milestone M4, `v0.2.0`, which completed milestone M3, and `v0.1.0`, which completed milestone M1); it is on the [release page](https://github.com/LarsLaskowski/Censio/releases/tag/v0.4.0) with the Companion ZIP, and the server image is `networlddev/censio:0.4.0`. The state of `main` carries the version `0.0.0`.

What `v0.4.0` can do:

- The Companion runs on Windows as a tray icon. Its tooltip and icon show the state of the connection to the game's statistics pipe (the game needs the launch argument `/pipe`), and its menu shows the status and the last tick. It records what the game sends to a local file, from the menu or with the switch `--record` (*First recording*), and plays such a file back with `--replay <file>` ([user guide](docs/USER_GUIDE.md#first-recording-step-by-step)), also into a server with `--server` and `--token-file` (*Sending a recording to a server*). `Open diagnostics` opens a window with the last tick; regions and goods are shown with their English and German names and their icons, read from your own game installation, kept in a private cache file in `%LOCALAPPDATA%\Censio\Cache` so that the next start does not read the game files again, and never sent (`Catalog: …` in the menu), islands and other values as the game sent them. It writes a log file to `%LOCALAPPDATA%\Censio\Logs`, and `Start with Windows` (off by default) starts it at sign-in.
- Contributors can anonymise a recording with a console tool of the repository, `dotnet run --project src/Censio.Anonymiser -c Release -- <recording> <output>`, which invents the names, perturbs the amounts and shifts the times; stop the recording first, and keep raw recordings private ([user guide](docs/USER_GUIDE.md#anonymising-a-recording-contributors)).
- The server runs as a Docker image. Its start page shows `Version <version>`, and `GET /health` answers `Healthy`. It takes a Companion's data on the WebSocket endpoint `ws://<host>:8080/ingest`, only from a Companion that presents the ingest token the server generated into `/config/ingest-token` or read from the file named by `Censio:Ingest:TokenFile` (the setting `Censio:Ingest:RequireToken` switches the token off, at the risk that anyone who reaches the server can send data). It creates or opens its SQLite database (`/config/censio.db` in the image) at start and logs `The database '<file>' was created.`, `... was opened.` or `... was updated to the current schema.` It stores the profiles, ticks, catalog and icons it receives before it acknowledges them, keeps the last state of each profile and restores it from the database at the next start (before it listens, with the log line `Profiles restored from the database: <n>.`). It keeps a tick for 7 days, then folds older ticks into hourly sums and counts that it keeps without limit (the hour of a profile's latest tick stays as ticks); the database file never shrinks, the space that folding frees is reused ([user guide](docs/USER_GUIDE.md#history-and-deleting-a-profile)).
- The web application sits in a frame with an app bar, a navigation, a light and a dark theme and English and German texts, switched with the buttons in its app bar (see the [user guide](docs/USER_GUIDE.md#web-interface)). It starts light, and in English or German as the browser's language list ranks them; a choice is kept in the browser for a year. Its data pages need no login and show every stored profile to anyone who reaches the server: the region overview (`/regions`, also in the navigation), the island list of a region and the goods of an island with their production, consumption, delta, buildings and productivity, each with the time of its data (`As of …`) and a banner while the Companion is not connected or cannot read the game. The page `Trade routes` (`/trade-routes`, also in the navigation) lists the routes of the profile's latest trade-routes message with their folder, ships, lost ships, major and minor problems and the islands of their stations, the routes with problems or lost ships first, under the time of that message; it needs a game that sends trade routes (pipe protocol 3) and a replay into a server of the same release. A good's name in that table opens the good's page with two trend charts of its production, consumption and delta over a period up to the profile's latest tick, one for the island and one summed over all islands of its region, and the island's page has an overview chart of its buildings, produced goods and goods in deficit above the table. A picker above the charts chooses the period (`Last hour`, `Last 24 hours`, the default, `Last 7 days` or `All`) and a switch the time axis (`Real time` or `Game time`, which places the points at their game time so that a pause of the game leaves no gap and shows no time labels, only the tooltip's game time); the week and `All` show one mean per hour, and the choice is kept in the browser. A line is interrupted where a value is missing or more than 15 minutes pass between two points, never drawn to zero; a hover shows the time and the values; ticks older than 7 days have become hourly means, so a recording older than that shows as hourly means after the next retention run (see the [user guide](docs/USER_GUIDE.md#web-interface)). The pages and charts follow new data without a reload, and the profile switch in the app bar lists the stored profiles. Until a Companion sends data they show `No data yet. The pages fill once the Companion sends data.` The region overview of a profile (`/profiles/<profile>/regions`) has the button `Delete history`, which removes the profile with all its data after a confirmation; it works only on a page opened by IP address, as `localhost` or by a host name that the setting `AllowedHosts` lists ([user guide](docs/USER_GUIDE.md#history-and-deleting-a-profile)).

Not in `v0.4.0` yet, in `main` and a later release: **local mode**. With `"mode": "local"` in the Companion's settings file the Companion hosts the server in its own process, so a player without a home server or NAS gets the pages and the history without Docker. The menu shows `Server: …` and `Open Censio`, which opens the pages in the browser; the pages fill from the live data the Companion sends to it and from a replay, and the data stays in `%LOCALAPPDATA%\Censio\Server`. It is for Windows 11 x64 only; see *Local mode* below, with its accepted risk.

Also not in `v0.4.0` yet, in `main` and a later release: **forward mode**. With `"mode": "forward"`, `"serverAddress"` and the server's `"serverToken"` in the Companion's settings file the Companion sends the live data to a server that runs elsewhere, with the menu line `Server: …`, and reconnects on its own; see *Forward mode* below, with its warnings.

What `v0.4.0` cannot do:

- Names and icons in the Companion only in the diagnostics window (regions and goods): the Companion loads them from its cache or reads them from your installation at every start, and reads them again when the game was updated (it checks when the game connects), and shows a bare number (GUID) where it cannot name one, and no icon where the game's files give none (the log line `Catalog icons: …` counts them); buildings and workforce have no column yet.
- Nothing is sent from the Companion to the server except a recording you replay with `--server` (*Sending a recording to a server*); live sending is not in this release (*Forward mode*, in `main`), so the pages fill only from a replay.
- The charts show at most the newest 2,880 points and only a good's production, consumption and delta and an island's buildings, produced goods and goods in deficit: there is no period of your own choosing (dates, zoom), the game-time axis has no time labels, and the trend column of the goods table and the island list shows a fixed last 4 hours (the newest 240 points at most, so a replay faster than about one tick per 75 seconds shows less) and no other window.
- The Companion has no settings for a server address or a token; a replay takes them as the switches `--server` and `--token-file` (forward mode, in `main`, reads them from the settings file).

## Getting Censio

- Releases: once a release exists, it is listed on `https://github.com/LarsLaskowski/Censio/releases` with the Companion ZIP under *Assets*; the Docker image of the same version is published on Docker Hub as `networlddev/censio:<version>`.
- Edge builds: none are offered.
- The current state of `main`, or any state without a release, is built from source. Get the source with `git clone`, then change into the folder:

```text
git clone https://github.com/LarsLaskowski/Censio.git
```

```text
cd Censio
```

Alternatively use *Code*, *Download ZIP* on the GitHub page of the repository and extract it. Run the commands of the source builds below in that folder, one at a time.

Not supported: arm64 hosts, Windows containers, and Windows other than Windows 10 22H2 or Windows 11 (x64); local mode (*Local mode* below) is for Windows 11 only.

## Server (Docker)

The Censio server runs as the Docker image `networlddev/censio` (linux/amd64) on Docker Hub. Every release publishes the tag `<version>` (for example `0.4.0`) and moves the tag `latest` to the newest release. You need Docker 23 or later (Linux Docker Engine, or Docker Desktop for Windows with the WSL 2 backend and Linux containers). The commands below use the version `0.4.0`; replace it with the version you want, or with `latest`. Run the commands one at a time: a command that fails makes the next one fail too. Not yet tried: pulling and running the published image `0.4.0`.

```text
docker pull networlddev/censio:0.4.0
```

```text
docker run -d --name censio -p 8080:8080 -v censio-config:/config networlddev/censio:0.4.0
```

Check that it answers (the response is `Healthy`; not yet tried: in Windows PowerShell 5.1 `curl` is an alias of `Invoke-WebRequest`, which should show `StatusCode` `200` and `Content` `Healthy` and may ask for a confirmation first) and read its log:

```text
curl http://localhost:8080/health
```

```text
docker logs censio
```

Open `http://localhost:8080/` for the start page; it shows `Version 0.4.0`. If a run failed, remove the container before starting it again, because its name is taken:

```text
docker rm -f censio
```

If port 8080 is taken on the host, Docker reports "port is already allocated"; map another host port, for example `-p 9000:8080`. The server speaks plain HTTP without a login; to reach it from the host alone, publish it on the loopback address (a published port bypasses host firewalls such as ufw and firewalld on Linux):

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.4.0
```

The server of the release `0.3.0` and of later releases (not the image `0.2.0`) takes a Companion's data on the WebSocket endpoint `ws://<host>:8080/ingest` and accepts it only with an ingest token. The server generates the token at its first start and stores it in the file `/config/ingest-token` (mode `0600`, owned by the container user); it never writes it to the log. Read it with the command below, put it in the file that a replay's `--token-file` names (*Sending a recording to a server*), and rotate it by stopping the server, deleting the file and starting the server again. Alternatively, `Censio:Ingest:TokenFile` names a file of your own that holds the token (32 to 1,024 visible ASCII characters); the server then reads that file instead of `ingest-token` and the [user guide](docs/USER_GUIDE.md) shows how on Linux Docker Engine. Not yet tried: these commands in a container and on NAS hardware.

```text
docker exec censio cat /config/ingest-token
```

To change a setting or to move to a newer image, recreate the container on the same volume: `docker stop censio`, `docker rm censio`, then the same `docker run` command with the new tag. The volume `censio-config` keeps the key ring, the ingest token and the database, so the Companion's token keeps working; pull `latest` first (`docker pull networlddev/censio:latest`), because `docker run` reuses a local `latest`. Without `-v` the data is lost with the container, an update from `0.2.0` keeps only the key ring, so read the new token and put it in the token file of a replay (`--token-file`), and an update from `0.3.0` updates the database to the current schema and keeps its profiles and ticks as the start of the history, but refuses a Companion of `0.3.0`. The [user guide](docs/USER_GUIDE.md) lists the steps.

The [user guide](docs/USER_GUIDE.md) describes the data directory, the token, NAS hosts, TLS and the limits.

### Build the server from source

This builds the current state of the repository into a local image. It needs Docker 23 or later with BuildKit and access to `mcr.microsoft.com`. Run it in the root folder of the checkout (*Getting Censio*):

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

## Companion

The Companion is the Windows program that reads the statistics of the running game. Download `Censio-<version>-win-x64.zip` from the release page `https://github.com/LarsLaskowski/Censio/releases` (under *Assets* of the release). It needs Windows 10 22H2 or Windows 11, 64-bit; it needs no installed .NET, no installer and no administrator rights. Not yet tried: the ZIP of `v0.4.0` on a machine without .NET.

1. Extract the ZIP to a folder of your choice. It holds one file, `Censio.Companion.exe`.
2. Start `Censio.Companion.exe`. A tray icon appears; its menu shows the status, the last tick, the game installation (`Game: build <build> (Ubisoft Connect)`, found without any setting), the game catalog (`Catalog: reading`, then `Catalog: checking`, `Catalog: extracting names`, `Catalog: extracting icons, <n> entries` and `Catalog: <n> entries`, or `Catalog: <n> entries` at once from the cache; `Catalog: no game` or `Catalog: failed` otherwise, see the [user guide](docs/USER_GUIDE.md#game-catalog)), `Open diagnostics`, `Start recording`, `Open recordings folder`, `Open log folder`, `Start with Windows`, `Version <version>` and `Exit`; in local mode (*Local mode* below) also `Server: …` and `Open Censio`, in forward mode (*Forward mode* below) `Server: …`.

The program is not signed, so Windows SmartScreen may show "Windows protected your PC" the first time. Choose "More info", then "Run anyway" (on a German Windows: "Weitere Informationen", then "Trotzdem ausführen"). Tip reported by the Product Manager from a German Windows, not tried against a real release download: open the properties of the extracted `Censio.Companion.exe` (or of the ZIP itself), tick *Zulassen* (on an English Windows probably *Unblock*; not yet tried) and apply; SmartScreen then does not ask.

#### Start with Windows

The Companion does not start with Windows unless you turn it on: choose `Start with Windows` in the tray menu. A check mark shows that it is on; choosing the entry again turns it off. Turning it on writes one value, `Censio Companion`, under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`: it applies to your Windows account only, and the entry needs no administrator rights. The value is the full path of the running `Censio.Companion.exe` in quotes, without arguments, so the Companion starts the way a double click starts it (it neither records nor replays).

- Extract the ZIP to a folder in your own profile that stays where it is, for example `%LOCALAPPDATA%\Programs\Censio`. Do not use a shared folder or the root of a drive that other users can write to: whoever can replace the file there runs it at your next sign-in.

Paths that cannot be used, a leftover entry after moving the exe, and the Windows startup apps list are covered in the [user guide](docs/USER_GUIDE.md#start-with-windows).

#### First recording

1. Add `/pipe` to the launch arguments of the game in Ubisoft Connect (*Library*, the three dots on Anno 117, *Manage*, *Properties*, field *Add launch arguments*, then *Close*); a Steam copy has an alternative in the user guide. The argument takes effect at the next game start.
2. Start the game and load a save. The tray tooltip changes from `Waiting for game` to `Receiving`.
3. Right-click the tray icon and choose `Start recording`.
4. Play, then choose `Stop recording`.
5. The file `recording-<yyyyMMdd-HHmmss>Z.jsonl` is in `%LOCALAPPDATA%\Censio\Recordings` (`Open recordings folder`). It is private: do not post or attach it.

Not yet tried: whether `/pipe` set in Steam reaches the game through Ubisoft Connect, and the download from the release `v0.4.0`. The full walkthrough, the launcher steps, the tray states with what to do, and how to replay a recording are in the [user guide](docs/USER_GUIDE.md#first-recording-step-by-step).

#### Sending a recording to a server

To try the server without the game, the Companion can send a recording to it: `Censio.Companion.exe --replay "<full path of the recording>" --server ws://<host>:8080/ingest --token-file "<full path of the token file>" --speed 10`. Use a server of the same release as the Companion, not the image `0.2.0`: the two refuse each other when their contract versions differ, as the releases `0.3.0` and `0.4.0` do (this Companion shows `server does not support contract version 2`, a Companion of `0.3.0` `server does not support contract version 1`); the token file holds the server's ingest token (without `--token-file` only a server with the token switched off accepts the replay). The recording becomes a new profile `Replay <time>` on the server. Over `ws://` the token and the data cross the network unencrypted, a raw recording belongs only in your own server, and the token file stays private and is deleted when the testing ends. The steps, the server from source, the token file, the fixture `long-session.jsonl`, the status texts and the errors are in the [user guide](docs/USER_GUIDE.md#sending-a-recording-to-a-server). Tried by the Product Manager on Windows at `--speed 10` with the fixture `long-session.jsonl`: a server in Docker (image built from this state of the repository) and a server from source both filled the pages (regions and islands), and a token file written by Windows PowerShell 5.1 with `>` was accepted. Not yet tried: `docker cp` for the token file, a token file saved by Notepad as UTF-16 BE, `wss://` on Windows, the Windows certificate store, a configured system proxy and a remote Docker host.

#### Local mode

Local mode lets the Companion host the server itself, so the pages and the history need no Docker and no NAS. It is off by default, it needs a Companion of a release after `v0.4.0` or a build of `main`, and it is for **Windows 11, 64-bit** only: on Windows 10 22H2 the Companion runs as above, but local mode is not supported there (it is neither tried nor refused). It needs no installed .NET, no installer, no administrator rights and no firewall permission.

1. Close a running Companion (`Exit` in the tray menu). Create the file `%LOCALAPPDATA%\Censio\settings.json`, or add the keys to the one you have, with `"mode": "local"` and, if you want another port than 5000, `"localPort"`:

```json
{
  "mode": "local",
  "localPort": 5117
}
```

2. Start `Censio.Companion.exe`. Below `Catalog: …` the menu shows `Server: starting`, then `Server: running at http://localhost:5117/` (`5000` without `localPort`), and `Open Censio`, directly above `Open diagnostics`, becomes enabled. Choose `Open Censio`: your default browser opens the pages; the start page shows the version.
3. The pages are empty (`No data yet. …`) until the game sends: the Companion sends the live data to its server as in *Forward mode*, to the profile `Live`; you can fill them from a replay as well. Choose `Exit` (a running Companion ends a second start silently), then start `Censio.Companion.exe --replay "<full path of the recording>" --speed 10`, without `--server`. The Companion starts its server, waits for it (`Replay: Connecting to server`), sends the recording with the server's own token, so that you need no token file, and ends at `Replay finished: <n> ticks sent, <r> refused`. Choose `Open Censio` and pick the profile `Replay <time>` in the profile switch. With `--server` the replay goes to that address as in *Sending a recording to a server*. `Exit` stops the server within 5 seconds; after the next start in local mode the pages show the same profiles and history.

The keys are read once at start (a change counts after a restart) and matched ignoring case: `mode` is `local` (or `forward`, *Forward mode*; any other text is invalid; a blank `mode` counts as none), `localPort` an integer from 1 to 65535 written without quotes or decimal point (`5117`, not `"5117"` or `5117.0`) and read only with `mode` `local`. The server's data lies in `%LOCALAPPDATA%\Censio\Server`: the database `censio.db`, the key ring and the token file `ingest-token`, private to your Windows account and kept when you remove the Companion; delete the folder by hand to start empty.

What the menu line says:

| Line | Meaning | What to do |
| ---- | ------- | ---------- |
| `Server: starting` | The server starts in the background | Wait |
| `Server: running at http://localhost:<port>/` | It runs; `Open Censio` is enabled | Nothing |
| `Server: port <port> in use` | Another program, a `Censio.Server` or the Companion of another Windows user holds the port | Close it, or set another `localPort` and restart the Companion |
| `Server: failed` | Any other failure: the folder or the database cannot be used, a newer Censio wrote the database, or the server stopped; a port that Windows reserves fails this way too | Choose `Open log folder` and read the log of the day (it names the reason, without the token); try another `localPort`; a restart tries again |
| `Server: settings invalid` | A value of `mode` or `localPort` is wrong (the log names it); no server starts, the game search is not affected | Correct the file and restart |

A replay without `--server` ends with `Replay error: local server not running` when the server does not start. `Open Censio` that cannot start a browser only writes a warning to the log.

**Accepted risk.** The server listens on `127.0.0.1` and `[::1]` only, so no other device of your network reaches it, and it answers only the host names `localhost`, `127.0.0.1` and `[::1]` (any other, and a request without a host name, with `400`), so a page of another site cannot read it through your browser. But the pages need no login: every program and every other signed-in Windows user on the same PC can reach the port, read every profile and delete history. Without the ingest token, which lies in your private folder, none of them can send data. Use local mode only on a PC whose users and programs you trust. Tried by the Product Manager on Windows 11 x64 without .NET: the release ZIP with the steps above, that no firewall prompt appears, and a port held by a standalone `Censio.Server`. Not yet tried: a port that Windows reserves. Details in the [user guide](docs/USER_GUIDE.md#local-mode).

#### Forward mode

Forward mode lets the Companion send what the game reports, live, to a server that runs elsewhere (Docker on a NAS or a home server), and reconnect on its own after a server restart or a network loss. It is off by default, it needs a Companion of a release after `v0.4.0` or a build of `main` and a server of the same release (contract version 2), and it runs wherever the Companion does (Windows 10 22H2 and Windows 11, 64-bit).

1. Close a running Companion (`Exit` in the tray menu). Create the file `%LOCALAPPDATA%\Censio\settings.json`, or add the keys to the one you have. `serverAddress` is the server's ingest address with its path, and `serverToken` the server's ingest token (for the Docker image `docker exec censio cat /config/ingest-token`; leave the key out when the server has the token switched off, `Censio:Ingest:RequireToken` `false`):

```json
{
  "mode": "forward",
  "serverAddress": "ws://nas:8080/ingest",
  "serverToken": "<the server's ingest token>"
}
```

2. Start `Censio.Companion.exe`. Below `Catalog: …` the menu shows `Server: connecting`, then `Server: connected`, also while the game is not running. When the game sends, its ticks, trade routes and session events reach the server in the order read, and the pages update live.
3. A failure shows `Server: unreachable, retrying`, `Server: not answering, retrying`, `Server: connection lost, retrying`, `Server: invalid reply, retrying` or `Server: another Companion is connected, retrying`, and the Companion tries again after 1, 2, 4, 8 and 16 seconds, then every 30 seconds. A connection that delivers nothing is found dead within 60 seconds. A wrong token (`Server: token refused`) and a server that does not know contract version 2 (`Server: does not support contract version 2`) are not retried: correct the file or update, and restart the Companion.

The keys are read once at start (a change counts after a restart) and matched ignoring case, only with `mode` `forward`. `serverAddress` is an absolute `ws://` or `wss://` address with a host, without user name, password, fragment or white space; `serverToken` is 32 to 1,024 visible ASCII characters. A wrong value shows `Server: settings invalid`, sends nothing and is named in the log; the game folder is not affected. What waits during an outage is kept in memory (at most 64 messages and 16 MiB, the oldest are dropped, the pages then show a gap) and is lost when you choose `Exit`.

**Warnings.** Until profiles can be chosen, everything goes to the one profile `Live`: use one playthrough per server, or delete `Live`'s history on the pages between playthroughs. Over `ws://` the token and the data cross the network unencrypted, at every start and every retry, to whatever host answers the address; a name without a domain (such as `nas`) may be answered by any device of the network, so use `wss://`, or an address on a network you trust, when the PC leaves your home network. `settings.json` now holds the token in plain text: do not share the file. The steps, the texts and what is lost are in the [user guide](docs/USER_GUIDE.md#forward-mode).

### Build the Companion from source

Building needs the .NET 11 SDK (version 11.0.100 or a later patch or feature band), access to `nuget.org` and the source (*Getting Censio*), on any operating system the SDK supports; only running the program needs Windows 10 22H2 or Windows 11, x64. Run the command in the root folder of the checkout. It uses the flags of the release build, except the version, and writes to the folder `publish`:

```text
dotnet publish src/Censio.Companion/Censio.Companion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -o publish
```

The folder `publish` then holds only `Censio.Companion.exe`. Start it on Windows (`publish\Censio.Companion.exe`); the tray menu shows `Version 0.0.0`. The command is written to run in PowerShell 7 and Windows PowerShell 5.1. The maintainer published the Companion with this command, started it, and its tray menu showed `Version 0.0.0`; which PowerShell was used was not recorded. Not yet tried: Windows PowerShell 5.1.

## Configuration

| Setting | Environment variable | Default | Meaning |
| ------- | -------------------- | ------- | ------- |
| `Censio:DataDirectory` | `CENSIO__DATADIRECTORY` | unset outside the Docker image, `/config` in the image | Absolute path of the server's data directory; it holds the data-protection keys in `data-protection-keys` and, only while `Censio:Ingest:TokenFile` is unset, the ingest token in the file `ingest-token`, and the database `censio.db`. |
| `Censio:Database:File` | `CENSIO__DATABASE__FILE` | unset: `censio.db` in the data directory (`/config/censio.db` in the image) | Absolute path of the server's SQLite database file; its folder is created when missing, and no data directory is needed for it. Without this setting and without a data directory the database is kept in memory and lost when the server stops (the server logs a warning). A path that is not absolute (the empty string included), a file that cannot be written, a file that is not a Censio database, a database written by a newer version and a failed update to the current schema stop the start. Keep the file on a local disk, not on a network share. |
| `ASPNETCORE_HTTP_PORTS` | `ASPNETCORE_HTTP_PORTS` | `8080` in the Docker image; outside it the server listens on `http://localhost:5000` | Port the server listens on inside the Docker image. |
| `AllowedHosts` | `ALLOWEDHOSTS` | `*` (host filtering off) | The host names the server answers, separated by `;` (`*.<domain>` for a domain). With names listed, a request for any other host answers `400`, so list every name and address the server is reached by, `127.0.0.1` for the image's health check included. The list also decides which pages may delete a profile's history: IP addresses and `localhost` always may, a name only when listed. Behind a reverse proxy the proxy must pass the client's host (nginx: `proxy_set_header Host $host;`) and answer only its own names ([user guide](docs/USER_GUIDE.md#history-and-deleting-a-profile)). |
| `Censio:Ingest:RequireToken` | `CENSIO__INGEST__REQUIRETOKEN` | `true` | Whether the endpoint `/ingest` requires the ingest token (`true`, the default) or accepts any Companion (`false`, any case). With `false` the server logs a warning and anyone who reaches it can send data: use it only on a network where every host is trusted. Any other value, the empty string included, stops the start. |
| `Censio:Ingest:TokenFile` | `CENSIO__INGEST__TOKENFILE` | unset | Absolute path of a file that holds the ingest token (32 to 1,024 visible ASCII characters), read at start instead of `ingest-token`. The server never creates, changes or logs the file or its path. A path that is not absolute (the empty string included), a file that cannot be read or holds no valid token, and the combination with `Censio:Ingest:RequireToken` `false` stop the start. |

The Companion has optional settings, in the file `%LOCALAPPDATA%\Censio\settings.json`. It is read once at start and never written by the Companion; create it by hand if the menu does not show your game (`Game: not found`), for example for a Steam copy, to switch on local mode (*Local mode*) or to send live data to a server (*Forward mode*):

```json
{
  "gameFolder": "C:\\Games\\Anno 117"
}
```

| Setting | Meaning |
| ------- | ------- |
| `gameFolder` | The folder of your Anno 117 installation: the one that holds `maindata\config.rda` and `bin\win64\anno117.exe`. Write a full path and double every backslash (`C:\\Games\\Anno 117`), or use forward slashes. Variables such as `%USERPROFILE%` are not expanded. Without the setting the Companion looks in Ubisoft Connect's records and its default games folder. |
| `mode` | `"local"` makes the Companion host the server in its own process (*Local mode*), `"forward"` makes it send live data to the server of `serverAddress` (*Forward mode*); without the key, or with a blank text, both are off. Any other value is invalid and shows `Server: settings invalid`. |
| `localPort` | With `mode` `local`: the port of the hosted server, an integer from 1 to 65535 without quotes; default `5000`. Not read without `mode` `local`. The data lies in `%LOCALAPPDATA%\Censio\Server`. |
| `serverAddress` | With `mode` `forward`: the server's ingest address, an absolute `ws://` or `wss://` address with a host and its path (`ws://nas:8080/ingest`), without user name, password, fragment or white space; required. Not read without `mode` `forward`. |
| `serverToken` | With `mode` `forward`: the server's ingest token, 32 to 1,024 visible ASCII characters (surrounding space and line breaks are trimmed; write `"` and `\` in JSON as `\"` and `\\`); leave it out when the server has the token switched off. The file then holds a credential: do not share it. |

The key is read ignoring case, comments and trailing commas are accepted. The menu line `Game: …` shows the result, and a wrong folder or a file that cannot be read shows its reason there and in the log: `Game: searching` (wait, it ends by itself); `Game: build <build> (Ubisoft Connect)` or `Game: build <build> (settings file)` (found, nothing to do); `Game: build unknown (<source>)` (found, but the program file has no readable version; the game is still used); `Game: not found` (name the folder in `gameFolder`); `Game: folder in settings not found` (correct the path); `Game: folder in settings is not a full path` (write the full path starting with the drive letter); `Game: folder in settings is not an Anno 117 installation` (name the folder that holds `maindata` and `bin`); `Game: settings file invalid` (correct the file; the log names the reason). The [user guide](docs/USER_GUIDE.md#game-installation) explains each text. A wrong `mode`, `localPort`, `serverAddress` or `serverToken` shows `Server: settings invalid` and does not affect the game folder.

## User guide

The [user guide](docs/USER_GUIDE.md) covers installation, the first start, data and logs, and how to report a problem. Report a security vulnerability privately as [SECURITY.md](SECURITY.md) describes, never in a public issue.

## Licence

Censio is released under the MIT licence; see [LICENSE](LICENSE).

# Censio (Deutsch)

Censio liest die Statistik eines laufenden Anno-117-Spiels unter Windows und zeigt sie live und im Verlauf in einer Webanwendung: Ein Windows-Companion extrahiert die Daten, eine Blazor-Server-Anwendung speichert und zeigt sie an.

English version: [Censio](#censio)

## Status

Censio ist in Entwicklung. Das Release `v0.4.0` schließt den Meilenstein M5 ab (nach `v0.3.0`, das den Meilenstein M4 abgeschlossen hat, `v0.2.0`, das den Meilenstein M3 abgeschlossen hat, und `v0.1.0`, das den Meilenstein M1 abgeschlossen hat); es steht mit dem Companion-ZIP auf der [Release-Seite](https://github.com/LarsLaskowski/Censio/releases/tag/v0.4.0), und das Server-Image ist `networlddev/censio:0.4.0`. Der Stand von `main` trägt die Version `0.0.0`.

Das kann `v0.4.0`:

- Der Companion läuft unter Windows als Tray-Symbol. Sein Tooltip und Symbol zeigen den Zustand der Verbindung zur Statistik-Pipe des Spiels (das Spiel braucht das Startargument `/pipe`), und sein Menü zeigt den Status und den letzten Tick. Er zeichnet auf, was das Spiel sendet, in eine lokale Datei, über das Menü oder mit dem Schalter `--record` (*Erste Aufzeichnung*), und spielt eine solche Datei mit `--replay <Datei>` ab ([Benutzerhandbuch](docs/USER_GUIDE.md#erste-aufzeichnung-schritt-für-schritt)), auch in einen Server mit `--server` und `--token-file` (*Eine Aufzeichnung an einen Server senden*). `Open diagnostics` öffnet ein Fenster mit dem letzten Tick; Regionen und Waren werden mit ihren englischen und deutschen Namen und ihren Symbolen gezeigt, die aus Ihrer eigenen Spielinstallation gelesen, in einer privaten Cache-Datei in `%LOCALAPPDATA%\Censio\Cache` aufbewahrt, damit der nächste Start die Spieldateien nicht erneut liest, und nie gesendet werden (`Catalog: …` im Menü), Inseln und die übrigen Werte so, wie das Spiel sie gesendet hat. Er schreibt eine Logdatei nach `%LOCALAPPDATA%\Censio\Logs`, und `Start with Windows` (standardmäßig aus) startet ihn bei der Anmeldung.
- Mitwirkende können eine Aufzeichnung mit einem Konsolenwerkzeug des Repositorys anonymisieren, `dotnet run --project src/Censio.Anonymiser -c Release -- <Aufzeichnung> <Ausgabe>`; es erfindet die Namen, verändert die Beträge und verschiebt die Zeiten. Die Aufzeichnung vorher beenden, unbearbeitete Aufzeichnungen bleiben privat ([Benutzerhandbuch](docs/USER_GUIDE.md#eine-aufzeichnung-anonymisieren-für-mitwirkende)).
- Der Server läuft als Docker-Image. Seine Startseite zeigt `Version <version>`, und `GET /health` antwortet mit `Healthy`. Er nimmt die Daten eines Companions auf dem WebSocket-Endpunkt `ws://<Host>:8080/ingest` entgegen, und zwar nur von einem Companion, der das Ingest-Token vorlegt, das der Server in `/config/ingest-token` erzeugt oder aus der in `Censio:Ingest:TokenFile` genannten Datei gelesen hat (die Einstellung `Censio:Ingest:RequireToken` schaltet das Token ab, mit dem Risiko, dass jeder, der den Server erreicht, Daten senden kann). Er legt beim Start seine SQLite-Datenbank (im Image `/config/censio.db`) an oder öffnet sie und schreibt `The database '<Datei>' was created.`, `... was opened.` oder `... was updated to the current schema.` ins Log. Er speichert die Profile, Ticks, den Katalog und die Symbole, die er empfängt, bevor er sie bestätigt, hält den letzten Stand jedes Profils und stellt ihn beim nächsten Start aus der Datenbank wieder her (vor dem Lauschen, mit der Logzeile `Profiles restored from the database: <n>.`). Er behält einen Tick 7 Tage und fasst ältere Ticks dann zu Stundensummen und -anzahlen zusammen, die er ohne Begrenzung behält (die Stunde des letzten Ticks eines Profils bleibt als Ticks stehen); die Datenbankdatei schrumpft nie, der Platz, den das Zusammenfassen freigibt, wird wiederverwendet ([Benutzerhandbuch](docs/USER_GUIDE.md#verlauf-und-löschen-eines-profils)).
- Die Webanwendung steht in einem Rahmen mit Kopfleiste, Navigation, einem hellen und einem dunklen Design sowie englischen und deutschen Texten, umgeschaltet mit den Schaltflächen der Kopfleiste (siehe das [Benutzerhandbuch](docs/USER_GUIDE.md#weboberfläche)). Sie startet hell und auf Englisch oder Deutsch, wie die Sprachliste des Browsers sie einordnet; eine Auswahl bleibt ein Jahr im Browser gespeichert. Ihre Datenseiten brauchen keine Anmeldung und zeigen jedem, der den Server erreicht, alle gespeicherten Profile: die Regionsübersicht (`/regions`, auch in der Navigation), die Inselliste einer Region und die Waren einer Insel mit Produktion, Verbrauch, Delta, Gebäuden und Produktivität, jeweils mit der Zeit ihrer Daten (`Stand: …`) und einem Hinweis, solange der Companion nicht verbunden ist oder das Spiel nicht lesen kann. Die Seite `Handelsrouten` (`/trade-routes`, auch in der Navigation) listet die Routen der letzten Handelsrouten-Nachricht des Profils mit Ordner, Schiffen, verlorenen Schiffen, großen und kleinen Problemen und den Inseln ihrer Stationen, die Routen mit Problemen oder verlorenen Schiffen zuerst, unter der Zeit dieser Nachricht; sie braucht ein Spiel, das Handelsrouten sendet (Pipe-Protokoll 3), und ein Abspielen in einen Server desselben Releases. Der Name einer Ware in dieser Tabelle öffnet die Seite der Ware mit zwei Trenddiagrammen ihrer Produktion, ihres Verbrauchs und ihres Deltas über einen Zeitraum bis zum letzten Tick des Profils, eines für die Insel und eines als Summe über alle Inseln ihrer Region, und die Seite der Insel zeigt über der Tabelle ein Übersichtsdiagramm ihrer Gebäude, produzierten Waren und Waren im Defizit. Eine Auswahl über den Diagrammen wählt den Zeitraum (`Letzte Stunde`, `Letzte 24 Stunden`, voreingestellt, `Letzte 7 Tage` oder `Gesamt`) und ein Schalter die Zeitachse (`Echtzeit` oder `Spielzeit`, die die Punkte an ihrer Spielzeit anordnet, sodass eine Spielpause keine Lücke lässt und keine Zeitbeschriftungen zeigt, nur die Spielzeit im Tooltip); die Woche und `Gesamt` zeigen ein Mittel je Stunde, und die Wahl bleibt im Browser gespeichert. Eine Linie wird unterbrochen, wo ein Wert fehlt oder mehr als 15 Minuten zwischen zwei Punkten vergehen, und nie auf null gezogen; beim Darüberfahren erscheinen die Zeit und die Werte; Ticks, die älter als 7 Tage sind, sind zu Stundenmitteln geworden, eine ältere Aufzeichnung erscheint deshalb nach dem nächsten Aufbewahrungslauf als Stundenmittel (siehe das [Benutzerhandbuch](docs/USER_GUIDE.md#weboberfläche)). Die Seiten und Diagramme folgen neuen Daten ohne Neuladen, und der Profilschalter in der Kopfleiste listet die gespeicherten Profile. Bis ein Companion Daten sendet, zeigen sie `Noch keine Daten. Die Seiten füllen sich, sobald der Companion Daten sendet.` Die Regionenübersicht eines Profils (`/profiles/<Profil>/regions`) hat die Schaltfläche `Verlauf löschen`, die das Profil nach einer Bestätigung mit allen Daten entfernt; sie funktioniert nur auf einer Seite, die über die IP-Adresse, als `localhost` oder über einen Hostnamen geöffnet wurde, den die Einstellung `AllowedHosts` nennt ([Benutzerhandbuch](docs/USER_GUIDE.md#verlauf-und-löschen-eines-profils)).

Noch nicht in `v0.4.0`, aber in `main` und einem späteren Release: der **lokale Modus**. Mit `"mode": "local"` in der Einstellungsdatei des Companions betreibt der Companion den Server im eigenen Prozess, sodass ein Spieler ohne Heimserver oder NAS die Seiten und den Verlauf ohne Docker bekommt. Das Menü zeigt `Server: …` und `Open Censio`, das die Seiten im Browser öffnet; die Seiten füllen sich durch die Live-Daten, die der Companion an ihn sendet, und durch ein Abspielen, und die Daten bleiben in `%LOCALAPPDATA%\Censio\Server`. Er ist nur für Windows 11 x64; siehe *Lokaler Modus* unten, mit seinem akzeptierten Risiko.

Auch noch nicht in `v0.4.0`, aber in `main` und einem späteren Release: der **Weiterleitungsmodus**. Mit `"mode": "forward"`, `"serverAddress"` und dem `"serverToken"` des Servers in der Einstellungsdatei des Companions sendet der Companion die Live-Daten an einen Server, der anderswo läuft, mit der Menüzeile `Server: …`, und verbindet sich von selbst neu; siehe *Weiterleitungsmodus* unten, mit seinen Warnungen.

Das kann `v0.4.0` nicht:

- Namen und Symbole im Companion nur im Diagnosefenster (Regionen und Waren): Der Companion lädt sie bei jedem Start aus seinem Cache oder liest sie aus Ihrer Installation und liest sie erneut, wenn das Spiel aktualisiert wurde (er prüft das, wenn das Spiel sich verbindet), und zeigt eine blanke Zahl (GUID), wo er keinen Namen findet, und kein Symbol, wo die Spieldateien keines liefern (die Logzeile `Catalog icons: …` zählt sie); Gebäude und Arbeitskräfte haben noch keine Spalte.
- Der Companion sendet nichts an den Server, außer einer Aufzeichnung, die Sie mit `--server` abspielen (*Eine Aufzeichnung an einen Server senden*); das Senden im laufenden Betrieb gibt es in diesem Release nicht (*Weiterleitungsmodus*, in `main`), die Seiten füllen sich also nur durch ein Abspielen.
- Die Diagramme zeigen höchstens die neuesten 2.880 Punkte und nur Produktion, Verbrauch und Delta einer Ware sowie Gebäude, produzierte Waren und Waren im Defizit einer Insel: Es gibt keinen frei gewählten Zeitraum (Datum, Zoom), die Spielzeitachse hat keine Zeitbeschriftungen, die Trendspalte der Warentabelle und der Inselliste zeigt ein festes Fenster der letzten 4 Stunden (höchstens die neuesten 240 Punkte, daher zeigt ein Abspielen, das schneller als etwa ein Tick je 75 Sekunden ist, weniger) und kein anderes.
- Der Companion hat keine Einstellungen für eine Serveradresse oder ein Token; ein Abspielen nimmt sie als Schalter `--server` und `--token-file` (der Weiterleitungsmodus, in `main`, liest sie aus der Einstellungsdatei).

## Censio beziehen

- Releases: Sobald ein Release existiert, steht es auf `https://github.com/LarsLaskowski/Censio/releases`, mit dem Companion-ZIP unter *Assets*; das Docker-Image derselben Version wird auf Docker Hub als `networlddev/censio:<version>` veröffentlicht.
- Edge-Builds: Es werden keine angeboten.
- Der aktuelle Stand von `main` oder jeder Stand ohne Release wird aus dem Quellcode gebaut. Den Quellcode mit `git clone` holen und dann in den Ordner wechseln:

```text
git clone https://github.com/LarsLaskowski/Censio.git
```

```text
cd Censio
```

Alternativ auf der GitHub-Seite des Repositorys *Code*, *Download ZIP* wählen und das ZIP entpacken. Die Befehle der Quellcode-Builds unten in diesem Ordner ausführen, einzeln nacheinander.

Nicht unterstützt: arm64-Hosts, Windows-Container und Windows außer Windows 10 22H2 oder Windows 11 (x64); der lokale Modus (*Lokaler Modus* unten) ist nur für Windows 11.

## Server (Docker)

Der Censio-Server läuft als Docker-Image `networlddev/censio` (linux/amd64) auf Docker Hub. Jedes Release veröffentlicht den Tag `<version>` (zum Beispiel `0.4.0`) und setzt den Tag `latest` auf das neueste Release. Voraussetzung ist Docker 23 oder neuer (Docker Engine unter Linux oder Docker Desktop für Windows mit WSL-2-Backend und Linux-Containern). Die Befehle unten verwenden die Version `0.4.0`; sie durch die gewünschte Version oder durch `latest` ersetzen. Die Befehle werden einzeln ausgeführt: Schlägt ein Befehl fehl, schlägt auch der nächste fehl. Noch nicht ausprobiert: das Laden und Starten des veröffentlichten Images `0.4.0`.

```text
docker pull networlddev/censio:0.4.0
```

```text
docker run -d --name censio -p 8080:8080 -v censio-config:/config networlddev/censio:0.4.0
```

Prüfen, ob er antwortet (die Antwort lautet `Healthy`; noch nicht ausprobiert: in Windows PowerShell 5.1 ist `curl` ein Alias für `Invoke-WebRequest`, das `StatusCode` `200` und `Content` `Healthy` anzeigen sollte und vorher eine Bestätigung verlangen kann), und sein Log lesen:

```text
curl http://localhost:8080/health
```

```text
docker logs censio
```

Die Startseite liegt unter `http://localhost:8080/`; sie zeigt `Version 0.4.0`. Ist ein Start fehlgeschlagen, den Container vor einem neuen Start entfernen, weil sein Name belegt ist:

```text
docker rm -f censio
```

Ist Port 8080 auf dem Host belegt, meldet Docker „port is already allocated“; dann einen anderen Host-Port abbilden, zum Beispiel `-p 9000:8080`. Der Server spricht reines HTTP ohne Login; um ihn nur vom Host aus erreichbar zu machen, ihn auf der Loopback-Adresse veröffentlichen (unter Linux umgeht ein veröffentlichter Port Host-Firewalls wie ufw und firewalld):

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.4.0
```

Der Server des Releases `0.3.0` und späterer Releases (nicht das Image `0.2.0`) nimmt die Daten eines Companions auf dem WebSocket-Endpunkt `ws://<Host>:8080/ingest` entgegen und akzeptiert sie nur mit einem Ingest-Token. Der Server erzeugt das Token beim ersten Start und speichert es in der Datei `/config/ingest-token` (Modus `0600`, Eigentümer ist der Container-Benutzer); ins Log schreibt er es nie. Es wird mit dem Befehl unten gelesen und in die Datei geschrieben, die das `--token-file` eines Abspielens nennt (*Eine Aufzeichnung an einen Server senden*), und es wird erneuert, indem der Server gestoppt, die Datei gelöscht und der Server neu gestartet wird. Alternativ nennt `Censio:Ingest:TokenFile` eine eigene Datei mit dem Token (32 bis 1.024 sichtbare ASCII-Zeichen); der Server liest dann diese Datei statt `ingest-token`, und das [Benutzerhandbuch](docs/USER_GUIDE.md#censio-benutzerhandbuch) zeigt es für Linux Docker Engine. Noch nicht ausprobiert: diese Befehle in einem Container und auf NAS-Hardware.

```text
docker exec censio cat /config/ingest-token
```

Um eine Einstellung zu ändern oder auf ein neueres Image zu wechseln, den Container auf demselben Volume neu erstellen: `docker stop censio`, `docker rm censio`, dann derselbe `docker run`-Befehl mit dem neuen Tag. Das Volume `censio-config` behält den Schlüsselring, das Ingest-Token und die Datenbank, deshalb gilt das Token des Companions weiter; `latest` zuerst laden (`docker pull networlddev/censio:latest`), weil `docker run` ein lokales `latest` wiederverwendet. Ohne `-v` gehen die Daten mit dem Container verloren, ein Update von `0.2.0` behält nur den Schlüsselring, das neue Token also lesen und in die Token-Datei eines Abspielens (`--token-file`) schreiben, und ein Update von `0.3.0` bringt die Datenbank auf das aktuelle Schema und behält ihre Profile und Ticks als Anfang des Verlaufs, lehnt aber einen Companion von `0.3.0` ab. Das [Benutzerhandbuch](docs/USER_GUIDE.md#censio-benutzerhandbuch) nennt die Schritte.

Das [Benutzerhandbuch](docs/USER_GUIDE.md#censio-benutzerhandbuch) beschreibt das Datenverzeichnis, das Token, NAS-Hosts, TLS und die Einschränkungen.

### Server aus dem Quellcode bauen

Das baut den aktuellen Stand des Repositorys zu einem lokalen Image. Es braucht Docker 23 oder neuer mit BuildKit und Zugriff auf `mcr.microsoft.com`. Im Hauptordner des Checkouts ausführen (*Censio beziehen*):

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

## Companion

Der Companion ist das Windows-Programm, das die Statistik des laufenden Spiels liest. `Censio-<version>-win-x64.zip` von der Release-Seite `https://github.com/LarsLaskowski/Censio/releases` herunterladen (beim Release unter *Assets*). Er braucht Windows 10 22H2 oder Windows 11, 64 Bit; er braucht kein installiertes .NET, keinen Installer und keine Administratorrechte. Noch nicht ausprobiert: das ZIP von `v0.4.0` auf einem Rechner ohne .NET.

1. Das ZIP in einen beliebigen Ordner entpacken. Es enthält eine Datei, `Censio.Companion.exe`.
2. `Censio.Companion.exe` starten. Ein Tray-Symbol erscheint; sein Menü zeigt den Status, den letzten Tick, die Spielinstallation (`Game: build <build> (Ubisoft Connect)`, ohne jede Einstellung gefunden), den Spielkatalog (`Catalog: reading`, dann `Catalog: checking`, `Catalog: extracting names`, `Catalog: extracting icons, <n> entries` und `Catalog: <n> entries`, oder gleich `Catalog: <n> entries` aus dem Cache; sonst `Catalog: no game` oder `Catalog: failed`, siehe [Benutzerhandbuch](docs/USER_GUIDE.md#spielkatalog)), `Open diagnostics`, `Start recording`, `Open recordings folder`, `Open log folder`, `Start with Windows`, `Version <version>` und `Exit`; im lokalen Modus (*Lokaler Modus* unten) zusätzlich `Server: …` und `Open Censio`, im Weiterleitungsmodus (*Weiterleitungsmodus* unten) `Server: …`.

Das Programm ist nicht signiert, deshalb zeigt Windows SmartScreen beim ersten Start unter Umständen „Der Computer wurde durch Windows geschützt“. „Weitere Informationen“ wählen, dann „Trotzdem ausführen“ (unter englischem Windows: "More info", "Run anyway"). Tipp, vom Product Manager unter deutschem Windows gemeldet und noch nicht an einem echten Release-Download ausprobiert: die Eigenschaften der entpackten `Censio.Companion.exe` (oder des ZIP selbst) öffnen, *Zulassen* ankreuzen (unter englischem Windows vermutlich *Unblock*; noch nicht ausprobiert) und übernehmen; SmartScreen fragt dann nicht.

#### Start mit Windows

Der Companion startet nur dann mit Windows, wenn Sie es einschalten: im Tray-Menü `Start with Windows` wählen. Ein Häkchen zeigt, dass es eingeschaltet ist; den Eintrag erneut zu wählen schaltet es aus. Das Einschalten schreibt einen Wert, `Censio Companion`, unter `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`: Er gilt nur für Ihr Windows-Konto, und der Eintrag braucht keine Administratorrechte. Der Wert ist der vollständige Pfad der laufenden `Censio.Companion.exe` in Anführungszeichen, ohne Argumente; der Companion startet also wie bei einem Doppelklick (er zeichnet weder auf noch spielt er ab).

- Das ZIP in einen Ordner im eigenen Profil entpacken, der an seinem Platz bleibt, zum Beispiel `%LOCALAPPDATA%\Programs\Censio`. Keinen gemeinsamen Ordner und nicht das Stammverzeichnis eines Laufwerks verwenden, in das andere Benutzer schreiben können: Wer die Datei dort ersetzen kann, führt sie bei Ihrer nächsten Anmeldung aus.

Nicht verwendbare Pfade, ein übrig gebliebener Eintrag nach dem Verschieben der Exe und die Windows-Liste der Autostart-Apps stehen im [Benutzerhandbuch](docs/USER_GUIDE.md#start-mit-windows).

#### Erste Aufzeichnung

1. `/pipe` in Ubisoft Connect zu den Startargumenten des Spiels hinzufügen (*Bibliothek*, die drei Punkte bei Anno 117, *Verwalten*, *Eigenschaften*, Feld *Startargumente hinzufügen*, dann *Schließen*); für eine Steam-Kopie gibt es im Benutzerhandbuch eine Alternative. Das Argument wirkt beim nächsten Start des Spiels.
2. Das Spiel starten und einen Spielstand laden. Der Tray-Tooltip wechselt von `Waiting for game` zu `Receiving`.
3. Mit der rechten Maustaste auf das Tray-Symbol klicken und `Start recording` wählen.
4. Spielen, dann `Stop recording` wählen.
5. Die Datei `recording-<yyyyMMdd-HHmmss>Z.jsonl` liegt in `%LOCALAPPDATA%\Censio\Recordings` (`Open recordings folder`). Sie ist privat: nicht posten oder anhängen.

Noch nicht ausprobiert: ob ein in Steam gesetztes `/pipe` das Spiel über Ubisoft Connect erreicht, und der Download vom Release `v0.4.0`. Der ganze Ablauf, die Launcher-Schritte, die Tray-Zustände mit dem, was zu tun ist, und das Abspielen einer Aufzeichnung stehen im [Benutzerhandbuch](docs/USER_GUIDE.md#erste-aufzeichnung-schritt-für-schritt).

#### Eine Aufzeichnung an einen Server senden

Um den Server ohne das Spiel auszuprobieren, kann der Companion ihm eine Aufzeichnung senden: `Censio.Companion.exe --replay "<vollständiger Pfad der Aufzeichnung>" --server ws://<Host>:8080/ingest --token-file "<vollständiger Pfad der Token-Datei>" --speed 10`. Dafür einen Server desselben Releases wie der Companion verwenden, nicht das Image `0.2.0`: beide lehnen sich ab, wenn ihre Vertragsversionen sich unterscheiden, wie die Releases `0.3.0` und `0.4.0` (dieser Companion zeigt `server does not support contract version 2`, ein Companion von `0.3.0` `server does not support contract version 1`); die Token-Datei enthält das Ingest-Token des Servers (ohne `--token-file` nimmt nur ein Server mit abgeschaltetem Token das Abspielen an). Die Aufzeichnung wird auf dem Server ein neues Profil `Replay <Zeit>`. Über `ws://` laufen Token und Daten unverschlüsselt durchs Netz, eine rohe Aufzeichnung gehört nur auf Ihren eigenen Server, und die Token-Datei bleibt privat und wird gelöscht, wenn das Testen endet. Die Schritte, der Server aus dem Quellcode, die Token-Datei, die Testdatei `long-session.jsonl`, die Statustexte und die Fehler stehen im [Benutzerhandbuch](docs/USER_GUIDE.md#eine-aufzeichnung-an-einen-server-senden). Vom Product Manager unter Windows mit `--speed 10` und der Testdatei `long-session.jsonl` ausprobiert: ein Server in Docker (Image aus diesem Stand des Repositorys gebaut) und ein Server aus dem Quellcode füllten beide die Seiten (Regionen und Inseln), und eine Token-Datei, die Windows PowerShell 5.1 mit `>` geschrieben hat, wurde akzeptiert. Noch nicht ausprobiert: `docker cp` für die Token-Datei, eine in Notepad als UTF-16 BE gespeicherte Token-Datei, `wss://` unter Windows, der Windows-Zertifikatspeicher, ein eingerichteter System-Proxy und ein entfernter Docker-Host.

#### Lokaler Modus

Im lokalen Modus betreibt der Companion den Server selbst, sodass die Seiten und der Verlauf weder Docker noch ein NAS brauchen. Er ist standardmäßig aus, braucht einen Companion eines Releases nach `v0.4.0` oder einen Build von `main` und ist nur für **Windows 11, 64 Bit**: Unter Windows 10 22H2 läuft der Companion wie oben, aber der lokale Modus wird dort nicht unterstützt (er wird weder versucht noch abgelehnt). Er braucht kein installiertes .NET, keinen Installer, keine Administratorrechte und keine Firewall-Freigabe.

1. Einen laufenden Companion beenden (`Exit` im Tray-Menü). Die Datei `%LOCALAPPDATA%\Censio\settings.json` anlegen oder die Schlüssel in die vorhandene eintragen, mit `"mode": "local"` und, wenn Sie einen anderen Port als 5000 möchten, `"localPort"`:

```json
{
  "mode": "local",
  "localPort": 5117
}
```

2. `Censio.Companion.exe` starten. Unter `Catalog: …` zeigt das Menü `Server: starting`, dann `Server: running at http://localhost:5117/` (`5000` ohne `localPort`), und `Open Censio`, direkt über `Open diagnostics`, wird aktiv. `Open Censio` wählen: Ihr Standardbrowser öffnet die Seiten; die Startseite zeigt die Version.
3. Die Seiten sind leer (`No data yet. …`), bis das Spiel sendet: Der Companion sendet die Live-Daten wie im *Weiterleitungsmodus* an seinen Server, in das Profil `Live`; Sie können sie zusätzlich durch ein Abspielen füllen. `Exit` wählen (ein laufender Companion beendet einen zweiten Start stillschweigend), dann `Censio.Companion.exe --replay "<vollständiger Pfad der Aufzeichnung>" --speed 10` starten, ohne `--server`. Der Companion startet seinen Server, wartet auf ihn (`Replay: Connecting to server`), sendet die Aufzeichnung mit dem eigenen Token des Servers, sodass Sie keine Token-Datei brauchen, und endet bei `Replay finished: <n> ticks sent, <r> refused`. `Open Censio` wählen und in der Profilauswahl das Profil `Replay <Zeit>` wählen. Mit `--server` geht das Abspielen an diese Adresse wie unter *Eine Aufzeichnung an einen Server senden*. `Exit` stoppt den Server innerhalb von 5 Sekunden; nach dem nächsten Start im lokalen Modus zeigen die Seiten dieselben Profile und denselben Verlauf.

Die Schlüssel werden einmal beim Start gelesen (eine Änderung gilt nach einem Neustart) und ohne Beachtung der Groß- und Kleinschreibung zugeordnet: `mode` ist `local` (oder `forward`, *Weiterleitungsmodus*; jeder andere Text ist ungültig; ein leeres `mode` zählt als fehlend), `localPort` eine ganze Zahl von 1 bis 65535 ohne Anführungszeichen und Dezimalpunkt (`5117`, nicht `"5117"` oder `5117.0`), die nur mit `mode` `local` gelesen wird. Die Daten des Servers liegen in `%LOCALAPPDATA%\Censio\Server`: die Datenbank `censio.db`, der Schlüsselring und die Token-Datei `ingest-token`, privat für Ihr Windows-Konto und beim Entfernen des Companions behalten; den Ordner von Hand löschen, um leer zu starten.

Was die Menüzeile sagt:

| Zeile | Bedeutung | Was zu tun ist |
| ----- | --------- | -------------- |
| `Server: starting` | Der Server startet im Hintergrund | Warten |
| `Server: running at http://localhost:<port>/` | Er läuft; `Open Censio` ist aktiv | Nichts |
| `Server: port <port> in use` | Ein anderes Programm, ein `Censio.Server` oder der Companion eines anderen Windows-Benutzers belegt den Port | Es schließen oder einen anderen `localPort` setzen und den Companion neu starten |
| `Server: failed` | Jeder andere Fehler: Der Ordner oder die Datenbank sind nicht nutzbar, ein neueres Censio hat die Datenbank geschrieben, oder der Server wurde beendet; ein Port, den Windows reserviert, scheitert ebenfalls so | `Open log folder` wählen und das Log des Tages lesen (es nennt den Grund, ohne das Token); einen anderen `localPort` versuchen; ein Neustart versucht es erneut |
| `Server: settings invalid` | Ein Wert von `mode` oder `localPort` ist falsch (das Log nennt ihn); kein Server startet, die Spielsuche ist nicht betroffen | Die Datei korrigieren und neu starten |

Ein Abspielen ohne `--server` endet mit `Replay error: local server not running`, wenn der Server nicht startet. Ein `Open Censio`, das keinen Browser starten kann, schreibt nur eine Warnung ins Log.

**Akzeptiertes Risiko.** Der Server lauscht nur auf `127.0.0.1` und `[::1]`, sodass kein anderes Gerät Ihres Netzes ihn erreicht, und er beantwortet nur die Hostnamen `localhost`, `127.0.0.1` und `[::1]` (jeden anderen und eine Anfrage ohne Hostnamen mit `400`), sodass eine Seite einer anderen Website ihn nicht über Ihren Browser lesen kann. Die Seiten brauchen aber keine Anmeldung: Jedes Programm und jeder andere angemeldete Windows-Benutzer auf demselben PC kann den Port erreichen, jedes Profil lesen und den Verlauf löschen. Ohne das Ingest-Token, das in Ihrem privaten Ordner liegt, kann keiner von ihnen Daten senden. Den lokalen Modus nur auf einem PC verwenden, dessen Benutzern und Programmen Sie vertrauen. Vom Product Manager unter Windows 11 x64 ohne .NET ausprobiert: das Release-ZIP mit den Schritten oben, dass keine Firewall-Abfrage erscheint, und ein Port, den ein eigenständiger `Censio.Server` belegt. Noch nicht ausprobiert: ein Port, den Windows reserviert. Einzelheiten im [Benutzerhandbuch](docs/USER_GUIDE.md#lokaler-modus).

#### Weiterleitungsmodus

Im Weiterleitungsmodus sendet der Companion, was das Spiel meldet, live an einen Server, der anderswo läuft (Docker auf einem NAS oder einem Heimserver), und verbindet sich nach einem Neustart des Servers oder einem Netzausfall von selbst neu. Er ist standardmäßig aus, braucht einen Companion eines Releases nach `v0.4.0` oder einen Build von `main` und einen Server desselben Releases (Vertragsversion 2) und läuft dort, wo der Companion läuft (Windows 10 22H2 und Windows 11, 64 Bit).

1. Einen laufenden Companion beenden (`Exit` im Tray-Menü). Die Datei `%LOCALAPPDATA%\Censio\settings.json` anlegen oder die Schlüssel in die vorhandene eintragen. `serverAddress` ist die Ingest-Adresse des Servers mit ihrem Pfad und `serverToken` das Ingest-Token des Servers (beim Docker-Image `docker exec censio cat /config/ingest-token`; den Schlüssel weglassen, wenn beim Server das Token abgeschaltet ist, `Censio:Ingest:RequireToken` `false`):

```json
{
  "mode": "forward",
  "serverAddress": "ws://nas:8080/ingest",
  "serverToken": "<das Ingest-Token des Servers>"
}
```

2. `Censio.Companion.exe` starten. Unter `Catalog: …` zeigt das Menü `Server: connecting`, dann `Server: connected`, auch solange das Spiel nicht läuft. Sobald das Spiel sendet, erreichen seine Ticks, Handelsrouten und Sitzungsereignisse den Server in der gelesenen Reihenfolge, und die Seiten aktualisieren sich live.
3. Ein Fehler zeigt `Server: unreachable, retrying`, `Server: not answering, retrying`, `Server: connection lost, retrying`, `Server: invalid reply, retrying` oder `Server: another Companion is connected, retrying`, und der Companion versucht es nach 1, 2, 4, 8 und 16 Sekunden erneut, danach alle 30 Sekunden. Eine Verbindung, über die nichts ankommt, wird innerhalb von 60 Sekunden als tot erkannt. Ein falsches Token (`Server: token refused`) und ein Server, der die Vertragsversion 2 nicht kennt (`Server: does not support contract version 2`), werden nicht wiederholt: die Datei korrigieren oder aktualisieren und den Companion neu starten.

Die Schlüssel werden einmal beim Start gelesen (eine Änderung gilt nach einem Neustart) und ohne Beachtung der Groß- und Kleinschreibung zugeordnet, nur mit `mode` `forward`. `serverAddress` ist eine absolute `ws://`- oder `wss://`-Adresse mit Host, ohne Benutzername, Passwort, Fragment und Leerraum; `serverToken` besteht aus 32 bis 1.024 sichtbaren ASCII-Zeichen. Ein falscher Wert zeigt `Server: settings invalid`, sendet nichts und wird im Log genannt; der Spielordner ist nicht betroffen. Was während eines Ausfalls wartet, bleibt im Arbeitsspeicher (höchstens 64 Nachrichten und 16 MiB, die ältesten werden verworfen, die Seiten zeigen dann eine Lücke) und geht verloren, wenn Sie `Exit` wählen.

**Warnungen.** Solange Profile nicht gewählt werden können, geht alles in das eine Profil `Live`: pro Server eine Spielrunde verwenden oder den Verlauf von `Live` zwischen den Spielrunden auf den Seiten löschen. Über `ws://` laufen das Token und die Daten unverschlüsselt durch das Netz, bei jedem Start und jedem erneuten Versuch, zu jedem Host, der die Adresse beantwortet; ein Name ohne Domain (etwa `nas`) kann von jedem Gerät des Netzes beantwortet werden, verwenden Sie also `wss://` oder eine Adresse in einem Netz, dem Sie vertrauen, wenn der PC Ihr Heimnetz verlässt. `settings.json` enthält jetzt das Token im Klartext: die Datei nicht weitergeben. Die Schritte, die Texte und was verloren geht, stehen im [Benutzerhandbuch](docs/USER_GUIDE.md#weiterleitungsmodus).

### Companion aus dem Quellcode bauen

Das Bauen braucht das .NET-11-SDK (Version 11.0.100 oder ein späterer Patch oder ein späteres Feature Band), Zugriff auf `nuget.org` und den Quellcode (*Censio beziehen*), auf jedem Betriebssystem, das das SDK unterstützt; nur das Ausführen des Programms braucht Windows 10 22H2 oder Windows 11, x64. Den Befehl im Hauptordner des Checkouts ausführen. Er verwendet die Flags des Release-Builds, außer der Version, und schreibt in den Ordner `publish`:

```text
dotnet publish src/Censio.Companion/Censio.Companion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -o publish
```

Der Ordner `publish` enthält danach nur `Censio.Companion.exe`. Unter Windows starten (`publish\Censio.Companion.exe`); das Tray-Menü zeigt `Version 0.0.0`. Der Befehl ist so geschrieben, dass er in PowerShell 7 und Windows PowerShell 5.1 läuft. Der Maintainer hat den Companion mit diesem Befehl veröffentlicht und gestartet, und sein Tray-Menü zeigte `Version 0.0.0`; welche PowerShell verwendet wurde, wurde nicht festgehalten. Noch nicht ausprobiert: Windows PowerShell 5.1.

## Konfiguration

| Einstellung | Umgebungsvariable | Standard | Bedeutung |
| ----------- | ----------------- | -------- | --------- |
| `Censio:DataDirectory` | `CENSIO__DATADIRECTORY` | außerhalb des Docker-Images nicht gesetzt, im Image `/config` | Absoluter Pfad des Datenverzeichnisses des Servers; es enthält die Data-Protection-Schlüssel in `data-protection-keys` und, nur solange `Censio:Ingest:TokenFile` nicht gesetzt ist, das Ingest-Token in der Datei `ingest-token`, sowie die Datenbank `censio.db`. |
| `Censio:Database:File` | `CENSIO__DATABASE__FILE` | nicht gesetzt: `censio.db` im Datenverzeichnis (im Image `/config/censio.db`) | Absoluter Pfad der SQLite-Datenbankdatei des Servers; ihr Ordner wird angelegt, wenn er fehlt, und ein Datenverzeichnis ist dafür nicht nötig. Ohne diese Einstellung und ohne Datenverzeichnis liegt die Datenbank im Arbeitsspeicher und geht beim Stoppen des Servers verloren (der Server schreibt eine Warnung ins Log). Ein Pfad, der nicht absolut ist (auch die leere Zeichenkette), eine Datei, die nicht beschrieben werden kann, eine Datei, die keine Censio-Datenbank ist, eine Datenbank einer neueren Version und eine gescheiterte Aktualisierung auf das aktuelle Schema stoppen den Start. Die Datei auf einem lokalen Datenträger halten, nicht auf einer Netzwerkfreigabe. |
| `ASPNETCORE_HTTP_PORTS` | `ASPNETCORE_HTTP_PORTS` | `8080` im Docker-Image; außerhalb lauscht der Server auf `http://localhost:5000` | Port, auf dem der Server im Docker-Image lauscht. |
| `AllowedHosts` | `ALLOWEDHOSTS` | `*` (Hostfilterung aus) | Die Hostnamen, die der Server beantwortet, durch `;` getrennt (`*.<Domain>` für eine Domain). Sind Namen eingetragen, wird eine Anfrage für jeden anderen Host mit `400` beantwortet, tragen Sie also jeden Namen und jede Adresse ein, über die der Server erreicht wird, auch `127.0.0.1` für den Health-Check des Images. Die Liste entscheidet außerdem, welche Seiten den Verlauf eines Profils löschen dürfen: IP-Adressen und `localhost` immer, ein Name nur, wenn er eingetragen ist. Hinter einem Reverse-Proxy muss der Proxy den Host des Clients weitergeben (nginx: `proxy_set_header Host $host;`) und nur seine eigenen Namen beantworten ([Benutzerhandbuch](docs/USER_GUIDE.md#verlauf-und-löschen-eines-profils)). |
| `Censio:Ingest:RequireToken` | `CENSIO__INGEST__REQUIRETOKEN` | `true` | Ob der Endpunkt `/ingest` das Ingest-Token verlangt (`true`, der Standard) oder jeden Companion annimmt (`false`, in beliebiger Schreibweise). Mit `false` schreibt der Server eine Warnung ins Log, und jeder, der ihn erreicht, kann Daten senden: nur in einem Netz verwenden, in dem jedem Host vertraut wird. Jeder andere Wert, auch die leere Zeichenkette, stoppt den Start. |
| `Censio:Ingest:TokenFile` | `CENSIO__INGEST__TOKENFILE` | nicht gesetzt | Absoluter Pfad einer Datei mit dem Ingest-Token (32 bis 1.024 sichtbare ASCII-Zeichen), die beim Start statt `ingest-token` gelesen wird. Der Server legt die Datei oder ihren Pfad nie an, ändert oder protokolliert sie nie. Ein Pfad, der nicht absolut ist (auch die leere Zeichenkette), eine Datei, die nicht gelesen werden kann oder kein gültiges Token enthält, und die Kombination mit `Censio:Ingest:RequireToken` `false` stoppen den Start. |

Der Companion hat optionale Einstellungen, in der Datei `%LOCALAPPDATA%\Censio\settings.json`. Sie wird einmal beim Start gelesen und vom Companion nie geschrieben; von Hand anlegen, wenn das Menü Ihr Spiel nicht zeigt (`Game: not found`), zum Beispiel bei einer Steam-Kopie, um den lokalen Modus einzuschalten (*Lokaler Modus*) oder um Live-Daten an einen Server zu senden (*Weiterleitungsmodus*):

```json
{
  "gameFolder": "C:\\Games\\Anno 117"
}
```

| Einstellung | Bedeutung |
| ----------- | --------- |
| `gameFolder` | Der Ordner Ihrer Anno-117-Installation: der, der `maindata\config.rda` und `bin\win64\anno117.exe` enthält. Einen vollständigen Pfad schreiben und jeden Backslash verdoppeln (`C:\\Games\\Anno 117`) oder Schrägstriche verwenden. Variablen wie `%USERPROFILE%` werden nicht aufgelöst. Ohne die Einstellung sucht der Companion in den Einträgen von Ubisoft Connect und in dessen Standard-Spieleordner. |
| `mode` | `"local"` lässt den Companion den Server im eigenen Prozess betreiben (*Lokaler Modus*), `"forward"` lässt ihn Live-Daten an den Server von `serverAddress` senden (*Weiterleitungsmodus*); ohne den Schlüssel oder mit einem leeren Text sind beide aus. Jeder andere Wert ist ungültig und zeigt `Server: settings invalid`. |
| `localPort` | Mit `mode` `local`: der Port des betriebenen Servers, eine ganze Zahl von 1 bis 65535 ohne Anführungszeichen; Standard `5000`. Ohne `mode` `local` wird er nicht gelesen. Die Daten liegen in `%LOCALAPPDATA%\Censio\Server`. |
| `serverAddress` | Mit `mode` `forward`: die Ingest-Adresse des Servers, eine absolute `ws://`- oder `wss://`-Adresse mit Host und ihrem Pfad (`ws://nas:8080/ingest`), ohne Benutzername, Passwort, Fragment und Leerraum; erforderlich. Ohne `mode` `forward` wird sie nicht gelesen. |
| `serverToken` | Mit `mode` `forward`: das Ingest-Token des Servers, 32 bis 1.024 sichtbare ASCII-Zeichen (umgebender Leerraum und Zeilenumbrüche werden abgeschnitten; `"` und `\` in JSON als `\"` und `\\` schreiben); weglassen, wenn beim Server das Token abgeschaltet ist. Die Datei enthält dann eine Zugangsinformation: nicht weitergeben. |

Der Schlüssel wird ohne Beachtung der Groß- und Kleinschreibung gelesen, Kommentare und nachgestellte Kommas sind erlaubt. Die Menüzeile `Game: …` zeigt das Ergebnis, und ein falscher Ordner oder eine nicht lesbare Datei zeigt dort und im Log ihren Grund: `Game: searching` (warten, es endet von selbst); `Game: build <build> (Ubisoft Connect)` oder `Game: build <build> (settings file)` (gefunden, nichts zu tun); `Game: build unknown (<source>)` (gefunden, aber die Programmdatei hat keine lesbare Version; das Spiel wird trotzdem verwendet); `Game: not found` (den Ordner in `gameFolder` nennen); `Game: folder in settings not found` (den Pfad korrigieren); `Game: folder in settings is not a full path` (den vollständigen Pfad mit Laufwerksbuchstaben schreiben); `Game: folder in settings is not an Anno 117 installation` (den Ordner nennen, der `maindata` und `bin` enthält); `Game: settings file invalid` (die Datei korrigieren; das Log nennt den Grund). Das [Benutzerhandbuch](docs/USER_GUIDE.md#spielinstallation) erklärt jeden Text. Ein falsches `mode`, `localPort`, `serverAddress` oder `serverToken` zeigt `Server: settings invalid` und berührt den Spielordner nicht.

## Benutzerhandbuch

Das [Benutzerhandbuch](docs/USER_GUIDE.md#censio-benutzerhandbuch) behandelt Installation, den ersten Start, Daten und Logs sowie das Melden eines Problems. Eine Sicherheitslücke privat melden, wie [SECURITY.md](SECURITY.md) es beschreibt, nie in einem öffentlichen Issue.

## Lizenz

Censio steht unter der MIT-Lizenz; siehe [LICENSE](LICENSE).
