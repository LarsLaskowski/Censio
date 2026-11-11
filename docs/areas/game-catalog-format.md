# Game catalog: format for the server

## Scope

The versioned catalog document: the JSON form of the game catalog ([`game-catalog.md`](game-catalog.md)) and of the
references to its icons ([`game-catalog-icons.md`](game-catalog-icons.md)) that the Companion will give the server, which
cannot read game files ([`0023`](../decisions/0023-game-names-and-icons-extracted-locally.md)). It states the properties,
the value rules, the order of the reader's checks, the bounds, the refusal kinds and their texts, the compatibility rules
and the privacy of the document. The reader runs on input from outside and is strict. This feature only converts, writes
and reads the document in memory: it sends, stores and shows nothing, and the conversion has no caller yet.

## Behavior

Records [0055](../decisions/0055-catalog-document-refers-to-icons-by-content-hash.md) (icon references) and
[0056](../decisions/0056-catalog-document-read-strictly-by-exact-schema-version.md) (strict reading, versions) hold the
reasoning.

### Document

JSON in UTF-8 without a byte order mark, compact. Every property is required and always written; `null` stands where an
entry has no value.

| Property | Where | Value |
| -------- | ----- | ----- |
| `schemaVersion` | document | the integer `1` |
| `gameVersion` | document | the game build as a string, or `null` when unknown |
| `entries` | document | array of entries |
| `guid` | entry | the asset GUID, a non-zero signed 32-bit integer (negative ones are valid) |
| `kind` | entry | `region`, `good`, `workforce` or `building`, nothing else, lowercase only |
| `names` | entry | object, language tag to name; only the languages with a name (`en`, `de`), so it may be empty |
| `regionGuid` | entry | the region's GUID for a `region` entry, `null` for every other kind |
| `regionTypes` | entry | array of strings in their order, or `null` when not stated; an empty array means none |
| `icon` | entry | 64 lowercase hex digits, the SHA-256 of the icon's PNG data, or `null` |

An example of invented values:

```json
{"schemaVersion":1,"gameVersion":"1.2.3.4","entries":[{"guid":1001,"kind":"region","names":{"de":"Beispielland","en":"Exampleland"},"regionGuid":7,"regionTypes":["Sample"],"icon":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"},{"guid":-2002,"kind":"good","names":{"en":"Sample goods"},"regionGuid":null,"regionTypes":null,"icon":null}]}
```

The icon images are not in the document, and entries that share an image carry the same reference.

### Conversion from the catalog

- The document has schema version 1 and the build as game version (`null` when unknown).
- One entry per catalog entry that a lookup by its GUID answers, in the catalog's order. An entry of GUID `0`, the second
  entry of a repeated GUID and an entry of an ambiguous GUID are left out.
- The names are `en` and `de`, each left out when missing or empty. The region GUID and the region types are taken as the
  catalog has them.
- The icon reference is the SHA-256 of the PNG data of the entry's icon in lowercase hex; none without an icon or without
  an icon set.
- The conversion validates nothing: the writer does.

### Writing

- The same catalog gives the same bytes: entries and region types in their order, languages in ordinal order of their tag.
- The writer refuses what the reader would refuse and writes nothing then: another schema version (*unsupported version*),
  a count or size above its bound (*too large*), a value that breaks a rule, a `null` where none is allowed or a string that
  is no well-formed Unicode text (*invalid*).
- Writing a document and reading it back gives the same document.

### Reading

The reader takes UTF-8 bytes and checks in this order; the first failing step decides the kind:

1. The length of the document against its bound.
2. The whole document is well-formed JSON (depth at most 64, nothing after the document, valid UTF-8, no lone surrogate,
   also when escaped), each string's length is checked before it is decoded, and the counts of the bounds are checked
   before anything is built. The same pass finds `schemaVersion` among the document's own properties: it must be present
   once and be an integer without fraction or exponent inside the signed 32-bit range.
3. A `schemaVersion` other than `1` is *unsupported version*, whatever else the document holds.
4. The structure of version 1: no unknown property, no repeated property (also a repeated language tag), none missing, the
   names in exactly the case given, the JSON types right, no `null` where none is allowed, GUIDs and region GUIDs
   integers in the signed 32-bit range, a kind of exactly the four words.
5. The value rules.

A document is used whole or not at all.

### Value rules

- The GUID is not `0` and appears on one entry only.
- A `region` entry has a region GUID other than `0`; no other kind has one.
- A name is not empty. A language tag is lowercase BCP 47 of 1 to 35 characters: a primary subtag of 2 or 3 letters `a` to
  `z`, then subtags of 1 to 8 letters `a` to `z` or digits `0` to `9`, each after one `-` (`pt-br` is valid, `pt-BR` and
  `EN` are not).
- A region type is not empty.
- An icon reference is exactly 64 characters `0` to `9` and `a` to `f`.
- A game version is four numbers joined by `.`, each of ASCII digits only, 0 to 65,535, `0` or without a leading zero,
  without sign or space, not all four zero.
- A catalog with no entries, an entry with no name or with one language only is valid.

All character checks are ASCII-only: letters and digits of other scripts do not count.

## Limits and errors

| Bound | Value |
| ----- | ----- |
| Document | 16 MiB (16,777,216 bytes) |
| String, in UTF-8 bytes after unescaping | 65,536 |
| Entries | 16,384 |
| Names in one entry | 16 |
| Region types in one entry | 32 |

A document at a bound is not refused for that size. At the count bounds the model holds about 1.05 million strings; the
read measured about 150 MiB peak working set (a 7 to 10 MB input, the runtime included).

Every refusal is a result with a kind and a fixed text, never an exception and never content of the document (no name,
region type or reference):

| Kind | Text |
| ---- | ---- |
| Malformed | `Catalog document is malformed` |
| Unsupported version | `Catalog schema version <n> is not supported; supported: 1` |
| Invalid | `Catalog document breaks a rule of schema version 1` |
| Too large | `Catalog document exceeds a size bound` |

*Malformed* is not JSON, truncated, invalid UTF-8 or a lone surrogate, content after the document, or a break of step 4; a
truncated document with `"schemaVersion": 2` is *malformed*, because step 2 comes first. *Unsupported version* names the
number found. *Too large* is a length or count above a bound.

### Compatibility

One integer `schemaVersion`. Every change of what a document may hold or what a value means (a property, a kind, a value
range) raises it; a new language, a new game version or a new entry does not. A reader accepts exactly the versions it
lists and reads each by that version's rules; an unknown version is refused, never read by the nearest known rules.

### Privacy

The document holds the player's names, so it is private like the cache ([`game-catalog-cache.md`](game-catalog-cache.md)).
No refusal text and no log line holds its content.

## Related decisions

- [`decisions/0055-catalog-document-refers-to-icons-by-content-hash.md`](../decisions/0055-catalog-document-refers-to-icons-by-content-hash.md)
  — icons by the SHA-256 of their PNG, no image data in the document.
- [`decisions/0056-catalog-document-read-strictly-by-exact-schema-version.md`](../decisions/0056-catalog-document-read-strictly-by-exact-schema-version.md)
  — one integer version, read strictly, exactly the listed versions.
- [`decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md`](../decisions/0018-pipe-parsed-in-companion-versioned-json-contract.md)
  — a mismatch is refused, never misread.
- [`decisions/0033-pipe-reader-strict-from-the-reference-source.md`](../decisions/0033-pipe-reader-strict-from-the-reference-source.md)
  — external input is read strictly and fails closed.

## Not here

- Building the catalog: [`game-catalog.md`](game-catalog.md); the icons: [`game-catalog-icons.md`](game-catalog-icons.md);
  the cache: [`game-catalog-cache.md`](game-catalog-cache.md).
- Sending, receiving, storing and showing the catalog and the images (#27, #42).
- The code: `CatalogDocumentReader` and `CatalogDocumentWriter` in `src/Censio.Contracts`, the conversion in
  `src/Censio.Companion.Core`.
