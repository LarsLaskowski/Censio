# Spec: Regions, islands and goods with live updates

Source: Issue #32
Status: Approved by Lead

## Problem

The server holds the current state per profile ([`server-state.md`](../../docs/areas/server-state.md)), but no page
shows it and the profile switch is a placeholder ([`web-ui.md`](../../docs/areas/web-ui.md)). A player cannot see what
each island produces and consumes, how old the data is, or whether the Companion still delivers.

## Scenarios

- S1: Given a profile with a stored tick, when the user chooses `Regions` (`Regionen`) in the navigation, then the
  region overview shows the profile's regions with name, island count and key figures (AC2).
- S2: When the user opens a region, its island list shows each island's name and key figures; a row opens the island
  detail.
- S3: The island detail shows a goods table (AC4).
- S4: When a tick of the shown profile, the catalog, an icon or the profile's name is stored, open pages show it
  without a reload.
- S5: When the user picks a profile in the switch, the pages show its data; the Companion still chooses which profile
  receives ticks (record 0041).
- S6: A data page shows the time of its data; a banner says when the Companion is not connected or cannot read the
  game (AC8).

## Errors and edge cases

- E1: Without a profile the switch stays disabled (`Profile`); without a profile, or for a profile without a stored
  tick (a hello alone creates one), every data page shows `No data yet. The pages fill once the Companion sends data.` / `Noch keine Daten. Die Seiten füllen sich, sobald der
  Companion Daten sendet.`
- E2: The latest tick has no values for an island (it lacks the island, or it is the empty tick the game sends after a
  load): the island stays listed with `–` for its figures; its detail shows `No values for this island in the latest
  tick.` / `Keine Werte für diese Insel im letzten Tick.` Nothing is shown as zero and no older tick is used.
- E3: No usable catalog, or a GUID the catalog cannot name: a session GUID without a catalog region entry is a region of
  its own, `Region <session GUID>`; the good is `Good <GUID>` / `Ware <GUID>`; a name in one language only is shown in
  both; no icon leaves an empty icon cell. Two regions with equal shown names get their GUID appended in parentheses.
- E4: An empty island name shows `Unnamed island` / `Unbenannte Insel`.
- E5: A profile without a name shows `Unnamed profile` / `Unbenanntes Profil`; profiles whose shown names are equal
  get the first eight characters of their identity appended in parentheses.
- E6: A value the tick holds as no value (`null`) shows `–`.
- E7: An address naming a profile, region or island that is unknown or malformed (any length, any characters) shows
  `Not found.` / `Nicht gefunden.` inside the frame; the address is neither echoed nor logged.
- E8: Reading a profile that is not held, also while it waits for another page's read: a page with nothing shown yet
  shows `Loading…` / `Wird geladen…`, a page that is updating keeps its figures until the new ones replace them; a
  failed read shows
  `The data cannot be read right now. Reload the page to try again.` / `Die Daten können gerade nicht gelesen werden.
  Laden Sie die Seite neu, um es erneut zu versuchen.`
- E9: More than 100 profiles: the switch lists the first 100 in its order and then the disabled line
  `<n> more profiles` / `<n> weitere Profile`.
- E10: An island with more than 500 goods: the table shows the first 500 entries of the tick, ordered by name, and the line
  `Showing 500 of <n> goods.` / `500 von <n> Waren angezeigt.`
- E11: A stored icon that is not a PNG image (it does not start with the PNG signature followed by an `IHDR` chunk; it is
  not decoded further) shows as no icon and is not served.
- E12: While the browser has not given its UTC offset (before the page is interactive, or its script failed), times are
  shown in UTC with the suffix ` UTC`.
- E13: Ticks faster than a page renders: the page ends at the newest state, skipping some between.

## Non-functional requirements

- N1: No login (record 0006): anyone who reaches the server reads every profile, as README and user guide state. Every
  name is shown as text, never as markup.
- N2: Bounded reads and memory: the pages read profiles that are not held one at a time across the server
  (server-state, *Reading on demand*): at most one database read is in flight. A page keeps only what it shows, derived from the state: the
  figures per region and island and the rows of the shown island (at most 500); it holds no reference to a profile state
  once they are derived, held snapshots included. A language switch, a theme switch and a banner change re-render from
  these figures without a read.
- N3: A stored tick or a banner change reaches open pages within 2 seconds on an idle server; a page never blocks a
  follower's thread (server-state, *Followers and notices*).
- N4: Every resource, the icons included, comes from the server itself (web-ui *Resources*); icons are served only as
  PNG images.
- N5: Numbers are formatted without culture data (record 0072): production, consumption and delta with two decimals and
  grouped thousands (`1,234.50` / `1.234,50`), delta with its sign (`+1.25`, `-0.50`, `0.00`), productivity as a whole
  percent (`87%` / `87 %`), times as `yyyy-MM-dd HH:mm` in both languages. The server resolves no time zone: the browser
  gives its UTC offset for the shown instant, so a daylight-saving change is right.
- N6: Delta colours: negative and positive deltas have their own palette colour in both themes, each at least 4.5:1
  against the surface, checked by the contrast test; zero takes the text colour. The sign carries the meaning without
  colour.
- N7: No log line holds a name, a figure or a value from an address. New texts come from `UiTexts` in both languages.
- N8: The Companion's connection and its last reported game state (`status`) are held in memory only, forgotten when it
  disconnects, and announced to the pages when they change; no new setting, and the database is read as it is.
- N9: Desktop browsers only (#53).

## Acceptance criteria

- AC1: Replaying the fixture `long-session.jsonl` into the server without a catalog, converted as the Companion converts
  it, up to its last tick with data shows three regions, the session GUIDs 3245, 6627 and 149679 with 13, 9 and 2
  islands, each island under the name of the latest tick that carries it (the renamed island (6627, 10, 1) included),
  and in each island's detail exactly the goods of that island's latest tick with their values; after the final empty
  tick the 24 islands stay listed without values. With a catalog whose region entries give two of the session GUIDs one
  region GUID, the two form one region with the sum of their islands (S1, S2, S3, E2, E3).
- AC2: The profile's islands are grouped by the region GUID of the catalog's region entry of their session GUID; a
  session GUID without such an entry is a region of its own (E3). A region is named by the region entry of its smallest
  session GUID, in the shown language; only regions with islands are listed, ordered by shown name (ordinal, case
  ignored), ties by GUID, equal names told apart as in E3. Its key figures: islands (all listed islands); buildings (sum
  of building counts), produced goods (entries with a building count above 0) and goods in deficit (entries with a delta
  below 0), each summed over its islands with values in the latest tick, and `–` when none of its islands has values
  (S1, E2, E3).
- AC3: The island list of a region shows per island its name and the three figures of AC2 for that island, `–` for
  an island without values, ordered by name as in AC2, ties by island key; each row opens the island detail (S2, E2, E4).
- AC4: The island detail lists one row per entry of the island in the latest tick, ordered by shown good name as in AC2:
  icon, name, production (generation), consumption, delta, buildings (building count), productivity (average
  productivity), formatted as N5, `–` for `null`, delta coloured as N6, at most 500 rows with the E10 line (S3, E6,
  E10, E11, N5, N6).
- AC5: Names: a good or region takes the catalog name of the shown language, else the other language's, else the E3
  fallback; an island its latest name or E4; an icon is shown when the catalog entry names one whose PNG is stored, and
  a language switch changes the names at once; icons come from the server as PNG images (E3, E4, E11, N4).
- AC6: On an open page, a stored tick of the shown profile replaces the shown values without a reload; a tick of another
  profile leaves the shown data unchanged and adds a new profile to the switch, except on a region overview without a
  picked profile (AC7); a stored catalog or icon and a hello that renames the profile update names, icons and the switch
  likewise (S4, E13, N3).
- AC7: The profile switch is enabled once a profile exists and lists the profiles by shown name with E5 and E9. A
  region overview without a picked profile shows the profile whose latest tick is newest and follows it live: when a
  tick makes another profile the newest, it shows that one. A picked profile is part of the page's address and stays
  until another is picked, and every region and island page carries the profile it shows in its address, so a reload
  and a copied address show it (S5, E1, E5, E9).
- AC8: Every data page shows `As of <time>` / `Stand: <time>`, the receive time of the shown profile's latest tick at the
  UTC offset the browser gives for that instant (tested with an injected offset; E12). A banner shows `The Companion is not connected.` / `Der Companion ist nicht verbunden.`
  while none is; `The game is not running.` / `Das Spiel läuft nicht.` while the connected Companion's last status is
  `waitingForGame`; `The Companion cannot read the game's statistics.` / `Der Companion kann die Statistik des Spiels
  nicht lesen.` for `pipeBusy`, `protocolMismatch` or `error`; no banner for `connected`, `receiving` or no status since
  the hello. A Companion's connect, its disconnect and a `status` with another pipe state change the banner of open
  pages without a reload (S6, E12, N3, N8).
- AC9: A page without data, with an unknown or malformed address, while reading a not-held profile and after a failed
  read shows the texts of E1, E7 and E8; a failed read is logged without content and retried only on reload (E1, E7, E8,
  N7).
- AC10: Pages that need not-held profiles at the same time cause at most one database read at a time, the others
  waiting as in E8; once a page shows its figures, no profile state it read or was given stays reachable from it (a weak
  reference to the state is collected); names render as text (a name with markup shows its characters); no new log line holds a name or value; every
  new text exists in English and German; the delta colours pass the contrast test (N1, N2, N6, N7).

## Out of scope

- Deficits and surpluses per region (#47), efficiency (#49),
  economics (#50), workforce (#51), search (#48), trends and history (#35 to #38), phone layout (#53, N9), sorting by column.
- Sending data (#40, #34): until then the pages show E1 outside tests, as README and user guide say.
- Choosing the profile that receives ticks (the Companion, record 0041); deleting profiles (#35).

## Open questions

- Which key figures? Answered: the Lead chose islands, buildings, produced goods and goods in deficit (AC2), counts that
  leave per-region balances, productivity and money to #47, #49 and #50.
- What is a region? Answered: a catalog region entry is a session (the session GUID of an island's key) carrying a
  region GUID several sessions may share ([`game-catalog.md`](../../docs/areas/game-catalog.md), *Kinds*); islands
  group by it (AC2), and no region is written into the code.
- How does the server know the game is closed? Answered: from the Companion's connection and its `status` message,
  which the ingest discards today ([`ingest.md`](../../docs/areas/ingest.md)), so it is kept (N8).
- Which time zone? Answered: the Lead chose the browser's offset for the shown instant, with UTC as the marked
  fallback (E12, N5).
- What if the latest tick is empty? Answered: [`server-state.md`](../../docs/areas/server-state.md): no values, never
  zeros (E2).

## Area documents

- New `docs/areas/web-pages.md`: the three pages, figures, names, formats, limits, live updates, as-of line, banner,
  addresses, bounded reads, logging.
- `web-ui.md`: the profile switch's function (no longer a placeholder), the `Regions` navigation entry, the delta colours
  in palette and contrast, the new texts, and the new function of `js/censio.js` that gives the browser's UTC offset for
  an instant (*Resources* says today that it reads nothing); *Not here* points to `web-pages.md`.
- `ingest.md`: the `status` message is kept as the Companion's game state until it disconnects; connect, disconnect and
  a changed pipe state are announced to the followers.
- `server-state.md`: the pages as callers of reads on demand and followers; the notice of a change of the Companion's
  connection or game state among the notices; *Not here* updated.
- `contract.md`: *Security considerations for the endpoint* names #32 as the feature that checks a PNG before serving
  it (E11), instead of #42.
- `server.md`: any new route (the icons) among the routes reachable without login.

## Challenge

1. Region keyed by session GUID while catalog region entries carry a shared region GUID (major). Accepted: islands now
   group by the region GUID of their session's catalog region entry, a session without one is a region of its own
   (`Region <session GUID>`), the name is that of the region entry of the region's smallest session GUID, and equal
   shown names get the GUID appended (AC2, E3). AC1 now states the result without a catalog (three regions) and adds
   the merge of two session GUIDs under one region GUID.
2. The memory bound of N2/AC10 left open what a page keeps and shows while waiting (major). Accepted: a page keeps only
   the derived figures (per region and island, plus at most 500 rows of the shown island) and holds no reference to a
   profile state, held snapshots included, once they are derived; language, theme and banner changes re-render from
   them; the bound is one database read in flight across the server (N2). E8 says what a waiting page shows (`Loading…`
   with nothing shown yet, otherwise its previous figures). AC10 is testable: a store whose read blocks sees one read at
   a time, and a weak reference to the state is collected once the page has rendered.
3. E1 and AC7 disagreed on a profile without a tick. Accepted: the switch is enabled once a profile exists; a profile
   without a tick shows the E1 text (E1).
4. Region sums after the final empty tick would show 0. Accepted: the three summed figures show `–` when none of the
   region's islands has values; the island count stays (AC2).
5. The banner had no notice path for live updates. Accepted: AC8 now requires connect, disconnect and a new pipe state
   to change the banner of open pages within N3's 2 seconds; N8 and the `ingest.md` and `server-state.md` updates name
   the new notice to the followers.
6. Whether a page without a picked profile follows a newer profile. Accepted: a region overview without a picked
   profile follows the newest profile live; a picked profile stays, and region and island pages always carry their
   profile in the address (AC6, AC7).
7. Time zone without tzdata or culture data on the server. Accepted: the server resolves no zone; the browser gives its
   UTC offset for the shown instant (N5, AC8, tested with an injected offset), and the new `js/censio.js` function is in
   the `web-ui.md` update.
8. AC1 can be exact. Accepted: AC1 names the session GUIDs 3245, 6627 and 149679 with 13, 9 and 2 islands and the
   renamed island (6627, 10, 1) by the name of the latest tick carrying it, not by a literal. For the plan: the test
   needs a project reference from `Censio.Server.Tests` to `Censio.Companion.Core` (`ContractMessageConverter`) and a
   linked copy of the fixture, as `Censio.GameData.Tests.csproj` links its fixture.
9. The PNG check belonged to #42 by `contract.md`. Accepted: E11 defines a PNG as the PNG signature followed by an
   `IHDR` chunk, without a full decode, and a failing icon is neither shown nor served; `contract.md` is in *Area
   documents* so its pointer names #32.
