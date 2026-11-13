# Plan: Layout, theming and navigation with MudBlazor

Source: Issue #31 | [spec.md](spec.md)
Tier: security — the change adds routes reachable without login (MudBlazor's and an own static script and style
under the static assets) and makes `/` read browser input (two cookies and `Accept-Language`), which is the network
exposure the `security` row names.
Security areas touched: Network exposure (`src/Censio.Server/Program.cs`, `Components/App.razor`, new static assets);
CI workflow (`.github/workflows/ci.yml`, one coverage exclusion); dependencies (MudBlazor, bUnit for tests); build
configuration (`src/Censio.Server/Censio.Server.csproj`, `tests/Censio.Server.Tests/Censio.Server.Tests.csproj`,
`Directory.Packages.props`).

## Problem / root cause

A feature: [spec.md](spec.md).

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | The layout renders only the body; the start page has a bare `<h1>` and `<p>`; `<html lang="en">` is fixed | `Components/Layout/MainLayout.razor:3`, `Components/Pages/Home.razor:6-7`, `Components/App.razor:2` | confirmed |
| 2 | The release check reads the version line by line with `sed 's\|.*<p>Version \([^<]*\)</p>.*\|\1\|p'` | `.github/scripts/release-check.sh`, `check_image` (line 313) | confirmed |
| 3 | No MudBlazor or bUnit package yet | `Directory.Packages.props` | confirmed |
| 4 | MudBlazor 9.13.0 is MIT, targets `net10.0` (no `net11.0` group), and its CSS and script name no remote host | nuspec; `grep` of `MudBlazor.min.css` and `MudBlazor.min.js` from the 9.13.0 package for `http`: only the licence and project URLs in comments | confirmed |
| 5 | MudBlazor's default font family is Roboto, which nothing serves unless a page links a font host | `Roboto`, `Helvetica` strings in `MudBlazor.dll` 9.13.0; MudBlazor's install guide adds the Google Fonts link by hand | confirmed |
| 6 | A disabled MudBlazor button takes `color: var(--mud-palette-action-disabled) !important` | `MudBlazor.min.css` 9.13.0, rule `.mud-button-root:disabled` | confirmed |
| 7 | An inline `style` declaration with `!important` beats a style sheet's `!important` declaration | CSS Cascade 4, importance then origin then specificity (inline style counts as the highest) | confirmed |
| 8 | bUnit 2.15.2 is MIT and has a `net11.0` dependency group | nuspec | confirmed |
| 9 | MudBlazor 9.13.0 runs on .NET 11 (built for `net10.0`) with the server's shared framework | to verify: the Dev in step 4 — *Build* and the existing server tests on the skeleton with the package added | unverified |
| 10 | `MudThemeProvider` writes the CSS variables of the palette `IsDarkMode` selects into the prerendered HTML, as `--mud-palette-background: <value>` | to verify: the Tester in step 5, reading `MudThemeProvider` of MudBlazor 9.13.0 for the variable names and the colour format the tests compare | unverified |
| 11 | The server tests pass with `InvariantGlobalization` set in the test project, as the image runs | to verify: the Tester in step 5, a full run of `Censio.Server.Tests`; a failing existing test goes to the Lead (mode `decide`) | unverified |
| 12 | `/` reads no request input today; `server.md` says so | `server.md`, *Limits and errors*; `App.razor` | confirmed |
| 13 | The Docker image runs without ICU; `new CultureInfo("de")` throws there by default | `server.md`, *Docker image*; .NET's `PredefinedCulturesOnly` default under invariant mode | confirmed |
| 14 | No web page shows Companion data or offers a server action today | `Home.razor`; `server-state.md` line 10 "No page shows the state yet" | confirmed |
| 15 | The palette below meets N1 | WCAG 2.1 relative luminance computed for every pair of the table (scratch script); lowest text pair 5.91:1, lowest line pair 3.71:1 | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `ProgramGetRootShowsFrameInLightThemeWithEnglishTexts`, `ProgramGetRootShowsVersionInReleaseCheckMarkup`; existing `ProgramGetRootReturnsStartPageWithTitleHeadingAndVersion` | `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC2 | `UiPreferencesReadLanguageWithoutStoredValueFollowsLanguageList` (DataRows: `de-DE,en;q=0.8`, `en-US,de`, `fr,de`, `fr`, empty, null, `en;q=0.5, de`, `de;q=0, en`, `de;q=0`, `*`, `DE-at`, `de;q=0.5, en;q=0.5`, an unparsable header); `ProgramGetRootWithGermanLanguageListShowsGermanTexts` | `UiPreferencesTests.cs`, `ProgramTests.cs` |
| AC3 | `MainLayoutThemeSwitchTogglesDarkModeWithoutReload` | `MainLayoutTests.cs` |
| AC4 | `ProgramGetRootWithDarkCookieRendersDarkPalette`; `MainLayoutThemeSwitchStoresChoice`; manual: kept after a browser restart (below) | `ProgramTests.cs`, `MainLayoutTests.cs` |
| AC5 | `MainLayoutLanguageSwitchChangesTextsAndDocumentLanguage`, `MainLayoutLanguageSwitchStoresChoice`; `ProgramGetRootWithLanguageCookieOverridesLanguageList` | `MainLayoutTests.cs`, `ProgramTests.cs` |
| AC6 | `UiTextsForLanguageReturnsSpecTexts` (one DataRow per table cell); the server test project runs with `InvariantGlobalization` | `UiTextsTests.cs` |
| AC7 | `MainLayoutProfileSwitchIsDisabledWithLabelOnly` | `MainLayoutTests.cs` |
| AC8 | `MainLayoutNavigationButtonClosesAndOpensDrawer`; manual: the content takes the freed width (below) | `MainLayoutTests.cs` |
| AC9 | `UiPreferencesReadThemeWithUnknownValueReturnsLight`, `UiPreferencesReadLanguageWithUnknownStoredValueFollowsLanguageList` (DataRows: `DARK`, `fr`, `De`, ` dark`, `dark;x`, 4,000 `a`, `<script>`, `%0Adark`); `ProgramGetRootWithUnknownCookiesShowsDefaultsWithoutEcho` | `UiPreferencesTests.cs`, `ProgramTests.cs` |
| AC10 | `CensioThemeCreatePalettesMeetContrastLevels` (both palettes; text pairs 4.5, line and icon pairs 3.0) | `CensioThemeTests.cs` |
| AC11 | `ProgramGetRootLoadsResourcesOnlyFromOwnOrigin` | `ProgramTests.cs` |
| AC12 | existing `ProgramGetUnknownPathReturnsNotFound`, `ProgramGetHealthReturnsHealthyPlainText` | `ProgramTests.cs` |

Test notes for the Tester:

- AC1, AC5, AC9 compare the exact texts of the spec's table. The release-check test applies the same expression as
  `release-check.sh` to every line of the body (`^.*<p>Version ([^<]*)</p>.*$`) and expects exactly one match, equal
  to the product version.
- AC4's HTTP test sends `Cookie: censio-theme=dark` and asserts the dark palette's background variable (fact 10) and
  the absence of the light one; AC5's sends `censio-language=de` with `Accept-Language: en` and asserts `lang="de"` and
  `Startseite`.
- AC9's HTTP test sends unknown values for both cookies, expects `200`, `lang="en"`, the light background, the body
  not containing any sent value, and no log entry containing it: the server tests link
  `tests/Censio.Server.Data.Tests/RecordingLoggerProvider.cs` (as they link `RecordingTimeProvider.cs`) and add it
  through `WithWebHostBuilder(... ConfigureLogging ...)`.
- AC11: every `href` and `src` of `link`, `script` and `img` in the body is a relative path (no scheme, no `//`); each
  answers `200`, and no style sheet among them contains `@import`, `url(http` or `url(//`.
- The `MainLayout` tests use bUnit's `BunitContext` with `AddMudServices()` and loose JavaScript interop, render
  `MainLayout` with a `Body` and the cascading `UiTheme` and `UiLanguage` start values, and assert the interop calls
  `censio.storePreference` (`censio-theme`/`dark`, `censio-language`/`de`) and `censio.setDocumentLanguage` (`de`).
  AC3 asserts `MudThemeProvider.IsDarkMode` and the button's `aria-label` from the table; AC8 asserts the drawer's
  `Open` state; AC7 asserts the `disabled` attribute, the exact visible text and that the layout has no `ServerState`
  dependency (it renders with none registered).
- Manual (Product Manager, before step 9, on a local run of the branch head with
  `dotnet run --project src/Censio.Server -c Release` and Chrome or Edge): choose dark and German, close and reopen the
  browser, reload — dark and German from the first paint (AC4, AC5); close the navigation — the content widens (AC8);
  check the rendered contrast with the browser's developer tools in both themes, the focus ring of each control
  included (N1). The orchestrator shows the palette table below with this request.

## Approach

**Packages and wiring.** `MudBlazor` 9.13.0 in `Directory.Packages.props` and `Censio.Server.csproj`; `bunit` 2.15.2
in the props and `Censio.Server.Tests.csproj`, which also sets `InvariantGlobalization` `true` (AC6, record 0072).
`Program.cs` adds `builder.Services.AddMudServices()`; no other change to the pipeline and no new endpoint.
`_Imports.razor` adds `@using MudBlazor`.

**App.razor (static, prerendered)** takes `[CascadingParameter] HttpContext`, reads the theme with
`UiPreferences.ReadTheme(Request.Cookies[ThemeCookieName])` and the language with
`UiPreferences.ReadLanguage(Request.Cookies[LanguageCookieName], Request.Headers.AcceptLanguage)`, writes
`<html lang="@UiPreferences.StoredValue(language)">`, links `_content/MudBlazor/MudBlazor.min.css` and the script
`_content/MudBlazor/MudBlazor.min.js` and `js/censio.js` through `@Assets[...]`, and passes both values to
`<Routes @rendermode="InteractiveServer" Theme=… Language=… />` (enums serialize as root parameters). No font link.

**Routes.razor** cascades `Theme` and `Language` (by type) around the router.

**UiPreferences** (static): `ReadTheme` returns `Dark` only for the ordinal value `dark`, else `Light`.
`ReadLanguage` returns the stored language for the ordinal values `en` and `de`; otherwise it parses the header with
`StringWithQualityHeaderValue.TryParseList` (`Microsoft.Net.Http.Headers`) — unparsable or empty: `English` — orders
the entries by quality descending, stable by position, drops quality 0, and takes the first whose primary subtag
(before the first `-`) is `de` or `en`, case-insensitive; none: `English`. No value is logged, echoed or stored on the
server. The header's size is bounded by Kestrel's request-header limit; no own bound.

**UiTexts** (sealed, two static instances): the six texts of the spec's table per language; `LanguageName` gives
`English` and `Deutsch`. Nothing in the web UI creates a `CultureInfo`, uses `IStringLocalizer` or request
localization (record 0072).

**MainLayout.razor** holds the current theme, language and drawer state, starting from the cascaded values, and
cascades the current `UiTexts` to the page. Layout, top to bottom:

- `MudThemeProvider` (`Theme` = `CensioTheme.Create()` once per layout, `IsDarkMode` from the state; never asks for the
  system preference), `MudPopoverProvider`.
- `MudLayout` with a `MudAppBar` (elevation 1): the navigation `MudIconButton` (menu icon, `aria-label` from the
  table); `MudText` `Typo.h6` `Censio`; `MudSpacer`; the profile placeholder; the language `MudMenu` (translate icon,
  `AriaLabel` from the table, items `English` and `Deutsch`); the theme `MudIconButton` (dark-mode icon in the light
  theme, light-mode icon in the dark, `aria-label` from the table).
- The profile placeholder: `MudButton`, `Variant.Text`, person icon, text `Profile`/`Profil`, `Disabled="true"` (not
  focusable), `Style="color: var(--mud-palette-appbar-text) !important"` (facts 6, 7). It injects nothing.
- `MudDrawer` `DrawerVariant.Persistent`, open at start, `ClipMode.Always`, with a `MudNavMenu` of one
  `MudNavLink` `Href="/"` `Match="NavLinkMatch.All"` (home icon, `Home`/`Startseite`).
- `MudMainContent` with a `MudContainer` around `@Body`.

A theme click flips the state, calls `censio.storePreference("censio-theme", "light"|"dark")` and re-renders; a
language choice sets the state, calls `censio.storePreference("censio-language", "en"|"de")` and
`censio.setDocumentLanguage("en"|"de")`. A failed interop call (connection lost, E4) is caught as
`JSDisconnectedException` or `JSException` and ignored: the open page keeps the new state, and nothing is stored or
logged.

**wwwroot/js/censio.js** (new): `window.censio = { storePreference(name, value), setDocumentLanguage(tag) }`; the first
writes `name=value; Max-Age=31536000; Path=/; SameSite=Lax` to `document.cookie`, the second sets
`document.documentElement.lang`. Both receive only the constants above. No `catch` (Sonar `javascript:S2486`).

**Home.razor** keeps `<PageTitle>Censio</PageTitle>`, writes `<h1 class="mud-typography mud-typography-h3">Censio</h1>`
and keeps `<p>Version @…</p>` bare and on one line (N5, fact 2).

**CensioTheme** (static) `Create()` returns a `MudTheme` with the palettes below; typography: default family
`"Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`, headings `h1`–`h6` `Georgia, "Times New Roman", serif`
(system fonts only, N2). Unlisted palette values keep MudBlazor's defaults.

| Palette value | Light | Dark |
| ------------- | ----- | ---- |
| Primary (Roman red / terracotta) | `#8B1E1E` | `#E8907A` |
| PrimaryContrastText | `#FFFFFF` | `#1C1310` |
| Secondary (bronze gold / gold) | `#7A5A12` | `#D9AE4A` |
| SecondaryContrastText | `#FFFFFF` | `#1C1310` |
| AppbarBackground | `#6E1616` | `#2E1515` |
| AppbarText | `#FFFFFF` | `#F5EDE3` |
| Background (marble / night) | `#FAF6EF` | `#1A1614` |
| Surface | `#FFFFFF` | `#251F1C` |
| DrawerBackground | `#F2EADD` | `#211B18` |
| DrawerText | `#2B2420` | `#EDE4D8` |
| DrawerIcon | `#5A4F45` | `#C2B5A6` |
| TextPrimary | `#2B2420` | `#EDE4D8` |
| TextSecondary | `#5A4F45` | `#C2B5A6` |
| ActionDefault | `#5A4F45` | `#C2B5A6` |
| LinesInputs | `#8A7D70` | `#8F8274` |

AC10's pairs, both palettes — text 4.5:1: TextPrimary and TextSecondary on Background and Surface, AppbarText on
AppbarBackground, DrawerText on DrawerBackground, Primary on Background, Surface and DrawerBackground (the marked nav
entry), PrimaryContrastText on Primary, SecondaryContrastText on Secondary, Secondary on Background and Surface;
3:1: DrawerIcon on DrawerBackground, ActionDefault and LinesInputs on Background and Surface.

## Options considered

- Preferences in local storage, or cookies set through a new endpoint, against cookies written by a script: record
  0071 (local storage paints the default first; an endpoint is a new open route with a redirect to confine).
- Resource files with request localization, or ICU in the image, against an own text table: record 0072.
- Profile placeholder kept focusable with `aria-disabled` (challenge note 2's alternative) — lost: a focusable control
  that does nothing is noise for keyboard users; the disabled button with an explicit label colour meets N1.
- Interactive tests by hand only, without bUnit — lost: AC3, AC5, AC7 and AC8 are the feature's core and would have
  no unit test; bUnit is a test library like `FakeTimeProvider`, not a mocking library.
- Fonts served by the server (a bundled Roboto or a serif) — lost against system fonts: a font file adds a licence and
  bytes for a desktop-only UI (record 0032) where Segoe UI and Georgia are present.
- Drawer `Responsive` (MudBlazor's default, an overlay on narrow windows) — lost: the phone layout is #53, and
  `Persistent` gives the freed width AC8 asks for.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server | `UiTheme.cs`, `UiLanguage.cs`, `UiPreferences.cs`, `UiTexts.cs`, `CensioTheme.cs` | new |
| Censio.Server | `Components/App.razor`, `Routes.razor`, `_Imports.razor`, `Layout/MainLayout.razor`, `Pages/Home.razor` | rewritten as *Approach* says |
| Censio.Server | `Program.cs`, `Censio.Server.csproj` | `AddMudServices()`; package reference |
| Censio.Server | `wwwroot/js/censio.js` | new |
| build | `Directory.Packages.props`; `tests/Censio.Server.Tests/Censio.Server.Tests.csproj` | MudBlazor, bunit; bunit reference, `InvariantGlobalization`, link of `RecordingLoggerProvider.cs` |
| CI | `.github/workflows/ci.yml` line 71 | `sonar.coverage.exclusions` gains `src/Censio.Server/wwwroot/js/**` |

## Signatures (for the Dev's skeleton)

Namespace `Censio.Server` for the C# types; the enums' members are Light and Dark, English and German.

- `public enum UiTheme`
- `public enum UiLanguage`
- `public static class UiPreferences`, with the constants `UiPreferences.ThemeCookieName` (value censio-theme) and
  `UiPreferences.LanguageCookieName` (value censio-language) and
  - `public static UiTheme ReadTheme(string? storedValue)`
  - `public static UiLanguage ReadLanguage(string? storedValue, string? acceptLanguage)`
  - `public static string StoredValue(UiTheme theme)` and its overload for a `UiLanguage`
- `public class UiTexts` (sealed), with
  - `public static UiTexts For(UiLanguage language)`
  - `public static string LanguageName(UiLanguage language)`
  - the read-only text properties `UiTexts.NavigationButton`, `UiTexts.HomeEntry`, `UiTexts.ProfileSwitch`,
    `UiTexts.SwitchToDarkTheme`, `UiTexts.SwitchToLightTheme`, `UiTexts.LanguageSwitch`
- `public static class CensioTheme` with `public static MudTheme Create()`
- the component parameters (attribute Parameter) `Routes.Theme` and `Routes.Language`, and the cascading parameters
  (attribute CascadingParameter, by type) `MainLayout.InitialTheme` and `MainLayout.InitialLanguage`, all public
  properties with getter and setter

Existing files the skeleton rewrites: `Routes.razor` and `MainLayout.razor` (parameters only, markup unchanged until
step 6). Names checked against *Writing code*: no nested type, and neither enum name ends in Flags or Enum.

## Test files

`tests/Censio.Server.Tests/UiPreferencesTests.cs`, `UiTextsTests.cs`, `CensioThemeTests.cs`, `MainLayoutTests.cs`
(new), `ProgramTests.cs` (new tests added).

Existing test code that calls a changed signature: none. `ProgramTests`' existing start-page tests keep their inputs and
must pass unchanged.

Files no unit test can cover: `src/Censio.Server/wwwroot/js/censio.js` — a browser script without a test runner; its
calls are asserted by the bUnit tests, its effect by the manual check; excluded in `sonar.coverage.exclusions`
(record 0071), outside `COVERAGE_PATHSPECS` locally.

## Areas

- New area `web-ui` (Lead decision), index row: scope "The web UI's frame: app bar, navigation, profile switch
  placeholder, the light and dark theme with their palette, typography and contrast levels, the language choice and
  its default, the two cookies, unknown values, the frame's texts in English and German, resources from the server
  only, desktop browsers"; not here: "The HTTP surface and the start page's version line (see server), pages' data
  (#32)".
- `server.md`: as the spec's *Area documents* says.

## Documentation updates

All by the Dev, after the code is green:

- `docs/areas/web-ui.md` (new, from `_template.md`) with its index row in `docs/areas/README.md`: the behavior of
  *Approach* as rules, the palette table and contrast levels, typography, cookie names, values, `Max-Age` 365 days,
  `Path=/`, `SameSite=Lax`, the language ranking, the unknown-value rule, the text table; links records 0025, 0071,
  0072.
- `docs/areas/server.md`: *Start page* (shown in the frame of `web-ui.md`, title, heading and version line kept, the
  version line stays a bare `<p>` for the release check), *Limits and errors* (`/` reads the two cookies and
  `Accept-Language`, see `web-ui.md`), *Not here* and the area index row's "web theming" point at `web-ui`.
- `docs/ARCHITECTURE.md`: `Censio.Server` row "Web host, UI (frame, themes and languages,
  [`areas/web-ui.md`](areas/web-ui.md))"; *Security model* untrusted input gains "the browser's preference cookies
  and language header".
- `README.md` and `docs/USER_GUIDE.md`, English then German: the theme and language switches, light and English or
  the browser's language by default, kept per browser for a year, the disabled profile switch until a later version.
- `.squad/project.md` (exact text reported to the orchestrator): *Security areas*, Network exposure line gains "the
  preference cookies and `Accept-Language` read on `/` (`UiPreferences`, `App.razor`) — [web-ui](../docs/areas/web-ui.md)";
  *Integration surface* gains "**A new page or UI text** touches: `UiTexts` (English and German) and its tests, the
  navigation in `MainLayout.razor`, `docs/areas/web-ui.md` (*Texts*), the user guide". *Test doubles*:
  `RecordingLoggerProvider` "linked into the Server tests" (Tester reports this line).
- Records 0071 and 0072: the Lead sets `Area: web-ui` once the index row exists (`tasks.md`).

## Architecture check

No guarantee of `.squad/project.md` changes: plain HTTP and the open start page stay (record 0006); no page shows
Companion data or offers a server action, so 0006's revisit condition is not reached (N7, fact 14); ticks and the
ingest are untouched. The server stores nothing new. The browser loads nothing from another host (N2).

## Security considerations

- [x] Limits: the cookie values are compared whole and ordinally against four constants, no length limit needed; the
  header is bounded by Kestrel's request-header limit before parsing; nothing is decoded.
- [x] Exceptions: none reaches the user. `TryParseList` does not throw; interop failures are caught and ignored; an
  unparsable header gives English.
- Nothing read from a cookie or header is echoed, logged or stored; the only values written to `document.cookie` and
  `lang` are constants. The cookies are not `HttpOnly` (a script writes them) and not `Secure` (plain HTTP, record
  0006); they carry no identity. `SameSite=Lax`.
- New public static assets: MudBlazor's CSS and script and `js/censio.js`, served by `MapStaticAssets` like the Blazor
  script, no directory listing. No new endpoint, header, CORS rule or listener.

## Decision records

- `docs/decisions/0071-web-ui-choices-kept-in-browser-cookies-written-by-a-script.md` (Proposed)
- `docs/decisions/0072-web-ui-texts-from-an-own-table-without-culture-data.md` (Proposed)

## Out of scope / follow-ups

As the spec's *Out of scope*. No follow-up issue.
