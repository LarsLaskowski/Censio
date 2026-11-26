# Contract

## Scope

The JSON contract version 2 between the Companion and the server: the eleven messages, their wire form, the version rule
and the frozen hello, identities, the reader's checks in order, the bounds, the refusal texts and what never crosses.
The server and the Companion rely on it. The server's endpoint reads and answers these messages ([`ingest.md`](ingest.md));
a Companion that replays a recording with `--server` sends them ([`replay-sending.md`](replay-sending.md)) and a live Companion does ([`live-sending.md`](live-sending.md)).

## Behavior

### Wire form

- A message is one UTF-8 JSON object without byte order mark, compact. Its first properties are `type` and
  `contractVersion` (`2`), then its own properties in the order below. Every property is required and always written; a
  value is `null` only where stated. Names are camelCase, enum values camelCase strings.
- A float is a JSON number in the range of a 32-bit float, written in its shortest form that reads back to the same
  bits (`-0` stays negative zero), or `null` for no value. A number outside that range is never read as a value.
- A map is an array of `[guid, amount]` pairs of two int32 numbers in ascending GUID order. Islands and entries keep the
  tick's order; maps and `missingIcons` are written sorted, so the same message gives the same bytes.
- A UUID is 36-character lowercase hyphenated text, never all zero. A receive time is `yyyy-MM-ddTHH:mm:ss.fffffffZ`
  exactly (UTC, seven fractional digits). Binary data (`document`, `png`) is Base64 text of the standard alphabet with
  canonical padding.

| `type` | Direction | Properties after `type` and `contractVersion` |
| ------ | --------- | --------------------------------------------- |
| `hello` | Companion to server | `token` (string or `null`), `companionVersion`, `profileId`, `profileName` |
| `helloAccepted` | server to Companion | `serverVersion`, `catalogIdentity` (64 lowercase hex or `null`) |
| `helloRefused` | server to Companion | `reason` (`unsupportedVersion`, `tokenRefused`, `companionConnected`), `receivedVersion`, `supportedVersions` |
| `catalogUpload` | Companion to server | `id`, `document` (Base64 of the catalog document's bytes) |
| `iconImage` | Companion to server | `id`, `reference`, `png` (Base64) |
| `tick` | Companion to server | `id`, `profileId`, `receiveTime`, `gameTime`, `islands` |
| `tradeRoutes` | Companion to server | `id`, `profileId`, `receiveTime`, `routes` |
| `sessionStart`, `sessionEnd` | Companion to server | `id`, `profileId`, `receiveTime` |
| `status` | Companion to server | `pipeState` (`waitingForGame`, `connected`, `receiving`, `pipeBusy`, `protocolMismatch`, `error`) |
| `acknowledgement` | server to Companion | `id`, `refusal`, `refusedVersion`, `missingIcons` |

An island is `sessionGuid`, `islandId`, `areaIndex`, `sessionId`, `name`, `entries`; an entry is `productGuid`,
`generation`, `consumption`, `delta`, `perfectGeneration`, `perfectConsumption`, `buildingCount`, `maintenance`,
`income`, `profit`, `summedProductivity`, `averageProductivity`, `workforceAmounts`, `buildingAmounts`. Eight of the
entry's values are floats (generation, consumption, delta, perfect generation, perfect consumption, income, summed and
average productivity), three are int32 (building count, maintenance, profit). The refusal of an acknowledgement is
`null` (accepted) or one of `malformed`, `unsupportedVersion`, `invalid`, `tooLarge`, `catalogMalformed`,
`catalogUnsupportedVersion`, `catalogInvalid`, `catalogTooLarge`.

A route of `routes` is `id`, `name`, `folder`, `majorProblems`, `minorProblems`, `ships`, `lostShips`, `stations`; a station
is `sessionGuid`, `islandId`, `areaIndex`, the key of an island. The route's `id` and its four counts are
int32 numbers, `islandId` and `areaIndex` bytes. Routes and stations keep the order of the pipe's message. The message
holds the routes and their stations' island keys only: a route's goods, a station's flags, owner and session ID byte do
not cross (*What never crosses*).

Examples with invented values (shortened where marked):

```json
{"type":"hello","contractVersion":2,"token":"example-token","companionVersion":"0.3.0","profileId":"aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee","profileName":"Alice"}
{"type":"helloAccepted","contractVersion":2,"serverVersion":"0.3.0","catalogIdentity":null}
{"type":"helloRefused","contractVersion":2,"reason":"unsupportedVersion","receivedVersion":1,"supportedVersions":[2]}
{"type":"catalogUpload","contractVersion":2,"id":"11111111-2222-4333-8444-555555555555","document":"eyJzY2hlbWFWZXJzaW9uIjox..."}
{"type":"iconImage","contractVersion":2,"id":"11111111-2222-4333-8444-555555555555","reference":"0f4636c78f65d3639ece5a064b5ae753e3408614a14fb18ab4d7540d2c248543","png":"iVBORw=="}
{"type":"tick","contractVersion":2,"id":"11111111-2222-4333-8444-555555555555","profileId":"aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee","receiveTime":"2026-11-12T10:20:30.1234567Z","gameTime":42,"islands":[{"sessionGuid":180023,"islandId":1,"areaIndex":0,"sessionId":2,"name":"Isle","entries":[{"productGuid":1010017,"generation":1.5,"consumption":0.25,"delta":1.25,"perfectGeneration":2,"perfectConsumption":0,"buildingCount":3,"maintenance":-20,"income":null,"profit":7,"summedProductivity":300,"averageProductivity":1,"workforceAmounts":[[1010052,40]],"buildingAmounts":[]}]}]}
{"type":"tradeRoutes","contractVersion":2,"id":"11111111-2222-4333-8444-555555555555","profileId":"aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee","receiveTime":"2026-11-12T10:20:30.1234567Z","routes":[{"id":7,"name":"Wood to the capital","folder":"","majorProblems":0,"minorProblems":1,"ships":2,"lostShips":0,"stations":[{"sessionGuid":180023,"islandId":1,"areaIndex":0},{"sessionGuid":180023,"islandId":2,"areaIndex":0}]}]}
{"type":"sessionStart","contractVersion":2,"id":"11111111-2222-4333-8444-555555555555","profileId":"aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee","receiveTime":"2026-11-12T10:20:30.1234567Z"}
{"type":"sessionEnd","contractVersion":2,"id":"66666666-7777-4888-9999-000000000001","profileId":"aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee","receiveTime":"2026-11-12T10:25:00.0000000Z"}
{"type":"status","contractVersion":2,"pipeState":"receiving"}
{"type":"acknowledgement","contractVersion":2,"id":"11111111-2222-4333-8444-555555555555","refusal":null,"refusedVersion":null,"missingIcons":["0f4636c78f65d3639ece5a064b5ae753e3408614a14fb18ab4d7540d2c248543"]}
```

### Versions and the frozen form

- The contract version is an exact integer. A message of any version other than `2`, whatever else it holds, is refused
  as *unsupported version* with the version found: `Contract version <n> is not supported; supported: 2`. Version 1
  (release 0.3.0) had no `tradeRoutes` message; a Companion of that release and this server refuse each other with both
  versions named ([record 0096](../decisions/0096-contract-version-2-adds-trade-routes-read-exactly.md)).
- `type` and `contractVersion` are root properties in every version. The hello and the refused hello reply keep exactly
  the properties above, the three reasons included, in every later version. A hello of any version is read for its
  version and then refused as above; a refused hello reply of any version whose form is exactly the frozen one is read
  and returned with its reason, received version and supported versions, so a Companion tells a newer server's refusal
  from a damaged message. A refused reply of another form is *unsupported version*.
- The endpoint checks the version before the token and before the second-Companion rule, so a Companion and a server of
  different versions always refuse each other with the versions named.

### Identities and acknowledgements

- A tick, a trade-routes message, a session start, a session end, a catalog upload and an icon image carry an `id`: a UUID the Companion assigns
  when it takes the message and keeps with it. A resend after a lost acknowledgement carries the same `id`; two
  messages of equal content taken separately carry different ones.
- A tick, a trade-routes message and a session event carry the identity (`profileId`) of the profile they were taken for, not the name; they are
  read with that profile, not with the profile of the connection's hello. The hello carries the profile's name.
- An acknowledgement names the `id` it answers. Accepted or refused, the Companion may discard the message; no
  acknowledgement means the Companion resends. A refusal carries its kind and `refusedVersion` for the two
  unsupported-version kinds (and for no other); an acknowledgement of a catalog upload lists in `missingIcons` the icon
  references the server lacks, sorted, none for a refusal.
- An icon's `reference` is the SHA-256 of its PNG data in lowercase hex; the catalog identity of the hello reply is the
  SHA-256 of the catalog document's bytes, equal for an equal catalog because the catalog writer is deterministic.
- A session event's and a trade-routes message's `receiveTime` is the read stamp of its pipe message. A session result of the pipe carries no time, so
  the caller takes the stamp from the tick assembler's read observer and gives it to the converter; it is never the time
  of conversion or sending (a trade-routes item the same way). A tick's `receiveTime` and `gameTime` are those of the closed pipe tick.
- A tick for which the Companion's buffer dropped data has no message: silence is the gap (record 0031); the buffer ([`live-sending.md`](live-sending.md), *Buffer*) needs no
  message for it.

### What never crosses

- The player figure's name of a session start, pipe errors and version messages and the WebSocket close reason are in
  no message. A float that is infinity or NaN crosses as `null`, never as a number.
- The token, the profile name, the island names and the route and folder names are private: no refusal text, log line or string form of a message
  holds them (the message types are classes with no generated `ToString`).
- Refusal texts hold no message content: no token, name, catalog content or image data.

## Limits and errors

### The reader's checks

The reader (`ContractReader`) takes the bytes of one message and returns the message or a typed refusal, never an
exception. A refusal of a tick, trade-routes message, session start, session end, catalog upload or icon image that is decided after the
message was built (the value rules or the catalog document, step 5) also returns that message's `id`, unless it is all zero, so an
endpoint can acknowledge the refusal by it; every other refusal, and every refusal of a server-to-Companion type,
returns none. An accepted catalog upload also returns the catalog document it read in step 5 (no second read), and the
catalog error *unsupported version* carries the schema version found, which an acknowledgement names as
`refusedVersion`. It decides in this order, the first failing step deciding:

1. An input above 25,165,824 bytes is *too large*, before it is parsed.
2. One syntax pass over the tokens: well-formed JSON of valid UTF-8, the root an object, at most 64 levels, nothing after
   it, no byte order mark, no lone surrogate (also when written as an escape). Every string and property name has its raw
   length checked before it is unescaped and its unescaped UTF-8 length after, against the bound of its position.
   Positions are matched by property path on unescaped names, at every position whatever `type` holds and wherever
   `type` stands (it may come last). Counts are taken by path the same way and checked before anything is built. A number
   at one of the eight float positions that is not a finite 32-bit float (`1e39`, also as an integer) is refused here.
   `contractVersion` must be present once as an int32 integer without fraction or exponent, `type` once as a string. A
   failure is *malformed*, a bound *too large*; the size and count bounds are thus decided before the version.
3. A `contractVersion` other than `2` is handled as under *Versions and the frozen form*.
4. The structure of the type: exact names and case, no unknown, repeated or missing property, the JSON types, integer
   ranges (`byte` 0 to 255), no number written as text, no `null` where none is allowed, no `null` element in a list, a
   map pair of exactly two numbers. An unknown `type` is *malformed*. Base64 is checked before decoding: the raw token
   holds no escape, only the standard alphabet and canonical padding.
5. The value rules, then, for a catalog upload, the decoded document goes through the catalog reader and its refusal is
   *catalog refused* with the catalog error and its text ([`game-catalog-format.md`](game-catalog-format.md)).

### Bounds

| Bound | Value | Measured |
| ----- | ----- | -------- |
| Message | 25,165,824 bytes (24 MiB) | on the raw input, before parsing |
| `png`, `document` at the root | 23,767,724 characters, decoded at most 17,825,792 bytes (`document` then also the catalog's 16 MiB, by the catalog reader) | on the raw token, before any unescaping |
| Token, versions, profile name, every other string and every property name | 1,024 UTF-8 bytes | after unescaping, after a raw-length check of six times the bound |
| Island name | 765 UTF-8 bytes | after unescaping |
| Route name and folder name of a route | 765 UTF-8 bytes each | after unescaping, after the raw-length check; positional: `name` and `folder` of a route, 1,024 bytes elsewhere |
| Islands per tick | 256 | counted in step 2 |
| Entries and map pairs per tick | 524,288 | counted in step 2 |
| Pairs per map | 1,024 | counted in step 2 |
| Routes per trade-routes message | 1,024 | counted in step 2 |
| Stations per route / per trade-routes message | 256 / 52,428 | counted in step 2; the per-route count restarts at each `stations` array |
| `supportedVersions` / `missingIcons` | 16 / 16,384 | counted in step 2 |

The trade-routes bounds follow the pipe's caps and its 1 MiB frame (1,024 routes, 256 stations a route, 52,428 stations at 20
bytes, names of 255 pipe bytes of which each may decode to U+FFFD: 765 bytes; [`pipe.md`](pipe.md), *Limits and errors*), so every
frame the pipe accepts crosses. A message at every count bound fits the message bound by a wide margin: at most 2,048 names
of at most 4,590 raw bytes (six times the bound), 52,428 stations of at most 59 bytes and the route properties come to under
13 MB (arithmetic from the bounds), and the same message with names of 765 ASCII characters is 4,211,573 bytes written
(measured with `ContractWriter`).

The `png` and `document` bound is measured on the raw token. A raw token over the bound is *too large*; an escape in
these strings is refused in any case, so an escaped token within the bound is *malformed* (step 4). The decoded size is
taken from the length and padding of the token, so 23,767,724 characters that would decode to one byte more than
17,825,792 are *too large* as well.

### Value rules (*invalid*)

UUID form; receive time form; a trade-routes message of any number of routes (none included), each with names that are valid UTF-8
(empty allowed), any int32 counts and any number of stations (none included), repeated route IDs and names allowed; a tick of 1 to 256 islands with distinct keys (session GUID, island ID, area index);
strictly ascending map GUIDs, which also excludes a repeated GUID; token `null` or not empty; profile name, Companion
version and server version not empty; `reference` and `catalogIdentity` 64 lowercase hex; `png` not empty and its
SHA-256 equal to `reference`; `supportedVersions` 1 to 16 distinct ascending positive values; `refusedVersion` set exactly
for the two *unsupported version* refusals; `missingIcons` 64 lowercase hex, strictly ascending, empty for a refusal.
Text that is not the UUID or receive time form is *invalid*, not *malformed*.

### Kinds and texts

| Kind | Text |
| ---- | ---- |
| Malformed | `Contract message is malformed` |
| Unsupported version | `Contract version <n> is not supported; supported: 2` |
| Invalid | `Contract message breaks a rule of contract version 3` |
| Too large | `Contract message exceeds a size bound` |
| Catalog refused | the catalog's own text (`Catalog document is malformed`, `Catalog schema version <n> is not supported; supported: 1`, `Catalog document breaks a rule of schema version 1`, `Catalog document exceeds a size bound`) |

An acknowledgement gives the same texts for its kinds (`GetRefusalText`), so the Companion never shows text from the
server. Texts hold no message content.

### The writer

The writer (`ContractWriter`) checks the same bounds and rules and writes nothing on a refusal: a float that is not
finite, a receive time with an offset other than zero, an all-zero UUID, a lone surrogate or a `null` are *invalid*; a
count, string or binary above its bound, or a message that would exceed the message bound, is *too large*. It stops
writing as soon as the message bound is passed, so a tick at the pipe's limits (about 200 MB) is never built whole. The
writer sorts maps and `missingIcons`. For a catalog upload it checks only the document's size and the identity; the
document is expected to be the catalog writer's bytes, which already refuses what the catalog reader refuses, and the
server's refusal reaches the Companion as a catalog kind of the acknowledgement.

### Memory of a read

Measured on the real `ContractReader.Read`, .NET 11, Release, workstation non-concurrent garbage collection, one
process per case, the input read from a file before the measurement, three runs per case. The scratch program was
outside the repository; the catalogs are described below exactly enough to rebuild them byte for byte.

All three catalog shapes are a `catalogUpload` with `id` `11111111-2222-4333-8444-555555555555`, written by
`CatalogDocumentWriter` (names sorted) and wrapped by `ContractWriter`. The document: `schemaVersion` 1, `gameVersion`
`1.2.3.4`, 16,384 entries; entry `i` (0 to 16,383) has `guid` `i + 1`, `kind` `good`, `regionGuid` `null` and `icon`
`i` in lowercase hexadecimal, left-padded with `0` to 64 digits. Every name value and every region type is `x`
repeated `L` times, `L` the largest length that keeps the document within the bound of 16,777,216 bytes.

| Shape | Names of each entry | `regionTypes` of each entry | `L` | Document | SHA-256 of the document | Message |
| ----- | ------------------- | --------------------------- | --- | -------- | ----------------------- | ------- |
| A | 16, tags `aa` to `ap` | `null` | 46 | 16,602,325 bytes | `68910f3c29a42bc1f8201baecf756f4501e0ba68752f72279dea96b432dd5d21` | 22,136,538 bytes |
| B | as A | 32 | 13 | 16,291,029 bytes | `7ce273bb1c433039d77a6951c48c66fa948a10d22bb378335e91db5409f3292f` | 21,721,474 bytes |
| C | as B, each tag its two letters followed by `-a` sixteen times (34 characters, 17 subtags) | 32 | 2 | 16,028,885 bytes | `8d6710b30e08d7ad2d097cada6d9b350d9cabf6a8efb16dec550e83edefea3ea` | 21,371,950 bytes |

```
dotnet build -c Release
dotnet bin/Release/net11.0/measure.dll gen A catalog-a.json     # likewise B and C; writes the document beside it
dotnet bin/Release/net11.0/measure.dll gen minimal-tick  minimal-tick.json
dotnet bin/Release/net11.0/measure.dll gen pairs-tick    pairs-tick.json
dotnet bin/Release/net11.0/measure.dll gen four-pairs-tick four-pairs-tick.json
dotnet bin/Release/net11.0/measure.dll run catalog-a.json      # likewise catalog-b.json, catalog-c.json, minimal-tick.json, pairs-tick.json and four-pairs-tick.json
```

| Case | Input | Read time | Model retained | Peak managed (input included) | Allocated during read | Peak working set |
| ---- | ----- | --------- | -------------- | ----------------------------- | --------------------- | ---------------- |
| Catalog upload, shape A | 22,136,538 bytes | 376 to 384 ms | 98.9 MiB (120.0 MiB with the input) | 159.8 MiB | 138.4 MiB | 206.1 MiB |
| Catalog upload, shape B | 21,721,474 bytes | 489 to 503 ms | 109.4 MiB (130.2 MiB with the input) | 174.6 MiB | 153.6 MiB | 221.0 MiB |
| Catalog upload, shape C | 21,371,950 bytes | 1,233 to 1,255 ms | 113.2 MiB (133.6 MiB with the input) | 333.8 MiB | 417.8 MiB | 385.4 MiB |
| Tick of 91,761 minimal entries (all floats `null`, empty maps) in 256 islands (not the worst tick) | 25,165,389 bytes | 718 to 889 ms | 26.3 MiB (50.4 MiB with the input) | 70.6 MiB | 46.4 MiB | 121.0 MiB |
| Tick of 92,097 entries with four workforce pairs each (368,388 pairs, floats written as `0`), in 256 islands | 25,165,356 bytes | 875 to 1,331 ms | 43.3 MiB (67.3 MiB with the input) | 114.4 MiB | 90.1 MiB | 169.2 MiB |
| Tick of 524,288 entries and pairs, read straight into the maps | 4,241,243 bytes | 265 ms | 19.1 MiB (23.2 MiB with the input) | 40.2 MiB | 36.1 MiB | 88.7 MiB |

Each figure of a catalog row is the largest of its three runs, the read time its range. Output of the run with the
highest peak (shape C, the third run; peak managed sampled every millisecond):

```
input bytes            : 21,371,950
managed before (input) : 20.4 MiB
managed after (model)  : 133.6 MiB  (input + model alive)
managed retained model : 113.2 MiB
peak managed (sampled, 1 ms, incl. input): 333.8 MiB
allocated during Read  : 417.4 MiB
peak working set       : 385.4 MiB
read time              : 1233 ms  error=none
```

The largest read is therefore a catalog upload of shape C: 333.8 MiB of managed memory at its peak with the input,
385.4 MiB of working set. The peak depends on the catalog's shape, not only its size: the three messages are of nearly
the same size (21.4 to 22.1 million bytes), yet the peak of C is twice that of A, because the reader allocates per tag
and per subtag. The worst tick at the bound is one of entries with a few pairs each: about 114 MiB at its peak with the
input.

### Security considerations for the endpoint (#28)

- The reader does not check the direction: the endpoint refuses a server-to-Companion type from a Companion.
- The endpoint bounds the first, unauthenticated message to 32 KiB, above the largest valid hello (four strings of at most
  1,024 bytes, each at most 6,144 bytes raw when escaped).
- It bounds a WebSocket message at the message bound and reads past 32 KiB only on the one accepted Companion's
  connection, so at most one read of the size above runs at a time, with the token off as well; the memory of an
  accepted catalog upload is measured in [`ingest.md`](ingest.md), *Memory of a catalog upload*.
- PNG data is carried, hashed and bounded, not validated as PNG: whoever serves it to a browser checks it. The server's
  icon route does (#32): it serves bytes only when they start with the PNG signature followed by an `IHDR` chunk
  ([`web-pages.md`](web-pages.md), *Icon route*).

## Related decisions

- [`decisions/0058`](../decisions/0058-contract-read-strictly-by-exact-version-with-a-frozen-hello.md) — exact version, frozen hello and refusal.
- [`decisions/0059`](../decisions/0059-contract-messages-carry-a-companion-assigned-identity-and-their-profile.md) — identity per message, profile per message.
- [`decisions/0060`](../decisions/0060-contract-message-bound-caps-ticks-below-the-pipes-limits.md) — one 24 MiB bound, Base64 data.
- [`decisions/0018`](../decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md) — the Companion parses, the server reads a versioned contract.
- [`decisions/0041`](../decisions/0041-profile-selected-in-the-companion-figure-name-only-a-hint.md) — the figure name never crosses.
- [`decisions/0056`](../decisions/0056-catalog-document-read-strictly-by-exact-schema-version.md) — the catalog document inside an upload.
- [`decisions/0063`](../decisions/0063-refused-contract-messages-acknowledged-by-id-or-the-connection-closed.md) — the reader returns the `id` of a refused message and the catalog of an accepted upload.
- [`decisions/0096`](../decisions/0096-contract-version-2-adds-trade-routes-read-exactly.md) — version 2 adds the trade-routes message and is read exactly.

## Not here

- The server's endpoint and what it answers to each message: [`ingest.md`](ingest.md); its token: [`ingest-token.md`](ingest-token.md).
- The Companion's sending of a replay: [`replay-sending.md`](replay-sending.md); live sending: [`live-sending.md`](live-sending.md); the persistent buffer ([`live-sending.md`](live-sending.md), *Buffer*); uploading the catalog and the icons ([`live-sending.md`](live-sending.md), *Catalog*).
- The catalog document itself: [`game-catalog-format.md`](game-catalog-format.md).
- The pipe's frames and ticks: [`pipe.md`](pipe.md).
- The code: `ContractReader`, `ContractWriter` and the message types in `src/Censio.Contracts`, the conversion of pipe
  data in `src/Censio.Companion.Core`.
