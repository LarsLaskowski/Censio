# Security Policy

## Supported Versions

Only the latest release receives security fixes.

| Version | Supported |
| ------- | --------- |
| latest  | Yes       |
| older   | No        |

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Report vulnerabilities privately:

Use GitHub's private vulnerability reporting: the **Security** tab of this repository, *Report a vulnerability*.

Include in your report:

- A clear description of the vulnerability
- Steps to reproduce
- Potential impact
- Any suggested fix (optional)

You will receive an acknowledgement within **5 business days**. We aim to release a fix or mitigation
within **30 days** for confirmed vulnerabilities. We will keep you informed of progress throughout the
process.

## Deployment Security Considerations

The server is meant to run in a trusted network (home network or behind a reverse proxy with TLS). Before
exposing it: keep the ingest token private (the server generates it at its first start into the file `ingest-token` of its data directory, mode `0600` on Linux, or reads a token of at least 32 characters from the file that `Censio:Ingest:TokenFile` names, whose permissions are yours to set (readable for the server's user only); it never logs the token or that path; rotate it by deleting the generated file or changing the configured one and restarting; never put it in a file in the repository or in an issue), give
the process write access only to its cache and recording directories and its data directory (whose `data-protection-keys` holds the unencrypted data-protection key ring and which holds the token file and the database file `censio.db`: it holds the profile and island names, so keep the file, the `-wal` and `-shm` files beside it and every copy of it readable for the server's user only, `chmod 600` on a restored backup, and on a local disk), and restrict the WebSocket port to the
hosts that run a Companion. The ingest endpoint `/ingest` requires the token by default and refuses browser upgrades. The setting `Censio:Ingest:RequireToken` set to `false` switches the token off: anyone who reaches the server can then send data, which the server stores, and fill its database file, so switch it off only on a network where every host is trusted. Without a rate limit, a host that reaches the endpoint can keep the real Companion out by holding its 16 places for connections that have not completed their hello, and each refused hello writes a log line that a non-rotating log can accumulate; restricting who reaches the port is the remedy (`docs/areas/ingest.md`). The server speaks plain HTTP only: terminate TLS at a reverse proxy in front of it; the token and the data cross the network unencrypted without one. Its pages, among them the data pages that show every profile's regions, islands, figures and names, the icons under `/icons/`, and `/health` need no login: anyone who reaches the port reads all stored data, so restrict who reaches it. Without address configuration it listens on loopback only, outside the Docker image. The image listens on all interfaces of the container on port 8080, and the `-p` mapping decides who can reach it (`-p 127.0.0.1:8080:8080` for the host alone). On Linux a published port bypasses host firewalls such as ufw and firewalld, so restrict it with the mapping, not with the firewall.

## Scope

The following are considered in scope for vulnerability reports:

- Parsing of the Anno 117 statistics pipe and of game files (Companion), of the Companion's catalog cache `%LOCALAPPDATA%\Censio\Cache\catalog.bin` (read back and checked as a whole, each icon written again by the Companion's own PNG writer), of the Companion's settings file `%LOCALAPPDATA%\Censio\settings.json` and of Ubisoft Connect's install records in the registry (both read only; a UNC game folder the player names in the settings file is probed over the network)
- The Companion's local connection to the statistics pipe `\\.\pipe\anno117` (read-only, impersonation level identification)
- The WebSocket ingest `/ingest` and its token, the token file `ingest-token` in the data directory and the file named by `Censio:Ingest:TokenFile` (server)
- The server's HTTP endpoints (the start page, the data pages under `/regions` and `/profiles/`, `/icons/{reference}`, `/health`, the Blazor hub)
- File paths for the cache, for recordings and for the Companion's log folder (including the pruning of old log files and opening these folders in Explorer)
- The Windows autostart: the per-user Run value `Censio Companion` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, which the Companion writes and deletes
- The data-protection key ring in the data directory
- The server's SQLite database file in the data directory or named by `Censio:Database:File`, its owner-only modes and the statements that read and write it
- Logging of external data
- Release publishing: the release workflows (`release.yml`, `notify-website.yml`), their secrets in the GitHub environment `release`, and moving the Docker Hub tag `latest`
- Dependency vulnerabilities in packages consumed by the project

The following are **out of scope**:

- Attacks that require local system access or physical access to the host
- Issues arising from misconfiguration of the deployment environment
