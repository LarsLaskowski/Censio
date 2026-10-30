# Server

## Scope

The server's HTTP surface: the address it listens on, the start page, the health check, how unknown paths and
plain HTTP are treated, and the version it shows. Operators who run the server and the Docker health probe rely
on it.

## Behavior

### Listen address

- Without address configuration the server listens on `http://localhost:5000` (loopback only) and logs the line
  `Now listening on: http://localhost:5000`. No launch profile is committed, so `dotnet run` and `dotnet
  Censio.Server.dll` both start in the `Production` environment on that address.
- Another address is set with the standard ASP.NET Core settings (`--urls`, `ASPNETCORE_URLS`,
  `ASPNETCORE_HTTP_PORTS`); the server has no setting of its own for it.

### Start page

- `GET /` answers `200` with an HTML page titled `Censio`, with the heading `Censio` and the line
  `Version <version>`, in the interactive server render mode. The page loads the Blazor script under
  `_framework/blazor.web` (a fingerprinted name is possible), which the server serves.
- `<version>` is the informational version the server was built with: `0.0.0` without a version, otherwise the
  build property `Version` exactly as passed (`-p:Version=<v>`), without a source-revision suffix.

### Health check

- `GET /health` answers `200`, content type `text/plain`, body exactly `Healthy`, in every environment. It checks
  no dependency and reveals nothing else (no version, no check results, no exception text).

### Plain HTTP and unknown paths

- The server speaks plain HTTP only. It adds no redirect to HTTPS and no `Strict-Transport-Security` header;
  TLS is the job of a reverse proxy in front of it (`SECURITY.md`).
- A request for an unknown path answers `404` with an empty body. In `Production` an unhandled error answers
  `500` with an empty body; the server has no exception page, status-code page or error handler of its own.
- `/`, `/health` and the Blazor hub need no login. Antiforgery protection is on.
- Host filtering is off (`AllowedHosts` is `*`), a deliberate choice (record 0006).

## Limits and errors

- The server reads no request body and takes no input on `/` or `/health`.
- If the port is taken, the server ends with the ASP.NET Core startup error in the console; it has no handling of
  its own.
- Request paths and hosts are not logged at the default levels (`Microsoft.AspNetCore` logs warnings only).

## Related decisions

- [`decisions/0005-product-version-from-one-build-property.md`](../decisions/0005-product-version-from-one-build-property.md) — the version comes from the build property `Version`.
- [`decisions/0006-server-plain-http-and-open-health.md`](../decisions/0006-server-plain-http-and-open-health.md) — plain HTTP, open `/` and `/health`, host filtering off.
- [`decisions/0007-server-web-assets-independent-of-restore-inputs.md`](../decisions/0007-server-web-assets-independent-of-restore-inputs.md) — the project requests the Blazor scripts itself, so the image contains them.

## Not here

- Data ingest from the Companion (later issue), the Docker image (#4) and the release workflows (#5, #6).
- Web layout and theming (#31).
