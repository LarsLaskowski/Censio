# Spec: Show trade routes and item offers of pipe protocol 3

Source: Issue #225
Status: Approved by Lead

## Problem

Since #221 the Companion decodes the trade routes (type 4) and item offers (type 5) the game sends under protocol 3
([pipe](../../docs/areas/pipe.md), *Message types*), but `PipeTickBuilder.AddMessage` has no case for them, so they
produce no item and nothing reaches the server (confirmed: only `src/Censio.Pipe` names them). A player who wants to
know which trade routes have problems or have lost ships must look in the game; the server shows only production.

Checked against the fixture `tests/Censio.Pipe.Tests/Fixtures/protocol-3-trade-routes.jsonl` (decoded in a scratch
script):

- Confirmed: every station's island key (session GUID, island ID, area index) is one of the 25 island keys of the
  file's island frames, and every good of a station is a product GUID the island frames carry, so stations and goods
  can be named from the island list and the catalog. Ship counts are 1 or 2, the owner is 41 everywhere, a trade-routes
  frame arrives about every two minutes.
- Refuted, the issue's "confirms what the fields mean": major problems, minor problems and lost ships are 0 in all 627
  routes of the 11 frames (the anonymiser keeps a 0), so the file cannot show what these counts mean; record
  [0095](../../docs/decisions/0095-trade-routes-and-item-offers-decoded-strictly-and-anonymised-with-invented-names.md)
  asks a feature that shows them to check them against the game first.
- Item offers name 7 traders and 99 item GUIDs, none of them a good: the catalog holds regions, goods, workforce and
  buildings only ([game-catalog](../../docs/areas/game-catalog.md), *Kinds*), so neither has a name the server can show.
- The Companion sends to a server only in a replay (`--server`); live sending is #40.

## Scenarios

Scope as the Product Manager chose (*Open questions*, Q1): trade routes now, item offers later.

- S1: Given a replay of the fixture sent to the server, when the user opens `Trade routes` / `Handelsrouten` of the
  profile, then a page headed `Trade routes` / `Handelsrouten` (tab title `Censio – Trade routes`) lists in a table the
  57 routes of the profile's latest trade-routes message, each with its name, folder, ships, lost ships, major
  problems, minor problems and its stations as island names in route order. Its as-of line gives that message's
  receive time, not the latest tick's; the Companion banner shows as on every data page; the page has no
  `Delete history` button.
- S2: Given routes with major problems, minor problems or lost ships, when the page opens, then a line above the table
  reads `<n> trade routes, <p> with problems or lost ships.` / `<n> Handelsrouten, <p> mit Problemen oder verlorenen
  Schiffen.` (`1 trade route, …` / `1 Handelsroute, …` for one) and those routes come first, each count above 0 in the error colour.
- S3: Given the page is open, when a newer trade-routes message of the shown profile arrives, then the table shows it
  without a reload.
- S4: Given any page, when the user picks the navigation entry, then it leads to the trade routes of the shown profile,
  or of the newest profile where no profile is shown, as the `Regions` entry does.
- S5: Given the server restarts, when the user opens the page, then it shows the routes it showed before the stop.
- S6: Given a profile that has never received trade routes (a protocol 2 game), or whose latest message holds none,
  when the page opens, then it says so instead of an empty table.

## Errors and edge cases

- E1: An empty route name shows `Unnamed route` / `Unbenannte Route`; an empty folder shows `–`.
- E2: A station's island that the profile's island list lacks shows `Island <session GUID>/<island ID>/<area index>`
  / `Insel …`; an island with an empty name `Unnamed island` / `Unbenannte Insel`.
- E3: Two routes with one name or one ID (the fixture repeats a name) are two rows; nothing is merged.
- E4: Every count is shown as received, also lost ships above ships or a negative value; a route without stations
  shows `–` in the stations cell.
- E5: A trade-routes message older than the profile's latest (by receive time, then storing order) is acknowledged and
  replaces nothing.
- E6: A trade-routes message the server's contract reader refuses (malformed, too large, invalid) is acknowledged with
  its refusal and changes nothing.
- E7: A route or folder name that looks like markup is shown as text.
- E8: An unknown or malformed profile in the address shows `Not found.`; a profile that is not held shows `Loading…`
  and the read-failure text of [web-pages](../../docs/areas/web-pages.md) as the other data pages do.
- E9: Deleting a profile's history removes its trade routes too; the dialog text reads `… its regions, islands, goods,
  trade routes and history. …` / `… seine Regionen, Inseln, Waren, Handelsrouten und sein Verlauf. …`. A trade-routes
  message of the deleted profile that arrives during the removal creates the profile again, as a tick does.
- E10: A replay counts a sent trade-routes message in no `<n>` of `Replay finished: <n> ticks sent, <r> refused` and of
  the log line `Replay sent <n> ticks, <r> refused`, and in `<r>` when it is refused.

## Non-functional requirements

- N1: Every trade-routes frame the pipe reader accepts (its caps and 1 MiB, [pipe](../../docs/areas/pipe.md),
  *Limits and errors*) crosses the contract and is shown whole. The contract bounds a trade-routes message before it
  is parsed, at no less than the pipe allows: 1,024 routes, 256 stations a route, 52,428 stations a message (1 MiB at
  20 bytes a station), names of 765 UTF-8 bytes (255 pipe bytes, each possibly U+FFFD). Its value rules allow no
  routes, repeated route IDs and names, any int32 count and a route without stations.
- N2: Route and folder names are typed by the player and private like island names: no log line, refusal text or
  string form of a message holds them, and after a deletion's clean-up none that only the deleted profile held occurs
  in the database file or its `-wal` and `-shm` files ([history](../../docs/areas/history.md), *Deleting a profile*).
- N3: Only the latest trade routes of a profile are kept: no history, no chart. A page reads a profile that is not held
  through the one reader of [record 0074](../../docs/decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md).
- N4: Compatibility: the contract becomes version 2 (record
  [0058](../../docs/decisions/0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md): every change of
  what a message may hold raises the version), read exactly; a Companion and a server of different versions refuse
  each other with the versions named, and the texts naming version 1 (`supported: 1`, `breaks a rule of contract
  version 1` in [contract](../../docs/areas/contract.md), `server does not support contract version 1` in
  [replay-sending](../../docs/areas/replay-sending.md)) name version 2. A database of an earlier release is read and
  extended, its ticks unchanged.
- N5: The pages need no login, like every data page (record 0006). No new configuration.

## Acceptance criteria

- AC1: A replay of `protocol-3-trade-routes.jsonl` to a server makes `/profiles/{profile}/trade-routes`, headed and
  titled as S1 says, list the 57 routes of the file's last trade-routes frame (the repeated name as two rows), each
  with name, folder, ships, lost ships, major and minor problems and its stations' island names in route order, under
  `As of` of that frame's receive time, with the Companion banner and without `Delete history` (S1, E3, N1).
- AC2: On messages with counts above 0, the routes are ordered by major problems, minor problems and lost ships, each
  descending, then by shown name (ordinal, letter case ignored), folder and ID; the summary line of S2 counts the
  routes with any of the three above 0, and each such count shows in the palette's `Error` colour, its number carrying
  the meaning without the colour (S2).
- AC3: The navigation lists `Trade routes` / `Handelsrouten` after `Regions`, leading to
  `/profiles/{profile}/trade-routes` of the shown profile and to `/trade-routes` (the newest profile, followed live)
  elsewhere, marked on every address that starts with its link; both routes open without a login (S4, E8, N5).
- AC4: A newer trade-routes message of the shown profile updates the open page without a reload; an older one replaces
  nothing (S3, E5).
- AC5: The page shows `No trade routes received yet.` / `Noch keine Handelsrouten empfangen.` for a profile without
  trade routes, `No trade routes in the latest data.` / `Keine Handelsrouten in den letzten Daten.` for an empty latest
  message, and the texts of E1, E2 and E7; `/trade-routes` while no profile has a tick shows the `No data yet. …` text of
  [web-pages](../../docs/areas/web-pages.md) (S6, E1, E2, E7).
- AC6: After a restart the server shows each profile's latest trade routes as before the stop; a deletion removes
  them, shows the dialog text of E9 and leaves no route or folder name of the profile in the database files after the
  clean-up (real schema); a trade-routes message during the removal recreates the profile (S5, E9, N2, N3).
- AC7: The contract reader accepts a message at every bound of N1 and with no routes, and refuses one over a bound as
  *too large* and one that breaks its form or value rules as *malformed* or *invalid*, the refusal acknowledged by its
  `id`; no refusal text or log line holds a route or folder name; a replay counts as E10 says (E6, E10, N1, N2).
- AC8: Companion and server write and read contract version 2 only; a hello or a refused hello reply of version 1
  is refused with both versions named, and the texts of N4 name version 2; a database written by v0.3.0 opens with its
  ticks unchanged (N4).
- AC9: On the Product Manager's game, a route the game shows with a problem or a lost ship shows a count above 0 in
  the matching column after a replay of a recording of it (manual, Product Manager) (S2).
- AC10: A message with a repeated route ID, lost ships above ships, a negative count and a route without stations is
  shown row for row as received (E3, E4).

## Out of scope

- Item offers: a follow-up issue, which first extends the catalog to items and traders (Q1).
- A route's goods, the station flags and the owner; a route detail page; history or charts of trade routes.
- Live sending (#40): until it lands, trade routes reach the server by a replay, as ticks do.
- The Companion's diagnostics window.

## Open questions

- Q1: Which part of the issue does this feature build, and how are the meanings of the problem counts confirmed?
  Answered: by the Product Manager, option A: trade routes only, the counts shown under the reference's names and
  confirmed by AC9 on the Product Manager's game; item offers go to a follow-up issue that first extends the catalog
  to items and traders.
- Q2: Is the server's state of trade routes kept across a restart? Answered: yes (Lead), as the rest of a profile's
  state ([server-state](../../docs/areas/server-state.md), *Restore at start*), latest only.

## Area documents

- [web-pages](../../docs/areas/web-pages.md): the trade-routes routes, heading, the table, its order, marks, summary,
  texts, and the page's as-of time as an exception to the latest-tick rule.
- [web-ui](../../docs/areas/web-ui.md): the navigation entry, its texts and the changed deletion dialog text.
- [contract](../../docs/areas/contract.md): version 2, the trade-routes message, its bounds, value rules and privacy.
- [pipe](../../docs/areas/pipe.md): the tick assembler delivers a trade-routes item (today "produces no item").
- [replay-sending](../../docs/areas/replay-sending.md) and [ingest](../../docs/areas/ingest.md): what is sent, counted
  and acknowledged, the version texts.
- [server-state](../../docs/areas/server-state.md), [server-data](../../docs/areas/server-data.md) and
  [history](../../docs/areas/history.md): the latest trade routes per profile, stored, restored, deleted and cleaned up.

## Challenge

- 1 (major, `Warning` colour not in the palette): accepted. The palette has no tested warning value; every count above
  0 now takes the `Error` colour, already checked at 4.5:1 on surface in both palettes, and the number itself carries
  the meaning (S2, AC2). No new palette value.
- 2 (minor, contract version): accepted. N4 and AC8 name version 2, read exactly, and list the version-1 texts that
  change; reading both versions was not chosen, since only replays send and record 0058 reads one version exactly.
- 3 (minor, value rules and station bound): accepted. N1 states the value rules (no routes, repeats, any int32, a
  route without stations), the bounds including 52,428 stations a message and 765-byte names; E4 gives the empty
  stations cell; AC7 tests every bound.
- 4 (minor, fixture cannot show E4 or AC2): accepted. AC1 covers only what the fixture holds; AC2 is tested on
  messages with counts above 0, and the new AC10 covers repeated IDs, lost ships above ships, negative counts and a
  route without stations on built messages.
- 5 (minor, replay counts): accepted. E10, tested in AC7.
- 6 (minor, deletion guarantees): accepted. E9, N2 and AC6 carry the clean-up of names from the database files, the
  recreation during removal and the dialog text in both languages.
- 7 (minor, page frame): accepted. S1 and AC1 give heading, tab title, the as-of time of the trade-routes message (an
  exception recorded in web-pages), the banner and no `Delete history` button.
