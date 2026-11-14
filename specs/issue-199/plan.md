# Plan: Deep pages load without styles (#199); the as-of time flashes UTC on reload (#200)

Source: Issue #199, issue #200
Tier: standard — two behavior changes of the server's pages (the document base and what the as-of line shows before the browser's answer) that create, change or remove no security control.
Security areas touched: Network exposure (`App.razor`, which decides where the pages load their static assets from); Parsing of external input (`DataStatus`, the browser's UTC offset: its handling is unchanged, only what is shown before it arrives)

## Problem / root cause

- #199: `src/Censio.Server/Components/App.razor` lines 4-9 have no `<base>` element, and lines 7-8 and 12-14 write the
  style sheets and scripts as relative paths (`@Assets["css/censio.css"]` and so on). The browser resolves them
  against the page's own address, so below the first path segment they point under the page path and answer 404.
  `blazor.web.js` reads `document.baseURI` as the circuit's base address and to decide which links it follows in place,
  so the missing base also misleads Blazor on such a page (row 5).
- #200: `DataStatus.razor` line 8 formats the time with `PageFormat.Time(asOf, _offsetMinutes)`, and `_offsetMinutes` is
  `null` until `AskOffsetAsync` (`DataStatus.razor.cs` lines 125-140) has an answer. That call runs only in
  `OnAfterRenderAsync` (lines 150-165), which the server's prerender never runs and which the interactive render
  reaches only after its first render. Both renders therefore show the time in UTC with ` UTC` (`PageFormat.cs`
  `Time`), and the answer then replaces it.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `App.razor` has no `<base href>`; asset links are relative | `App.razor` lines 1-16; `ProgramTests` `_censioStyleSheet` matches `(^|/)css/censio…` | confirmed |
| 2 | A deep URL resolves the assets below its path and gets 404 | the issue's reproduction (`/profiles/a/islands/b/c/css/censio.<hash>.css` 404); HTML URL resolution | confirmed |
| 3 | The deep pages came with #192 | `git log --diff-filter=A -- …/IslandDetail.razor` gives 877bc0c (#192) | confirmed |
| 4 | In-app links, the icon route and the cookie path are root-relative | `PageAddress.cs` lines 81-133, `IslandDetail.razor.cs` line 56, `censio.js` `Path=/`, `MainLayout.razor` line 31 | confirmed |
| 5 | `blazor.web.js` uses `document.baseURI` for the circuit's base (`getBaseURI`) and for the in-place link check | read in `microsoft.aspnetcore.app.internal.assets/11.0.0/staticwebassets/_framework/blazor.web.js` | confirmed |
| 6 | Without a base, a deep page loaded in full misroutes or reloads on in-app navigation once interactive (related defect) | to verify: AC2's manual check by the Product Manager, before step 9 | unverified |
| 7 | No path base can be configured today | `Program.cs`: no `UsePathBase`, no `UseForwardedHeaders` | confirmed |
| 8 | Prerender and first interactive render show UTC, the answer then the local time (#200's "likely cause") | `DataStatus.razor` line 8, `DataStatus.razor.cs` lines 150-165; static prerender runs no `OnAfterRenderAsync` | confirmed |
| 9 | #200's "a time shown in UTC says so" already holds | `PageFormat.Time` appends ` UTC` without a usable offset | confirmed |
| 10 | Record 0078 is unreleased | `git ls-remote --tags origin`: v0.1.0, v0.2.0; `git tag --contains 877bc0c` is empty | confirmed |
| 11 | A browser that never answers ends the call by the circuit's interop timeout, which settles the line in UTC | to verify: the Dev reads the default of `CircuitOptions.JSInteropDefaultCallTimeout` in step 6 and reports it (`AskOffsetAsync` already catches `OperationCanceledException`) | unverified |
| 12 | `DataStatusTests.DataStatusShowsUtcWithoutOffset` asserts the behavior #200 removes | `DataStatusTests.cs`, the test with an unanswered setup expecting `… 12:00 UTC` | confirmed |

## Acceptance criteria

- AC1: A full load of any page — `/`, `/regions`, `/profiles/<p>/regions`, `/profiles/<p>/sessions/<s>`,
  `/profiles/<p>/islands/<s>/<i>/<a>` — carries `<base href="/">` in its head, and every style sheet
  (`link[rel=stylesheet]`) and script (`script[src]`) of the page, resolved against the document's base as a browser
  does, answers `200` from the server.
- AC2: On a full load of a deep island page with data (a reload), the page is styled, keeps its content once
  interactive, and the profile switch and the links open their pages.
- AC3: Until the browser's first answer about its offset — the prerender and the interactive render before that answer
  — a data page's as-of line is an empty paragraph: no time, no `As of` / `Stand:`, no ` UTC`. Once the browser answers
  with an offset within -840 to 840 minutes, the line shows `As of <time>` / `Stand: <time>` at that offset, without a
  suffix.
- AC4: When the browser's answer is unusable (a failed call, an answer that is no number, an offset out of range, a
  lost connection), the line shows the time in UTC with the suffix ` UTC`.
- AC5: After the first answer, a new as-of time (a live update) is shown at once at the last offset the browser gave,
  never as an empty line, and follows the browser's answer for the new instant.

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `ProgramGetPageResolvesResourcesAgainstRootBase` (new; DataRows for the five URLs, a malformed profile such as `q7malformedq7` for the deep ones) | `tests/Censio.Server.Tests/ProgramTests.cs` |
| AC2 | manual: the Product Manager, on a local run of the stamped head from source (`dotnet run --project src/Censio.Server -c Release --no-launch-profile`) with a recording replayed into it (`docs/USER_GUIDE.md`, *Sending a recording to a server*), before step 9 | — |
| AC3 | `DataStatusShowsNoTimeWhileBrowserHasNotAnswered` (new, replaces `DataStatusShowsUtcWithoutOffset`); `DataStatusShowsAsOfAtBrowserOffset` (existing, gets `AC3`) | `tests/Censio.Server.Tests/DataStatusTests.cs` |
| AC4 | `DataStatusOutOfRangeOffsetShowsUtc`, `DataStatusInteropFailureShowsUtc` (existing, adapted, get `AC4`) | `tests/Censio.Server.Tests/DataStatusTests.cs` |
| AC5 | `DataStatusNewAsOfShowsAtLastOffsetWhileAsking` (new; cannot fail on today's code, deferred to step 6) | `tests/Censio.Server.Tests/DataStatusTests.cs` |

Test cases:

- AC1: GET the page; the document base is the `href` of the first `base[href]` resolved against the page's URL, or
  the page's URL when there is none (so today's code fails on a 404, not on a missing element); assert the base's `href`
  is exactly `/`; resolve each `link[rel=stylesheet]` `href` and `script[src]` `src` against it with `new Uri(base, href)`
  and GET it. Assert the five resources (two style sheets, three scripts) and that each answers `200`, as one compared
  list of "address → status", so an empty list cannot pass. Fails today for the deep rows.
- AC3: the setup for `censio.utcOffsetMinutes` without a result (as the replaced test); assert exactly one `p`, its text
  empty after `Trim()`, and no `UTC` in the markup. Fails today (`As of 2026-11-13 12:00 UTC`).
- AC4: the two existing tests assert the text right after the invocation count; with the line empty until the failure
  is handled that races, so the Tester moves the text assertion into `WaitForAssertionAsync`.
- AC5: two setups matched on the instant argument: the first instant answers `60`, the second has no result; render,
  wait for `As of 2026-11-13 13:00`, then set `AsOf` one hour later (`Render` with the new parameter); assert
  `As of 2026-11-13 14:00` with the second call still unanswered.

## Approach

1. `App.razor`: add `<base href="/" />` as the first element of `<head>` after `<meta charset>`, before any link or
   script. Nothing else changes in the document.
2. `DataStatus`: a private field `_isOffsetSettled` (`bool`, XML doc "A value that indicates whether the browser's first
   answer about its offset, or its failure, has arrived"), set to `true` in `AskOffsetAsync` after the `try`/`catch`
   (both paths). It is never reset: a later instant keeps `_offsetMinutes` until its own answer (AC5).
   `DataStatus.razor`: when `AsOf` has a value and `_isOffsetSettled` is `false`, render `<p>&nbsp;</p>` (one no-break
   space, U+00A0, so the line keeps its height and the table below does not move); otherwise the existing line. With no
   `AsOf` nothing is rendered, as today.
3. Layout: the paragraph stays where the as-of line is; no new text, no new CSS. In-app navigation creates a new
   `DataStatus`, so its line is empty for one round trip there too — accepted (record 0078).

## Options considered

- #199: a base following the request's path base (the issue's suggestion) and root-relative asset paths without a
  base; both lost, reasons in record 0081 (no path base exists, the links are root-relative anyway, a request-derived
  base would decide where scripts load from; root-relative assets leave Blazor's base wrong).
- #200: showing UTC until the answer (today, the defect), the offset in a cookie read at render time (a first visit
  still flashes, a new untrusted input, wrong across daylight saving), and no prerender for `DataStatus` (the first
  interactive render still precedes the answer); reasons in record 0078. Not rendering the paragraph at all lost to
  the empty paragraph: the line appearing would push the table down.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Server | `Components/App.razor` | `<base href="/" />` in the head |
| Censio.Server | `Components/DataStatus.razor`, `DataStatus.razor.cs` | empty line until the first answer |
| Censio.Server.Tests | `ProgramTests.cs`, `DataStatusTests.cs` | tests above |

## Signatures (for the Dev's skeleton)

None: the change adds one private field and changes markup; no step 4.

## Test files

- `tests/Censio.Server.Tests/ProgramTests.cs`
- `tests/Censio.Server.Tests/DataStatusTests.cs`

Existing test code that calls a changed signature: none. Existing tests whose input the change handles differently:
`DataStatusShowsUtcWithoutOffset` (replaced, Tester, step 5), `DataStatusOutOfRangeOffsetShowsUtc` and
`DataStatusInteropFailureShowsUtc` (adapted, Tester, step 5). The page tests (`RegionsTests`, `RegionIslandsTests`,
`IslandDetailTests`) assert no as-of text beyond its absence on a not-found page and stay unchanged.

Files no unit test can cover: none.

## Areas

- `web-ui`: *Resources* — the resources are relative to the document base `/`.
- `web-pages`: *As-of line and banner* — the line before the first answer and on a new instant.

## Documentation updates

All by the Dev, against the code as built:

- `docs/areas/web-ui.md`, *Resources*: "by relative paths" becomes relative to the document base `/` (`<base href="/">`
  in `App.razor`), so a page at any URL depth loads them; record 0081 under *Related decisions*.
- `docs/areas/web-pages.md`, *As-of line and banner*: replace "Until the browser has answered, or when it cannot, the
  time is in UTC and carries the suffix ` UTC`" with the rule of AC3-AC5 (empty line until the first answer, UTC with
  ` UTC` only for an unusable answer, a new as-of time at the last offset until its answer). *The browser's offset*
  stays true.
- `docs/USER_GUIDE.md` line 440 (English, "in UTC until the browser has answered") and line 902 (German, "in UTC, bis
  der Browser geantwortet hat"): the time appears once the browser has answered and is in UTC, marked `UTC`, only
  when the browser cannot give its time zone.
- Sweep: `README.md` line 26, `docs/areas/server.md` *Start page* and `docs/ARCHITECTURE.md` state nothing the change
  makes untrue; no changelog exists; no `.squad/project.md` or `.squad/stack.md` entry changes.

## Architecture check

No guarantee of `.squad/project.md` changes: resources still come only from the server itself (same origin, base `/`),
and the browser's answer stays untrusted with the same range check. `docs/ARCHITECTURE.md` needs no update.
Both `.razor` files are inputs of the image build: the Dev runs the *Image build check* (`.squad/stack.md`) before the
review.

## Security considerations

- [x] No limits: the range check of the offset (-840 to 840) is unchanged.
- [x] Exceptions reaching a user-visible message: none; a failed browser call still only leaves the time in UTC.
- The base is a literal, no request data reaches it (record 0081, option 2 says why).

## Decision records

- `docs/decisions/0081-pages-resolve-resources-against-a-fixed-document-base.md` (Proposed, new)
- `docs/decisions/0078-times-shown-at-the-browsers-utc-offset.md` (unreleased, edited in place: options 3-5, decision,
  consequences)

## Out of scope / follow-ups

- Running the server under a path base (record 0081, *Consequences*).
