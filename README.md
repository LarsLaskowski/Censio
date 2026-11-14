# Censio

Censio reads the statistics of a running Anno 117 game on Windows and shows them live and over time in a web application: a Windows Companion extracts the data, a Blazor Server application stores and displays it.

Deutsche Version: [Censio (Deutsch)](#censio-deutsch)

## Status

Censio is in development. The release `v0.2.0` completes milestone M3 (after `v0.1.0`, which completed milestone M1); it is on the [release page](https://github.com/LarsLaskowski/Censio/releases/tag/v0.2.0) with the Companion ZIP, and the server image is `networlddev/censio:0.2.0`. The state of `main` carries the version `0.0.0`.

What `v0.2.0` can do:

- The Companion runs on Windows as a tray icon. Its tooltip and icon show the state of the connection to the game's statistics pipe (the game needs the launch argument `/pipe`), and its menu shows the status and the last tick. It records what the game sends to a local file, from the menu or with the switch `--record` (*First recording*), and plays such a file back with `--replay <file>` ([user guide](docs/USER_GUIDE.md#first-recording-step-by-step)). `Open diagnostics` opens a window with the last tick; regions and goods are shown with their English and German names and their icons, read from your own game installation, kept in a private cache file in `%LOCALAPPDATA%\Censio\Cache` so that the next start does not read the game files again, and never sent (`Catalog: …` in the menu), islands and other values as the game sent them. It writes a log file to `%LOCALAPPDATA%\Censio\Logs`, and `Start with Windows` (off by default) starts it at sign-in.
- Contributors can anonymise a recording with a console tool of the repository, `dotnet run --project src/Censio.Anonymiser -c Release -- <recording> <output>`, which invents the names, perturbs the amounts and shifts the times; stop the recording first, and keep raw recordings private ([user guide](docs/USER_GUIDE.md#anonymising-a-recording-contributors)).
- The server runs as a Docker image. Its start page shows `Version <version>`, and `GET /health` answers `Healthy`.

What `v0.2.0` cannot do:

- Names and icons only in the diagnostics window (regions and goods): the Companion loads them from its cache or reads them from your installation at every start, and reads them again when the game was updated (it checks when the game connects), and shows a bare number (GUID) where it cannot name one, and no icon where the game's files give none (the log line `Catalog icons: …` counts them); buildings and workforce have no column yet.
- Nothing is sent from the Companion to the server.
- The web application shows no game data and there is no history.
- The Companion has no settings, such as a server address or a token.

Not yet released (the state of `main`): the server takes a Companion's data on the WebSocket endpoint `ws://<host>:8080/ingest`, only from a Companion that presents the ingest token the server generated into `/config/ingest-token` or read from the file named by `Censio:Ingest:TokenFile` (the setting `Censio:Ingest:RequireToken` switches the token off, at the risk that anyone who reaches the server can send data). It creates or opens its SQLite database (`/config/censio.db` in the image) at start and logs `The database '<file>' was created.`, `... was opened.` or `... was updated to the current schema.` It stores the profiles, ticks, catalog and icons it receives before it acknowledges them, keeps the last state of each profile and restores it from the database at the next start (before it listens, with the log line `Profiles restored from the database: <n>.`). The database file grows with every tick until the retention of a later release (#35). No Companion sends anything yet, so the pages below stay empty until one does (#40, #34).

Also not yet released: the start page sits in a frame with an app bar, a navigation, a light and a dark theme and English and German texts, switched with the buttons in its app bar (see the [user guide](docs/USER_GUIDE.md#web-interface)). It starts light, and in English or German as the browser's language list ranks them; a choice is kept in the browser for a year. The server's data pages need no login and show every stored profile to anyone who reaches the server: the region overview (`/regions`, also in the navigation), the island list of a region and the goods of an island with their production, consumption, delta, buildings and productivity, each with the time of its data (`As of …`) and a banner while the Companion is not connected or cannot read the game. They follow new data without a reload, and the profile switch in the app bar lists the stored profiles. Until a Companion sends data they show `No data yet. The pages fill once the Companion sends data.` (see the [user guide](docs/USER_GUIDE.md#web-interface)).

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

Not supported: arm64 hosts, Windows containers, and Windows other than Windows 10 22H2 or Windows 11 (x64).

## Server (Docker)

The Censio server runs as the Docker image `networlddev/censio` (linux/amd64) on Docker Hub. Every release publishes the tag `<version>` (for example `0.2.0`) and moves the tag `latest` to the newest release. You need Docker 23 or later (Linux Docker Engine, or Docker Desktop for Windows with the WSL 2 backend and Linux containers). The commands below use the version `0.2.0`; replace it with the version you want, or with `latest`. Run the commands one at a time: a command that fails makes the next one fail too. Not yet tried: pulling and running the published image `0.2.0`.

```text
docker pull networlddev/censio:0.2.0
```

```text
docker run -d --name censio -p 8080:8080 -v censio-config:/config networlddev/censio:0.2.0
```

Check that it answers (the response is `Healthy`; not yet tried: in Windows PowerShell 5.1 `curl` is an alias of `Invoke-WebRequest`, which should show `StatusCode` `200` and `Content` `Healthy` and may ask for a confirmation first) and read its log:

```text
curl http://localhost:8080/health
```

```text
docker logs censio
```

Open `http://localhost:8080/` for the start page; it shows `Version 0.2.0`. If a run failed, remove the container before starting it again, because its name is taken:

```text
docker rm -f censio
```

If port 8080 is taken on the host, Docker reports "port is already allocated"; map another host port, for example `-p 9000:8080`. The server speaks plain HTTP without a login; to reach it from the host alone, publish it on the loopback address (a published port bypasses host firewalls such as ufw and firewalld on Linux):

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.2.0
```

The server of the state of `main` and of later releases (not the image `0.2.0`) takes a Companion's data on the WebSocket endpoint `ws://<host>:8080/ingest` and accepts it only with an ingest token. The server generates the token at its first start and stores it in the file `/config/ingest-token` (mode `0600`, owned by the container user); it never writes it to the log. Read it with the command below, enter it in the Companion, and rotate it by stopping the server, deleting the file and starting the server again. Alternatively, `Censio:Ingest:TokenFile` names a file of your own that holds the token (32 to 1,024 visible ASCII characters); the server then reads that file instead of `ingest-token` and the [user guide](docs/USER_GUIDE.md) shows how on Linux Docker Engine. Not yet tried: these commands in a container and on NAS hardware.

```text
docker exec censio cat /config/ingest-token
```

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

The Companion is the Windows program that reads the statistics of the running game. Download `Censio-<version>-win-x64.zip` from the release page `https://github.com/LarsLaskowski/Censio/releases` (under *Assets* of the release). It needs Windows 10 22H2 or Windows 11, 64-bit; it needs no installed .NET, no installer and no administrator rights. Not yet tried: the ZIP of `v0.2.0` on a machine without .NET.

1. Extract the ZIP to a folder of your choice. It holds one file, `Censio.Companion.exe`.
2. Start `Censio.Companion.exe`. A tray icon appears; its menu shows the status, the last tick, the game installation (`Game: build <build> (Ubisoft Connect)`, found without any setting), the game catalog (`Catalog: reading`, then `Catalog: checking`, `Catalog: extracting names`, `Catalog: extracting icons, <n> entries` and `Catalog: <n> entries`, or `Catalog: <n> entries` at once from the cache; `Catalog: no game` or `Catalog: failed` otherwise, see the [user guide](docs/USER_GUIDE.md#game-catalog)), `Open diagnostics`, `Start recording`, `Open recordings folder`, `Open log folder`, `Start with Windows`, `Version <version>` and `Exit`.

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

Not yet tried: whether `/pipe` set in Steam reaches the game through Ubisoft Connect, and the download from the release `v0.2.0`. The full walkthrough, the launcher steps, the tray states with what to do, and how to replay a recording are in the [user guide](docs/USER_GUIDE.md#first-recording-step-by-step).

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
| `Censio:Ingest:RequireToken` | `CENSIO__INGEST__REQUIRETOKEN` | `true` | Whether the endpoint `/ingest` requires the ingest token (`true`, the default) or accepts any Companion (`false`, any case). With `false` the server logs a warning and anyone who reaches it can send data: use it only on a network where every host is trusted. Any other value, the empty string included, stops the start. |
| `Censio:Ingest:TokenFile` | `CENSIO__INGEST__TOKENFILE` | unset | Absolute path of a file that holds the ingest token (32 to 1,024 visible ASCII characters), read at start instead of `ingest-token`. The server never creates, changes or logs the file or its path. A path that is not absolute (the empty string included), a file that cannot be read or holds no valid token, and the combination with `Censio:Ingest:RequireToken` `false` stop the start. |

The Companion has one optional setting, in the file `%LOCALAPPDATA%\Censio\settings.json`. It is read once at start and never written by the Companion; create it by hand only if the menu does not show your game (`Game: not found`), for example for a Steam copy:

```json
{
  "gameFolder": "C:\\Games\\Anno 117"
}
```

| Setting | Meaning |
| ------- | ------- |
| `gameFolder` | The folder of your Anno 117 installation: the one that holds `maindata\config.rda` and `bin\win64\anno117.exe`. Write a full path and double every backslash (`C:\\Games\\Anno 117`), or use forward slashes. Variables such as `%USERPROFILE%` are not expanded. Without the setting the Companion looks in Ubisoft Connect's records and its default games folder. |

The key is read ignoring case, comments and trailing commas are accepted. The menu line `Game: …` shows the result, and a wrong folder or a file that cannot be read shows its reason there and in the log: `Game: searching` (wait, it ends by itself); `Game: build <build> (Ubisoft Connect)` or `Game: build <build> (settings file)` (found, nothing to do); `Game: build unknown (<source>)` (found, but the program file has no readable version; the game is still used); `Game: not found` (name the folder in `gameFolder`); `Game: folder in settings not found` (correct the path); `Game: folder in settings is not a full path` (write the full path starting with the drive letter); `Game: folder in settings is not an Anno 117 installation` (name the folder that holds `maindata` and `bin`); `Game: settings file invalid` (correct the file; the log names the reason). The [user guide](docs/USER_GUIDE.md#game-installation) explains each text.

## User guide

The [user guide](docs/USER_GUIDE.md) covers installation, the first start, data and logs, and how to report a problem. Report a security vulnerability privately as [SECURITY.md](SECURITY.md) describes, never in a public issue.

## Licence

Censio is released under the MIT licence; see [LICENSE](LICENSE).

# Censio (Deutsch)

Censio liest die Statistik eines laufenden Anno-117-Spiels unter Windows und zeigt sie live und im Verlauf in einer Webanwendung: Ein Windows-Companion extrahiert die Daten, eine Blazor-Server-Anwendung speichert und zeigt sie an.

English version: [Censio](#censio)

## Status

Censio ist in Entwicklung. Das Release `v0.2.0` schließt den Meilenstein M3 ab (nach `v0.1.0`, das den Meilenstein M1 abgeschlossen hat); es steht mit dem Companion-ZIP auf der [Release-Seite](https://github.com/LarsLaskowski/Censio/releases/tag/v0.2.0), und das Server-Image ist `networlddev/censio:0.2.0`. Der Stand von `main` trägt die Version `0.0.0`.

Das kann `v0.2.0`:

- Der Companion läuft unter Windows als Tray-Symbol. Sein Tooltip und Symbol zeigen den Zustand der Verbindung zur Statistik-Pipe des Spiels (das Spiel braucht das Startargument `/pipe`), und sein Menü zeigt den Status und den letzten Tick. Er zeichnet auf, was das Spiel sendet, in eine lokale Datei, über das Menü oder mit dem Schalter `--record` (*Erste Aufzeichnung*), und spielt eine solche Datei mit `--replay <Datei>` ab ([Benutzerhandbuch](docs/USER_GUIDE.md#erste-aufzeichnung-schritt-für-schritt)). `Open diagnostics` öffnet ein Fenster mit dem letzten Tick; Regionen und Waren werden mit ihren englischen und deutschen Namen und ihren Symbolen gezeigt, die aus Ihrer eigenen Spielinstallation gelesen, in einer privaten Cache-Datei in `%LOCALAPPDATA%\Censio\Cache` aufbewahrt, damit der nächste Start die Spieldateien nicht erneut liest, und nie gesendet werden (`Catalog: …` im Menü), Inseln und die übrigen Werte so, wie das Spiel sie gesendet hat. Er schreibt eine Logdatei nach `%LOCALAPPDATA%\Censio\Logs`, und `Start with Windows` (standardmäßig aus) startet ihn bei der Anmeldung.
- Mitwirkende können eine Aufzeichnung mit einem Konsolenwerkzeug des Repositorys anonymisieren, `dotnet run --project src/Censio.Anonymiser -c Release -- <Aufzeichnung> <Ausgabe>`; es erfindet die Namen, verändert die Beträge und verschiebt die Zeiten. Die Aufzeichnung vorher beenden, unbearbeitete Aufzeichnungen bleiben privat ([Benutzerhandbuch](docs/USER_GUIDE.md#eine-aufzeichnung-anonymisieren-für-mitwirkende)).
- Der Server läuft als Docker-Image. Seine Startseite zeigt `Version <version>`, und `GET /health` antwortet mit `Healthy`.

Das kann `v0.2.0` nicht:

- Namen und Symbole nur im Diagnosefenster (Regionen und Waren): Der Companion lädt sie bei jedem Start aus seinem Cache oder liest sie aus Ihrer Installation und liest sie erneut, wenn das Spiel aktualisiert wurde (er prüft das, wenn das Spiel sich verbindet), und zeigt eine blanke Zahl (GUID), wo er keinen Namen findet, und kein Symbol, wo die Spieldateien keines liefern (die Logzeile `Catalog icons: …` zählt sie); Gebäude und Arbeitskräfte haben noch keine Spalte.
- Der Companion sendet nichts an den Server.
- Die Webanwendung zeigt keine Spieldaten, und es gibt keinen Verlauf.
- Der Companion hat keine Einstellungen, etwa eine Serveradresse oder ein Token.

Noch nicht veröffentlicht (der Stand von `main`): Der Server nimmt die Daten eines Companions auf dem WebSocket-Endpunkt `ws://<Host>:8080/ingest` entgegen, und zwar nur von einem Companion, der das Ingest-Token vorlegt, das der Server in `/config/ingest-token` erzeugt oder aus der in `Censio:Ingest:TokenFile` genannten Datei gelesen hat (die Einstellung `Censio:Ingest:RequireToken` schaltet das Token ab, mit dem Risiko, dass jeder, der den Server erreicht, Daten senden kann). Er legt beim Start seine SQLite-Datenbank (im Image `/config/censio.db`) an oder öffnet sie und schreibt `The database '<Datei>' was created.`, `... was opened.` oder `... was updated to the current schema.` ins Log. Er speichert die Profile, Ticks, den Katalog und die Symbole, die er empfängt, bevor er sie bestätigt, hält den letzten Stand jedes Profils und stellt ihn beim nächsten Start aus der Datenbank wieder her (vor dem Lauschen, mit der Logzeile `Profiles restored from the database: <n>.`). Die Datenbankdatei wächst mit jedem Tick bis zur Aufbewahrung eines späteren Releases (#35). Noch kein Companion sendet etwas, deshalb bleiben die folgenden Seiten leer, bis einer sendet (#40, #34).

Außerdem noch nicht veröffentlicht: Die Startseite steht in einem Rahmen mit Kopfleiste, Navigation, einem hellen und einem dunklen Design sowie englischen und deutschen Texten, umgeschaltet mit den Schaltflächen der Kopfleiste (siehe das [Benutzerhandbuch](docs/USER_GUIDE.md#weboberfläche)). Sie startet hell und auf Englisch oder Deutsch, wie die Sprachliste des Browsers sie einordnet; eine Auswahl bleibt ein Jahr im Browser gespeichert. Die Datenseiten des Servers brauchen keine Anmeldung und zeigen jedem, der den Server erreicht, alle gespeicherten Profile: die Regionsübersicht (`/regions`, auch in der Navigation), die Inselliste einer Region und die Waren einer Insel mit Produktion, Verbrauch, Delta, Gebäuden und Produktivität, jeweils mit der Zeit ihrer Daten (`Stand: …`) und einem Hinweis, solange der Companion nicht verbunden ist oder das Spiel nicht lesen kann. Sie folgen neuen Daten ohne Neuladen, und der Profilschalter in der Kopfleiste listet die gespeicherten Profile. Bis ein Companion Daten sendet, zeigen sie `Noch keine Daten. Die Seiten füllen sich, sobald der Companion Daten sendet.` (siehe das [Benutzerhandbuch](docs/USER_GUIDE.md#weboberfläche)).

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

Nicht unterstützt: arm64-Hosts, Windows-Container und Windows außer Windows 10 22H2 oder Windows 11 (x64).

## Server (Docker)

Der Censio-Server läuft als Docker-Image `networlddev/censio` (linux/amd64) auf Docker Hub. Jedes Release veröffentlicht den Tag `<version>` (zum Beispiel `0.2.0`) und setzt den Tag `latest` auf das neueste Release. Voraussetzung ist Docker 23 oder neuer (Docker Engine unter Linux oder Docker Desktop für Windows mit WSL-2-Backend und Linux-Containern). Die Befehle unten verwenden die Version `0.2.0`; sie durch die gewünschte Version oder durch `latest` ersetzen. Die Befehle werden einzeln ausgeführt: Schlägt ein Befehl fehl, schlägt auch der nächste fehl. Noch nicht ausprobiert: das Laden und Starten des veröffentlichten Images `0.2.0`.

```text
docker pull networlddev/censio:0.2.0
```

```text
docker run -d --name censio -p 8080:8080 -v censio-config:/config networlddev/censio:0.2.0
```

Prüfen, ob er antwortet (die Antwort lautet `Healthy`; noch nicht ausprobiert: in Windows PowerShell 5.1 ist `curl` ein Alias für `Invoke-WebRequest`, das `StatusCode` `200` und `Content` `Healthy` anzeigen sollte und vorher eine Bestätigung verlangen kann), und sein Log lesen:

```text
curl http://localhost:8080/health
```

```text
docker logs censio
```

Die Startseite liegt unter `http://localhost:8080/`; sie zeigt `Version 0.2.0`. Ist ein Start fehlgeschlagen, den Container vor einem neuen Start entfernen, weil sein Name belegt ist:

```text
docker rm -f censio
```

Ist Port 8080 auf dem Host belegt, meldet Docker „port is already allocated“; dann einen anderen Host-Port abbilden, zum Beispiel `-p 9000:8080`. Der Server spricht reines HTTP ohne Login; um ihn nur vom Host aus erreichbar zu machen, ihn auf der Loopback-Adresse veröffentlichen (unter Linux umgeht ein veröffentlichter Port Host-Firewalls wie ufw und firewalld):

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.2.0
```

Der Server des Stands von `main` und späterer Releases (nicht das Image `0.2.0`) nimmt die Daten eines Companions auf dem WebSocket-Endpunkt `ws://<Host>:8080/ingest` entgegen und akzeptiert sie nur mit einem Ingest-Token. Der Server erzeugt das Token beim ersten Start und speichert es in der Datei `/config/ingest-token` (Modus `0600`, Eigentümer ist der Container-Benutzer); ins Log schreibt er es nie. Es wird mit dem Befehl unten gelesen und im Companion eingetragen, und es wird erneuert, indem der Server gestoppt, die Datei gelöscht und der Server neu gestartet wird. Alternativ nennt `Censio:Ingest:TokenFile` eine eigene Datei mit dem Token (32 bis 1.024 sichtbare ASCII-Zeichen); der Server liest dann diese Datei statt `ingest-token`, und das [Benutzerhandbuch](docs/USER_GUIDE.md#censio-benutzerhandbuch) zeigt es für Linux Docker Engine. Noch nicht ausprobiert: diese Befehle in einem Container und auf NAS-Hardware.

```text
docker exec censio cat /config/ingest-token
```

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

Der Companion ist das Windows-Programm, das die Statistik des laufenden Spiels liest. `Censio-<version>-win-x64.zip` von der Release-Seite `https://github.com/LarsLaskowski/Censio/releases` herunterladen (beim Release unter *Assets*). Er braucht Windows 10 22H2 oder Windows 11, 64 Bit; er braucht kein installiertes .NET, keinen Installer und keine Administratorrechte. Noch nicht ausprobiert: das ZIP von `v0.2.0` auf einem Rechner ohne .NET.

1. Das ZIP in einen beliebigen Ordner entpacken. Es enthält eine Datei, `Censio.Companion.exe`.
2. `Censio.Companion.exe` starten. Ein Tray-Symbol erscheint; sein Menü zeigt den Status, den letzten Tick, die Spielinstallation (`Game: build <build> (Ubisoft Connect)`, ohne jede Einstellung gefunden), den Spielkatalog (`Catalog: reading`, dann `Catalog: checking`, `Catalog: extracting names`, `Catalog: extracting icons, <n> entries` und `Catalog: <n> entries`, oder gleich `Catalog: <n> entries` aus dem Cache; sonst `Catalog: no game` oder `Catalog: failed`, siehe [Benutzerhandbuch](docs/USER_GUIDE.md#spielkatalog)), `Open diagnostics`, `Start recording`, `Open recordings folder`, `Open log folder`, `Start with Windows`, `Version <version>` und `Exit`.

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

Noch nicht ausprobiert: ob ein in Steam gesetztes `/pipe` das Spiel über Ubisoft Connect erreicht, und der Download vom Release `v0.2.0`. Der ganze Ablauf, die Launcher-Schritte, die Tray-Zustände mit dem, was zu tun ist, und das Abspielen einer Aufzeichnung stehen im [Benutzerhandbuch](docs/USER_GUIDE.md#erste-aufzeichnung-schritt-für-schritt).

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
| `Censio:Ingest:RequireToken` | `CENSIO__INGEST__REQUIRETOKEN` | `true` | Ob der Endpunkt `/ingest` das Ingest-Token verlangt (`true`, der Standard) oder jeden Companion annimmt (`false`, in beliebiger Schreibweise). Mit `false` schreibt der Server eine Warnung ins Log, und jeder, der ihn erreicht, kann Daten senden: nur in einem Netz verwenden, in dem jedem Host vertraut wird. Jeder andere Wert, auch die leere Zeichenkette, stoppt den Start. |
| `Censio:Ingest:TokenFile` | `CENSIO__INGEST__TOKENFILE` | nicht gesetzt | Absoluter Pfad einer Datei mit dem Ingest-Token (32 bis 1.024 sichtbare ASCII-Zeichen), die beim Start statt `ingest-token` gelesen wird. Der Server legt die Datei oder ihren Pfad nie an, ändert oder protokolliert sie nie. Ein Pfad, der nicht absolut ist (auch die leere Zeichenkette), eine Datei, die nicht gelesen werden kann oder kein gültiges Token enthält, und die Kombination mit `Censio:Ingest:RequireToken` `false` stoppen den Start. |

Der Companion hat eine optionale Einstellung, in der Datei `%LOCALAPPDATA%\Censio\settings.json`. Sie wird einmal beim Start gelesen und vom Companion nie geschrieben; von Hand nur anlegen, wenn das Menü Ihr Spiel nicht zeigt (`Game: not found`), zum Beispiel bei einer Steam-Kopie:

```json
{
  "gameFolder": "C:\\Games\\Anno 117"
}
```

| Einstellung | Bedeutung |
| ----------- | --------- |
| `gameFolder` | Der Ordner Ihrer Anno-117-Installation: der, der `maindata\config.rda` und `bin\win64\anno117.exe` enthält. Einen vollständigen Pfad schreiben und jeden Backslash verdoppeln (`C:\\Games\\Anno 117`) oder Schrägstriche verwenden. Variablen wie `%USERPROFILE%` werden nicht aufgelöst. Ohne die Einstellung sucht der Companion in den Einträgen von Ubisoft Connect und in dessen Standard-Spieleordner. |

Der Schlüssel wird ohne Beachtung der Groß- und Kleinschreibung gelesen, Kommentare und nachgestellte Kommas sind erlaubt. Die Menüzeile `Game: …` zeigt das Ergebnis, und ein falscher Ordner oder eine nicht lesbare Datei zeigt dort und im Log ihren Grund: `Game: searching` (warten, es endet von selbst); `Game: build <build> (Ubisoft Connect)` oder `Game: build <build> (settings file)` (gefunden, nichts zu tun); `Game: build unknown (<source>)` (gefunden, aber die Programmdatei hat keine lesbare Version; das Spiel wird trotzdem verwendet); `Game: not found` (den Ordner in `gameFolder` nennen); `Game: folder in settings not found` (den Pfad korrigieren); `Game: folder in settings is not a full path` (den vollständigen Pfad mit Laufwerksbuchstaben schreiben); `Game: folder in settings is not an Anno 117 installation` (den Ordner nennen, der `maindata` und `bin` enthält); `Game: settings file invalid` (die Datei korrigieren; das Log nennt den Grund). Das [Benutzerhandbuch](docs/USER_GUIDE.md#spielinstallation) erklärt jeden Text.

## Benutzerhandbuch

Das [Benutzerhandbuch](docs/USER_GUIDE.md#censio-benutzerhandbuch) behandelt Installation, den ersten Start, Daten und Logs sowie das Melden eines Problems. Eine Sicherheitslücke privat melden, wie [SECURITY.md](SECURITY.md) es beschreibt, nie in einem öffentlichen Issue.

## Lizenz

Censio steht unter der MIT-Lizenz; siehe [LICENSE](LICENSE).
