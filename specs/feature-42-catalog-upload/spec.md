# Spec: Catalog upload on connect and after re-extraction

Source: Issue #42
Status: Approved by Lead

## Problem

A player whose server runs in Docker or in local mode sees `Region <GUID>`, `Good <GUID>` and no good icons on its
pages ([`web-pages.md`](../../docs/areas/web-pages.md)). The server cannot read game files (record 0023); the Companion
builds the catalog, the server already stores and shows `catalogUpload` and `iconImage`
([`ingest.md`](../../docs/areas/ingest.md), [`server-state.md`](../../docs/areas/server-state.md), *Catalog*), but
nothing sends them ([`live-sending.md`](../../docs/areas/live-sending.md), *What is sent*).

Checked against the code: `IngestClient` passes nothing of `helloAccepted` to its caller, and its reply bound of
65,536 bytes is below the largest catalog acknowledgement (16,384 `missingIcons`, about 1.1 MB, `ingest.md`). The
real catalog's icon count is not measured.

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
- E8: A catalog or icon not written, or not acknowledged, within 30 seconds each (a slow link, a large catalog, a slow
  store) is `Server: not answering, retrying`, as for any message. The upload is retried after the reconnect under the
  rule of E11 (N6).
- E9: The server's catalog came from another installation: the identities differ, so the upload replaces it.
- E10: `Exit` during an upload keeps its bounds (2 seconds, 5 in local mode; `live-sending.md`, *Exit*). The rest is S5
  at the next start.
- E11: A connection that ends while a catalog or one of its icons is in flight (no acknowledgement in time, a close
  such as 1011 after a failed store, a lost connection) defers that catalog: after the next accepted hello the live
  messages that wait at that hello are sent first, and the catalog follows once they are acknowledged. After the third
  such connection in a row for the same catalog it is not tried again in this run, and `Catalog not sent: the
  connection failed 3 times during its upload` is logged. A completed upload or a newer catalog starts the count again.

## Non-functional requirements

- N1: Order: one message in flight on the live connection as today. After `helloAccepted` the catalog and its icons
  come before the waiting live messages, except for a deferred catalog (E11), which follows them. A catalog that
  becomes ready later follows the message in flight. Ticks taken
  meanwhile wait in the buffer within its limits.
- N2: Memory: besides the catalog and icons it holds, an upload holds at its peak, while the message is written, the
  document's bytes (at most 16 MiB) and the written catalog message (at most the contract's 24 MiB). From the send on
  it holds only the written message, until its acknowledgement; the document model and its bytes are released before
  the send. Afterwards it holds the acknowledgement's list of missing references and one written icon message at a
  time.
- N3: Replies: an acknowledgement of a catalog upload is read up to the size the contract allows (16,384 references,
  about 1.1 MB). Every other reply keeps the bound of 65,536 bytes
  ([`replay-sending.md`](../../docs/areas/replay-sending.md), *Limits and errors*).
- N4: Privacy: the catalog and the icons go only to the server live sending connects to, over the connection
  configured for it: unencrypted with a `ws://` `serverAddress`, under TLS with `wss://` (record 0023: only to the
  player's own server). No log line holds a name, an icon reference, a path or
  any content. The figure name is still never sent (record 0041), and the cache file is still never sent.
- N5: Compatibility: contract version 2 and the server are unchanged. No new setting, menu line or tooltip text. A
  server of release 0.3.0 refuses the hello as before.
- N6: Accepted risk: a link or a server that cannot take the catalog within 30 seconds costs up to three reconnects
  per catalog and run (E8, E11); the live messages that wait meanwhile are sent between them, so the buffer drops only
  what an outage beyond its limits would drop anyway. Without E11's deferral and count the upload would be retried
  first at every reconnect, no tick would be sent, and the buffer would drop the oldest messages at `bufferHours` or
  `bufferMegabytes`; that is why the rule exists.
- N7: Log lines, Information unless named, each written once per occasion: `Catalog sent to the server: <i> icons sent,
  <r> refused` (after the last icon's acknowledgement, also with `0 icons`), `Catalog already on the server` (at most
  once per accepted hello and per ready catalog), `Server refused the catalog: <kind>` (Warning, the kind by its name as
  in `Server refused a live message: <kind>`), `Catalog not sent: the contract writer refused it` (Warning), `Catalog not sent: the connection failed 3 times
  during its upload` (Warning) ([`companion-log.md`](../../docs/areas/companion-log.md)).

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
  run, also after a reconnect. A refused icon is counted as `<r>`, not as `<i>`. A catalog the writer refuses logs `Catalog not sent:
  the contract writer refused it` and sends nothing. Live messages are sent afterwards in all three cases (E2, E3, E4).
- AC7: An acknowledgement of a catalog upload with 16,384 `missingIcons` is read and its icons sent. Any other reply
  above 65,536 bytes still ends the connection as `Server: invalid reply, retrying` (E7, N3).
- AC8: No catalog or icon message is written to the buffer folder or counted in the `Buffer:` line, and `Exit` during
  an upload ends within the bounds of `live-sending.md`, *Exit* (E5, E10).
- AC9: The five log lines of N7 are written with exactly that text and level, and none holds a name, reference or
  path (N4, N7).
- AC10: A Docker server started with an empty `/config` shows, after the first connection of a forward-mode Companion
  with the real game installed, region and good names and good icons on its pages. Checked by hand by the Product
  Manager on a build of the branch (the issue's criterion).
- AC11: `README.md` and `docs/USER_GUIDE.md`, English and then German, describe the upload, the skip, the log lines and
  what the pages show, so that an external tester can try it with Docker (the issue's *Documentation*). Both also say
  that a replay sends no catalog: names and icons reach a server only from a live connection of a Companion with the
  game installed, and a replay into a server shows them only when such a connection stored them there before.
- AC12: An upload holds the document's bytes and the written catalog message together only while the message is
  written, then the written message alone until its acknowledgement, then the list of missing references and one
  written icon message at a time; the plan names the seam that observes it (N2).
- AC13: With a server that never acknowledges the catalog (or closes with 1011 on it), the live messages that wait and
  those taken later are acknowledged on the following connections. After the third failed connection during its
  upload the catalog is not sent again in this run and `Catalog not sent: the connection failed 3 times during its
  upload` is logged once (E11, N6).

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
  (`ServerState.CompleteCatalogIdentity`).
- Catalog before or after the buffered messages? Answered: (Lead) before, unless the previous connection ended
  during its upload: then after the messages that wait (E11). Names appear soonest, and the catalog of a real
  installation is small next to a day of buffered ticks.
- A catalog without icons? Answered: (code) the converter writes `icon` `null` without an icon set, so names travel
  alone (S7).

## Area documents

- `docs/areas/live-sending.md`: a new section *Catalog* (sending, skip, order, deferral, refusals, not buffered);
  *What is sent*, *Privacy*, *Log* and *Not here* follow. The plan measures it against `AREA_MAX_WORDS` and splits if
  needed.
- `docs/areas/companion-log.md`: the five lines. The reply bound's exception lives in
  `live-sending.md`, *Limits and errors* (a replay never uploads).
- The "(#42)" and "not yet" pointers of `local-mode.md`, `game-catalog-format.md` (also its *Scope*),
  `game-catalog-cache.md`, `contract.md`, `ingest.md` and `docs/ARCHITECTURE.md` name live sending.

## Challenge

- MAJOR 1 (a catalog that keeps failing blocks live sending): accepted. New E11: a connection that ends while a catalog
  or icon is in flight defers the catalog behind the messages waiting at the next hello, and after three such
  connections in a row the catalog is not tried again in this run, with a log line. N1, N6, N7, the third open
  question and AC9 follow; AC13 requires that a catalog failing every time does not stop ticks. E8 now points to E11.
- MINOR 1 (memory peak during the write): accepted. N2 names the document's bytes beside the written message as the
  peak while writing, and requires the document model and bytes released before the send; AC12 checks that wording.
- MINOR 2 (E8 wording): accepted; E8 reads "not written, or not acknowledged, within 30 seconds each".
- MINOR 3 (`wss://`): accepted; N4 says the catalog goes over the configured connection, unencrypted only with `ws://`.
- MINOR 4 (replay in the documentation): accepted; AC11 requires README and user guide to say that a replay sends no
  catalog and that names and icons come only from a live connection of a Companion with the game installed.

## Amendments

| Criterion | Change | Reason | Step | Decided by |
| --------- | ------ | ------ | ---- | ---------- |
| AC6 | `<i>` counts only accepted icons | E4's writer-refused icon is never sent | 5 | Lead (decide) |
