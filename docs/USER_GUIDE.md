# Censio user guide

Censio reads the statistics of a running Anno 117 game on Windows and shows them live and over time in a web application: a Windows Companion extracts the data, a Blazor Server application stores and displays it. The status and the quick start are in the [README](../README.md).

Deutsche Version: [Censio-Benutzerhandbuch](#censio-benutzerhandbuch)

## Installation

### Server (Docker)

The Censio server runs as the Docker image `networlddev/censio` (linux/amd64) on Docker Hub. Every release publishes the tag `<version>` (for example `0.1.0`) and moves the tag `latest` to the newest release. The versions are listed on the release page `https://github.com/LarsLaskowski/Censio/releases` and the tags on `https://hub.docker.com/r/networlddev/censio/tags`. You need Docker 23 or later (Linux Docker Engine, or Docker Desktop for Windows with the WSL 2 backend and Linux containers). The commands below use the version `0.1.0`; replace it with the version you want, or with `latest`. Run the commands one at a time: a command that fails makes the next one fail too. Not yet tried: pulling and running the published image, until the release `v0.1.0` exists.

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

The Companion is the Windows program that reads the statistics of the running game. Download `Censio-<version>-win-x64.zip` from the release page `https://github.com/LarsLaskowski/Censio/releases` (under *Assets* of the release). It needs Windows 10 22H2 or Windows 11, 64-bit; it needs no installed .NET, no installer and no administrator rights. Windows other than these and arm64 are not supported. Not yet tried: the ZIP on a machine without .NET, until a release exists and it has been tried on a Windows machine.

1. Extract the ZIP to a folder of your choice. It holds one file, `Censio.Companion.exe`.
2. Start `Censio.Companion.exe`. A tray icon appears; its menu shows `Version <version>`.

The program is not signed, so Windows SmartScreen may show "Windows protected your PC" the first time. Choose "More info", then "Run anyway" (on a German Windows: "Weitere Informationen", then "Trotzdem ausführen").

#### Connecting to the game

The game creates its statistics pipe only when it is started with the launch argument `/pipe` (add `/pipe` to the launch options of your game launcher). Without it the Companion cannot connect, and it looks the same as a game that is not running. Start the Companion before or after the game; it needs no restart when the game is quit and started again. It only reads from the pipe and never writes to it.

Hover over the tray icon: the tooltip reads `Censio Companion: <state>`.

| Tooltip | Meaning |
| ------- | ------- |
| `Waiting for game` | The pipe does not exist: the game is not running or was not started with `/pipe`. The Companion keeps looking (pauses grow from 1 to 10 seconds) |
| `Connected` | The pipe is open; the game has not yet sent its first message. The game sends it at once, so you will hardly ever see this state |
| `Receiving` | The game is sending data |
| `Pipe busy` | The pipe exists but another program holds it (for example another pipe tool); the game serves one client at a time, so close other pipe readers. The Companion keeps retrying (pauses grow from 1 to 10 seconds) and shows `Receiving` at the next attempt after the pipe is free. It can also stay at `Pipe busy` after you restart the Companion while the game is in the main menu, until you load a game |
| `Protocol mismatch` | The pipe speaks another protocol version than 2 (for example after a game update); the Companion stays connected and continues when version 2 arrives |
| `Error` | Connecting failed or the connection broke off. When you quit the game, `Error` can show briefly before `Waiting for game`; that is normal |

When the game quits, the Companion returns to `Waiting for game` and connects again by itself when the game is back; loading another save and a return to the main menu keep the connection. `Exit` in the tray menu works in every state and ends the program within 2 seconds.

#### Recording and replay

For testing and bug reports the Companion can keep what the game sends and play it back later. Both are started with a switch on the command line (for example `Censio.Companion.exe --record` in a terminal or in a shortcut's target); the tray menu has no entry for them. Close a running Companion first: a second start ends at once.

- **Record:** `Censio.Companion.exe --record` connects to the game as usual and writes every connection to its own file in `%LOCALAPPDATA%\Censio\Recordings` (paste the path into the Explorer address bar). A file `recording-<yyyyMMdd-HHmmss>Z.jsonl` (UTC time of the first data) is created when the game's first data arrives; when the game is quit and started again, the next connection writes a new file. The tooltip then ends with `(recording)`. If writing fails (for example the disk is full or too slow), recording stops for the rest of the run, the tooltip ends with `(recording failed)` and the Companion keeps working. `Exit` in the tray menu writes what is still waiting, for at most 1 second, and ends the program. If the Companion is killed or the computer loses power, the last lines of a file can be missing; whether Windows sign-out or shutdown writes them is not tried.
- **Replay:** `Censio.Companion.exe --replay <file> [--speed <factor>|max]` reads a recording from a local file instead of the game's pipe and plays it back as the game sent it. `--speed` is `max` (no pauses) or a number from `0.1` to `1000`, written with a decimal point also on a German Windows (`--speed 10` pauses a tenth as long); without it the replay runs at real speed. The Companion does not open the pipe while it replays, and the file is only read. Nothing consumes the replayed data yet, so only the tooltip shows the progress: `Replay: Connected`, then `Replay: Receiving` (or `Replay: Protocol mismatch`), and after the last frame `Replay finished` until `Exit`. A problem with the file shows as `Replay error: <reason>` with `file not found`, `not a recording`, `unsupported version`, `invalid line <n>` or `read failed`.
- **Invalid command line:** an unknown argument, `--speed` without `--replay`, a speed outside the range, `--replay` without a file, or `--record` together with `--replay` show `Censio Companion: Invalid command line`; the Companion then neither records nor replays, and `Exit` works.

**A recording is private.** It holds the savegame headline, island names and economic data exactly as the game sent them. It stays in the folder on your computer, the Companion never sends it anywhere and never deletes it. Do not attach or post a raw recording publicly (not in an issue either); only anonymised fixtures are meant to be shared. Delete the files yourself when you no longer need them.

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

Building needs the .NET 10 SDK (version 10.0.100 or a later feature band), access to `nuget.org` and the source (see [Getting Censio](../README.md#getting-censio)), on any operating system the SDK supports; only running the program needs Windows 10 22H2 or Windows 11, x64. Run the command in the root folder of the checkout. It uses the flags of the release build, except the version, and writes to the folder `publish`:

```text
dotnet publish src/Censio.Companion/Censio.Companion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -o publish
```

The folder `publish` then holds only `Censio.Companion.exe`. Start it on Windows (`publish\Censio.Companion.exe`); the tray menu shows `Version 0.0.0`. The command is written to run in PowerShell 7 and Windows PowerShell 5.1. The maintainer published the Companion with this command, started it, and its tray menu showed `Version 0.0.0`; which PowerShell was used was not recorded. Not yet tried: Windows PowerShell 5.1.

## First start

Server: open `http://localhost:8080/`; the start page shows the heading `Censio` and the line `Version <version>`. `curl http://localhost:8080/health` answers `Healthy`, and after a few seconds `docker ps` shows the container as `(healthy)`. The first start creates the first data-protection key, and ASP.NET Core logs the warning `No XML encryptor configured` once (see *Limits*).

Companion: the program opens no window and shows no taskbar button. One icon appears in the notification area with the tooltip `Censio Companion: <state>` (the states are listed under *Connecting to the game* above); its menu holds `Version <version>` (greyed out), a separator and `Exit`, which removes the icon and ends the program within 2 seconds. A second start in the same Windows session does nothing: it ends at once without a message and the first keeps running. Windows SmartScreen asks once at the first start (see *Companion* above).

## Data and logs

Server: the data directory is `/config` in the Docker image. It holds the data-protection key ring in `/config/data-protection-keys`; the keys are unencrypted, so keep the directory and its backups private. The server writes its log to the console; read it with `docker logs censio`. Log times are UTC. The settings are listed in the [README](../README.md#configuration).

Companion: it stores no data and writes no log. Only with `--record` does it write recording files to `%LOCALAPPDATA%\Censio\Recordings`; they hold the data of your game as it was sent, stay on your computer and are never deleted by the program, so delete them by hand (*Recording and replay*).

## Reporting problems

Open a GitHub issue with the [Bug report](../.github/ISSUE_TEMPLATE/bug_report.md) template on `https://github.com/LarsLaskowski/Censio/issues/new/choose`. Include:

- the version: the start page (`Version <version>`) or the Companion's tray menu;
- how you run it: your `docker run` command (without secrets and private paths) or how you started the Companion, and your host operating system;
- the log: an excerpt of `docker logs censio` (redact tokens and other secrets); the Companion writes no log. Never attach a raw recording (`--record`) to an issue or post it publicly, it holds private names and game data; describe the problem and the tooltip instead, and keep the file until a maintainer asks how to hand it in.

Report a security vulnerability privately as [SECURITY.md](../SECURITY.md) describes, never in a public issue.

# Censio-Benutzerhandbuch

Censio liest die Statistik eines laufenden Anno-117-Spiels unter Windows und zeigt sie live und im Verlauf in einer Webanwendung: Ein Windows-Companion extrahiert die Daten, eine Blazor-Server-Anwendung speichert und zeigt sie an. Status und Schnellstart stehen in der [README](../README.md#censio-deutsch).

English version: [Censio user guide](#censio-user-guide)

## Installation

### Server (Docker)

Der Censio-Server läuft als Docker-Image `networlddev/censio` (linux/amd64) auf Docker Hub. Jedes Release veröffentlicht den Tag `<version>` (zum Beispiel `0.1.0`) und setzt den Tag `latest` auf das neueste Release. Die Versionen stehen auf der Release-Seite `https://github.com/LarsLaskowski/Censio/releases`, die Tags auf `https://hub.docker.com/r/networlddev/censio/tags`. Voraussetzung ist Docker 23 oder neuer (Docker Engine unter Linux oder Docker Desktop für Windows mit WSL-2-Backend und Linux-Containern). Die Befehle unten verwenden die Version `0.1.0`; sie durch die gewünschte Version oder durch `latest` ersetzen. Die Befehle werden einzeln ausgeführt: Schlägt ein Befehl fehl, schlägt auch der nächste fehl. Noch nicht ausprobiert: das Laden und Starten des veröffentlichten Images, bis das Release `v0.1.0` existiert.

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

Der Companion ist das Windows-Programm, das die Statistik des laufenden Spiels liest. `Censio-<version>-win-x64.zip` von der Release-Seite `https://github.com/LarsLaskowski/Censio/releases` herunterladen (beim Release unter *Assets*). Er braucht Windows 10 22H2 oder Windows 11, 64 Bit; er braucht kein installiertes .NET, keinen Installer und keine Administratorrechte. Anderes Windows und arm64 werden nicht unterstützt. Noch nicht ausprobiert: das ZIP auf einem Rechner ohne .NET, bis ein Release existiert und es auf einem Windows-Rechner ausprobiert wurde.

1. Das ZIP in einen beliebigen Ordner entpacken. Es enthält eine Datei, `Censio.Companion.exe`.
2. `Censio.Companion.exe` starten. Ein Tray-Symbol erscheint; sein Menü zeigt `Version <version>`.

Das Programm ist nicht signiert, deshalb zeigt Windows SmartScreen beim ersten Start unter Umständen „Der Computer wurde durch Windows geschützt“. „Weitere Informationen“ wählen, dann „Trotzdem ausführen“ (unter englischem Windows: "More info", "Run anyway").

#### Verbindung zum Spiel

Das Spiel legt seine Statistik-Pipe nur an, wenn es mit dem Startargument `/pipe` gestartet wird (`/pipe` zu den Startoptionen des Spiele-Launchers hinzufügen). Ohne dieses Argument kann der Companion sich nicht verbinden; das sieht genauso aus wie ein Spiel, das nicht läuft. Den Companion vor oder nach dem Spiel starten; er braucht keinen Neustart, wenn das Spiel beendet und erneut gestartet wird. Er liest nur aus der Pipe und schreibt nie hinein.

Mit der Maus über das Tray-Symbol fahren: Der Tooltip lautet `Censio Companion: <Zustand>`.

| Tooltip | Bedeutung |
| ------- | --------- |
| `Waiting for game` | Die Pipe existiert nicht: Das Spiel läuft nicht oder wurde nicht mit `/pipe` gestartet. Der Companion sucht weiter (die Pausen wachsen von 1 auf 10 Sekunden) |
| `Connected` | Die Pipe ist geöffnet; das Spiel hat noch keine erste Nachricht gesendet. Das Spiel sendet sie sofort, dieser Zustand ist daher kaum zu sehen |
| `Receiving` | Das Spiel sendet Daten |
| `Pipe busy` | Die Pipe existiert, wird aber von einem anderen Programm gehalten (zum Beispiel einem anderen Pipe-Werkzeug); das Spiel bedient nur einen Client gleichzeitig, andere Pipe-Leser also schließen. Der Companion versucht es weiter (die Pausen wachsen von 1 auf 10 Sekunden) und zeigt beim nächsten Versuch nach dem Freiwerden der Pipe `Receiving`. Er kann auch nach einem Neustart des Companions bei `Pipe busy` bleiben, solange das Spiel im Hauptmenü ist, bis ein Spiel geladen wird |
| `Protocol mismatch` | Die Pipe spricht eine andere Protokollversion als 2 (zum Beispiel nach einem Spiel-Update); der Companion bleibt verbunden und macht weiter, sobald Version 2 eintrifft |
| `Error` | Das Verbinden ist fehlgeschlagen oder die Verbindung ist abgebrochen. Beim Beenden des Spiels kann `Error` kurz vor `Waiting for game` erscheinen; das ist normal |

Beendet man das Spiel, kehrt der Companion zu `Waiting for game` zurück und verbindet sich von selbst wieder, sobald das Spiel zurück ist; das Laden eines anderen Spielstands und der Wechsel ins Hauptmenü behalten die Verbindung. `Exit` im Tray-Menü funktioniert in jedem Zustand und beendet das Programm innerhalb von 2 Sekunden.

#### Aufzeichnen und Abspielen

Für Tests und Fehlermeldungen kann der Companion aufzeichnen, was das Spiel sendet, und es später wieder abspielen. Beides wird mit einem Schalter in der Kommandozeile gestartet (zum Beispiel `Censio.Companion.exe --record` in einem Terminal oder im Ziel einer Verknüpfung); das Tray-Menü hat dafür keinen Eintrag. Einen laufenden Companion vorher schließen: Ein zweiter Start endet sofort.

- **Aufzeichnen:** `Censio.Companion.exe --record` verbindet sich wie gewohnt mit dem Spiel und schreibt jede Verbindung in eine eigene Datei in `%LOCALAPPDATA%\Censio\Recordings` (den Pfad in die Adressleiste des Explorers einfügen). Eine Datei `recording-<yyyyMMdd-HHmmss>Z.jsonl` (UTC-Zeit der ersten Daten) entsteht, sobald die ersten Daten des Spiels eintreffen; wird das Spiel beendet und neu gestartet, schreibt die nächste Verbindung eine neue Datei. Der Tooltip endet dann mit `(recording)`. Schlägt das Schreiben fehl (zum Beispiel weil die Platte voll oder zu langsam ist), endet die Aufzeichnung für den Rest des Laufs, der Tooltip endet mit `(recording failed)`, und der Companion arbeitet weiter. `Exit` im Tray-Menü schreibt noch Wartendes, höchstens 1 Sekunde lang, und beendet das Programm. Wird der Companion beendet („abgeschossen“) oder fällt der Strom aus, können die letzten Zeilen einer Datei fehlen; ob Windows beim Abmelden oder Herunterfahren sie schreibt, ist nicht ausprobiert.
- **Abspielen:** `Censio.Companion.exe --replay <Datei> [--speed <Faktor>|max]` liest eine Aufzeichnung aus einer lokalen Datei statt aus der Pipe des Spiels und spielt sie so ab, wie das Spiel sie gesendet hat. `--speed` ist `max` (keine Pausen) oder eine Zahl von `0.1` bis `1000`, auch unter deutschem Windows mit Dezimalpunkt geschrieben (`--speed 10` macht die Pausen auf ein Zehntel kürzer); ohne Angabe läuft das Abspielen in Echtzeit. Der Companion öffnet beim Abspielen die Pipe nicht, und die Datei wird nur gelesen. Die abgespielten Daten verwendet noch nichts, daher zeigt nur der Tooltip den Fortschritt: `Replay: Connected`, dann `Replay: Receiving` (oder `Replay: Protocol mismatch`) und nach dem letzten Frame `Replay finished` bis `Exit`. Ein Problem mit der Datei erscheint als `Replay error: <Grund>` mit `file not found`, `not a recording`, `unsupported version`, `invalid line <n>` oder `read failed`.
- **Ungültige Kommandozeile:** ein unbekanntes Argument, `--speed` ohne `--replay`, eine Geschwindigkeit außerhalb des Bereichs, `--replay` ohne Datei oder `--record` zusammen mit `--replay` zeigen `Censio Companion: Invalid command line`; der Companion zeichnet dann weder auf noch spielt er ab, und `Exit` funktioniert.

**Eine Aufzeichnung ist privat.** Sie enthält die Überschrift des Spielstands, Inselnamen und Wirtschaftsdaten genau so, wie das Spiel sie gesendet hat. Sie bleibt im Ordner auf dem eigenen Rechner, der Companion sendet sie nirgendwohin und löscht sie nie. Eine unbearbeitete Aufzeichnung nie öffentlich anhängen oder posten (auch nicht in einem Issue); geteilt werden sollen nur anonymisierte Testdaten. Die Dateien selbst löschen, wenn sie nicht mehr gebraucht werden.

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

Das Bauen braucht das .NET-10-SDK (Version 10.0.100 oder ein späteres Feature Band), Zugriff auf `nuget.org` und den Quellcode (siehe [Censio beziehen](../README.md#censio-beziehen)), auf jedem Betriebssystem, das das SDK unterstützt; nur das Ausführen des Programms braucht Windows 10 22H2 oder Windows 11, x64. Den Befehl im Hauptordner des Checkouts ausführen. Er verwendet die Flags des Release-Builds, außer der Version, und schreibt in den Ordner `publish`:

```text
dotnet publish src/Censio.Companion/Censio.Companion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -o publish
```

Der Ordner `publish` enthält danach nur `Censio.Companion.exe`. Unter Windows starten (`publish\Censio.Companion.exe`); das Tray-Menü zeigt `Version 0.0.0`. Der Befehl ist so geschrieben, dass er in PowerShell 7 und Windows PowerShell 5.1 läuft. Der Maintainer hat den Companion mit diesem Befehl veröffentlicht und gestartet, und sein Tray-Menü zeigte `Version 0.0.0`; welche PowerShell verwendet wurde, wurde nicht festgehalten. Noch nicht ausprobiert: Windows PowerShell 5.1.

## Erster Start

Server: `http://localhost:8080/` öffnen; die Startseite zeigt die Überschrift `Censio` und die Zeile `Version <version>`. `curl http://localhost:8080/health` antwortet mit `Healthy`, und nach wenigen Sekunden zeigt `docker ps` den Container als `(healthy)`. Beim ersten Start wird der erste Data-Protection-Schlüssel angelegt, und ASP.NET Core meldet einmal die Warnung `No XML encryptor configured` (siehe *Einschränkungen*).

Companion: Das Programm öffnet kein Fenster und zeigt keine Schaltfläche in der Taskleiste. Im Infobereich erscheint ein Symbol mit dem Tooltip `Censio Companion: <Zustand>` (die Zustände stehen oben unter *Verbindung zum Spiel*); sein Menü enthält `Version <version>` (ausgegraut), einen Trenner und `Exit`, das das Symbol entfernt und das Programm innerhalb von 2 Sekunden beendet. Ein zweiter Start in derselben Windows-Sitzung tut nichts: Er endet sofort ohne Meldung, und der erste läuft weiter. Windows SmartScreen fragt beim ersten Start einmal nach (siehe *Companion* oben).

## Daten und Logs

Server: Das Datenverzeichnis ist im Docker-Image `/config`. Es enthält den Data-Protection-Schlüsselring in `/config/data-protection-keys`; die Schlüssel sind unverschlüsselt, deshalb das Verzeichnis und seine Sicherungen privat halten. Der Server schreibt sein Log auf die Konsole; Lesen mit `docker logs censio`. Die Logzeiten sind UTC. Die Einstellungen stehen in der [README](../README.md#konfiguration).

Companion: Er speichert keine Daten und schreibt kein Log. Nur mit `--record` schreibt er Aufzeichnungsdateien nach `%LOCALAPPDATA%\Censio\Recordings`; sie enthalten die Daten des eigenen Spiels, wie sie gesendet wurden, bleiben auf dem Rechner und werden vom Programm nie gelöscht, also von Hand löschen (*Aufzeichnen und Abspielen*).

## Probleme melden

Ein GitHub-Issue mit der Vorlage [Bug report](../.github/ISSUE_TEMPLATE/bug_report.md) auf `https://github.com/LarsLaskowski/Censio/issues/new/choose` öffnen. Angeben:

- die Version: die Startseite (`Version <version>`) oder das Tray-Menü des Companions;
- wie er läuft: den `docker run`-Befehl (ohne Geheimnisse und private Pfade) oder wie der Companion gestartet wurde, und das Host-Betriebssystem;
- das Log: einen Auszug aus `docker logs censio` (Tokens und andere Geheimnisse schwärzen); der Companion schreibt kein Log. Eine unbearbeitete Aufzeichnung (`--record`) nie an ein Issue anhängen oder öffentlich posten, sie enthält private Namen und Spieldaten; stattdessen das Problem und den Tooltip beschreiben und die Datei aufheben, bis ein Maintainer sagt, wie sie übergeben werden kann.

Eine Sicherheitslücke privat melden, wie [SECURITY.md](../SECURITY.md) es beschreibt, nie in einem öffentlichen Issue.
