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
exposing it: set a strong ingest token through an environment variable, never in a file in the repository, give
the process write access only to its cache and recording directories and its data directory (whose `data-protection-keys` holds the unencrypted data-protection key ring), and restrict the WebSocket port to the
hosts that run a Companion. The server speaks plain HTTP only: terminate TLS at a reverse proxy in front of it. Its start page and `/health` need no login, and without address configuration it listens on loopback only, outside the Docker image. The image listens on all interfaces of the container on port 8080, and the `-p` mapping decides who can reach it (`-p 127.0.0.1:8080:8080` for the host alone). On Linux a published port bypasses host firewalls such as ufw and firewalld, so restrict it with the mapping, not with the firewall.

## Scope

The following are considered in scope for vulnerability reports:

- Parsing of the Anno 117 statistics pipe and of game files (Companion)
- The Companion's local connection to the statistics pipe `\\.\pipe\anno117` (read-only, impersonation level identification)
- The WebSocket ingest and its token (server)
- The server's HTTP endpoints (start page, `/health`, the Blazor hub)
- File paths for the cache and for recordings
- The data-protection key ring in the data directory
- Logging of external data
- Dependency vulnerabilities in packages consumed by the project

The following are **out of scope**:

- Attacks that require local system access or physical access to the host
- Issues arising from misconfiguration of the deployment environment
