# Spec: Catalog upload on connect and after re-extraction

Source: Issue #42
Status: Draft

## Problem

A player who runs the server in Docker (or the server the Companion hosts in local mode) sees bare numbers on its pages:
`Region <GUID>` and `Good <GUID>`, and no good icons ([`web-pages.md`](../../docs/areas/web-pages.md), names and
fallbacks). The server cannot read game files ([record 0023](../../docs/decisions/0023-game-names-and-icons-extracted-locally.md));
only the Companion builds the catalog of names and icons from the player's installation
([`game-catalog.md`](../../docs/areas/game-catalog.md), [`game-catalog-cache.md`](../../docs/areas/game-catalog-cache.md)).
Everything else already exists: the catalog converts into the catalog document
([`game-catalog-format.md`](../../docs/areas/game-catalog-format.md)) and the contract's `catalogUpload` and `iconImage`
messages, which the server stores and shows ([`ingest.md`](../../docs/areas/ingest.md), *Messages after the hello*;
[`server-state.md`](../../docs/areas/server-state.md), *Catalog*). But nothing sends them: live sending says "No catalog,
icon or figure name is sent" ([`live-sending.md`](../../docs/areas/live-sending.md), *What is sent*). So no one
testing with Docker can see the pages as intended.

Checked against the code: `IngestClient` reads `helloAccepted` but passes nothing from it to its caller, so the
catalog identity the server names there is not seen yet. Its reply bound is 65,536 bytes, but an acknowledgement of a
catalog upload may list up to 16,384 `missingIcons`, about 1.1 MB ([`ingest.md`](../../docs/areas/ingest.md), *Memory
of a catalog upload*). The real catalog measured for #23 has 1,040 entries, and only goods and regions have icons, so a
fresh server's list is probably far shorter, but no measurement says by how much.

## Scenarios

"Live sending" covers forward mode and local mode ([`live-sending.md`](../../docs/areas/live-sending.md)). A catalog is
*ready* when the start's cache load or extraction, or a later re-extraction, has ended with names (menu
`Catalog: <n> entries`, `…, not cached` or `…, icons failed`). The Companion's *catalog identity* is the SHA-256 of its
catalog document's bytes, which the server names the same way ([`contract.md`](../../docs/areas/contract.md),
*Identities and acknowledgements*).

- S1: Given a fresh Docker server without a catalog and a Companion in forward mode whose catalog is ready, when its
  hello is accepted (`helloAccepted` names `catalogIdentity` `null`), then before any waiting live message it sends
  one `catalogUpload` with its document. For each reference in that upload's acknowledgement's `missingIcons` it then
  sends one `iconImage`, in the listed order, one message in flight, and then sends the waiting live messages. The
  open pages show the names of regions and goods and the goods' icons without a reload (`web-pages.md`, *Live updates*).
  The log says `Catalog sent to the server: <i> icons sent, <r> refused`.
- S2: Given a server whose `helloAccepted` names a `catalogIdentity` equal to the Companion's catalog identity (a
  restart, a reconnect, an update of the Companion that extracts the same catalog again), then no catalog and no icon is
  sent and the log says `Catalog already on the server`.
- S3: Given an accepted connection whose catalog is not ready yet (the first start still extracts), then live messages
  are sent as today, and once the catalog is ready the upload of S1 or the skip of S2 follows the message in flight.
- S4: Given a game update found when the game connects (re-extraction, `game-catalog-cache.md`, *Later checks*), when
  the new catalog is ready while the connection is open, then its identity is compared with the one the server holds
  (named at the hello or uploaded on this connection). A different one is uploaded after the message in flight, as in
  S1, and replaces the server's catalog. An equal one sends nothing.
- S5: Given a connection that ended while the icons were sent (lost connection, `Exit`, a restart of the server), at the
  next accepted hello the server names `null` (its catalog lacks icons, record 0070). The Companion then uploads the
  catalog again, which stores nothing new on the server, and sends only the icons the new acknowledgement lists.
- S6: Given local mode, the same holds for the hosted server on `127.0.0.1`. Its pages show names and icons, and its
  data folder keeps the catalog, so the next start skips (S2).
- S7: Given a catalog whose icons failed (`Catalog: <n> entries, icons failed`), the document carries no icon
  references (`game-catalog-format.md`, *Conversion*). It is uploaded and no icon follows, so the pages show names
  without icons. A later start that reads the icons makes a different identity and uploads again (S4 rule).

## Errors and edge cases

- E1: No game or `Catalog: failed`: no catalog is ready, nothing is sent and no line is logged. The server keeps any
  catalog stored earlier, otherwise its pages show the GUID fallbacks.
- E2: The acknowledgement of the catalog carries a refusal (`invalid`, `catalogMalformed`,
  `catalogUnsupportedVersion`, `catalogInvalid`, `catalogTooLarge`): no icon is sent, `Server refused the catalog:
  <kind>` is logged, and this catalog is not uploaded again in this run, also not after a reconnect. A newer catalog
  (S4) or a restart may upload. Live sending goes on.
- E3: An icon's acknowledgement carries a refusal: the next icon follows. The refusal is only counted in the line of S1
  (`<r>`), so no line is logged per icon.
- E4: The contract writer refuses the catalog message (above its bounds): nothing is sent, `Catalog not sent: the
  contract writer refused it` is logged, and this catalog is not tried again in this run. An icon the writer refuses is
  counted as refused (E3).
- E5: The connection fails during the upload or its icons: the reconnect of
  [`live-sending.md`](../../docs/areas/live-sending.md) (*Reconnect*) follows with its menu texts, then S2 or S5. Catalog
  and icon messages are never written to the buffer folder, do not count in `Buffer: <n> waiting` and are not
  limited by `bufferHours` or `bufferMegabytes`.
- E6: A newer catalog becomes ready while an older one's icons are still being sent: the older one's remaining icons
  are not sent, and the newer one follows the message in flight. No `Catalog sent` line is written for the older one.
- E7: An acknowledgement of a catalog upload with up to 16,384 `missingIcons` is read and acted on. It is not
  `Server: invalid reply, retrying`.
- E8: A catalog or icon not acknowledged within 30 seconds (a slow link, a large catalog) is `Server: not answering,
  retrying`, as for any message. The upload is retried after the reconnect, so on a link too slow for the catalog this
  repeats (accepted risk, *Non-functional requirements* N6).
- E9: The server's catalog came from another installation (another PC, another game build): the identities differ, so
  the upload replaces it ([`ingest.md`](../../docs/areas/ingest.md): a later upload replaces the earlier one).
- E10: `Exit` during an upload keeps its bounds (2 seconds, 5 in local mode; `live-sending.md`, *Exit*). The rest is S5
  at the next start.

## Non-functional requirements

- N1: Order: one message in flight on the live connection as today. After `helloAccepted` the catalog and its icons
  come before the waiting live messages. A catalog that becomes ready later follows the message in flight. Ticks taken
  meanwhile wait in the buffer within its limits.
- N2: Memory: besides the catalog it holds, the Companion holds at most the written catalog message (at most the
  contract's 24 MiB), one written icon message and the acknowledgement's list of missing references at a time.
- N3: Replies: an acknowledgement of a catalog upload is read up to the size the contract allows (16,384 references,
  about 1.1 MB). Every other reply keeps the bound of 65,536 bytes
  ([`replay-sending.md`](../../docs/areas/replay-sending.md), *Limits and errors*).
- N4: Privacy: the catalog and the icons go only to the server live sending connects to, in forward mode over `ws://`
  unencrypted (record 0023: only to the player's own server). No log line holds a name, an icon reference, a path or
  any content. The figure name is still never sent (record 0041), and the cache file is still never sent.
- N5: Compatibility: contract version 2 and the server are unchanged. No new setting, menu line or tooltip text. A
  server of release 0.3.0 refuses the hello as before.
- N6: Accepted risk: a link that cannot carry the catalog message within 30 seconds retries the upload at every
  reconnect (E8).
- N7: Log lines, Information unless named, each written once per occasion: `Catalog sent to the server: <i> icons sent,
  <r> refused` (after the last icon's acknowledgement, also with `0 icons`), `Catalog already on the server` (at most
  once per accepted hello and per ready catalog), `Server refused the catalog: <kind>` (Warning, the kind by its name as
  in `Server refused a live message: <kind>`), `Catalog not sent: the contract writer refused it` (Warning)
  ([`companion-log.md`](../../docs/areas/companion-log.md)).

## Acceptance criteria

- AC1: With a catalog ready, the first accepted hello whose `catalogIdentity` is `null` or differs from the
  Companion's catalog identity is followed by exactly one `catalogUpload` of the catalog writer's bytes of
  `GameCatalogDocumentConverter.ToDocument` (catalog, icons, build). Then comes one `iconImage` per reference of its
  acknowledgement's `missingIcons`, in that order, with the PNG whose SHA-256 is the reference, and then the waiting
  buffered messages. This holds in forward and in local mode, also when the server's catalog came from another
  installation (S1, S6, E9, N1).
- AC2: A `helloAccepted` whose `catalogIdentity` equals the Companion's catalog identity is followed by no
  `catalogUpload` and no `iconImage`, and `Catalog already on the server` is logged (S2).
- AC3: A catalog that becomes ready while a connection is open (the first catalog of the start, or a re-extraction) is
  uploaded after the message in flight when its identity differs from the one the server holds for this connection,
  and not at all when it equals it. A newer ready catalog stops the icons of an older one (S3, S4, E6).
- AC4: After a connection lost during the icons, the next accepted hello with `catalogIdentity` `null` leads to the
  upload again and to exactly the icons its acknowledgement lists; so does a catalog or icon not acknowledged within 30
  seconds, after `Server: not answering, retrying` and the reconnect (S5, E5, E8).
- AC5: A catalog whose icons failed is uploaded with every `icon` `null` and no `iconImage`. Without a ready catalog
  (no game, catalog failed) nothing is sent (S7, E1).
- AC6: A refused catalog sends no icon, logs `Server refused the catalog: <kind>` and is not uploaded again in this
  run, also after a reconnect. A refused icon is counted as `<r>`. A catalog the writer refuses logs `Catalog not sent:
  the contract writer refused it` and sends nothing. Live messages are sent afterwards in all three cases (E2, E3, E4).
- AC7: An acknowledgement of a catalog upload with 16,384 `missingIcons` is read and its icons sent. Any other reply
  above 65,536 bytes still ends the connection as `Server: invalid reply, retrying` (E7, N3).
- AC8: No catalog or icon message is written to the buffer folder or counted in the `Buffer:` line, and `Exit` during
  an upload ends within the bounds of `live-sending.md`, *Exit* (E5, E10).
- AC9: The four log lines of N7 are written with exactly that text and level, and none holds a name, reference or
  path (N4, N7).
- AC10: A Docker server started with an empty `/config` shows, after the first connection of a forward-mode Companion
  with the real game installed, region and good names and good icons on its pages. Checked by hand by the Product
  Manager on a build of the branch (the issue's criterion).
- AC11: `README.md` and `docs/USER_GUIDE.md`, English and then German, describe the upload, the skip, the log lines and
  what the pages show, so that an external tester can try it with Docker (the issue's *Documentation*).
- AC12: While it uploads, the Companion holds at most the written catalog message, one written icon message and the
  list of missing references besides its catalog; the plan names the seam that observes it (N2).

## Out of scope

- A replay (`--replay`, with `--server` or into the local server) sends no catalog. It shows the catalog a live
  connection stored earlier in the same server's database.
- No menu or tooltip text for the upload, no server change, no contract change, no new setting (N5).
- A link too slow for the catalog message within 30 seconds (N6): accepted, retried at each reconnect.
- Showing workforce and building names on the pages, and the choice of a profile (#43).

## Open questions

- Which connections upload? Answered: (Lead) live sending only, in forward and local mode. The issue depends on #40
  (the live client), and a re-extraction exists only with a live pipe connection (`game-catalog-cache.md`, *Later
  checks*). A replay sends what a recording holds. The Product Manager may extend this at the spec's approval.
- What is "the same version" the server already has? Answered: (code, record 0070) the `catalogIdentity` of
  `helloAccepted`, the SHA-256 of the stored document, named only while all its icons are stored
  (`ServerState.CompleteCatalogIdentity`, `ContractCatalogUpload.ComputeIdentity`).
- Catalog before or after the buffered messages? Answered: (Lead) before. Names appear soonest, and the catalog of a
  real installation is small next to a day of buffered ticks.
- A catalog without icons? Answered: (code) the converter writes `icon` `null` without an icon set, so names travel
  alone (S7).

## Area documents

- `docs/areas/live-sending.md`: a new section *Catalog* (when it is sent, the skip, the order, re-extraction,
  interruption, refusals, what the buffer never holds); *What is sent* and *Privacy* lose "No catalog, icon"; the *Log*
  table gains the four lines; *Not here* loses "the catalog (#42)". The plan measures the document against
  `AREA_MAX_WORDS` and splits it into its own area if needed.
- `docs/areas/replay-sending.md`: the reply bound's exception for catalog acknowledgements, if the shared rule changes
  there; *Scope* and *Not here* point to live sending for the catalog.
- `docs/areas/local-mode.md`: "The catalog's names and icons are not in local mode yet" is replaced by a pointer.
- `docs/areas/companion-log.md`: the four lines.
- `docs/areas/game-catalog-format.md`, `game-catalog-cache.md`, `contract.md`, `ingest.md`: the "(#42)" pointers name
  live sending. `game-catalog-format.md` *Scope* no longer says that nothing sends the document.
- `docs/ARCHITECTURE.md`: "which nothing sends yet (#42)" names live sending.
