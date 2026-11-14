# Web UI

## Scope

The frame of the server's web pages: the app bar, the navigation, the profile switch, the light and the
dark theme with their palette, typography and contrast levels, the language choice and its default, the two cookies
that keep the choices, the treatment of unknown stored values, the frame's texts in English and German, and that every
resource comes from the server itself. Desktop browsers only. Users of the web application rely on it; the pages'
data is in [`web-pages.md`](web-pages.md).

## Behavior

### Frame

- Every page is shown inside one frame: an app bar on top, a navigation on the left and the page content in the
  remaining width.
- The app bar shows, from left to right, the navigation button, the name `Censio`, the profile switch, the language
  switch and the theme switch.
- The navigation is open when a page opens and lists the start page (`Home` / `Startseite`, link `/`) and the region
  overview (`Regions` / `Regionen`). The Regions entry leads to the region overview of the profile whose page is shown
  (`/profiles/{profile}/regions`, followed as the address changes) and to `/regions` on every other address; it is marked
  on every address that starts with its link. The navigation button
  closes and opens it; while it is closed the content takes the freed width.
- The profile switch is a menu in the app bar with the text `Profile` / `Profil`. It is disabled while no profile
  exists. Otherwise it lists at most 100 profiles by shown name, each a link to that profile's region overview, then the
  disabled line `<n> more profiles` / `<n> weitere Profile` when there are more; a profile without a name is `Unnamed
  profile` / `Unbenanntes Profil`, and equal shown names are told apart as [`web-pages.md`](web-pages.md) says. The list
  follows the server's profiles without a reload. The switch only chooses what the data pages show; every name is shown
  as text.
- A page receives the language and the texts that are shown at the moment as cascaded values, so a page follows a
  language switch without a reload.

### Theme

- The default theme is light. The server never asks the browser for its system preference.
- The theme switch turns the page dark and back at once, without a reload; its accessible name names the theme it
  switches to.
- The theme is applied while the page is rendered on the server, so a stored dark theme appears from the first paint,
  without a light flash.

### Language

- The default language follows the browser's `Accept-Language` list: the entries are ranked by weight, highest first,
  an absent weight counts as 1, equal weights keep their order of appearance, weight 0 is excluded; the first entry
  whose primary language (before the first `-`, any letter case) is `de` or `en` decides. A list that names neither,
  is empty, absent or cannot be read, gives English.
- The language switch offers `English` and `Deutsch` (each in its own language). Choosing one changes every text of
  the frame and the page at once, without a reload, and sets the document language (`lang`) of the open page.
- A stored language overrides the browser's list from then on.

### Stored choices

- Each choice is kept in one cookie of the browser, written by a script of the page, not by the server:

  | Cookie | Values | Meaning |
  | ------ | ------ | ------- |
  | `censio-theme` | `light`, `dark` | the theme |
  | `censio-language` | `en`, `de` | the language |

- A cookie lives 365 days (`Max-Age=31536000`), has `Path=/` and `SameSite=Lax`. It is neither `HttpOnly` (a script
  writes it) nor `Secure` (the server speaks plain HTTP, [record 0006](../decisions/0006-server-plain-http-and-open-health.md)); it carries no identity. Only the
  constants above are ever written.
- The server reads both cookies and `Accept-Language` on the request for a page and keeps nothing: no value is stored,
  logged or echoed in the page.
- The values are compared whole and in ordinal letter case, after ASP.NET Core has percent-decoded the cookie
  (`%64ark` reads as `dark`). Any other value (another letter case, another code, markup, control characters, an
  overlong value) counts as no stored choice: the default applies and the page answers `200` as usual.
- A browser that keeps no cookies opens every page with the defaults; a switch still changes the open page.

### Palette

Colours are fixed by the theme; values not listed keep MudBlazor's defaults.

| Palette value | Light | Dark |
| ------------- | ----- | ---- |
| Primary | `#8B1E1E` | `#E8907A` |
| Primary contrast text | `#FFFFFF` | `#1C1310` |
| Secondary | `#7A5A12` | `#D9AE4A` |
| Secondary contrast text | `#FFFFFF` | `#1C1310` |
| App bar background | `#6E1616` | `#2E1515` |
| App bar text | `#FFFFFF` | `#F5EDE3` |
| Background | `#FAF6EF` | `#1A1614` |
| Surface | `#FFFFFF` | `#251F1C` |
| Drawer background | `#F2EADD` | `#211B18` |
| Drawer text | `#2B2420` | `#EDE4D8` |
| Drawer icon | `#5A4F45` | `#C2B5A6` |
| Text primary | `#2B2420` | `#EDE4D8` |
| Text secondary | `#5A4F45` | `#C2B5A6` |
| Action default | `#5A4F45` | `#C2B5A6` |
| Lines and inputs | `#8A7D70` | `#8F8274` |
| Error (a negative delta) | `#A4161A` | `#FF8A80` |
| Success (a positive delta) | `#1B6B2F` | `#7FD08E` |

### Contrast

Both palettes meet WCAG 2.1 level AA, checked by a unit test for every pair:

- Text, 4.5:1: text primary and text secondary on background and surface; app bar text on app bar background; drawer
  text on drawer background; primary on background, surface and drawer background (the marked navigation entry);
  primary contrast text on primary; secondary contrast text on secondary; secondary on background and surface.
- Lines, icons and focus, 3:1: drawer icon on drawer background; action default and lines and inputs on background and
  surface; focus ring: app bar text on app bar background, primary on drawer background, background and surface.
- Error and success on surface, 4.5:1 (the colours of the delta in the goods table).
- The profile switch takes the app bar text colour while it is disabled too (`wwwroot/css/censio.css`), instead of MudBlazor's disabled colour, so its label meets the text level.

### Focus ring

- A control that takes the keyboard focus shows a 2 px outline with a 2 px offset in the primary colour, in the app
  bar in the app bar text colour. It replaces MudBlazor's own focus tint, which reaches only 1.05:1 on the app bar.
  It is defined in `wwwroot/css/censio.css` for everything inside the frame.

### Typography

- Body text and controls use `"Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`; headings `h1` to `h6` use
  `Georgia, "Times New Roman", serif`. Only system fonts are named; no font file is served or fetched.

### Texts

| Element | English | German |
| ------- | ------- | ------ |
| Navigation button (accessible name) | `Open or close the navigation` | `Navigation öffnen oder schließen` |
| Navigation entry of `/` | `Home` | `Startseite` |
| Navigation entry of the region overview | `Regions` | `Regionen` |
| Island without a name | `Unnamed island` | `Unbenannte Insel` |
| Profile without a name | `Unnamed profile` | `Unbenanntes Profil` |
| Good the catalog does not name | `Good <GUID>` | `Ware <GUID>` |
| Profile switch | `Profile` | `Profil` |
| Profile switch, line for the profiles it does not list | `<n> more profiles` | `<n> weitere Profile` |
| Theme switch in the light theme (accessible name) | `Switch to the dark theme` | `Zum dunklen Design wechseln` |
| Theme switch in the dark theme (accessible name) | `Switch to the light theme` | `Zum hellen Design wechseln` |
| Language switch (accessible name) | `Language` | `Sprache` |
| Language options | `English`, `Deutsch` | `English`, `Deutsch` |

The texts come from a table of the application itself (`UiTexts`); the web UI creates no culture and uses no resource
files or request localization, so both languages render in the Docker image, which runs without culture data
([record 0072](../decisions/0072-web-ui-texts-from-an-own-table-without-culture-data.md)). A new page or UI text adds
its English and German text there and to this table; the texts of the data pages are in [`web-pages.md`](web-pages.md).

### Resources

- A page loads its style sheets, scripts and images only from the server itself, by relative paths: MudBlazor's style
  sheet and script, `css/censio.css`, `js/censio.js` and the Blazor script. No web font, no CDN, no external request.
- `js/censio.js` offers three functions: `storePreference` writes a preference cookie, `setDocumentLanguage` sets the
  document language, and `utcOffsetMinutes(isoInstant)` gives the browser's UTC offset in minutes at that instant
  (`-new Date(isoInstant).getTimezoneOffset()`, daylight saving included), which the as-of line of the data pages asks for
  ([`web-pages.md`](web-pages.md)). The first two read nothing; the third reads the browser's clock settings and catches
  nothing.

## Limits and errors

- A stored value the server does not know, a damaged or overlong value and a value with markup or control characters
  are treated as no choice; the value is neither shown on the page nor logged.
- When the browser does not store a choice made on an open page (the connection to the page was lost or the script
  failed), the page keeps the new theme or language and the server writes one `Debug` line,
  `The browser did not store the theme choice.` or `... language choice.`; the line names the kind of the choice,
  never its value or the exception text. Nothing is shown to the user.
- The server serves the files of the packages and of the project as static assets in every environment (it loads the
  static web assets manifest itself), so a run from the build output finds MudBlazor's files.

## Related decisions

- [`decisions/0025-mudblazor-and-blazor-apexcharts-light-theme.md`](../decisions/0025-mudblazor-and-blazor-apexcharts-light-theme.md) — MudBlazor is the component library, with a light and a dark theme.
- [`decisions/0071-web-ui-choices-kept-in-browser-cookies-written-by-a-script.md`](../decisions/0071-web-ui-choices-kept-in-browser-cookies-written-by-a-script.md) — the choices live in two cookies written by a script and read at render time.
- [`decisions/0072-web-ui-texts-from-an-own-table-without-culture-data.md`](../decisions/0072-web-ui-texts-from-an-own-table-without-culture-data.md) — the texts come from an own table, without culture data.

## Not here

- The HTTP surface, the start page's title, heading and version line: [`server.md`](server.md).
- The data pages, their texts, formats, live updates and the as-of line and banner: [`web-pages.md`](web-pages.md).
- The phone layout: a later feature (#53).
