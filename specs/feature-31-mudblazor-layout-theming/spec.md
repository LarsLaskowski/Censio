# Spec: Layout, theming and navigation with MudBlazor

Source: Issue #31
Status: Draft

## Problem

The web UI is a bare, unstyled, English-only start page ([`server.md`](../../docs/areas/server.md), *Start page*).
The pages of #32 and later need a common frame: app bar, navigation, a light and a dark theme with the Censio look, and
German or English texts (record [0025](../../docs/decisions/0025-mudblazor-and-blazor-apexcharts-light-theme.md)). A
player who keeps the page open next to the game in the evening wants it dark, and dark after a reload.

## Scenarios

- S1: Given a browser that never opened Censio, when the user opens `/`, then the page shows an app bar with the
  name `Censio`, a button that opens and closes the navigation, a disabled profile switch, a theme switch and a
  language switch; the navigation is open and lists the start page (`Home` / `Startseite`); the page uses the light
  theme; the start page still shows the heading `Censio` and the line `Version <version>`.
- S2: Given that browser, when its language list names German before English (any German variant such as `de`,
  `de-DE`, `de-AT`), then every text of the frame is German; when it names English first, or neither of the two,
  every text is English.
- S3: Given the light theme, when the user presses the theme switch, then the page turns dark at once, without a
  reload; pressing it again turns it light.
- S4: Given the dark theme was chosen, when the user reloads the page, closes and reopens the browser, or opens
  another Censio page in the same browser, then the page appears dark from its first paint, with no light flash
  before it.
- S5: Given English texts, when the user chooses `Deutsch` in the language switch, then every text of the frame and
  the page turns German at once, without a reload; choosing `English` turns them back. The choice survives a reload
  and a browser restart as in S4 and overrides the browser's language list from then on.
- S6: Given any theme and language, when the user points at or focuses the profile switch, then it reads
  `Profile` / `Profil` and cannot be opened; it shows no profile name and no other stored data.
- S7: Given the navigation is open, when the user presses the navigation button, then it closes and the page uses
  the freed width; pressing it again opens it. Choosing an entry of the navigation opens that page, and the entry of
  the page shown is marked.

The frame's texts, exactly:

| Element | English | German |
| ------- | ------- | ------ |
| Navigation button (accessible name) | `Open or close the navigation` | `Navigation öffnen oder schließen` |
| Navigation entry of `/` | `Home` | `Startseite` |
| Profile switch | `Profile` | `Profil` |
| Theme switch in the light theme (accessible name) | `Switch to the dark theme` | `Zum dunklen Design wechseln` |
| Theme switch in the dark theme (accessible name) | `Switch to the light theme` | `Zum hellen Design wechseln` |
| Language switch (accessible name) | `Language` | `Sprache` |
| Language options | `English`, `Deutsch` | `English`, `Deutsch` |

## Errors and edge cases

- E1: The browser keeps no stored choice (cookies or site data blocked, private window closed, data cleared): the
  page opens with the defaults of S1 and S2 and works; a switch still changes the open page, and nothing is shown
  about the lost choice.
- E2: A stored choice the server does not know (another language code, a theme name of another version, a damaged
  or overlong value, a value with markup or control characters): treated as no choice, the default applies, the page
  answers `200` as usual, and the value is neither shown on the page nor logged.
- E3: Two tabs of one browser: the other tab keeps its look and language until it is reloaded.
- E4: The connection to the server is lost: a switch pressed meanwhile changes nothing; a reload shows the last
  choice made before the loss.
- E5: An unknown path still answers `404` with an empty body, `/health` still answers `Healthy`, and neither gets the
  frame ([`server.md`](../../docs/areas/server.md), *Plain HTTP and unknown paths*, *Health check*).
- E6: The browser window is narrow (below the desktop width): the frame stays usable (the navigation can be closed
  and the app bar's controls stay reachable); a layout made for phones and tablets is #53's (record
  [0032](../../docs/decisions/0032-telegram-alerts-and-mobile-layout-later.md)).

## Non-functional requirements

- N1: Contrast, in both themes, for every text and control of the frame and the start page, checked against
  WCAG 2.1 level AA: text against its background at least 4.5:1, large text (at least 24 px, or 18.66 px bold) at
  least 3:1, the boundaries and icons of controls and the focus indicator at least 3:1 against their surroundings.
  A disabled control is exempt by WCAG, but the profile switch's label still meets 4.5:1.
- N2: Every resource the page loads (styles, scripts, fonts, icons) comes from the Censio server itself: no content
  delivery network and no font host, so a browser without internet access shows the same page and no third party
  learns that a page was opened.
- N3: The theme and language choice is kept only in the browser that made it, for at least 365 days after the last
  change; the server stores nothing about it, and no log line holds it. Nothing else is stored in the browser by this
  feature.
- N4: The page's `lang` attribute names the language shown (`en` or `de`), so screen readers and spelling tools
  follow the choice.
- N5: The start page keeps its title `Censio`, its heading `Censio` and the line `Version <version>`, in both
  languages and both themes, in the markup the release check reads (`.github/scripts/release-check.sh`, function
  `check_image`, which takes the version from a `<p>Version …</p>` element of the HTML of `/`).
- N6: The German texts work in the Docker image, which runs without ICU culture data
  ([`server.md`](../../docs/areas/server.md), *Docker image*).
- N7: No page of this feature shows data the Companion sent and none offers a state-changing action on the server, so
  record [0006](../../docs/decisions/0006-server-plain-http-and-open-health.md)'s condition for revisiting host
  filtering and the hub's `Origin` check is not reached; the switches change only the browser's own choice.
- N8: The look and the texts are made for desktop browsers (record 0032): current Chrome, Edge and Firefox.

## Acceptance criteria

- AC1: A first visit to `/` shows the app bar with the name `Censio`, the navigation button, the profile switch,
  the theme switch and the language switch, an open navigation with the start page's entry marked, the light theme
  (also when the operating system prefers dark), the title `Censio`, the heading `Censio` and `Version <version>`
  in the release check's markup (S1, S7, N5).
- AC2: Without a stored choice, the language is German exactly when a German variant comes before English in the
  browser's language list, otherwise English; the page's `lang` attribute names it (S2, N4, E1).
- AC3: The theme switch turns the open page from light to dark and back without a reload, and its accessible name
  is the one of the table for the theme shown (S3).
- AC4: After a dark theme was chosen, the HTML the server sends for `/` on the next request of that browser is
  already in the dark theme (no light first paint), also after a browser restart (S4, N3).
- AC5: Choosing `Deutsch` or `English` changes every text of the frame and the `lang` attribute without a reload,
  and the server's next HTML for that browser is in that language, whatever its language list says (S5, N4, N3).
- AC6: Every text of the frame in both languages is exactly the one of the table in *Scenarios*, and the German
  texts render in the Docker image (S1, S5, N6).
- AC7: The profile switch is disabled, reads `Profile` / `Profil`, cannot be opened and holds no profile name (S6,
  N7).
- AC8: The navigation button closes and opens the navigation, and the content takes the freed width, in the desktop
  browsers of N8 (S7, E6, N8).
- AC9: A stored value that is not a known theme or language leads to the defaults of AC1 and AC2, a `200` answer, and
  appears neither in the page nor in a log line (E2, N3).
- AC10: Every text-background pair and control boundary of the palette of both themes meets the contrast levels of
  N1 (N1).
- AC11: The HTML of `/` and every resource it loads come from the server's own origin; no `link`, `script`, `img`
  or `@import` names another host (N2).
- AC12: An unknown path answers `404` with an empty body and `/health` answers `Healthy`, as before (E5).

## Out of scope

- A working profile switch that lists profiles and chooses whose data a page shows: #32 (record
  [0041](../../docs/decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md): it chooses only what
  a page shows, never where ticks go). S6 deliberately shows no profile data.
- Game names in German or English: they follow this language choice once #32 shows them (record
  [0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md)).
- Navigation entries for pages that do not exist yet; each later page adds its own entry.
- Remembering whether the navigation is open; following the operating system's dark mode; languages other than
  German and English.
- A layout for phones and tablets: #53 (E6). Charts and their theme: #36.
- E3 and E4 need no criterion of their own: they follow from AC4 and AC5 and from Blazor's existing reconnect notice.

## Open questions

- Q1: What is the Censio colour palette? Nothing in the repository defines one. Answered: by the Product Manager —
  the Lead defines it in the plan with an Anno 117 / Roman look (for example deep red or purple with gold on warm
  neutrals), checked against N1 in both themes and recorded in `web-ui.md`; the Product Manager approves it.
- Q2: Which language does a first visit show? Answered: the browser's language list, English otherwise (S2); the
  issue names no default, and English is the project's first language (record 0030).
- Q3: Does the profile switch placeholder show the profiles? Answered: no — #32 builds the switch, and stored data
  on a page would reach record 0006's revisit condition (S6, N7).
- Q4: Which typeface? Answered: the plan picks it within N1 and N2 (served by the server itself or a system font).

## Area documents

- New area `web-ui` (`docs/areas/web-ui.md`, index row in `docs/areas/README.md`): the frame (app bar, navigation,
  profile switch placeholder), the two themes with the palette and the contrast levels, the default theme, the
  language choice and its default, how long a choice is kept and where, the treatment of an unknown stored value, the
  frame's texts in both languages, that every resource comes from the server itself, desktop browsers only.
- `docs/areas/server.md`: *Start page* says the page is shown in the frame of `web-ui.md` and keeps title, heading
  and version line; *Not here* points at `web-ui.md` instead of "#31"; the index row's "web theming" likewise.

## Challenge notes

Spec challenge (mode `spec`) of /home/user/Censio/specs/feature-31-mudblazor-layout-theming/spec.md, against issue #31 and its comment (record 0041).

The spec's main claims hold up against the code:
- **The start page today:** `MainLayout.razor` renders only `@Body`, `Home.razor` holds just the title, heading and version line, and `App.razor` hard-codes `<html lang="en">`.
- **N5 release check:** `.github/scripts/release-check.sh:313` really does extract the version with `sed 's|.*<p>Version \([^<]*\)</p>.*|\1|p'`, so the bare `<p>Version …</p>` must stay as it is.
- **N6 culture data:** `docs/areas/server.md` (*Docker image*) does say the runtime image runs with invariant globalization and no ICU.
- **No MudBlazor yet:** `Directory.Packages.props` has no MudBlazor, so the feature does not already exist.
- **N7 and record 0006:** record 0006 (Consequences, line 47-48) only revisits its choice for "game data or a state-changing action on a page", and N7 stays inside that.

No objection is major. Four are minor:

1. **minor: S2 / AC2 – "names German before English" leaves weighted language lists undefined.** Browsers send the language list as the `Accept-Language` header, which can carry weights (`q` values). A header like `en;q=0.5, de` or `de;q=0, en` puts the two languages in a different order by position than by weight. An implementation that compares positions and one that sorts by weight (as ASP.NET Core's `AcceptLanguageHeaderRequestCultureProvider` does) would both pass AC2 as written but answer differently. Instruction for the Lead: state in S2/AC2 that the order is by `q` weight, highest first (ties by position), and that `q=0` excludes a language. The Tester covers one weighted header in each direction.

2. **minor: S6 / AC7 / N1 – a "disabled" profile switch cannot be focused or hovered as S6 describes.** A disabled `<button>` is taken out of the tab order. MudBlazor's disabled buttons also get `pointer-events: none` and the theme's low-contrast disabled colour (about 38 % opacity). So "when the user points at or focuses the profile switch, then it reads `Profile`" fails on any build that uses the `disabled` attribute. N1's 4.5:1 for its label fails with MudBlazor's default disabled palette. Instruction for the Lead: make `Profile` / `Profil` the switch's visible text, drop "points at or focuses" from S6, and state that the label's colour is set explicitly to meet 4.5:1 rather than taken from the disabled palette. The alternative is to state that the control is `aria-disabled` and stays focusable; either way, pick one.

3. **minor: *Area documents* misses a statement it contradicts, and the documentation the issue asks for.** `docs/areas/server.md`, *Limits and errors*, says "The server reads no request body and takes no input on `/` or `/health`". With this feature, `/` reads the stored theme/language value (a cookie, since AC4 needs it on the request) and `Accept-Language`. `/` is also listed under *Network exposure* in the security areas of `.squad/project.md`. The issue's *Documentation* section asks for README and user guide text (English, then German, in each file) so that external testers can try the feature, and the spec does not mention it. Instruction for the Lead: add to *Area documents* that this `server.md` line changes to name the stored choice and the language header as the input `/` reads, linking E2's handling. Also add one line saying `README.md` and `docs/USER_GUIDE.md` describe the theme and language switches in both languages.

4. **minor: N6 / AC6 – "the German texts render in the Docker image" has no stated check outside the image.** Under invariant globalization, `new CultureInfo("de-DE")` throws `CultureNotFoundException` by default (`PredefinedCulturesOnly`). A build that does its localization through `CultureInfo` / `IStringLocalizer` could therefore pass every unit test on a machine with ICU and still fail in the image. S5's switch without a reload also cannot rely on the request's culture, which is fixed for the open page. Instruction for the Lead: phrase AC6 so that it is checked without Docker. The German texts must not depend on culture data (for example, the server's tests run one case with `System.Globalization.Invariant` set). Alternatively, name the image check as AC6's verification, with an owner.
