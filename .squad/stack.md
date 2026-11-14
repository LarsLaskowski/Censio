# Stack: .NET

The toolchain and the exact commands of this repository. Every squad member, skill and instruction file
refers to the entries below by their *italic name*. Seeded from the Squad-Spec-Repository-Template `dotnet`
profile and owned by this repository: keep it true when the build changes.

## Toolchain

- .NET SDK of `global.json` (the newest SDK of the major, which Dependabot raises; `rollForward` `latestFeature`: the newest
  installed feature band of that major, never the next major), target framework `net11.0` (set once in `Directory.Build.props`;
  `net11.0-windows` in the project file of `Censio.Companion`), solution `Censio.slnx`. Several majors may be installed side by side (a machine, CI); the
  repository is built for one: every project's target framework, `global.json` and every .NET image name the same
  major, which `dotnet_major.py` checks in the *Analyzer gate*, and a new project gets it from the SDK `global.json`
  selects. Dependabot keeps the SDK, the packages and the images current within that major (it reads an image only
  where a `FROM` line names it itself, as both `FROM` lines of `src/Censio.Server/Dockerfile` do); the move to the next one
  (target frameworks, `global.json`, the Microsoft.Extensions and ASP.NET Core packages, the images) is one change of its
  own.
- .NET tools: `reihitsu-format` (`Reihitsu.Cli`), `dotnet-sonarscanner` and `dotnet-ef` (the migrations of `Censio.Server.Data`, `docs/CONTRIBUTING.md`) are pinned in
  `.config/dotnet-tools.json` and restored with `dotnet tool restore`; CI runs both and the *Format* commands below run the formatter through the manifest (`dotnet tool run`), so every machine uses the pinned version. The formatter stays at
  the same version as the **Reihitsu.Analyzer** pinned in `Directory.Packages.props`, which `dotnet_config.py` checks
  in the *Analyzer gate* (CI checks the release line, *Check tool versions*); otherwise the formatter can revert code the analyzer considers
  correct. Dependabot raises both in one grouped NuGet pull request.
- Analyzers in every project via `Directory.Build.props`: **Reihitsu.Analyzer**, **SonarAnalyzer.CSharp**
  (the Sonar C# rules locally, so SonarQube issues surface before the push) and the MSTest analyzers.
- One build structure in every .NET repository of the template: `Directory.Build.props` sets the target framework
  and `Nullable`, `ImplicitUsings`, `GenerateDocumentationFile`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`
  and `AnalysisLevel` `latest-recommended` for every project, and no project file sets one again (only a platform of
  the same major, `net11.0-windows` in `Censio.Companion`) or gives a package a version; there is no rule set, no rule
  in `NoWarn` and no assembly-wide `SuppressMessage`, and text files are checked out with CRLF (`.gitattributes`;
  shell scripts and the Dockerfile keep LF). `dotnet_config.py` checks all of it in the *Analyzer gate*, also with
  `--lint-only`; the values of the target framework, the SDK and the packages are the project's.
- The SessionStart hook `.claude/hooks/session-start.sh` restores the tool manifest (the pinned formatter the commands below run through `dotnet tool run`, and the scanner CI uses), sets `DOTNET_ROOT` and restores the solution in
  remote sessions.

## Layout

- Production code: `src/` — `Censio.Contracts`, `Censio.Pipe`, `Censio.GameData`, `Censio.Server.Data`,
  `Censio.Server` (Blazor Server), `Censio.Companion.Core` (the Companion's platform-neutral logic),
  `Censio.Anonymiser` (a contributor console tool, not released) and `Censio.Companion` (the Windows tray shell,
  `net11.0-windows`, no test project).
- Tests: `tests/<Project>.Tests/`, one test class per type under test in `{TypeUnderTest}Tests.cs`,
  placed flat in the test project; every test project has a `MSTestSettings.cs` with `[assembly: DoNotParallelize]`
  (MSTEST0001), and a new test project adds the same file. Anonymised recordings used as fixtures lie in `tests/<Project>.Tests/Fixtures/`
  with a `README.md`.

## Commands

| Name | Command |
| ---- | ------- |
| *Restore* | `dotnet restore Censio.slnx` |
| *Format* (in the squad: by the member that changed the files, `.squad/team.md`) | `dotnet tool run reihitsu-format --force ./` |
| *Format check* | `dotnet tool run reihitsu-format --check ./` (CI runs it too, after *Restore*) |
| *Build* | `dotnet build Censio.slnx -c Release --no-restore` |
| *Test* | `dotnet test Censio.slnx -c Release --no-build` |
| *Single test* | `dotnet test tests/<Project>.Tests/<Project>.Tests.csproj --filter "FullyQualifiedName~ClassName.MethodName"` |
| *Test (project)* | `dotnet test tests/<Project>.Tests -c Release --no-build` (the test project of the changed code; a `patch` that changes only test code, `.squad/routing.md` *Gates of a patch* — it writes no coverage report, so a `patch` that changes production code runs *Test with coverage* instead) |
| *Test with coverage* | `rm -rf TestResults && dotnet test Censio.slnx -c Release --no-build --collect:"XPlat Code Coverage" --results-directory ./TestResults` (clears earlier runs: the coverage gate merges every report it finds) |
| *Coverage gate* | `python3 .squad/tools/coverage-check.py` |
| *Analyzer gate* | `python3 .squad/tools/analyzer-check.py` |
| *Sonar gate* | `python3 .squad/tools/sonar-check.py pr <number>` (step 11, on the draft pull request once its CI has finished; see *Sonar gate* below) |
| *Image build check* | `docker build -f src/Censio.Server/Dockerfile .` where Docker runs; otherwise the restore-order reproduction under *Image build check* below (`.squad/routing.md`, *Criteria verified only in CI*) |

## Analyzer gate

`analyzer-check.py` runs a full, non-incremental Release build with a SARIF error log per project and
reports every non-suppressed Roslyn diagnostic — `RH####`, `S####`, `MSTEST####`, `CA####`, at **any
severity**, including info-level ones that never appear as build warnings but that SonarQube Cloud
imports — located in a file changed since the merge base with `origin/main`, and every diagnostic without a
location (an assembly-level rule such as MSTEST0001) of a project with such a changed file. Its solution comes from
`.squad/tools/squad_settings.py`. In addition the build must show **zero `RH####` warnings and errors**
anywhere. Do not grep console build output instead: an incremental build prints no warnings at all.

Easy to get wrong by hand: `#region` blocks on every type, XML documentation on every member, no
underscores in member names, a region for an interface implementation named after the interface.

`docker:S8431` (tag and digest in one `FROM`) is excluded for `src/Censio.Server/Dockerfile` only, through `sonar.issue.ignore.multicriteria` in the *Begin SonarQube analysis* step of `.github/workflows/ci.yml`, whose analysis the *Sonar gate* reads (record 0010): the digest pins the base and the tag is what Dependabot updates.

The C# rules and their severities are the template's, the same in every .NET repository: `python3
.squad/tools/dotnet-rules.py` writes its baseline (`.squad/dotnet-rules.editorconfig`, the stricter setting of the
repositories winning) into the `squad:dotnet-rules` blocks of `.editorconfig` (`--check` reports them out of date).
No section of every C# file sets a rule or a code-style option outside the blocks; the project's own sections of paths
(generated code, migrations) come after them. A rule the code does not meet yet goes into `DOTNET_RULES_PENDING` in
`squad_settings.py` with the issue that brings the code up to it: it is written as `suggestion`, which the *Analyzer
gate* still reports in a changed file, and the entry goes once the issue is done; that holds for a rule the baseline
does not set too (one on only through `AnalysisLevel`, CA1848). The blocks do not reach the code the Razor compiler
generates from a `.razor` file's markup, nor a diagnostic without a location (MSTEST0001): the template-managed global
analyzer config `.squad/dotnet-rules.globalconfig` switches the baseline's off rules (S1125) off there too and sets
the severity of the rules reported without a location. A pending entry for one of those is written by `dotnet-rules.py`
into `.squad/dotnet-rules-pending.globalconfig`, whose higher `global_level` wins over the managed file;
`Directory.Build.props` names both in `GlobalAnalyzerConfigFiles`.

SonarAnalyzer.CSharp runs only the rules of *Sonar way core* by default. The rules the project's SonarQube Cloud C#
profile adds (*Sonar way comprehensive*: S3776, S1192, S107, …), and the core rules it switches off, are set in the
`squad:sonar-rules` block of `.editorconfig`, which `python3 .squad/tools/sonar-rules.py` writes from the two
profiles at `error` (`--check` reports a block out of date; a pending rule at `suggestion`), except the rules the
dotnet-rules baseline already sets, which keep the baseline's severity: the
*Analyzer gate* reports them in a changed file, so no rule of the profile is "one no local analyzer reports". A Sonar
rule the projects keep off is switched off in the baseline, since the built-in profile cannot be changed (S1125). Its
non-Roslyn checks (hotspots, taint analysis, duplication) have no local equivalent: the *Sonar gate*
below reports their issues and hotspots and the quality gate, duplication included, on the draft pull request.

Own browser scripts (`wwwroot/js/*.js`, no Node build) have no local analyzer: SonarQube Cloud's JavaScript
rules report on them only in the *Sonar gate* below, so write them to those rules from the start. The one that bit
(`javascript:S2486`): a `catch` block that ignores the exception holds exactly one whole-line comment and
nothing else — a comment before a `return` inside the block is still flagged.

Changed shell scripts (`*.sh`), GitHub workflows (`.github/workflows/*.yml`) and Dockerfiles are checked with
`shellcheck`, `actionlint` and `hadolint`; the SessionStart hook installs the three as pinned, checksum-verified
binaries (`.claude/hooks/install-linters.sh`), and the gate reports NOT RUN for one that is missing (a refused
download, for example). The gate itself checks two SonarQube Cloud shell rules shellcheck does not report:
`shelldre:S7679` (a positional parameter `$1`…`$9` used as a word of its own inside a function — assign it to a
local variable first) and `shelldre:S7688` (`[ … ]` instead of `[[ … ]]`). Other `shelldre:*` findings are first
reported by the *Sonar gate* below.

*Sonar gate* reads the CI code analysis of the draft pull request (squad step 11, *Draft until green* in
`.squad/orchestration.md`): `sonar-check.py pr <number>` checks that the analysis belongs to the pushed head and
fails on a failed quality gate, on every open issue and on every security hotspot to review. The work branch is
not scanned before the pull request: SonarQube Cloud does not let every organization read the data of non-main
branches through its API. A private project needs a SonarQube credential: a network secret of the cloud
environment for `sonarcloud.io` and `*.sonarcloud.io` (the agent proxy attaches the token to those requests, so no
session or member sees it), or else `SONAR_TOKEN` in the environment, which every process of the session can
read. A finding of a rule no local analyzer reports is noted in this section, in the same change, instead of a
squad lesson. `csharpsquid:S3776` (cognitive complexity above 15 in one method), `csharpsquid:S1192` (a string literal
repeated four times or more in one file) and `csharpsquid:S107` (a method with more than seven parameters) are not
such rules any more: the `squad:sonar-rules` block lets the *Analyzer gate* report them in a changed file. Write to
them from the start all the same: split a long method with nested branches into private methods, name a repeated
literal with a private constant, and bundle related arguments into a small type (a record or a readonly struct with
XML documentation) rather than adding parameters to a method that already has seven.

## Image build check

The Dockerfile copies the project files first, restores, then copies the sources and publishes with
`--no-restore`. The .NET SDK decides at restore time what the publish output holds (content items, static web
assets, the `_framework/` scripts of a Blazor app), so a publish from the full working tree can contain files
the image never gets (Vandox#24: `_framework/blazor.web.js` was in the local publish and missing from the
image, because no `.razor` file existed at restore time). A Blazor project therefore sets
`<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>` in its `.csproj`, so the restore requests the Blazor
scripts without any `.razor` file present (`Censio.Server`, record 0007); the script below stays unchanged. Where Docker runs, build the image. Where it does not
(the sandbox), reproduce the Dockerfile's order in a scratch directory; every change to a `.csproj`,
`Directory.*.props`, a `.razor` or other content item, a static web asset or the Dockerfile runs it before the
review. The image build needs both global configs of `.squad/` that `Directory.Build.props` names: a Dockerfile
that copies files by name copies them into the build stage, and a `.dockerignore` that leaves out `.squad/` lets them in
(`dotnet_config.py` checks both):

```bash
scratch="$(mktemp -d)"
git ls-files -z -- 'global.json' 'Directory.Build.props' 'Directory.Packages.props' \
  'src/Censio.Server/Censio.Server.csproj' 'src/Censio.Contracts/Censio.Contracts.csproj' \
  'src/Censio.Server.Data/Censio.Server.Data.csproj' | xargs -0 -I{} cp --parents {} "$scratch"
(cd "$scratch" && dotnet restore src/Censio.Server/Censio.Server.csproj -r linux-musl-x64)
git ls-files -z -- '.editorconfig' '.squad/dotnet-rules.globalconfig' '.squad/dotnet-rules-pending.globalconfig' 'src' | xargs -0 -I{} cp --parents {} "$scratch"
(cd "$scratch" && dotnet publish src/Censio.Server/Censio.Server.csproj -c Release --no-restore -r linux-musl-x64 --self-contained false -p:UseAppHost=false -o ./publish)
ls "$scratch"/publish/wwwroot/_framework/blazor.web*.js \
  || { echo "publish output lacks wwwroot/_framework/blazor.web*.js (see docs/decisions/0007)" >&2; false; }
```

Keep the first copy equal to the `COPY` lines before `RUN dotnet restore` in the Dockerfile and the second to the `COPY .editorconfig`, `COPY .squad/dotnet-rules.globalconfig .squad/dotnet-rules-pending.globalconfig .squad/` and `COPY src/` lines after it. A `--no-restore`
publish that fails, or an output that is missing, is the defect the CI release build would report after the
PR is open.

## Coverage exclusions

Two places compute coverage and must agree (`.squad/routing.md`, *Code a unit test cannot cover*): the
*Coverage gate* reads `COVERAGE_PATHSPECS` and `COVERAGE_EXCLUDES` in `.squad/tools/squad_settings.py` (by
default `src/*.cs` and `src/*.razor`, so own browser scripts do not count locally), and SonarQube Cloud reads
`sonar.coverage.exclusions`, passed as `/d:sonar.coverage.exclusions=…` in the *Begin SonarQube analysis*
step of `.github/workflows/ci.yml`. A file no unit test can cover (`wwwroot/js/**`, a generated file) is added
to that argument with a decision record — never to `sonar.exclusions`, which would drop it from the analysis
too.

`src/Censio.Companion/**` (the Windows shell) is excluded in `sonar.coverage.exclusions`, and locally by
construction: no test project loads that assembly, so the *Coverage gate* counts none of its files (record 0004).

## Writing code

- File-scoped namespaces; one top-level type per file; `using` outside the namespace (System first).
- Wrap every type's members in `#region` blocks **as you write the code**, grouped by member kind
  (`Constants`, `Fields`, `Constructors`, `Properties`, `Events`, `Methods`, …); a region that groups an
  interface implementation is named after the interface (e.g. `#region IParser`) and its description
  does not end with the word "implementation".
- XML documentation on all members (English, no `<remarks>`).
- Nullable reference types, `var`, language keywords over BCL types, LINQ method syntax only,
  `== false` instead of `!`, `is null` / `is not null`, no primary constructors, constructor injection
  with `_camelCase` readonly fields, `.ConfigureAwait(false)` in library/service code.
- Guards an analyzer asks for (e.g. `if (_logger.IsEnabled(...))` for CA1873) are written by the Dev,
  not added later by the Code Officer.
- Names the analyzers refuse, checked by the Lead before a name goes into the plan's *Signatures*: an enum
  whose name ends in `Flags` or `Enum` (S2344), and a nested type of any
  visibility (RH2101 class, RH2104 struct, RH2105 interface, RH2106 record, RH2107 enum, RH2108 delegate).

## Logger arguments

CodeQL's *Clear text storage of sensitive information* (`cs/cleartext-storage-of-sensitive-information`) judges
a value by its **name**: a variable, field, constant, property or parameter whose lower-case name contains
`password`, `passwd`, `trusted`, `account…id`, `accnt…id`, `account…key`, `accnt…key`, `license…key`,
`licence…key` or `voucher…code` counts as sensitive (`csharp/ql/lib/semmle/code/csharp/security/SensitiveActions.qll`
in `github/codeql`). For names there is no exemption: `PasswordHash` and a constant `EnvWebPasswordHash` that
holds only the name of an environment variable count too (the `hashed`/`encrypted`/`crypt` exemption applies
to string values, not to names). Such a name passed to a log call fails the PR in CI, and no local gate
reports it. The remedy is a neutral name for the value (a constant `WebLoginHashVariable` for the variable
name, for example) whose text a test pins, never a suppression.

## Writing tests

**Scripts only CI can run.** A script that needs systemd, root or a service the session lacks (an
end-to-end install check, for example) has a local smoke substitute, which the Tester runs before handing back:
`bash -n` and `shellcheck` on the script, and a mode of the script (or a test) that runs the documented lines
against a scratch directory with stub commands, without systemd. The plan lists the rows that can only run in
CI (`.squad/routing.md`, *Operator environments and documented procedures*).

See `docs/UNIT_TESTS.md`. The rules the MSTest and Sonar analyzers enforce and the Code Officer cannot fix
without handing back: pass `TestContext.CancellationToken` to every call that accepts a token
(MSTEST0049 / S8949), and use the specific `Assert` member instead of `Assert.IsTrue(...)` or
`StringAssert` (MSTEST0037 / MSTEST0046).

## Skeleton

New members with their full signature, XML docs and `#region` blocks, bodies
`throw new NotImplementedException();`, so the solution builds and the tests compile and fail.
Constructors store their arguments and never throw: a test harness builds these types, so only member bodies
fail.
The `Skeleton` body above is the default. A zero-value stub is allowed only where it would break a test that
exists and passes today: the new tests must still fail on the stub, and the Dev reports it as a deviation.
A skeleton raises diagnostics by construction, which the zero-diagnostic rule does not count against it: S1144
(unused private member), S1172 (unused parameter), S4487 (a private field assigned but not read yet), S2325 (a member
that does not use instance data yet), CS0067 (an event never raised yet), CS0649 (a field never assigned yet) and
RH2003 (`NotImplementedException`). Wrap exactly the members that raise one in `#pragma warning disable <the ids the
build reports>` / `#pragma warning restore <the same ids>` with the comment `// Skeleton: removed with the
implementation` — no other id; the Dev lists each pragma in its report and removes it in mode `implement`, and the
Reviewer blocks one that is still there.

## Dependencies

NuGet packages through **Central Package Management**: the version goes into `Directory.Packages.props`,
never into a `.csproj`. Dev-only analyzers are referenced for every project in `Directory.Build.props`.

## Concurrency

Builds and test runs share `bin/` and `obj/`; two at once break each other. `analyzer-check.py` serializes
itself with a lock (`obj/analyzer-check.lock`), plain builds do not.

## Known pitfalls

- `reihitsu-format` (1.2.0) turns an expression-bodied local function back into a block body, as the baseline's
  `csharp_style_expression_bodied_local_functions = false` (IDE0061) asks too, so leave such a local function as the
  formatter writes it.
- `reihitsu-format` asks for confirmation for more than 25 files; `--force` skips the prompt, which a
  non-interactive session cannot answer.
- `reihitsu-format` rewrites a `\u` escape in a C# string literal into the character it names, so a literal
  U+2028 then breaks the compilation and a test about an escape tests the wrong input — also each half of a
  surrogate pair (`\uD83D\uDE00` becomes the emoji). After *Format*, check
  the escapes of the changed files on disk (`grep -n '\\u' <file>`, *Escapes in files* in `.squad/team.md`);
  where the formatter keeps rewriting one, build the character at run time (`char.ConvertFromUtf32(0x2028)`)
  instead of writing it as an escape in a literal; a test that needs the escape text itself (a JSON input, say)
  keeps it in a raw string literal (`"""…"""`), whose `\u` the compiler and the formatter leave alone.
- A project with `UseWPF` and `UseWindowsForms` together gets a `CS0104` ("`Application` is ambiguous") from the
  implicit usings; the `.csproj` removes the `System.Windows.Forms` and `System.Drawing` global usings
  (`<Using Remove="…" />`, `Censio.Companion`).
- `Censio.Companion` sets `RuntimeIdentifier` `win-x64`, so every build, Linux included, writes the Windows
  `Censio.Companion.exe`; check it with `file` (`PE32+ executable`). Without the identifier the apphost follows
  the build machine (record 0004).
- The server image (`src/Censio.Server/Dockerfile`) is Alpine-based, so its tools are BusyBox: a command run
  inside it (`docker exec`, `docker run --entrypoint`) may not use GNU-only options such as `find -printf`; run
  the plain command and process its output on the host.
- In the cloud sandbox `dockerd` is installed but not running, and outbound HTTPS goes through the agent proxy,
  whose CA (`/root/.ccr/ca-bundle.crt`) the image's build stage does not trust: a plain
  `docker build -f src/Censio.Server/Dockerfile .` stops in `dotnet restore` with `NU1301 ... UntrustedRoot`. The
  *Image build check* there stays the restore-order reproduction. Where a criterion needs the image itself (a dry
  run), start `dockerd` as root in the background from the session's shell, so it inherits `HTTPS_PROXY` and pulls
  the base images through the proxy, wait until `docker info` answers, and build from the repository root with a
  copy of the Dockerfile on standard input that trusts the CA in the build stage only — the repository's
  Dockerfile stays unchanged, and the runtime stage gets neither the CA nor the proxy setting:

  ```bash
  { head -n 1 src/Censio.Server/Dockerfile
    printf '%s\n' 'COPY --from=ccr ca-bundle.crt /usr/local/share/ccr-ca-bundle.crt' \
      'ENV SSL_CERT_FILE=/usr/local/share/ccr-ca-bundle.crt'
    tail -n +2 src/Censio.Server/Dockerfile; } \
    | docker build --network host --build-arg HTTPS_PROXY="$HTTPS_PROXY" --build-context ccr=/root/.ccr \
      -f - -t censio .
  ```

  The Dockerfile's first line is the build stage's `FROM`; `--network host` lets the build reach the proxy on
  `127.0.0.1`. Never commit such a copy of the Dockerfile.
