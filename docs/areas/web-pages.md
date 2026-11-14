# Web pages

## Scope

The server's data pages: the region overview, the island list of a region, the island detail with its goods table, the
key figures, names and their fallbacks, the number and time formats, the row and profile limits, the live updates, the
as-of line and the banner, the addresses and their failures, the bounded reads of profiles, the icon route and what is
logged. Users of the web application rely on it.

## Behavior

### Pages and routes

- Four routes show data, all inside the frame of [`web-ui.md`](web-ui.md), none needs a login ([record 0006](../decisions/0006-server-plain-http-and-open-health.md)):

  | Route | Page |
  | ----- | ---- |
  | `/regions` | the region overview of the profile whose latest tick is newest; it follows that profile live |
  | `/profiles/{profile}/regions` | the region overview of the named profile |
  | `/profiles/{profile}/sessions/{session}` | the island list of the region that holds the session |
  | `/profiles/{profile}/islands/{session}/{island}/{area}` | the goods of one island |

- The profile in an address is a picked profile: it stays until another is picked, and every link a page offers carries
  the profile the page shows, so a reload and a copied address show the same profile ([record 0077](../decisions/0077-page-address-carries-the-shown-profile.md)).
  A region is addressed by its smallest session GUID; the island list shows the region that holds the session of the
  address, whichever of its sessions that is.
- The browser's tab title of every page is `Censio – <heading>`. The heading is the page's `h1`: `Regions` / `Regionen`
  on the overview, the region's name on the island list, the island's name on the detail, and `Regions` / `Regionen`
  while the page shows no data, a loading text or an error text.
- Every name is shown as text, never as markup.

### Regions

- Islands are grouped by the region GUID of the catalog region entry of their session GUID. A session GUID without such
  an entry is a region of its own. Only regions with islands are listed ([record 0073](../decisions/0073-islands-grouped-by-the-catalog-region-of-their-session.md)).
- The islands of a profile are those of its island list ([`server-state.md`](server-state.md)), plus those of the
  latest tick; each is shown under the name the island list holds, otherwise the name of the latest tick.
- A region is named by the catalog entry of its smallest session GUID, in the shown language, else in the other
  language, else `Region <region GUID>`; a session without a catalog region entry is `Region <session GUID>`. Two regions whose shown names are equal (ignoring letter case) get their GUID
  appended in parentheses.
- The regions are ordered by shown name (ordinal, letter case ignored), ties by GUID. The name is a link to the island
  list.
- The overview has the columns `Region`, `Islands`, `Buildings`, `Produced goods`, `Goods in deficit` (German `Region`,
  `Inseln`, `Gebäude`, `Produzierte Waren`, `Waren im Defizit`).

### Key figures

- Per island: buildings is the sum of the building counts of its entries in the latest tick, produced goods the entries
  with a building count above 0, goods in deficit the entries whose delta, rounded as *Number formats* says, is below 0
  (a delta that is no value is not counted).
- An island that the latest tick lacks, or that has no entries in it, has no figures: every figure shows `–`. Nothing
  is shown as zero and no older tick is used.
- Per region: the island count is the number of its islands; the other three figures are the sums over its islands with
  figures, and `–` when none has.

### Island list

- The heading is the region's name; under it the profile's shown name and a link `All regions` / `Alle Regionen` to the
  overview of the profile.
- The table has the columns `Island`, `Buildings`, `Produced goods`, `Goods in deficit` (German `Insel`, `Gebäude`,
  `Produzierte Waren`, `Waren im Defizit`). The islands are ordered by shown name (ordinal, letter case ignored), ties by
  session GUID, island ID and area index. The name is a link to the island's detail.
- An island with an empty name is shown as `Unnamed island` / `Unbenannte Insel` (`UiTexts`).

### Island detail

- The heading is the island's name; under it `<region> · <profile>`, the region a link back to its island list.
- The table lists one row per entry of the island in the latest tick, with the columns (an unlabelled icon column of
  24 pixels, then) `Good`, `Production`, `Consumption`, `Delta`, `Buildings`, `Productivity` (German `Ware`,
  `Produktion`, `Verbrauch`, `Delta`, `Gebäude`, `Produktivität`).
- At most 500 rows are shown: the first 500 entries of the island in the tick's order, shown ordered by shown good
  name (ordinal, letter case ignored), ties by product GUID. For an island with more entries the line
  `Showing 500 of <n> goods.` / `500 von <n> Waren angezeigt.` follows the table.
- A good is named by the catalog entry in the shown language, else in the other language, else `Good <GUID>` /
  `Ware <GUID>` (`UiTexts`, in both languages).
- A good shows its icon (`<img src="/icons/<reference>" alt="">`) only when the catalog entry names an icon whose PNG the
  server has stored; otherwise the icon cell stays empty.
- An island the latest tick has no values for (it lacks the island, or the tick is empty) keeps its heading and shows
  `No values for this island in the latest tick.` / `Keine Werte für diese Insel im letzten Tick.` instead of the table.

### Number formats

- Production and consumption: two decimals with grouped thousands, `1,234.50` in English and `1.234,50` in German.
- Delta: the same, with a sign: `+1.25`, `-0.50`, `0.00` (`+1,25` in German).
- Rounding rule: a delta is rounded to two decimals, midpoint away from zero, before anything is derived from it: its
  text, its sign, its colour and the goods-in-deficit count follow the rounded value. A delta of -0.004 therefore shows
  `0.00` in the text colour and is no deficit, and a rounded negative zero shows `0.00`.
- A negative delta takes the palette's `Error` colour, a positive one `Success` ([`web-ui.md`](web-ui.md)); zero takes
  the text colour. The sign carries the meaning without the colour.
- Productivity is stored as a percent (up to 435): it is rounded to a whole number, not multiplied, and shown `87%` in
  English and `87 %` in German.
- Counts are whole numbers; the buildings of an island and of a region are summed in 64 bits, so no sum overflows. A value that is no value (`null`) shows `–`.
- No number or time is formatted with a culture ([record 0072](../decisions/0072-web-ui-texts-from-an-own-table-without-culture-data.md)).

### As-of line and banner

- Every data page shows `As of <time>` / `Stand: <time>` for the receive time of the shown profile's latest tick, as
  `yyyy-MM-dd HH:mm` in both languages, at the UTC offset the browser gives for that instant, without a suffix.
  Until the browser's first answer the line is an empty paragraph (one no-break space, so the table below does not
  move): no time, no `As of` / `Stand:`, no ` UTC`. When the browser's answer is unusable (a failed call, an answer
  that is no number, an offset out of range) the time is shown in UTC with the suffix ` UTC`. A new as-of time after
  the first answer is shown at once at the last offset the browser gave, and follows the browser's answer for the new
  instant ([record 0078](../decisions/0078-times-shown-at-the-browsers-utc-offset.md)).
  The server resolves no time zone; the browser is asked once for each new as-of time.
- A browser that never answers leaves the line empty until the circuit's interactive-call timeout
  (`CircuitOptions.JSInteropDefaultCallTimeout`, 1 minute by default; the server sets no option) ends the call, then the
  time shows in UTC. While no interactive connection is ever made (for example WebSockets are blocked) the line stays
  empty. A lost connection is counted as no usable answer, but the user sees Blazor's reconnect display, not the time.
- A banner (a warning) above the as-of line shows the Companion's state ([`server-state.md`](server-state.md)):

  | State | English | German |
  | ----- | ------- | ------ |
  | no Companion connected | `The Companion is not connected.` | `Der Companion ist nicht verbunden.` |
  | connected, last status `waitingForGame` | `The game is not running.` | `Das Spiel läuft nicht.` |
  | connected, last status `pipeBusy`, `protocolMismatch` or `error` | `The Companion cannot read the game's statistics.` | `Der Companion kann die Statistik des Spiels nicht lesen.` |
  | connected, last status `connected` or `receiving`, or none since the hello | no banner | no banner |

- A page without data shows the banner as well; an address that names nothing shows neither.

### Texts of a page that shows no figures

| Case | English | German |
| ---- | ------- | ------ |
| no profile, a profile without a stored tick, or a profile none of whose ticks carried an island | `No data yet. The pages fill once the Companion sends data.` | `Noch keine Daten. Die Seiten füllen sich, sobald der Companion Daten sendet.` |
| an address that names a profile, region or island that is unknown or malformed | `Not found.` | `Nicht gefunden.` |
| a read of a profile that is not held, with nothing shown yet | `Loading…` | `Wird geladen…` |
| a failed read | `The data cannot be read right now. Reload the page to try again.` | `Die Daten können gerade nicht gelesen werden. Laden Sie die Seite neu, um es erneut zu versuchen.` |

A page that updates keeps the figures it shows until the new ones replace them. A failed read replaces them with the last
text and is not retried until the page is loaded again: a notice of the server state does not read it again.

### Live updates

- A page follows the server state ([`server-state.md`](server-state.md), *Followers and notices*) and shows a stored
  tick of its profile, a stored catalog or icon and a hello that renames the profile without a reload. A tick of another
  profile leaves a page unchanged.
- The region overview without a picked profile shows the profile with the newest latest tick and follows it: when a tick
  makes another profile the newest, it shows that one.
- A notice during a read causes one more read afterwards, so a page that falls behind ends at the newest state and skips
  the states between (it never queues one read per tick).
- A change of the Companion's connection or status changes the banner of open pages; it reads nothing.
- A language switch, a theme switch and a banner change re-render from the figures a page holds, without a read.
- A notice reaches a page through its own work item and never blocks the follower that delivers it.

### Profile switch

- The switch of the frame ([`web-ui.md`](web-ui.md)) lists the profiles by shown name (ordinal, letter case ignored,
  ties by identity), each a link to `/profiles/{profile}/regions`. A profile without a name is `Unnamed profile` /
  `Unbenanntes Profil`; profiles whose shown names are equal get the first eight characters of their identity appended in
  parentheses. The switch lists at most 100 profiles, then the disabled line `<n> more profiles` / `<n> weitere
  Profile`. It chooses only what the pages show; the Companion still chooses which profile receives ticks
  (record 0041).

### Icon route

- `GET /icons/{reference}` answers `200` with the stored PNG when `reference` is exactly 64 characters of `0-9a-f`
  (the icon's content hash), the server has an icon of that reference and its bytes start with the PNG signature
  followed by an `IHDR` chunk (the header check: the first 16 bytes; the image is not decoded). The response is
  `Content-Type: image/png` with `X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src 'none'; sandbox`
  and `Cache-Control: public, max-age=31536000, immutable` (the reference is the hash of the content).
- Every other case answers `404` with an empty body: a reference of another form, an unknown reference, and stored bytes
  that fail the header check, which are never served and show as no icon on a page ([record 0076](../decisions/0076-icons-served-by-content-hash-after-a-png-header-check.md)).
- An icon is read from the database on demand, one read at a time across the server; a request that is aborted leaves
  the wait and cancels its read.

### Logging

The pages log nothing of their own. A failed read of a profile is logged by the server state without a name or a value
([`server-state.md`](server-state.md), *Logging*); an address, a name, a figure and the browser's offset are never
logged.

## Limits and errors

### Addresses

- The segments of an address are parsed whole, after ASP.NET Core has percent-decoded them: the profile is exactly the
  36-character form of a GUID (`D`), the session an integer of at most ten digits with an optional leading minus, the
  island and the area a byte of at most ten digits, all digits ASCII, nothing else. Any other segment, of any length
  and with any characters, is not found.
- `Not found.` is shown for an address that matches one of the four routes but names a profile, region or island that
  is unknown or malformed. The address is neither echoed nor logged. A path that matches none of the routes keeps the
  empty `404` of [`server.md`](server.md), because the router has no content for it.
- The icon route reads its reference as *Icon route* says, before any read.

### Reads and memory

- A profile that the server holds is shown from the held state, without a database read. A profile that is not held is
  read from the database by one reader for the whole server, one read at a time; the pages that wait behind it show
  `Loading…` when they show nothing yet ([record 0074](../decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md)).
  The state is read through the same gate, never beside it: a profile that leaves the held profiles between the check
  and the read is read inside the gate.
- A page owns a cancellation token that it passes to every read, so a cancelled page leaves the gate. The token is
  cancelled when the request of the prerender is aborted and when the page is disposed (a navigation, the end of the
  circuit). A closed tab disposes the page at once, because the browser tells the server when it hides the page; a tab whose
  connection is lost without that signal keeps its circuit until the retention of disconnected circuits ends (default
  3 minutes), so its read is cancelled that much later. A cancelled read ends quietly: nothing is
  shown, logged or retried.
- A page keeps only a view: the shown names, the figures per region and island, and the rows of the shown island (at
  most 500 of them). It holds no reference to a profile state once the view is built, held states included. The memory of
  a page is proportional to the island count of its profile, which is unbounded ([`server-state.md`](server-state.md),
  *Memory*), and a worst island of 92,097 entries shows 500 rows.
- The bytes of an icon stay in memory after the icon read is released, until the response is written: up to 17,825,792
  bytes for each request in flight ([`contract.md`](contract.md), *Bounds*). The number of requests in flight is not
  limited, like the uploads of an accepted risk of [`ingest.md`](ingest.md) (*Accepted risks*).

### The browser's offset

- The browser's answer is untrusted. An offset outside -840 to 840 minutes (the 14 hours a time zone may differ from
  UTC) counts as none, as does a failed call, an answer that is no number and a lost connection; the time then stays in
  UTC. None of these is logged or shown.

### Failures

- A failed read of a profile shows the reload text; the server state logs the exception without a name or value. A
  failed icon read answers an empty `500`, as every unhandled error of [`server.md`](server.md).
- The time a notice needs to reach an open page (2 seconds on an idle server) and that a browser shows no broken-image
  glyph for an icon that answers `404` are checked by hand on a build with a replayed recording, not by a test.

## Related decisions

- [`decisions/0073-islands-grouped-by-the-catalog-region-of-their-session.md`](../decisions/0073-islands-grouped-by-the-catalog-region-of-their-session.md) — islands are grouped by the catalog region of their session.
- [`decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md`](../decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md) — pages keep only derived views and read unheld profiles one at a time.
- [`decisions/0075-companion-connection-and-game-state-held-in-the-server-state.md`](../decisions/0075-companion-connection-and-game-state-held-in-the-server-state.md) — the Companion's connection and game state are held in the server state and announced.
- [`decisions/0076-icons-served-by-content-hash-after-a-png-header-check.md`](../decisions/0076-icons-served-by-content-hash-after-a-png-header-check.md) — icons are served by their content hash after a PNG header check.
- [`decisions/0077-page-address-carries-the-shown-profile.md`](../decisions/0077-page-address-carries-the-shown-profile.md) — the page address carries the shown profile.
- [`decisions/0078-times-shown-at-the-browsers-utc-offset.md`](../decisions/0078-times-shown-at-the-browsers-utc-offset.md) — times are shown at the browser's UTC offset for the instant.

## Not here

- The frame, the theme, the language and the profile switch's look: [`web-ui.md`](web-ui.md).
- The state the pages read, its memory and its followers: [`server-state.md`](server-state.md).
- What the Companion sends and how it is stored: [`ingest.md`](ingest.md), [`contract.md`](contract.md).
- Deficits and surpluses per region, efficiency, economics, workforce, search, trends and history, the phone layout and sorting by column: later features (#47 to #51, #35 to #38, #53).
