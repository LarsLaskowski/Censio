# Censio user guide

## Docker

The Censio server runs as the Docker image `networlddev/censio` (linux/amd64) on Docker Hub. Every release publishes the tag `<version>` (for example `0.1.0`) and moves the tag `latest` to the newest release. The versions are listed on the release page `https://github.com/LarsLaskowski/Censio/releases` and the tags on `https://hub.docker.com/r/networlddev/censio/tags`. You need Docker 23 or later (Linux Docker Engine, or Docker Desktop for Windows with the WSL 2 backend and Linux containers). The commands below use the version `0.1.0`; replace it with the version you want, or with `latest`. Run the commands one at a time: a command that fails makes the next one fail too. Not yet tried: pulling and running the published image, until the release `v0.1.0` exists.

### Pull

```text
docker pull networlddev/censio:0.1.0
```

`docker run` pulls a missing image itself, so this step only fetches it in advance. A failed pull leaves nothing half-installed.

### Run

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

### Port

Inside the container the server listens on port 8080. If port 8080 is taken on the host, Docker reports "port is already allocated"; map another host port:

```text
docker run -d --name censio -p 9000:8080 -v censio-config:/config networlddev/censio:0.1.0
```

To change the port the server itself listens on, set `ASPNETCORE_HTTP_PORTS` (the health probe follows the first port of the list; `ASPNETCORE_URLS` is not supported by the image):

```text
docker run -d --name censio -e ASPNETCORE_HTTP_PORTS=9090 -p 9090:9090 -v censio-config:/config networlddev/censio:0.1.0
```

### Data directory and NAS hosts

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

### Network and TLS

Inside the image the server listens on all interfaces of the container, on plain HTTP, with no login on the start page and `/health`. The `-p` mapping decides who can reach it: `-p 8080:8080` publishes it on every interface of the host. To reach it from the host alone, bind the mapping to the loopback address:

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.1.0
```

On Linux a published port bypasses host firewalls such as ufw and firewalld, so restrict access with the mapping, not with the firewall. TLS is the job of a reverse proxy in front of the server; see `SECURITY.md`.

### Limits

- Only linux/amd64; arm64 hosts (including ARM-based Synology models) and Windows containers are not supported.
- The image runs with invariant globalization and has no time zone data: `TZ` has no effect and log times are UTC.
- ASP.NET Core's data-protection keys lie unencrypted in `/config/data-protection-keys`; keep the directory and its backups private. ASP.NET Core logs the warning `No XML encryptor configured` when the server creates the first key.
- The server logs to the console; read it with `docker logs censio`.

### Build from source

To run a state of the repository that has not been released, build the image from a checkout of this repository, with the commands run in its root directory. This needs access to `mcr.microsoft.com` and BuildKit. In the `docker run` commands of this guide, use the local name `censio` instead of `networlddev/censio:0.1.0`.


```text
docker build -f src/Censio.Server/Dockerfile -t censio .
```

The build stamps the version `0.0.0`; pass another one as a semantic version (`MAJOR.MINOR.PATCH`, optionally with pre-release and build parts), anything else fails the build with `VERSION is not a semantic version`:

```text
docker build -f src/Censio.Server/Dockerfile --build-arg VERSION=1.2.3 -t censio .
```

## Companion

The Companion is the Windows program that reads the statistics of the running game. Download `Censio-<version>-win-x64.zip` from the release page `https://github.com/LarsLaskowski/Censio/releases` (under *Assets* of the release). It needs Windows 10 22H2 or Windows 11, 64-bit; it needs no installed .NET, no installer and no administrator rights. Not yet tried: the ZIP on a machine without .NET, until it has been tried on a Windows machine.

1. Extract the ZIP to a folder of your choice. It holds one file, `Censio.Companion.exe`.
2. Start `Censio.Companion.exe`. A tray icon appears; its menu shows `Version <version>`.

The program is not signed, so Windows SmartScreen may show "Windows protected your PC" the first time. Choose "More info", then "Run anyway".

## Docker (Deutsch)

Der Censio-Server läuft als Docker-Image `networlddev/censio` (linux/amd64) auf Docker Hub. Jedes Release veröffentlicht den Tag `<version>` (zum Beispiel `0.1.0`) und setzt den Tag `latest` auf das neueste Release. Die Versionen stehen auf der Release-Seite `https://github.com/LarsLaskowski/Censio/releases`, die Tags auf `https://hub.docker.com/r/networlddev/censio/tags`. Voraussetzung ist Docker 23 oder neuer (Docker Engine unter Linux oder Docker Desktop für Windows mit WSL-2-Backend und Linux-Containern). Die Befehle unten verwenden die Version `0.1.0`; sie durch die gewünschte Version oder durch `latest` ersetzen. Die Befehle werden einzeln ausgeführt: Schlägt ein Befehl fehl, schlägt auch der nächste fehl. Noch nicht ausprobiert: das Laden und Starten des veröffentlichten Images, bis das Release `v0.1.0` existiert.

### Pull

```text
docker pull networlddev/censio:0.1.0
```

`docker run` lädt ein fehlendes Image selbst, dieser Schritt holt es nur vorab. Ein fehlgeschlagener Pull hinterlässt nichts Halbfertiges.

### Run

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

### Port

Im Container lauscht der Server auf Port 8080. Ist Port 8080 auf dem Host belegt, meldet Docker „port is already allocated“; dann einen anderen Host-Port abbilden:

```text
docker run -d --name censio -p 9000:8080 -v censio-config:/config networlddev/censio:0.1.0
```

Den Port, auf dem der Server selbst lauscht, ändert `ASPNETCORE_HTTP_PORTS` (der Health-Check folgt dem ersten Eintrag der Liste; `ASPNETCORE_URLS` unterstützt das Image nicht):

```text
docker run -d --name censio -e ASPNETCORE_HTTP_PORTS=9090 -p 9090:9090 -v censio-config:/config networlddev/censio:0.1.0
```

### Datenverzeichnis und NAS-Hosts

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

### Netzwerk und TLS

Im Image lauscht der Server auf allen Schnittstellen des Containers, über reines HTTP und ohne Login für Startseite und `/health`. Die `-p`-Abbildung entscheidet, wer ihn erreicht: `-p 8080:8080` veröffentlicht ihn auf jeder Schnittstelle des Hosts. Um ihn nur vom Host aus erreichbar zu machen, die Abbildung an die Loopback-Adresse binden:

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config networlddev/censio:0.1.0
```

Unter Linux umgeht ein veröffentlichter Port Host-Firewalls wie ufw und firewalld; den Zugriff deshalb über die Abbildung beschränken, nicht über die Firewall. TLS ist Aufgabe eines Reverse Proxys vor dem Server; siehe `SECURITY.md`.

### Einschränkungen

- Nur linux/amd64; arm64-Hosts (auch ARM-basierte Synology-Modelle) und Windows-Container werden nicht unterstützt.
- Das Image läuft mit invarianter Globalisierung und ohne Zeitzonendaten: `TZ` hat keine Wirkung, Logzeiten sind UTC.
- Die Data-Protection-Schlüssel von ASP.NET Core liegen unverschlüsselt in `/config/data-protection-keys`; das Verzeichnis und seine Sicherungen privat halten. ASP.NET Core meldet die Warnung `No XML encryptor configured`, wenn der Server den ersten Schlüssel anlegt.
- Der Server schreibt sein Log auf die Konsole; Lesen mit `docker logs censio`.

### Aus dem Quellcode bauen

Um einen Stand des Repositorys auszuführen, der nicht veröffentlicht ist, das Image aus einem Checkout dieses Repositorys bauen; die Befehle laufen in dessen Hauptverzeichnis. Das braucht Zugriff auf `mcr.microsoft.com` und BuildKit. In den `docker run`-Befehlen dieser Anleitung dann den lokalen Namen `censio` statt `networlddev/censio:0.1.0` verwenden.


```text
docker build -f src/Censio.Server/Dockerfile -t censio .
```

Der Build setzt die Version `0.0.0`; eine andere wird als Semantic Version übergeben (`MAJOR.MINOR.PATCH`, optional mit Pre-Release- und Build-Teil), alles andere lässt den Build mit `VERSION is not a semantic version` scheitern:

```text
docker build -f src/Censio.Server/Dockerfile --build-arg VERSION=1.2.3 -t censio .
```

## Companion (Deutsch)

Der Companion ist das Windows-Programm, das die Statistik des laufenden Spiels liest. `Censio-<version>-win-x64.zip` von der Release-Seite `https://github.com/LarsLaskowski/Censio/releases` herunterladen (beim Release unter *Assets*). Er braucht Windows 10 22H2 oder Windows 11, 64 Bit; er braucht kein installiertes .NET, keinen Installer und keine Administratorrechte. Noch nicht ausprobiert: das ZIP auf einem Rechner ohne .NET, bis es auf einem Windows-Rechner ausprobiert wurde.

1. Das ZIP in einen beliebigen Ordner entpacken. Es enthält eine Datei, `Censio.Companion.exe`.
2. `Censio.Companion.exe` starten. Ein Tray-Symbol erscheint; sein Menü zeigt `Version <version>`.

Das Programm ist nicht signiert, deshalb zeigt Windows SmartScreen beim ersten Start unter Umständen „Der Computer wurde durch Windows geschützt“. „Weitere Informationen“ wählen, dann „Trotzdem ausführen“.
