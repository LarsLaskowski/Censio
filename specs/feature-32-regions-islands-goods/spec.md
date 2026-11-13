# Spec: Regions, islands and goods with live updates

Source: Issue #32
Status: Draft

## Problem

The server stores every tick and holds the current state per profile ([`server-state.md`](../../docs/areas/server-state.md)),
but no page shows it: the start page shows only the version, and the profile switch is a disabled placeholder
([`web-ui.md`](../../docs/areas/web-ui.md)). A player who wants to see, next to the game, what each island produces and
consumes has nothing to look at, and cannot tell how old the data is or whether the Companion still delivers.

## Scenarios

- S1: Given a profile with a stored tick, when the user chooses `Regions` (`Regionen`) in the navigation, then the
  region overview shows one entry per region in which the profile has islands, with the region's name, its island count
  and its key figures (*AC2*).
- S2: Given the region overview, when the user opens a region, then its island list shows one row per island with the
  island's name and key figures; opening a row shows the island detail.
- S3: Given the island detail, then a goods table shows per good its icon, name, production, consumption, delta
  (signed and coloured), buildings and productivity.
- S4: Given an open page, when a tick of the shown profile is stored, then the page shows the new values without a
  reload; when the catalog, an icon or the profile's name changes, the page shows it likewise.
- S5: Given several profiles, when the user picks one in the profile switch, then every page shows that profile's data;
  a reload or a copied address shows the same profile. The switch only chooses what the page shows; which profile
  receives ticks is chosen in the Companion (record 0041).
- S6: Given a data page, then it shows the time of the data (`As of <time>` / `Stand: <time>`); when the Companion is
  not connected or reports that it cannot read the game, a banner above the content says so.

## Errors and edge cases

- E1: No profile or no stored tick: the switch stays disabled (`Profile`), and every data page shows
  `No data yet. The pages fill once the Companion sends data.` / `Noch keine Daten. Die Seiten füllen sich, sobald der
  Companion Daten sendet.`
- E2: The latest tick has no values for an island (it lacks the island, or it is the empty tick the game sends after a
  load): the island stays listed with `–` for its figures; its detail shows `No values for this island in the latest
  tick.` / `Keine Werte für diese Insel im letzten Tick.` Nothing is shown as zero and no older tick is used.
- E3: No usable catalog, or a GUID the catalog cannot name: the region is `Region <session GUID>`, the good
  `Good <GUID>` / `Ware <GUID>`; a name in one language only is shown in both; no icon leaves an empty icon cell.
- E4: An empty island name shows `Unnamed island` / `Unbenannte Insel`.
- E5: A profile without a name shows `Unnamed profile` / `Unbenanntes Profil`; profiles whose shown names are equal
  get the first eight characters of their identity appended in parentheses.
- E6: A value the tick holds as no value (`null`) shows `–`.
- E7: An address naming a profile, region or island that is unknown or malformed (any length, any characters) shows
  `Not found.` / `Nicht gefunden.` inside the frame; the address is neither echoed nor logged.
- E8: Reading a profile that is not held: the page shows `Loading…` / `Wird geladen…`; a failed read shows
  `The data cannot be read right now. Reload the page to try again.` / `Die Daten können gerade nicht gelesen werden.
  Laden Sie die Seite neu, um es erneut zu versuchen.`
- E9: More than 100 profiles: the switch lists the first 100 in its order and then the disabled line
  `<n> more profiles` / `<n> weitere Profile`.
- E10: An island with more than 500 goods: the table shows the first 500 in its order and the line
  `Showing 500 of <n> goods.` / `500 von <n> Waren angezeigt.`
- E11: A stored icon that is not a PNG image shows as no icon.
- E12: While the browser's time zone is unknown (before the page is interactive, or its script failed), times are shown
  in UTC with the suffix ` UTC`.
- E13: Ticks that arrive faster than a page renders: the page ends at the newest state; intermediate ones may be skipped.
  A page whose connection to the server is lost shows Blazor's reconnect notice and the current state after reconnecting.

## Non-functional requirements

- N1: Security: the pages need no login (record 0006); anyone who reaches the server reads every profile's names and
  figures, as README and user guide state. Every name (profile, island, catalog) is shown as text, never as markup.
- N2: Bounded reads: the pages read profiles that are not held one at a time across the server, and keep at most one such
  state in memory beyond the two held ones, whatever the number of open pages (server-state's *Reading on demand* asks
  callers reachable without login to bound their reads).
- N3: A stored tick reaches open pages of its profile within 2 seconds on an idle server; a page never blocks the
  ingest or a follower's thread (server-state, *Followers and notices*).
- N4: Every resource, the icons included, comes from the server itself (web-ui *Resources*); icons are served only as
  PNG images.
- N5: Numbers are formatted without culture data (record 0072): production, consumption and delta with two decimals and
  grouped thousands (`1,234.50` / `1.234,50`), delta with its sign (`+1.25`, `-0.50`, `0.00`), productivity as a whole
  percent (`87%` / `87 %`), times as `yyyy-MM-dd HH:mm` in both languages.
- N6: Delta colours: negative and positive deltas have their own palette colour in both themes, each at least 4.5:1
  against the surface, checked by the contrast test; zero takes the text colour. The sign carries the meaning without
  colour.
- N7: Privacy: no log line holds a profile, island or catalog name, a figure or a value from an address. New texts come
  from `UiTexts` in English and German.
- N8: The Companion's reported game state is held in memory only and forgotten when it disconnects; no new setting, and
  the database is read as it is.
- N9: Desktop browsers only; the phone layout is #53.

## Acceptance criteria

- AC1: Replaying the fixture `long-session.jsonl` into the server, converted as the Companion converts it, up to its last
  tick with data shows three regions (one per session GUID), 24 islands in all with the island counts the fixture gives
  per region, the renamed island under its newest name, and in each island's detail exactly the goods of that island's
  latest tick with their values; after the final empty tick the 24 islands stay listed without values (S1, S2, S3, E2).
- AC2: A region is the session GUID of the profile's islands, named by the catalog's region entry of that GUID in the
  shown language; only regions with islands are listed, ordered by shown name (ordinal, case ignored), ties by GUID.
  Its key figures, summed over its islands with values in the latest tick: islands (all listed islands), buildings (sum
  of building counts), produced goods (entries with a building count above 0), goods in deficit (entries with a delta
  below 0) (S1, E3).
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
  profile leaves the shown data unchanged and adds a new profile to the switch; a stored catalog or icon and a hello that
  renames the profile update names, icons and the switch likewise (S4, E13, N3).
- AC7: The profile switch is enabled once a profile exists and lists the profiles by shown name with E5 and E9; without
  a choice the pages show the profile whose latest tick is newest; a picked profile is part of the page's address, so a
  reload and a copied address show it (S5, E1, E5, E9).
- AC8: Every data page shows `As of <time>` / `Stand: <time>`, the receive time of the shown profile's latest tick in the
  browser's time zone (E12). A banner shows `The Companion is not connected.` / `Der Companion ist nicht verbunden.`
  while none is; `The game is not running.` / `Das Spiel läuft nicht.` while the connected Companion's last status is
  `waitingForGame`; `The Companion cannot read the game's statistics.` / `Der Companion kann die Statistik des Spiels
  nicht lesen.` for `pipeBusy`, `protocolMismatch` or `error`; no banner for `connected`, `receiving` or no status since
  the hello (S6, E12, N8).
- AC9: A page without data, with an unknown or malformed address, while reading a not-held profile and after a failed
  read shows the texts of E1, E7 and E8; a failed read is logged without content and retried only on reload (E1, E7, E8,
  N7).
- AC10: Concurrent pages that need not-held profiles cause at most one database read at a time and hold at most one such
  state; names render as text (a name with markup shows its characters); no new log line holds a name or value; every
  new text exists in English and German; the delta colours pass the contrast test (N1, N2, N6, N7).

## Out of scope

- Deficits and surpluses per region (#47), efficiency (#49),
  economics (#50), workforce (#51), search (#48), trends and history (#35 to #38), phone layout (#53, N9), sorting by column.
- Sending data: no Companion sends yet (#40), and #34 sends recordings; until then the pages show E1 outside tests.
- Choosing the profile that receives ticks (the Companion, record 0041); deleting profiles (#35).

## Open questions

- Which key figures? Answered: the Lead chose islands, buildings, produced goods and goods in deficit (AC2), counts that
  leave per-region balances, productivity and money to #47, #49 and #50.
- What is a region? Answered: from the catalog ([`game-catalog.md`](../../docs/areas/game-catalog.md), *Kinds*): a
  region entry is keyed by its session GUID, the first part of an island's key; no region is written into the code, so a
  later region appears by its catalog name.
- Which regions are listed? Answered: those with islands of the shown profile; a catalog entry without islands is not.
- How does the server know the game is closed? Answered: from the contract's `status` message and the Companion's
  connection ([`contract.md`](../../docs/areas/contract.md)); today the ingest discards `status`, so it must be kept (N8).
- How can testers try it? Answered: README and user guide describe the pages and say that they stay empty until a
  Companion sends (#40, #34).
- What does the profile switch do? Answered: the issue comment and record 0041: it chooses only what the page shows.
- Which time zone? Answered: the Lead chose the browser's, with UTC as the marked fallback (E12).
- What if the latest tick is empty? Answered: [`server-state.md`](../../docs/areas/server-state.md): no values, never
  zeros (E2).

## Area documents

- New `docs/areas/web-pages.md`: the region overview, island list and detail, key figures, names and fallbacks, formats,
  limits, live updates, the as-of line and banner, addresses and their failures, the bounded reads, logging and privacy.
- `web-ui.md`: the profile switch's function (no longer a placeholder), the `Regions` navigation entry, the delta colours
  in palette and contrast, the new texts; *Not here* points to `web-pages.md`.
- `ingest.md`: the `status` message is kept as the Companion's game state until it disconnects.
- `server-state.md`: the pages as callers of reads on demand and followers; *Not here* updated.
- `server.md`: any new route (the icons) among the routes reachable without login.
