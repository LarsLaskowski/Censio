# Spec: Show trade routes and item offers of pipe protocol 3

Source: Issue #225
Status: Draft

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
  profile, then a table lists the 57 routes of the profile's latest trade-routes message, each with its name, folder,
  ships, lost ships, major problems, minor problems and its stations as island names in route order, under the as-of
  line of that message's receive time.
- S2: Given routes with major problems, minor problems or lost ships, when the page opens, then a line above the table
  reads `<n> trade routes, <p> with problems or lost ships.` and those routes come first, a count above 0 marked.
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
- E4: Lost ships above ships are shown as received.
- E5: A trade-routes message older than the profile's latest (by receive time, then storing order) is acknowledged and
  replaces nothing.
- E6: A trade-routes message the server's contract reader refuses (malformed, too large, invalid) is acknowledged with
  its refusal and changes nothing; a replay counts it as refused in `Replay finished: <n> ticks sent, <r> refused`.
- E7: A route or folder name that looks like markup is shown as text.
- E8: An unknown or malformed profile in the address shows `Not found.`; a profile that is not held shows `Loading…`
  and the read-failure text of [web-pages](../../docs/areas/web-pages.md) as the other data pages do.
- E9: Deleting a profile's history removes its trade routes too.

## Non-functional requirements

- N1: Every trade-routes frame the pipe reader accepts (its caps and 1 MiB, [pipe](../../docs/areas/pipe.md),
  *Limits and errors*) crosses the contract and is shown whole; the contract bounds a trade-routes message before it
  is parsed, as it bounds a tick.
- N2: Route and folder names are typed by the player and private like island names: no log line, refusal text or
  string form of a message holds them.
- N3: Only the latest trade routes of a profile are kept: no history, no chart. A page reads a profile that is not held
  through the one reader of [record 0074](../../docs/decisions/0074-pages-keep-derived-views-and-read-unheld-profiles-one-at-a-time.md).
- N4: Compatibility: a contract change follows record
  [0058](../../docs/decisions/0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md), so a Companion and
  a server of different contract versions refuse each other with the versions named; a database of an earlier release
  is read and extended, its ticks unchanged.
- N5: The pages need no login, like every data page (record 0006). No new configuration.

## Acceptance criteria

- AC1: A replay of `protocol-3-trade-routes.jsonl` to a server makes `/profiles/{profile}/trade-routes` list the 57
  routes of the file's last trade-routes frame, each with name, folder, ships, lost ships, major and minor problems and
  its stations' island names in route order, under `As of` of that frame's receive time (S1, E3, E4, N1).
- AC2: The routes are ordered by major problems, minor problems and lost ships, each descending, then by shown name
  (ordinal, letter case ignored), folder and ID; the summary line of S2 counts the routes with any of the three above 0,
  and each such count shows in the palette's `Error` colour (major problems, lost ships) or `Warning` colour (minor
  problems) (S2).
- AC3: The navigation lists `Trade routes` / `Handelsrouten` after `Regions`, leading to
  `/profiles/{profile}/trade-routes` of the shown profile and to `/trade-routes` (the newest profile, followed live)
  elsewhere, marked on every address that starts with its link; both routes open without a login (S4, E8, N5).
- AC4: A newer trade-routes message of the shown profile updates the open page without a reload; an older one replaces
  nothing (S3, E5).
- AC5: The page shows `No trade routes received yet.` / `Noch keine Handelsrouten empfangen.` for a profile without
  trade routes, `No trade routes in the latest data.` / `Keine Handelsrouten in den letzten Daten.` for an empty latest
  message, and the texts of E1, E2 and E7 (S6, E1, E2, E7).
- AC6: After a restart the server shows each profile's latest trade routes as before the stop; a deletion of the
  profile's history removes them (S5, E9, N3).
- AC7: The contract reader refuses a trade-routes message over its bounds as *too large* and one that breaks its form or
  value rules as *malformed* or *invalid*, the refusal acknowledged by its `id`, and no refusal text or log line holds a
  route or folder name (E6, N1, N2).
- AC8: A server of this version answers a hello of contract version 1 and a Companion of this version a refused hello
  as record 0058 says, with the versions named; a database written by v0.3.0 opens with its ticks unchanged (N4).
- AC9: On the Product Manager's game, a route the game shows with a problem or a lost ship shows a count above 0 in
  the matching column after a replay of a recording of it (manual, Product Manager) (S2).

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

- [web-pages](../../docs/areas/web-pages.md): the trade-routes routes, the table, its order, marks, summary and texts.
- [web-ui](../../docs/areas/web-ui.md): the navigation entry and its texts.
- [contract](../../docs/areas/contract.md): the trade-routes message, its bounds, value rules and privacy.
- [pipe](../../docs/areas/pipe.md): the tick assembler delivers a trade-routes item (today "produces no item").
- [replay-sending](../../docs/areas/replay-sending.md) and [ingest](../../docs/areas/ingest.md): what is sent and
  acknowledged.
- [server-state](../../docs/areas/server-state.md), [server-data](../../docs/areas/server-data.md) and
  [history](../../docs/areas/history.md): the latest trade routes per profile, stored, restored and deleted.
