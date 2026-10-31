# Plan: Persist the data-protection key ring in /config

Source: Issue #64
Status: Draft (revised after Security and Devil's Advocate)
Tier: security — the change moves where the server stores cryptographic key material (the data-protection key ring) and adds a permission control on it (owner-only key directory), plus a fail-closed start check.
Security areas touched: storage of secrets (the data-protection key ring, new entry for `.squad/project.md`); file paths derived from configuration (`Censio:DataDirectory`, `DataProtectionSetup`); Docker configuration (`src/Censio.Server/Dockerfile`, `ENV`); CI script (`.github/scripts/image-check.sh`, run by the `image` job of `.github/workflows/ci.yml`)

## Problem / root cause

`src/Censio.Server/Program.cs:1-15` configures no data-protection storage, so ASP.NET Core uses its default:
`$HOME/.aspnet/DataProtection-Keys`, or an in-memory ring when that directory cannot be created. In the image
(`src/Censio.Server/Dockerfile`, `USER 1654:1654`, no `HOME` override) this is `/home/app/...` in the container
layer, or memory under `--user` (`HOME=/`). Either way the ring dies with the container. `/config`
(`Dockerfile`: `install -d -o 1654 -g 1654 -m 0755 /config`) is the only path that survives a recreation, and
nothing writes there yet (#29 and #33 are open).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The server sets no data-protection storage today | `src/Censio.Server/Program.cs:1-15`: only Razor components, health checks, antiforgery | confirmed |
| 2 | Issue: in the image the ring lies in the container layer (1654) or in memory (`--user`), lost on recreation | `docs/areas/server.md` *Docker image* (measured in #4); default lookup uses `HOME` (string `HOME` and message `Using an ephemeral key repository` in `Microsoft.AspNetCore.DataProtection.dll` 10.0.14 of the installed shared framework) | confirmed |
| 3 | Issue: "together with #29/#33" — both are still open, nothing writes `/config` | `gh api repos/LarsLaskowski/Censio/issues/29` and `/33`: `open`; `docs/areas/server.md`: "The server writes nothing to `/config` yet" | confirmed |
| 4 | `/config` exists in the image, owned 1654, mode 0755 | `src/Censio.Server/Dockerfile`, `RUN install -d -o 1654 -g 1654 -m 0755 /config` | confirmed |
| 5 | Code between `builder.Build()` and `RunAsync` runs under `WebApplicationFactory` | `Program.cs:11` maps `/health` after `Build()`; `ProgramTests.ProgramGetHealthReturnsHealthyPlainText` passes on it | confirmed |
| 6 | `CENSIO__DATADIRECTORY` binds to `Censio:DataDirectory` (`__` maps to `:`, keys case-insensitive) | ASP.NET Core environment variable configuration provider (documented); end-to-end in image-check case `I64-AC7a` (key file appears under `/config`) | confirmed |
| 7 | ASP.NET Core logs `No XML encryptor configured. Key {KeyId:B} may be persisted to storage in unencrypted form.` (warn, `XmlKeyManager[35]`) when the server creates a key in the file-system ring on Linux | measured by Security and the Devil's Advocate on ASP.NET Core 10.0.14 | confirmed |
| 8 | The library sets no Unix file mode on the key files (umask decides) | measured by Security (10.0.14): key files are created `0600` even under umask `0022`; the key directory mode is the server's (0700 on creation); refuted in the safe direction | refuted |
| 9 | A failure to write the ring surfaces from `IDataProtector.Protect` as `IOException` or `UnauthorizedAccessException` (not wrapped) | measured by Security (10.0.14): `Protect` wraps both in `CryptographicException` with the I/O exception as `InnerException` — for a user without write access to an existing 0700 directory and for a non-writable empty directory. Before it, the library logs its own `fail: KeyRingProvider[48] An error occurred while reading the key ring.`; the catch handles the wrapped form (*Approach*) | refuted |
| 10 | `Directory.CreateDirectory` below an existing regular file throws `IOException` on Linux, also as root (tests run as UID 0 here) | measured by Security: `DirectoryNotFoundException` (an `IOException`) as root | confirmed |
| 11 | No release tag exists, so records 0009–0011 are unreleased and edited in place | `git ls-remote --tags origin`: empty | confirmed |
| 12 | The image-check containers started without `-v` (AC6, AC7 of #4) run as 1654 and can write `/config` in the layer, so they keep starting | fact 4; `.github/scripts/image-check.sh` starts them without `--user` | confirmed |
| 13 | README/area claim "an open page needs a reload after a restart" is caused by the lost keys | to verify: not measured in this change (the Blazor circuit lives in server memory whatever the keys) — the Dev removes the causal sentence and adds no claim about reloads | unverified |
| 14 | Under `WebApplicationFactory`, `builder.Configuration` lacks the factory's settings before `Build()`; `app.Configuration` (the `IConfiguration` service) has them after | measured by Security and the Devil's Advocate | confirmed |
| 15 | Options configured through `AddOptions<KeyManagementOptions>().Configure<IConfiguration, ILoggerFactory>(…)` read the `IConfiguration` service when the options are first resolved, i.e. after `Build()`, so production and the factory see the same value | to verify: Tester, step 5 — `ProgramStartWithDataDirectoryWritesKeyRing` and `DataProtectionSetupAddReadsDataDirectoryWhenOptionsResolve` are the measurement | unverified |

## Acceptance criteria

- [ ] AC1: With `Censio:DataDirectory` set to an absolute path, the server stores the key ring in
  `<data directory>/data-protection-keys` and has created at least one `key-*.xml` there before it serves the
  first request.
- [ ] AC2: A second start on the same data directory reads the existing ring: a payload protected by the first
  instance is unprotected by the second, and no further key file is created.
- [ ] AC3: When the server creates the key directory on Linux, its mode is `0700`.
- [ ] AC4: Without `Censio:DataDirectory` (in the configuration the application resolves after `Build()`) the server sets no key repository of its own (ASP.NET Core's
  default stays), creates nothing and logs nothing about it.
- [ ] AC5: A value that is not a fully qualified path (`relative/dir`, the empty string) stops the start: the check
  returns `false`, logs exactly one `Critical` entry with the text
  `The data directory (Censio:DataDirectory) must be an absolute path, not '<value>'; the server stops.`
  and creates no directory; `Program` then exits with code 1 before it listens.
- [ ] AC6: A data directory where the key directory cannot be created or written stops the start: the check
  returns `false` and logs exactly one `Critical` entry with the text
  `The data-protection key ring cannot be stored in '<key directory>'; the server stops. Make the data directory writable for the user the server runs as.`
  carrying the exception; `Program` exits with code 1 after the application is disposed (so the console logger has
  written the line). This holds for a failure of the directory step and for a write failure that ASP.NET Core wraps
  in a `CryptographicException` (fact 9). ASP.NET Core's own `Error` entry may precede the `Critical` one.
- [ ] AC7: The image sets `CENSIO__DATADIRECTORY=/config`; a container on a volume writes the ring to
  `/config/data-protection-keys` (mode 700, owned by the user it runs as: 1654, or 99 under `--user 99:100`);
  a new container on the same volume keeps the same key files; a container under `--user 4242:4242` on that volume
  exits with code 1 and logs the AC6 text. Verified by `image-check.sh` (below) and the *Image build check*.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `DataProtectionSetupTryCreateKeyRingWithAbsoluteDataDirectoryWritesKeyFile`; `ProgramStartWithDataDirectoryWritesKeyRing` (factory with the setting, `GET /health`, then one `key-*.xml` in the key directory) | `tests/Censio.Server.Tests/DataProtectionSetupTests.cs`; `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC2 | `ProgramRestartWithSameDataDirectoryUnprotectsEarlierPayload` (two `CensioServerFactory` instances on one temp directory, one after the other; protector purpose fixed in the test; asserts the round trip and one key file) | `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC3 | `DataProtectionSetupTryCreateKeyRingNewKeyDirectoryIsOwnerOnly` (Linux only, `File.GetUnixFileMode`) | `tests/Censio.Server.Tests/DataProtectionSetupTests.cs` |
| AC4 | `DataProtectionSetupAddWithoutDataDirectoryRegistersNoRepository` (`IOptions<KeyManagementOptions>.Value.XmlRepository` is null); `DataProtectionSetupAddReadsDataDirectoryWhenOptionsResolve` (the `IConfiguration` holding the setting is registered after `AddCensioDataProtection` ran; the resolved repository points to it); `DataProtectionSetupTryCreateKeyRingWithoutDataDirectoryReturnsTrueAndLogsNothing` | `tests/Censio.Server.Tests/DataProtectionSetupTests.cs` |
| AC5 | `DataProtectionSetupTryCreateKeyRingWithRelativeDataDirectoryLogsCriticalAndReturnsFalse` (`[DataRow("relative/dir")]`, `[DataRow("")]`; exact text, nothing created) | `tests/Censio.Server.Tests/DataProtectionSetupTests.cs` |
| AC6 | `DataProtectionSetupTryCreateKeyRingWithUnwritableDataDirectoryLogsCriticalAndReturnsFalse` (data directory = a path below an existing temp file; exact text, exception not null); `DataProtectionSetupIsWrappedFileSystemFailureWithFileSystemInnerReturnsTrue` (`[DataRow]` per inner type: a `CryptographicException` built with `new UnauthorizedAccessException()` and with `new IOException()`); `DataProtectionSetupIsWrappedFileSystemFailureWithOtherInnerReturnsFalse` (no inner exception; `new FormatException()` inner) — tests run as root, so the wrapped branch is pinned through the predicate and end to end by `I64-AC7d` | `tests/Censio.Server.Tests/DataProtectionSetupTests.cs` |
| AC7 | image-check cases `I64-AC7a`–`I64-AC7d` (see *Approach*) | `.github/scripts/image-check.sh` |

Also, `DataProtectionSetupAddWithAbsoluteDataDirectoryRegistersFileSystemRepository` pins the binding
(`XmlRepository` is a `FileSystemXmlRepository` whose `Directory.FullName` is `<dir>/data-protection-keys`), as
*Integration surface* asks; it belongs to AC1.

All file-system tests use a fresh temp directory and delete it afterwards. The log is read through a hand-written
`RecordingLogger` (`ILogger` that keeps level, formatted message and exception) in
`tests/Censio.Server.Tests/RecordingLogger.cs`, the project's first test double.

## Approach

`DataProtectionSetup` (static, `src/Censio.Server/DataProtectionSetup.cs`):

- `AddCensioDataProtection(services)`: `services.AddDataProtection();` and
  `services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, ILoggerFactory>((options, configuration, loggerFactory) => …)`.
  The callback runs when the options are first resolved, from the `IConfiguration` service (the application's final
  configuration, fact 14/15), so registration and start check read the same value. It reads
  `configuration[DataDirectoryKey]`: absent (`null`) or not fully qualified (`Path.IsPathFullyQualified` false) →
  leaves `options` untouched (the start check reports the second case); otherwise
  `options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(KeyDirectory(value)), loggerFactory)` —
  what `PersistKeysToFileSystem` does, but deferred. `KeyDirectory(value)` is
  `Path.Combine(Path.GetFullPath(value), KeyDirectoryName)`, shared with the check.
- `TryCreateKeyRing(services, configuration, logger)`: absent → `true`. Not fully qualified → the AC5 entry,
  `false`. Otherwise, inside `try`: create the key directory — `Directory.CreateDirectory(path,
  UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)` unless `OperatingSystem.IsWindows()`,
  else `Directory.CreateDirectory(path)`; an existing directory keeps its mode — then
  `services.GetRequiredService<IDataProtectionProvider>().CreateProtector(KeyRingCheckPurpose).Protect([0])`, which
  makes the library read the ring and create the first key. Three catch clauses, nothing broader:
  `catch (IOException)` and `catch (UnauthorizedAccessException)` (the directory step, fact 10) and
  `catch (CryptographicException exception) when (IsWrappedFileSystemFailure(exception))` (the write inside `Protect`,
  fact 9) → each the AC6 entry with the caught exception, `false`. Any other exception propagates (the process ends
  non-zero, unhandled). `IsWrappedFileSystemFailure` is true exactly when `InnerException is IOException or
  UnauthorizedAccessException`.
- Log calls through `[LoggerMessage]` (or what the *Analyzer gate* accepts), level `Critical`, the texts of AC5
  and AC6 exactly; the placeholders hold the configured value and the full key directory path.

`Program.cs`: `builder.Services.AddCensioDataProtection();` before `Build()`; `await using var app = builder.Build();`
(the console logger writes from a background queue; without disposing, the Devil's Advocate lost the `Critical` line
in 2 of 8 runs, with it 0 of 12); right after it:
`if (DataProtectionSetup.TryCreateKeyRing(app.Services, app.Configuration, app.Logger) == false) { return 1; }`;
`return 0;` after `await app.RunAsync();`. No new configuration entry in `appsettings.json`: the default is
"unset", which a JSON `null` does not express reliably.

`Dockerfile`: `ENV CENSIO__DATADIRECTORY=/config` (next to `ASPNETCORE_HTTP_PORTS`).

`image-check.sh` (Tester), new steps after the existing ones they reuse, named with the prefix `I64-` so they do not
collide with the cases `AC6`/`AC7` of feature #4 already in the script; the header comment says the script also
checks the cases of issue 64 (`I64-…`):

- `I64-AC7a` (after AC4, on `censio-check-main` with volume `censio-check-config`): image env contains
  `CENSIO__DATADIRECTORY=/config`; `stat -c %a:%u /config/data-protection-keys` is `700:1654`; at least one
  `key-*.xml` in it; remember the sorted file list.
- `I64-AC7b` (after AC11 removed `main`): a new container on the same volume answers `/health`; the key file list
  equals the remembered one; removed afterwards.
- `I64-AC7c` (in AC13): `/config/data-protection-keys` exists, `stat -c %u` is `99`.
- `I64-AC7d`: `--user 4242:4242` on `censio-check-config` (key directory owned by 1654, mode 700); wait up to 60 s for
  `State.Status` `exited`; exit code `1`; `docker logs` contains the AC6 text with `'/config/data-protection-keys'`.

Failure texts follow the script's `image-check: <case>: <expected> / <actual>` form. New user-visible output is
only the two log lines, fixed above.

## Options considered

Full list with reasons in [record 0011](../../docs/decisions/0011-data-protection-key-ring-in-the-data-directory.md):
`ENV HOME=/config` without code (hidden path, silent in-memory fallback); a path hard-coded in containers; a
setting for the key directory alone; keys in #29's database; for an unwritable directory, the in-memory fallback or
a start that fails on the first page instead of stopping; keys encrypted with a certificate. Chosen: the data
directory setting with fail-closed start.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| `Censio.Server` | `DataProtectionSetup.cs` (new) | setting, registration, start check |
| `Censio.Server` | `Program.cs` | two calls, `await using` on the app, exit codes |
| `Censio.Server` | `Dockerfile` | `ENV CENSIO__DATADIRECTORY=/config` |
| `Censio.Server.Tests` | `DataProtectionSetupTests.cs`, `RecordingLogger.cs` (new); `ProgramTests.cs` | tests |
| — | `.github/scripts/image-check.sh` | cases I64-AC7a–I64-AC7d, header comment |

## Signatures (for the Dev's skeleton)

In `namespace Censio.Server;`, file `src/Censio.Server/DataProtectionSetup.cs`:

- `public static class DataProtectionSetup`
- `public const string DataDirectoryKey = "Censio:DataDirectory";`
- `public const string KeyDirectoryName = "data-protection-keys";`
- `public const string KeyRingCheckPurpose = "Censio.Server.KeyRingCheck";`
- `public static IServiceCollection AddCensioDataProtection(this IServiceCollection services)`
- `public static bool TryCreateKeyRing(IServiceProvider services, IConfiguration configuration, ILogger logger)`
- `public static bool IsWrappedFileSystemFailure(CryptographicException exception)` (public: the project has no
  `InternalsVisibleTo`, and the test needs it)
- `private static string KeyDirectory(string dataDirectory)`

`Program.cs` is rewritten by the skeleton step as in *Approach* (its body is final; the two members throw until
step 6). No existing file is final besides that.

## Test files

- `tests/Censio.Server.Tests/DataProtectionSetupTests.cs` (new)
- `tests/Censio.Server.Tests/ProgramTests.cs` (two tests added)
- `tests/Censio.Server.Tests/RecordingLogger.cs` (new test double)
- `.github/scripts/image-check.sh` (image cases, CI)

Existing test code that calls a changed signature: none — `CensioServerFactory` already takes settings and stays
unchanged (its settings reach the `IConfiguration` service, which the lazy binding reads, fact 14/15), and the
existing `ProgramTests` set no data directory (AC4 keeps their behavior).

Files no unit test can cover: none. The `return 1;` branch of `Program.cs` and the body of the
`CryptographicException` catch are reached only by `I64-AC7d` (unit tests run as root, no write is refused); they
are two lines and need no exclusion.

## Areas

`docs/areas/server.md` — new subsection *Data directory and key ring* (setting, absolute-path rule, default
outside the image, key directory, owner-only creation, unencrypted at rest, the two log lines and exit code 1);
*Docker image*: `/config` now holds the ring, the unwritable-directory sentence now says the server stops, the
data-protection bullet is replaced; *Limits and errors*: the two start failures; *Related decisions*: 0011.

## Documentation updates

Dev, after the code is green, against the code:

- `docs/areas/server.md` as in *Areas*; `docs/areas/README.md` index row *server*: scope adds "the data
  directory and its data-protection key ring".
- `README.md` and `docs/USER_GUIDE.md` (both sections, English then German, kept identical to each other):
  *Data directory and NAS hosts* — `/config` holds the key ring in `data-protection-keys`; `CENSIO__DATADIRECTORY`
  (default `/config` in the image); the sentence "A directory the container user cannot write does not stop the
  server from starting…" becomes: the container stops at start with exit code 1 and the AC6 line in `docker logs`;
  the new behavior is listed as not yet tried on NAS hardware; `CENSIO__DATADIRECTORY` can be pointed to another
  absolute path but not disabled in the image (`docker run -e` cannot unset it and an empty value stops the start). *Limits*: the data-protection bullet is replaced by
  "the keys lie unencrypted in `/config/data-protection-keys`; keep the directory and its backups private";
  ASP.NET Core's warning `No XML encryptor configured` (fact 7) appears at the first start.
- `docs/areas/server.md` states: key files owner-only (set by ASP.NET Core), key directory 0700 when the server
  creates it, an existing directory keeps its mode; on a write failure ASP.NET Core logs its own error entry
  (`KeyRingProvider[48]`) before the AC6 line — the AC6 line is not described as the only error line.
- `docs/ARCHITECTURE.md`: *Configuration* enumeration adds the data directory; *Deployment* says the image keeps the
  key ring in `/config` and links record 0011.
- `SECURITY.md`: *Deployment Security Considerations* — write access also to the data directory, whose
  `data-protection-keys` holds the unencrypted key ring; *Scope* list adds "The data-protection key ring in the
  data directory".
- `.squad/project.md`: *Security areas* adds "Data-protection key ring: `<Censio:DataDirectory>/data-protection-keys`
  (`DataProtectionSetup`), created owner-only, unencrypted at rest; the server stops when it cannot write it."
  *Integration surface* unchanged. Test documentation (Tester): *Test doubles* lists `RecordingLogger`.

Sweep done: `docs/areas/*.md`, `docs/ARCHITECTURE.md`, `README.md`, `docs/USER_GUIDE.md`, `SECURITY.md` searched for
`/config`, data protection and the configuration enumerations; the hits are the entries above.

## Architecture check

No guarantee in `.squad/project.md` (none recorded). Record 0009 stays (user 1654, `--user`, no `VOLUME`); its
consequence bullet about the keys is updated in place to point to 0011. Record 0006 (plain HTTP, open routes) is
untouched: no listener, route or header changes. The data directory comes from operator configuration only, never
from external input.

## Security considerations

- [ ] No limits: the change parses no external input; the setting is operator configuration.
- [ ] Exceptions reaching a user: none reaches an HTTP response. The AC6 log entry carries the I/O exception
  (operator console only); its text names the key directory path, which the operator configured.

## Decision records

- `docs/decisions/0011-data-protection-key-ring-in-the-data-directory.md` (Proposed, indexed)
- `docs/decisions/0009-server-image-runs-as-a-fixed-non-root-user.md` — consequence bullet edited in place (unreleased)

## Challenge

- M1 (= B2, registration reads configuration before `Build()`): accepted — lazy binding through
  `AddOptions<KeyManagementOptions>().Configure<IConfiguration, ILoggerFactory>`; `AddCensioDataProtection` loses its
  `configuration` parameter; new test `DataProtectionSetupAddReadsDataDirectoryWhenOptionsResolve`. The alternative
  `builder.UseSetting(...)` in `CensioServerFactory` was rejected: it fixes the tests, not the order dependency in
  `Program`, so a configuration source added late in production would still be missed.
- M2 (= B1, wrapped exception): accepted — fact 9 refuted; third catch clause with the `IsWrappedFileSystemFailure`
  filter and its tests; AC6 and the area document note ASP.NET Core's own `Error` entry before the `Critical` one.
- M3 (lost `Critical` line): accepted — `await using var app = builder.Build();`; AC6 says the app is disposed
  before exit code 1.
- m1 (fact 8): accepted — fact 8 refuted in the safe direction; docs as N2.
- m2 (fact 7): accepted — fact 7 confirmed; the docs state the warning.
- m3 (case names): accepted — prefix `I64-` and header comment.
- m4 (image default cannot be disabled): accepted as is (unreleased image, no tags); README and record 0011 say it.

## Security review

- B1: resolved as M2 — the catch is exactly `IOException`, `UnauthorizedAccessException` and
  `CryptographicException` whose `InnerException` is one of the two (exception filter); no catch-all, not every
  `CryptographicException`. Unit-tested through the public predicate (root-safe), end to end by `I64-AC7d`.
- B2: resolved as M1, lazy binding chosen (production and tests read the same resolved configuration).
- N1, N2, N3: facts 7, 8, 10 updated; documentation entries added under *Documentation updates*.
- N4: the area document does not call the AC6 line the only error line (*Documentation updates*).

## Out of scope / follow-ups

- The database path (#29) and the full `CENSIO__…` reference (#33) — they build on `Censio:DataDirectory`.
- Tightening the mode of a key directory that already exists, encrypting keys at rest, a fixed application name
  for the ring (the content root `/app` is constant in the image), several instances sharing one ring.
