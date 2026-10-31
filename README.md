# Censio

Censio reads the statistics of a running Anno 117 game on Windows and shows them live and over time in a web application: a Windows Companion extracts the data, a Blazor Server application stores and displays it.

## Docker

The Censio server runs as the Docker image `networlddev/censio` (linux/amd64). Until a published image exists, build it from a checkout of this repository. You need Docker 23 or later with BuildKit (Linux Docker Engine, or Docker Desktop for Windows with the WSL 2 backend and Linux containers) and access to `mcr.microsoft.com`. Run the commands one at a time, each in the repository's root directory: a command that fails makes the next one fail too.

### Build

```text
docker build -f src/Censio.Server/Dockerfile -t censio .
```

The build stamps the version `0.0.0`; pass another one as a semantic version (`MAJOR.MINOR.PATCH`, optionally with pre-release and build parts), anything else fails the build with `VERSION is not a semantic version`:

```text
docker build -f src/Censio.Server/Dockerfile --build-arg VERSION=1.2.3 -t censio .
```

### Run

Start the container with a named volume for the data directory `/config`:

```text
docker run -d --name censio -p 8080:8080 -v censio-config:/config censio
```

Check that it answers (the response is `Healthy`; in Windows PowerShell 5.1 `curl` is an alias of `Invoke-WebRequest` and works the same) and read its log:

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
docker run -d --name censio -p 9000:8080 -v censio-config:/config censio
```

To change the port the server itself listens on, set `ASPNETCORE_HTTP_PORTS` (the health probe follows the first port of the list; `ASPNETCORE_URLS` is not supported by the image):

```text
docker run -d --name censio -e ASPNETCORE_HTTP_PORTS=9090 -p 9090:9090 -v censio-config:/config censio
```

### Data directory and NAS hosts

`/config` is the directory for the server's data. The image declares no `VOLUME`, so every command above passes `-v`. A named volume (`censio-config`) is the default and the only form on Docker Desktop for Windows. The container never starts as root: it runs as user `1654`.

On a NAS whose shares belong to another user (Unraid `nobody:users` is `99:100`, a Synology user such as `1026:100`), start the container with that user and group; replace the path with your share:

```text
docker run -d --name censio --user 99:100 -p 8080:8080 -v /mnt/user/appdata/censio:/config censio
```

Or hand the directory to the image's user and start without `--user`:

```text
chown -R 1654:1654 /mnt/user/appdata/censio
```

```text
docker run -d --name censio -p 8080:8080 -v /mnt/user/appdata/censio:/config censio
```

A directory the container user cannot write does not stop the server from starting; writes to `/config` fail later, once the server stores data there. Build the image on the NAS, or build it elsewhere and move it:

```text
docker save -o censio.tar censio
```

Copy `censio.tar` to the NAS, then run on the NAS:

```text
docker load -i censio.tar
```

These NAS steps and the Docker Desktop steps are covered by an automated Linux check with user `99:100`; they have not been tried on Unraid or Synology hardware yet.

### Network and TLS

Inside the image the server listens on all interfaces of the container, on plain HTTP, with no login on the start page and `/health`. The `-p` mapping decides who can reach it: `-p 8080:8080` publishes it on every interface of the host. To reach it from the host alone, bind the mapping to the loopback address:

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config censio
```

On Linux a published port bypasses host firewalls such as ufw and firewalld, so restrict access with the mapping, not with the firewall. TLS is the job of a reverse proxy in front of the server; see `SECURITY.md`.

### Limits

- Only linux/amd64; arm64 hosts (including ARM-based Synology models) and Windows containers are not supported.
- The image runs with invariant globalization and has no time zone data: `TZ` has no effect and log times are UTC.
- ASP.NET Core's data-protection keys (antiforgery) are not kept: they are lost when the container is recreated, an open page needs a reload after a restart, and under `--user` with another UID the server logs a warning about it at start.
- The server logs to the console; read it with `docker logs censio`.

## Docker (Deutsch)

Der Censio-Server läuft als Docker-Image `networlddev/censio` (linux/amd64). Solange noch kein veröffentlichtes Image existiert, wird es aus einem Checkout dieses Repositorys gebaut. Voraussetzung sind Docker 23 oder neuer mit BuildKit (Docker Engine unter Linux oder Docker Desktop für Windows mit WSL-2-Backend und Linux-Containern) und Zugriff auf `mcr.microsoft.com`. Die Befehle werden einzeln ausgeführt, jeweils im Hauptverzeichnis des Repositorys: Schlägt ein Befehl fehl, schlägt auch der nächste fehl.

### Build

```text
docker build -f src/Censio.Server/Dockerfile -t censio .
```

Der Build setzt die Version `0.0.0`; eine andere wird als Semantic Version übergeben (`MAJOR.MINOR.PATCH`, optional mit Pre-Release- und Build-Teil), alles andere lässt den Build mit `VERSION is not a semantic version` scheitern:

```text
docker build -f src/Censio.Server/Dockerfile --build-arg VERSION=1.2.3 -t censio .
```

### Run

Den Container mit einem benannten Volume für das Datenverzeichnis `/config` starten:

```text
docker run -d --name censio -p 8080:8080 -v censio-config:/config censio
```

Prüfen, ob er antwortet (die Antwort lautet `Healthy`; in Windows PowerShell 5.1 ist `curl` ein Alias für `Invoke-WebRequest` und funktioniert genauso), und sein Log lesen:

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
docker run -d --name censio -p 9000:8080 -v censio-config:/config censio
```

Den Port, auf dem der Server selbst lauscht, ändert `ASPNETCORE_HTTP_PORTS` (der Health-Check folgt dem ersten Eintrag der Liste; `ASPNETCORE_URLS` unterstützt das Image nicht):

```text
docker run -d --name censio -e ASPNETCORE_HTTP_PORTS=9090 -p 9090:9090 -v censio-config:/config censio
```

### Datenverzeichnis und NAS-Hosts

`/config` ist das Verzeichnis für die Daten des Servers. Das Image deklariert kein `VOLUME`, deshalb geben alle Befehle oben `-v` an. Ein benanntes Volume (`censio-config`) ist der Standard und unter Docker Desktop für Windows die einzige Form. Der Container startet nie als root: Er läuft als Benutzer `1654`.

Auf einem NAS, dessen Freigaben einem anderen Benutzer gehören (Unraid `nobody:users` ist `99:100`, ein Synology-Benutzer etwa `1026:100`), den Container mit diesem Benutzer und dieser Gruppe starten; den Pfad durch die eigene Freigabe ersetzen:

```text
docker run -d --name censio --user 99:100 -p 8080:8080 -v /mnt/user/appdata/censio:/config censio
```

Oder das Verzeichnis dem Benutzer des Images übergeben und ohne `--user` starten:

```text
chown -R 1654:1654 /mnt/user/appdata/censio
```

```text
docker run -d --name censio -p 8080:8080 -v /mnt/user/appdata/censio:/config censio
```

Ein Verzeichnis, das der Container-Benutzer nicht beschreiben kann, hindert den Server nicht am Start; Schreibzugriffe auf `/config` schlagen erst fehl, sobald der Server dort Daten ablegt. Das Image auf dem NAS bauen, oder woanders bauen und übertragen:

```text
docker save -o censio.tar censio
```

`censio.tar` auf das NAS kopieren und dort ausführen:

```text
docker load -i censio.tar
```

Diese NAS-Schritte und die Schritte für Docker Desktop deckt eine automatische Linux-Prüfung mit dem Benutzer `99:100` ab; auf Unraid- oder Synology-Hardware wurden sie noch nicht ausprobiert.

### Netzwerk und TLS

Im Image lauscht der Server auf allen Schnittstellen des Containers, über reines HTTP und ohne Login für Startseite und `/health`. Die `-p`-Abbildung entscheidet, wer ihn erreicht: `-p 8080:8080` veröffentlicht ihn auf jeder Schnittstelle des Hosts. Um ihn nur vom Host aus erreichbar zu machen, die Abbildung an die Loopback-Adresse binden:

```text
docker run -d --name censio -p 127.0.0.1:8080:8080 -v censio-config:/config censio
```

Unter Linux umgeht ein veröffentlichter Port Host-Firewalls wie ufw und firewalld; den Zugriff deshalb über die Abbildung beschränken, nicht über die Firewall. TLS ist Aufgabe eines Reverse Proxys vor dem Server; siehe `SECURITY.md`.

### Einschränkungen

- Nur linux/amd64; arm64-Hosts (auch ARM-basierte Synology-Modelle) und Windows-Container werden nicht unterstützt.
- Das Image läuft mit invarianter Globalisierung und ohne Zeitzonendaten: `TZ` hat keine Wirkung, Logzeiten sind UTC.
- Die Data-Protection-Schlüssel von ASP.NET Core (Antiforgery) bleiben nicht erhalten: Sie gehen verloren, wenn der Container neu erstellt wird, eine offene Seite muss nach einem Neustart neu geladen werden, und unter `--user` mit einer anderen UID meldet der Server beim Start eine Warnung dazu.
- Der Server schreibt sein Log auf die Konsole; Lesen mit `docker logs censio`.
