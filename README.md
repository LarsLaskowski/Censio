# Censio

Censio reads the statistics of a running Anno 117 game on Windows and shows them live and over time in a web application: a Windows Companion extracts the data, a Blazor Server application stores and displays it.

Deutsche Version: [Censio (Deutsch)](#censio-deutsch)

## Status

Censio is in milestone M1. No release exists yet; the coming release `v0.1.0` completes the milestone. The state of `main` carries the version `0.0.0`.

What `v0.1.0` can do:

- The Companion runs on Windows as a tray icon. Its tooltip and icon show the state of the connection to the game's statistics pipe (the game needs the launch argument `/pipe`), and its menu shows the status and the last tick. It records what the game sends to a local file, from the menu or with the switch `--record` (*First recording*), and plays such a file back with `--replay <file>` ([user guide](docs/USER_GUIDE.md#first-recording-step-by-step)). `Open diagnostics` opens a window with the last tick, with regions, islands and goods as raw numbers (GUIDs). It writes a log file to `%LOCALAPPDATA%\Censio\Logs`, and `Start with Windows` (off by default) starts it at sign-in.
- The server runs as a Docker image. Its start page shows `Version <version>`, and `GET /health` answers `Healthy`.

What `v0.1.0` cannot do:

- No names or icons: game data appears only as raw numbers in the diagnostics window.
- Nothing is sent from the Companion to the server.
- The web application shows no game data and there is no history.
- The Companion has no settings, such as a server address or a token.
- There is no tool to anonymise a recording; a recording stays private.

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

The Censio server runs as the Docker image `networlddev/censio` (linux/amd64) on Docker Hub. Every release publishes the tag `<version>` (for example `0.1.0`) and moves the tag `latest` to the newest release. You need Docker 23 or later (Linux Docker Engine, or Docker Desktop for Windows with the WSL 2 backend and Linux containers). The commands below use the version `0.1.0`; replace it with the version you want, or with `latest`. Run the commands one at a time: a command that fails makes the next one fail too. Not yet tried: pulling and running the published image, until the release `v0.1.0` exists.

```text
docker pull networlddev/censio:0.1.0
```

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

Open `http://localhost:8080/` for the start page; it shows `Version 0.1.0`. If a run failed, remove the container before starting it again, because its name is taken:

```text
docker rm -f censio
```

If port 8080 is taken on the host, Docker reports "port is already allocated"; map another host port, for example `-p 9000:8080`. The server speaks plain HTTP without a login; to reach it from the host alone, publish it on the loopback address (a published port bypasses host firewalls such as ufw and firewalld on Linux):

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.1.0
```

The [user guide](docs/USER_GUIDE.md) describes the data directory, NAS hosts, TLS and the limits.

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

The Companion is the Windows program that reads the statistics of the running game. Download `Censio-<version>-win-x64.zip` from the release page `https://github.com/LarsLaskowski/Censio/releases` (under *Assets* of the release). It needs Windows 10 22H2 or Windows 11, 64-bit; it needs no installed .NET, no installer and no administrator rights. Not yet tried: the ZIP on a machine without .NET, until a release exists and it has been tried on a Windows machine.

1. Extract the ZIP to a folder of your choice. It holds one file, `Censio.Companion.exe`.
2. Start `Censio.Companion.exe`. A tray icon appears; its menu shows the status, the last tick, `Open diagnostics`, `Start recording`, `Open recordings folder`, `Open log folder`, `Start with Windows`, `Version <version>` and `Exit`.

The program is not signed, so Windows SmartScreen may show "Windows protected your PC" the first time. Choose "More info", then "Run anyway" (on a German Windows: "Weitere Informationen", then "Trotzdem ausführen"). Tip reported by the Product Manager from a German Windows, not tried against a real release download: open the properties of the extracted `Censio.Companion.exe` (or of the ZIP itself), tick *Zulassen* (on an English Windows probably *Unblock*; not yet tried) and apply; SmartScreen then does not ask.

#### Start with Windows

The Companion does not start with Windows unless you turn it on: choose `Start with Windows` in the tray menu. A check mark shows that it is on; choosing the entry again turns it off. Turning it on writes one value, `Censio Companion`, under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`: it applies to your Windows account only, and the entry needs no administrator rights (the maintainer tried it on Windows 11: no administrator prompt appeared). The value is the full path of the running `Censio.Companion.exe` in quotes, without arguments, so the Companion starts the way a double click starts it (it neither records nor replays).

- Extract the ZIP to a folder in your own profile that stays where it is, for example `%LOCALAPPDATA%\Programs\Censio`. Do not use a shared folder or the root of a drive that other users can write to: whoever can replace the file there runs it at your next sign-in.
- A folder path that contains `%` or is longer than 200 characters cannot be used: the entry stays unchecked, nothing is written and the log says `Could not change the autostart setting`.
- If you move the exe, or start a new version from another folder, the old entry still points to the old file and the menu shows the entry unchecked. To remove such a leftover entry, start any copy of `Censio.Companion.exe`, choose `Start with Windows` (it replaces the leftover entry with the path of that copy), then choose it again to turn it off; or delete the value `Censio Companion` in the Registry Editor at the key above. Turn the entry off before you delete the folder. Switching the old program off in the Windows startup apps list does not delete the value.
- A value with another path or with arguments counts as off; choosing the entry then replaces it.
- If you switch the Companion off in the Windows startup apps list (Settings, Apps, Startup), the menu does not show it and still shows the check mark; switch it on again in that list. A policy of your organization that blocks startup programs is not visible to the Companion either.
- The maintainer tried this on Windows 11: the Companion started with Windows after a restart with the entry on and did not start with it off; no administrator prompt appeared (under a standard account); folders with spaces, `&` or `,` in the path worked; a second Windows account was unaffected; and turning the entry off removed it. Not tried: whether SmartScreen asks at the first automatic start, which name the startup apps list shows, and a folder path with `%`.

#### First recording

1. Add `/pipe` to the launch arguments of the game in Ubisoft Connect (*Library*, the three dots on Anno 117, *Manage*, *Properties*, field *Add launch arguments*, then *Close*); a Steam copy has an alternative in the user guide. The argument takes effect at the next game start.
2. Start the game and load a save. The tray tooltip changes from `Waiting for game` to `Receiving`.
3. Right-click the tray icon and choose `Start recording`.
4. Play, then choose `Stop recording`.
5. The file `recording-<yyyyMMdd-HHmmss>Z.jsonl` is in `%LOCALAPPDATA%\Censio\Recordings` (`Open recordings folder`). It is private: do not post or attach it.

Tried by the Product Manager on Windows 11 (26H2) with Ubisoft Connect in German: the launcher step 1 up to entering `/pipe` and *Schließen* (*Bibliothek*, the three dots, *Verwalten*, *Eigenschaften*, *Startargumente hinzufügen*). Also tried with the real game: recording and its file, the tooltips and tray states, and the replay. Not yet tried: the English launcher labels, a Steam copy and the download from a release (no release exists). The full walkthrough, the launcher steps, the tray states with what to do, and how to replay a recording are in the [user guide](docs/USER_GUIDE.md#first-recording-step-by-step).

### Build the Companion from source

Building needs the .NET 10 SDK (version 10.0.100 or a later feature band), access to `nuget.org` and the source (*Getting Censio*), on any operating system the SDK supports; only running the program needs Windows 10 22H2 or Windows 11, x64. Run the command in the root folder of the checkout. It uses the flags of the release build, except the version, and writes to the folder `publish`:

```text
dotnet publish src/Censio.Companion/Censio.Companion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -o publish
```

The folder `publish` then holds only `Censio.Companion.exe`. Start it on Windows (`publish\Censio.Companion.exe`); the tray menu shows `Version 0.0.0`. The command is written to run in PowerShell 7 and Windows PowerShell 5.1. The maintainer published the Companion with this command, started it, and its tray menu showed `Version 0.0.0`; which PowerShell was used was not recorded. Not yet tried: Windows PowerShell 5.1.

## Configuration

| Setting | Environment variable | Default | Meaning |
| ------- | -------------------- | ------- | ------- |
| `Censio:DataDirectory` | `CENSIO__DATADIRECTORY` | unset outside the Docker image, `/config` in the image | Absolute path of the server's data directory; it holds the data-protection keys in `data-protection-keys`. |
| `ASPNETCORE_HTTP_PORTS` | `ASPNETCORE_HTTP_PORTS` | `8080` in the Docker image; outside it the server listens on `http://localhost:5000` | Port the server listens on inside the Docker image. |

The Companion has no settings yet.

## User guide

The [user guide](docs/USER_GUIDE.md) covers installation, the first start, data and logs, and how to report a problem. Report a security vulnerability privately as [SECURITY.md](SECURITY.md) describes, never in a public issue.

## Licence

Censio is released under the MIT licence; see [LICENSE](LICENSE).

# Censio (Deutsch)

Censio liest die Statistik eines laufenden Anno-117-Spiels unter Windows und zeigt sie live und im Verlauf in einer Webanwendung: Ein Windows-Companion extrahiert die Daten, eine Blazor-Server-Anwendung speichert und zeigt sie an.

English version: [Censio](#censio)

## Status

Censio befindet sich im Meilenstein M1. Es gibt noch kein Release; das kommende Release `v0.1.0` schließt den Meilenstein ab. Der Stand von `main` trägt die Version `0.0.0`.

Das kann `v0.1.0`:

- Der Companion läuft unter Windows als Tray-Symbol. Sein Tooltip und Symbol zeigen den Zustand der Verbindung zur Statistik-Pipe des Spiels (das Spiel braucht das Startargument `/pipe`), und sein Menü zeigt den Status und den letzten Tick. Er zeichnet auf, was das Spiel sendet, in eine lokale Datei, über das Menü oder mit dem Schalter `--record` (*Erste Aufzeichnung*), und spielt eine solche Datei mit `--replay <Datei>` ab ([Benutzerhandbuch](docs/USER_GUIDE.md#erste-aufzeichnung-schritt-für-schritt)). `Open diagnostics` öffnet ein Fenster mit dem letzten Tick, mit Regionen, Inseln und Waren als rohen Zahlen (GUIDs). Er schreibt eine Logdatei nach `%LOCALAPPDATA%\Censio\Logs`, und `Start with Windows` (standardmäßig aus) startet ihn bei der Anmeldung.
- Der Server läuft als Docker-Image. Seine Startseite zeigt `Version <version>`, und `GET /health` antwortet mit `Healthy`.

Das kann `v0.1.0` nicht:

- Keine Namen oder Symbole: Spieldaten erscheinen nur als rohe Zahlen im Diagnosefenster.
- Der Companion sendet nichts an den Server.
- Die Webanwendung zeigt keine Spieldaten, und es gibt keinen Verlauf.
- Der Companion hat keine Einstellungen, etwa eine Serveradresse oder ein Token.
- Es gibt kein Werkzeug, um eine Aufzeichnung zu anonymisieren; eine Aufzeichnung bleibt privat.

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

Der Censio-Server läuft als Docker-Image `networlddev/censio` (linux/amd64) auf Docker Hub. Jedes Release veröffentlicht den Tag `<version>` (zum Beispiel `0.1.0`) und setzt den Tag `latest` auf das neueste Release. Voraussetzung ist Docker 23 oder neuer (Docker Engine unter Linux oder Docker Desktop für Windows mit WSL-2-Backend und Linux-Containern). Die Befehle unten verwenden die Version `0.1.0`; sie durch die gewünschte Version oder durch `latest` ersetzen. Die Befehle werden einzeln ausgeführt: Schlägt ein Befehl fehl, schlägt auch der nächste fehl. Noch nicht ausprobiert: das Laden und Starten des veröffentlichten Images, bis das Release `v0.1.0` existiert.

```text
docker pull networlddev/censio:0.1.0
```

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

Die Startseite liegt unter `http://localhost:8080/`; sie zeigt `Version 0.1.0`. Ist ein Start fehlgeschlagen, den Container vor einem neuen Start entfernen, weil sein Name belegt ist:

```text
docker rm -f censio
```

Ist Port 8080 auf dem Host belegt, meldet Docker „port is already allocated“; dann einen anderen Host-Port abbilden, zum Beispiel `-p 9000:8080`. Der Server spricht reines HTTP ohne Login; um ihn nur vom Host aus erreichbar zu machen, ihn auf der Loopback-Adresse veröffentlichen (unter Linux umgeht ein veröffentlichter Port Host-Firewalls wie ufw und firewalld):

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.1.0
```

Das [Benutzerhandbuch](docs/USER_GUIDE.md#censio-benutzerhandbuch) beschreibt das Datenverzeichnis, NAS-Hosts, TLS und die Einschränkungen.

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

Der Companion ist das Windows-Programm, das die Statistik des laufenden Spiels liest. `Censio-<version>-win-x64.zip` von der Release-Seite `https://github.com/LarsLaskowski/Censio/releases` herunterladen (beim Release unter *Assets*). Er braucht Windows 10 22H2 oder Windows 11, 64 Bit; er braucht kein installiertes .NET, keinen Installer und keine Administratorrechte. Noch nicht ausprobiert: das ZIP auf einem Rechner ohne .NET, bis ein Release existiert und es auf einem Windows-Rechner ausprobiert wurde.

1. Das ZIP in einen beliebigen Ordner entpacken. Es enthält eine Datei, `Censio.Companion.exe`.
2. `Censio.Companion.exe` starten. Ein Tray-Symbol erscheint; sein Menü zeigt den Status, den letzten Tick, `Open diagnostics`, `Start recording`, `Open recordings folder`, `Open log folder`, `Start with Windows`, `Version <version>` und `Exit`.

Das Programm ist nicht signiert, deshalb zeigt Windows SmartScreen beim ersten Start unter Umständen „Der Computer wurde durch Windows geschützt“. „Weitere Informationen“ wählen, dann „Trotzdem ausführen“ (unter englischem Windows: "More info", "Run anyway"). Tipp, vom Product Manager unter deutschem Windows gemeldet und noch nicht an einem echten Release-Download ausprobiert: die Eigenschaften der entpackten `Censio.Companion.exe` (oder des ZIP selbst) öffnen, *Zulassen* ankreuzen (unter englischem Windows vermutlich *Unblock*; noch nicht ausprobiert) und übernehmen; SmartScreen fragt dann nicht.

#### Start mit Windows

Der Companion startet nur dann mit Windows, wenn Sie es einschalten: im Tray-Menü `Start with Windows` wählen. Ein Häkchen zeigt, dass es eingeschaltet ist; den Eintrag erneut zu wählen schaltet es aus. Das Einschalten schreibt einen Wert, `Censio Companion`, unter `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`: Er gilt nur für Ihr Windows-Konto, und der Eintrag braucht keine Administratorrechte (der Maintainer hat es unter Windows 11 ausprobiert: keine Administrator-Abfrage erschien). Der Wert ist der vollständige Pfad der laufenden `Censio.Companion.exe` in Anführungszeichen, ohne Argumente; der Companion startet also wie bei einem Doppelklick (er zeichnet weder auf noch spielt er ab).

- Das ZIP in einen Ordner im eigenen Profil entpacken, der an seinem Platz bleibt, zum Beispiel `%LOCALAPPDATA%\Programs\Censio`. Keinen gemeinsamen Ordner und nicht das Stammverzeichnis eines Laufwerks verwenden, in das andere Benutzer schreiben können: Wer die Datei dort ersetzen kann, führt sie bei Ihrer nächsten Anmeldung aus.
- Ein Ordnerpfad mit `%` oder mit mehr als 200 Zeichen kann nicht verwendet werden: Der Eintrag bleibt ohne Häkchen, es wird nichts geschrieben, und das Log meldet `Could not change the autostart setting`.
- Wenn Sie die Exe verschieben oder eine neue Version aus einem anderen Ordner starten, zeigt der alte Eintrag noch auf die alte Datei, und das Menü zeigt den Eintrag ohne Häkchen. Um einen solchen übrig gebliebenen Eintrag zu entfernen, starten Sie eine beliebige Kopie von `Censio.Companion.exe`, wählen `Start with Windows` (der Eintrag wird durch den Pfad dieser Kopie ersetzt) und wählen es dann erneut, um es auszuschalten; oder löschen Sie den Wert `Censio Companion` im Registrierungs-Editor am oben genannten Schlüssel. Schalten Sie den Eintrag aus, bevor Sie den Ordner löschen. Das Ausschalten des alten Programms in der Windows-Liste der Autostart-Apps löscht den Wert nicht.
- Ein Wert mit anderem Pfad oder mit Argumenten gilt als ausgeschaltet; die Wahl des Eintrags ersetzt ihn dann.
- Wenn Sie den Companion in der Windows-Liste der Autostart-Apps ausschalten (Einstellungen, Apps, Autostart), zeigt das Menü das nicht und zeigt weiter das Häkchen; schalten Sie ihn in dieser Liste wieder ein. Eine Richtlinie Ihrer Organisation, die Startprogramme sperrt, sieht der Companion ebenfalls nicht.
- Der Maintainer hat dies unter Windows 11 ausprobiert: Der Companion startete nach einem Neustart mit Windows, wenn der Eintrag eingeschaltet war, und startete nicht, wenn er ausgeschaltet war; keine Administrator-Abfrage erschien (unter einem Standardkonto); Ordner mit Leerzeichen, `&` oder `,` im Pfad funktionierten; ein zweites Windows-Konto blieb unberührt; und das Ausschalten des Eintrags entfernte ihn. Nicht ausprobiert: ob SmartScreen beim ersten automatischen Start fragt, welchen Namen die Autostart-Liste zeigt, und ein Ordnerpfad mit `%`.

#### Erste Aufzeichnung

1. `/pipe` in Ubisoft Connect zu den Startargumenten des Spiels hinzufügen (*Bibliothek*, die drei Punkte bei Anno 117, *Verwalten*, *Eigenschaften*, Feld *Startargumente hinzufügen*, dann *Schließen*); für eine Steam-Kopie gibt es im Benutzerhandbuch eine Alternative. Das Argument wirkt beim nächsten Start des Spiels.
2. Das Spiel starten und einen Spielstand laden. Der Tray-Tooltip wechselt von `Waiting for game` zu `Receiving`.
3. Mit der rechten Maustaste auf das Tray-Symbol klicken und `Start recording` wählen.
4. Spielen, dann `Stop recording` wählen.
5. Die Datei `recording-<yyyyMMdd-HHmmss>Z.jsonl` liegt in `%LOCALAPPDATA%\Censio\Recordings` (`Open recordings folder`). Sie ist privat: nicht posten oder anhängen.

Ausprobiert vom Product Manager unter Windows 11 (26H2) mit deutschem Ubisoft Connect: Schritt 1 bis zum Eintragen von `/pipe` und *Schließen* (*Bibliothek*, die drei Punkte, *Verwalten*, *Eigenschaften*, *Startargumente hinzufügen*). Ebenfalls mit dem echten Spiel ausprobiert: die Aufzeichnung und ihre Datei, die Tooltips und Tray-Zustände und das Abspielen. Noch nicht ausprobiert: die englischen Launcher-Bezeichnungen, eine Steam-Kopie und der Download von einem Release (es gibt keins). Der ganze Ablauf, die Launcher-Schritte, die Tray-Zustände mit dem, was zu tun ist, und das Abspielen einer Aufzeichnung stehen im [Benutzerhandbuch](docs/USER_GUIDE.md#erste-aufzeichnung-schritt-für-schritt).

### Companion aus dem Quellcode bauen

Das Bauen braucht das .NET-10-SDK (Version 10.0.100 oder ein späteres Feature Band), Zugriff auf `nuget.org` und den Quellcode (*Censio beziehen*), auf jedem Betriebssystem, das das SDK unterstützt; nur das Ausführen des Programms braucht Windows 10 22H2 oder Windows 11, x64. Den Befehl im Hauptordner des Checkouts ausführen. Er verwendet die Flags des Release-Builds, außer der Version, und schreibt in den Ordner `publish`:

```text
dotnet publish src/Censio.Companion/Censio.Companion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -o publish
```

Der Ordner `publish` enthält danach nur `Censio.Companion.exe`. Unter Windows starten (`publish\Censio.Companion.exe`); das Tray-Menü zeigt `Version 0.0.0`. Der Befehl ist so geschrieben, dass er in PowerShell 7 und Windows PowerShell 5.1 läuft. Der Maintainer hat den Companion mit diesem Befehl veröffentlicht und gestartet, und sein Tray-Menü zeigte `Version 0.0.0`; welche PowerShell verwendet wurde, wurde nicht festgehalten. Noch nicht ausprobiert: Windows PowerShell 5.1.

## Konfiguration

| Einstellung | Umgebungsvariable | Standard | Bedeutung |
| ----------- | ----------------- | -------- | --------- |
| `Censio:DataDirectory` | `CENSIO__DATADIRECTORY` | außerhalb des Docker-Images nicht gesetzt, im Image `/config` | Absoluter Pfad des Datenverzeichnisses des Servers; es enthält die Data-Protection-Schlüssel in `data-protection-keys`. |
| `ASPNETCORE_HTTP_PORTS` | `ASPNETCORE_HTTP_PORTS` | `8080` im Docker-Image; außerhalb lauscht der Server auf `http://localhost:5000` | Port, auf dem der Server im Docker-Image lauscht. |

Der Companion hat noch keine Einstellungen.

## Benutzerhandbuch

Das [Benutzerhandbuch](docs/USER_GUIDE.md#censio-benutzerhandbuch) behandelt Installation, den ersten Start, Daten und Logs sowie das Melden eines Problems. Eine Sicherheitslücke privat melden, wie [SECURITY.md](SECURITY.md) es beschreibt, nie in einem öffentlichen Issue.

## Lizenz

Censio steht unter der MIT-Lizenz; siehe [LICENSE](LICENSE).
