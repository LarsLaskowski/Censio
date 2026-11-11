# Plan: Versioned catalog format

Source: Issue #26 | [spec.md](spec.md)
Tier: standard — a new format with its reader and writer: input handling for a later network reader (size bounds, a parser's validation), no security control created or changed.
Security areas touched: Parsing of external input (the new catalog document reader in `src/Censio.Contracts`, which the server will run on WebSocket input, #27/#42); build configuration (two test project files: a fixture link and a runtime feature switch); `.squad/project.md` (*Security areas*, an entry extended).

## Problem / root cause

Feature: see [spec.md](spec.md); its criteria AC1–AC8 are the plan's.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `Censio.Contracts` references no project | its `.csproj`, `docs/ARCHITECTURE.md:19` | confirmed |
| 2 | The builder can produce an entry of GUID `0` | `GameCatalogAssets.cs:74`; `GameCatalogBuilder.cs:36,90` | confirmed |
| 3 | `GameCatalog` accepts repeated GUIDs, `Find` answers the first and nothing for an ambiguous GUID | `GameCatalog.cs` constructor and `Find` | confirmed |
| 4 | Region entries carry a non-zero `RegionGuid`, other kinds none | `GameCatalogBuilder.cs:92-121` | confirmed |
| 5 | A missing name is `null`; `Check` treats an empty one as missing | `GameCatalog.cs` `Check` | confirmed |
| 6 | The build is `null` or four numbers of 16-bit file-version parts, not all zero, without leading zeros | `GameInstallation.cs:57-65`; Win32 `VS_FIXEDFILEINFO` | confirmed |
| 7 | Entries naming one icon file share one `GameIcon` | `GameIconSet.cs`; `game-catalog-icons.md` *Entries* | confirmed |
| 8 | .NET 11 has `AllowDuplicateProperties` (also on the source-generation attribute), `RespectNullableAnnotations`, `UnmappedMemberHandling`, `Convert.ToHexStringLower` | reference pack 11.0.0 XML docs | confirmed |
| 9 | Duplicate dictionary keys are refused; `CopyString` throws `ArgumentException` on a short buffer and `InvalidOperationException` on an escaped lone surrogate; unescaped invalid UTF-8 passes the reader; `1.0`, `1e0`, `"1"`, 2147483648 into `int` throw `JsonException` | Security's scratch runs (plan review), pinned by AC4, AC5, AC7 rows | confirmed |
| 10 | A 64 MiB document of short strings retains over 500 MB of model | Security's measurement (plan review) | confirmed |
| 11 | At the bounds of spec E5 the model holds about 1.05 million strings and at most 32 MiB of UTF-16 text; estimated peak below 150 MiB | to verify: the Dev reads the document of `CatalogDocumentReaderReadAcceptsCountsAtBound` once in a scratch run in step 6 and logs the peak working set | unverified |
| 12 | Switching reflection off in `Censio.Companion.Core.Tests` would break `GameInstallationSearchTests.cs:871` | that line | confirmed |
| 13 | `Censio.Companion.Core.Tests` copies only its own fixtures; `GameCatalogReader.Read` builds a catalog from six streams | `Censio.Companion.Core.Tests.csproj:19`; `GameCatalogReader.cs:72` | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `GameCatalogDocumentConverterToDocumentMapsEveryField`, `GameCatalogDocumentConverterToDocumentSharesReferenceOfSharedImage`, `GameCatalogDocumentConverterToDocumentWithoutIconSetHasNoReferences`, `GameCatalogDocumentConverterToDocumentLeavesOutGuidZero`, `GameCatalogDocumentConverterToDocumentKeepsFirstOfRepeatedGuid`, `GameCatalogDocumentConverterToDocumentLeavesOutAmbiguousGuid` (catalog built as in `GameIconReaderTests.cs:769`: GUID 77 missing, 78 present), `GameCatalogDocumentConverterToDocumentWritesUnknownBuildAsNull`, `GameCatalogDocumentConverterToDocumentIsAcceptedByWriter` | `tests/Censio.Companion.Core.Tests/GameCatalogDocumentConverterTests.cs` |
| AC2 | `CatalogDocumentWriterWriteRoundTripsEveryField` (rows of the spec's cases), `CatalogDocumentWriterWriteTwiceGivesIdenticalBytes`, `CatalogDocumentWriterWriteOrdersLanguagesOrdinally`, `CatalogDocumentWriterWriteRoundTripsEmptyCatalog`, `CatalogDocumentWriterReflectionSerializationIsDisabled` | `tests/Censio.Contracts.Tests/CatalogDocumentWriterTests.cs` |
| AC3 | `GameCatalogDocumentConverterFixtureCatalogRoundTripsValid` | `tests/Censio.Companion.Core.Tests/GameCatalogDocumentConverterTests.cs` |
| AC4 | (reader tests in `CatalogDocumentReaderTests.cs`, the writer test in `CatalogDocumentWriterTests.cs`) `CatalogDocumentReaderReadRefusesOtherVersionAsUnsupported` (0, 2, -1), `CatalogDocumentReaderReadRefusesLaterVersionWithUnknownPropertyAsUnsupported`, `CatalogDocumentReaderReadRefusesMalformedVersion` (missing, `1.0`, `1e0`, `"1"`, 2147483648, repeated), `CatalogDocumentReaderReadRefusesTruncatedVersionTwoAsMalformed`, `CatalogDocumentReaderReadFindsEscapedVersionName` (`"schema\u0056ersion": 2` → unsupported version), `CatalogDocumentReaderReadIgnoresVersionInsideEntry` (only inside an entry → malformed), `CatalogDocumentWriterWriteRefusesOtherSchemaVersionAsUnsupported` (text `Catalog schema version 2 is not supported; supported: 1`) | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |
| AC5 | `CatalogDocumentReaderReadRefusesMalformedSyntax` (E1 rows), `CatalogDocumentReaderReadRefusesMalformedStructure` (E2 rows: unknown, repeated, missing, other-case property, wrong type, `null`, GUID 2147483648 and `5.0`, kind `Good` and `castle`, repeated language), `CatalogDocumentReaderReadRefusesInvalidUtf8`, `CatalogDocumentReaderReadRefusesLoneSurrogate` | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |
| AC6 | `CatalogDocumentReaderReadRefusesInvalidValue` (E4 rows, `EN`, `pt-BR`, `e`, `stra\u00dfe`, a 36-character tag, game versions `+1.0.0.0`, ` 1.0.0.0`, `1.0.0.0 `, `\u0661.0.0.0`, an icon with one uppercase hex digit), `CatalogDocumentReaderReadAcceptsLanguageTag` (`en`, `de`, `pt-br`), `CatalogDocumentReaderReadAcceptsNegativeGuid` | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |
| AC7 | `CatalogDocumentReaderReadRefusesDocumentAboveBound`, `CatalogDocumentReaderReadAcceptsDocumentAtBound`, `CatalogDocumentReaderReadRefusesStringAboveBound`, `CatalogDocumentReaderReadAcceptsStringAtBound`, `CatalogDocumentReaderReadAcceptsEscapedStringWithinBound`, `CatalogDocumentReaderReadRefusesCountsAboveBound` (rows: 16,385 entries, 17 names, 33 region types), `CatalogDocumentReaderReadAcceptsCountsAtBound` (one document of 16,384 entries, one of them with 16 names and 32 region types), `CatalogDocumentReaderReadRefusesCountsBeforeDeserializing` (17 names in an entry that also breaks a step-4 rule: *too large*, not *malformed*) | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |
| AC7 | `CatalogDocumentWriterWriteRefusesInvalidDocument` (E4 rows, undefined kind), `CatalogDocumentWriterWriteRefusesCountsAboveBound`, `CatalogDocumentWriterWriteRefusesStringAboveBound`, `CatalogDocumentWriterWriteRefusesLoneSurrogate`, `CatalogDocumentWriterWriteRefusesDocumentAboveBound` | `tests/Censio.Contracts.Tests/CatalogDocumentWriterTests.cs` |
| AC8 | `CatalogDocumentReaderReadReturnsFixedTextPerKind`, `CatalogDocumentReaderReadTextHoldsNoContent`, `CatalogDocumentReaderReadNeverThrows` (every refusal row of AC4–AC7 plus an empty input, a BOM, a top-level array and depth 65) | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |

The document at the bound is a valid one padded with spaces to 16,777,216 bytes. `CatalogDocumentReaderReadTextHoldsNoContent` refuses documents holding a marker name, region type and reference in each kind and asserts the text contains none of them.

## Approach

**Document (`Censio.Contracts`).** JSON, UTF-8 without BOM, compact. Top level `schemaVersion` (1), `gameVersion` (string or `null`), `entries` (array). Each entry: `guid`, `kind` (`region`, `good`, `workforce`, `building`), `names` (object, language tag → name, only languages with a name), `regionGuid` (number or `null`), `regionTypes` (array or `null`), `icon` (64 lowercase hex or `null`). Every property is required and always written. The model is plain sealed classes with `required` `init` properties (*Signatures*); `kind` goes through an internal `JsonConverter<CatalogEntryKind>` that reads exactly the four lowercase words (anything else throws `JsonException` → *malformed*) and writes them. An internal `CatalogDocumentJsonContext : JsonSerializerContext` with `[JsonSerializable(typeof(CatalogDocument))]` and `[JsonSourceGenerationOptions]`: camel case, `UnmappedMemberHandling = Disallow`, `AllowDuplicateProperties = false`, `RespectNullableAnnotations = true`, strict number handling, case-sensitive names, no comments or trailing commas. Every serializer call passes the context's type info, never options alone.

**Reader** (`CatalogDocumentReader.Read`, bytes in, result out, the spec's five steps):
1. Length above `MaxDocumentBytes` → *too large*.
2. One pass of `Utf8JsonReader` over the whole input (default depth 64). For each string and property-name token: not escaped — raw length above `MaxStringBytes` → *too large*, `Utf8.IsValid` false → *malformed*; escaped — `CopyString` into `buffer.AsSpan(0, MaxStringBytes)` of one rented buffer (`ArgumentException` → *too large*, `InvalidOperationException` → *malformed*), then `Utf8.IsValid` on the result; the buffer is returned with `clearArray: true`. The same pass counts, matching property names with `ValueTextEquals` (which unescapes): the items of the root's `entries` array (above `MaxEntries` → *too large*), and per item the properties of its `names` object (`MaxNamesPerEntry`) and the items of its `regionTypes` array (`MaxRegionTypesPerEntry`), whatever the version; and it finds `schemaVersion` with `ValueTextEquals` among the root's own properties only (`CurrentDepth` 1): a root that is no object, a missing or repeated `schemaVersion`, or a value that is not a number token with `TryGetInt32` → *malformed*.
3. Version ≠ 1 → *unsupported version*, text with the number (invariant culture).
4. `JsonSerializer.Deserialize` with the context → `JsonException` → *malformed*.
5. Internal `CatalogDocumentRules`: first a sweep for `null` in places the model forbids that the serializer does not check (an entry, a name value, a region type) → *malformed*; then the value rules of E4 → *invalid*.

`JsonException`, `InvalidOperationException` and `ArgumentException` are caught in steps 2 and 4 and mapped as above; an out-of-memory condition is not caught. The result holds the document or an error, never both.

**Writer** (`CatalogDocumentWriter.Write`): `SchemaVersion` ≠ 1 → *unsupported version* (the reader's text); the count bounds → *too large*; then the same `CatalogDocumentRules` (*invalid*, `null` included here, as the writer has no *malformed* input), an undefined kind → *invalid*, every string encoded with a throwing `UTF8Encoding` (a lone surrogate → *invalid*, more than `MaxStringBytes` bytes → *too large*). It serializes a normalized copy (names in a `SortedDictionary` with `StringComparer.Ordinal`) to a byte array with the default `JavaScriptEncoder` (non-ASCII escaped; deterministic), and refuses a result above `MaxDocumentBytes` (*too large*); on any refusal it returns no bytes.

**Value rules**, in `CatalogDocumentRules`, with ASCII-only checks (`char.IsAsciiLetterLower`, `char.IsAsciiDigit`, `char.IsAsciiHexDigitLower` or explicit ranges; never `char.IsLower`, `IsLetter`, `IsDigit`, `int.Parse` with default styles or `Convert.FromHexString`). Language tag: 1 to 35 characters; a primary subtag of 2 or 3 of `a`–`z`, then subtags of 1 to 8 of `a`–`z` or `0`–`9`, each after one `-`. **Game version**: four parts of ASCII digits joined by `.`, each 0 to 65,535, `0` or no leading zero, not all four `0`. **Icon**: exactly 64 of `0`–`9`, `a`–`f`.

**Converter (`Censio.Companion.Core`)** `GameCatalogDocumentConverter.ToDocument`: for each entry of `catalog.Entries` in order, keep it only when its GUID is not `0` and `ReferenceEquals(catalog.Find(entry.AssetGuid), entry)` (this leaves out repeated and ambiguous GUIDs); map the kind; names `en` from `EnglishName`, `de` from `GermanName`, each left out when null or empty; `RegionGuid` and `RegionTypes` as they are (the list copied); `icon` = `Convert.ToHexStringLower(SHA256.HashData(png))` of `icons?.Find(guid)`, computed once per `GameIcon` instance; `GameVersion` = the build. It validates nothing; the writer does.

No user-visible output.

## Options considered

- Icon data in the document, or references by index or file name: record 0055. A tolerant reader or a major/minor version: record 0056.
- Reflection-based serialization: excluded by the issue.
- `JsonStringEnumConverter`: reads case-insensitively and accepts numbers; one strict converter instead.
- Wire types with string kinds beside a typed model: no rule needs them once the kind converter is strict.
- The converter in `Censio.GameData`: it would depend on the contract for this alone.

## Affected projects and types

| Project | Type / file | Change |
| ------- | ----------- | ------ |
| Censio.Contracts | `CatalogDocument`, `CatalogDocumentEntry`, `CatalogEntryKind`, `CatalogDocumentReader`, `CatalogDocumentWriter`, `CatalogDocumentReadResult`, `CatalogDocumentWriteResult`, `CatalogDocumentError`, `CatalogDocumentErrorKind` | new, public |
| Censio.Contracts | `CatalogDocumentJsonContext`, `CatalogEntryKindJsonConverter`, `CatalogDocumentRules` | new, internal |
| Censio.Companion.Core | `GameCatalogDocumentConverter` | new |
| tests | `Censio.Contracts.Tests.csproj` | `<JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>` |
| tests | `Censio.Companion.Core.Tests.csproj` | `<None Include="..\Censio.GameData.Tests\Fixtures\GameCatalog\*.xml" Link="Fixtures\GameCatalog\%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />` |

No new package: System.Text.Json and SHA-256 are part of the shared framework.

## Signatures (for the Dev's skeleton)

Namespace Censio.Contracts. The JSON names of *Approach* are set with JsonPropertyName attributes on the properties.

- `public sealed class CatalogDocument`
- `public const int CurrentSchemaVersion = 1;`
- `public required int SchemaVersion { get; init; }`
- `public required string? GameVersion { get; init; }`
- `public required IReadOnlyList<CatalogDocumentEntry> Entries { get; init; }`
- `public sealed class CatalogDocumentEntry`
- `public required int AssetGuid { get; init; }`
- `public required CatalogEntryKind Kind { get; init; }`
- `public required IReadOnlyDictionary<string, string> Names { get; init; }`
- `public required int? RegionGuid { get; init; }`
- `public required IReadOnlyList<string>? RegionTypes { get; init; }`
- `public required string? IconReference { get; init; }`
- `public enum CatalogEntryKind { Region, Good, Workforce, Building }`
- `public enum CatalogDocumentErrorKind { Malformed, UnsupportedVersion, Invalid, TooLarge }`
- `public sealed class CatalogDocumentError`, with `public CatalogDocumentErrorKind Kind { get; }` (as above) and
  `public string Text { get; }`
- `public sealed class CatalogDocumentReadResult`
- `public CatalogDocument? Document { get; }`
- `public CatalogDocumentError? Error { get; }` (also on the write result)
- `public sealed class CatalogDocumentWriteResult`
- `public ReadOnlyMemory<byte> Utf8Json { get; }`
- `public static class CatalogDocumentReader`
- `public const int MaxDocumentBytes = 16777216;`
- `public const int MaxStringBytes = 65536;`
- `public const int MaxEntries = 16384;`
- `public const int MaxNamesPerEntry = 16;`
- `public const int MaxRegionTypesPerEntry = 32;`
- `public static CatalogDocumentReadResult Read(ReadOnlySpan<byte> utf8Json)`
- `public static class CatalogDocumentWriter`
- `public static CatalogDocumentWriteResult Write(CatalogDocument document)`

Namespace Censio.Companion.Core:

- `public static class GameCatalogDocumentConverter`
- `public static CatalogDocument ToDocument(GameCatalog catalog, GameIconSet? icons, string? build)`

The constructors and factories of the result and error types are internal. No name ends in Flags or Enum; no nested
type.

## Test files

- `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs`
- `tests/Censio.Contracts.Tests/CatalogDocumentWriterTests.cs`
- `tests/Censio.Companion.Core.Tests/GameCatalogDocumentConverterTests.cs`

Existing test code that calls a changed signature: none.

Files of the change no unit test can cover: none.

## Areas

New area **game-catalog-format** — the catalog document for the server: properties, value rules, check order, bounds, refusal kinds and texts, compatibility rules, privacy; not here: building the catalog (game-catalog), the icons (game-catalog-icons), the upload (#42). `game-catalog`, `game-catalog-cache` and `game-files` point to it.

## Documentation updates

All by the Dev once the code is green, unless noted:

- `docs/areas/game-catalog-format.md` (new, from `_template.md`): the content of *Approach* as rules (no type names as contract), a JSON example of invented values (two entries, one with an icon), the five bounds with their values, the kind table with the spec's exact texts, the five-step order, the bounds, the compatibility rules (spec N2), privacy (spec N5); *Related decisions* 0055, 0056, 0018, 0033.
- `docs/areas/README.md`: the new row; in the rows of game-catalog and game-catalog-cache, "the catalog format for the server (#26)" → "the catalog format for the server (see game-catalog-format)".
- `docs/areas/game-catalog.md:224`, `docs/areas/game-catalog-cache.md:94`: "The catalog format for the server and the upload (#26, #42)." → "The catalog format for the server: [`game-catalog-format.md`](game-catalog-format.md); the upload (#42)."; `docs/areas/game-files.md:144`: "the catalog format (#26)" → "the catalog format ([`game-catalog-format.md`](game-catalog-format.md))".
- `docs/ARCHITECTURE.md:19`: Contracts row → "The types shared by Companion and server: the catalog document with its reader and writer ([`areas/game-catalog-format.md`](areas/game-catalog-format.md)), and the product version"; row 24: add "the conversion of the catalog into the catalog document"; after the paragraph at line 59 one sentence: the catalog converts into the versioned catalog document of `Censio.Contracts`, which nothing sends yet (#42) (records 0055, 0056).
- `tests/Censio.Companion.Core.Tests/Fixtures/README.md` (Tester, new file): the linked `GameCatalog/*.xml` files come from `tests/Censio.GameData.Tests/Fixtures/GameCatalog/` and are described in its `README.md`; there is no `Fixtures/GameCatalog` source folder here.
- `.squad/project.md` (the Lead reports, the orchestrator writes), in *Security areas*, line *Parsing of external input*: after "the catalog cache file)," insert "the catalog document (`CatalogDocumentReader` in `src/Censio.Contracts`)," and after "[game-catalog-cache](../docs/areas/game-catalog-cache.md)," insert "[game-catalog-format](../docs/areas/game-catalog-format.md),"; in *Integration surface*, *A new external API call or DTO*, "the ingest messages in `Censio.Contracts`" → "the catalog document and the ingest messages in `Censio.Contracts`".
- `README.md`, `docs/USER_GUIDE.md`: none (spec, *Open questions*). No changelog exists.

## Architecture check

No new project reference. No guarantee of `.squad/project.md` changes: nothing is sent, stored or shown. Records 0018 (refuse an unsupported version), 0033 (strict) and 0023 (privacy) are kept.

## Security considerations

- [x] The document bound applies to the raw bytes before parsing, the string bound to the unescaped UTF-8 bytes before a .NET string exists, the count bounds before deserializing; invalid UTF-8 is refused, never replaced; the writer counts the same.
- [x] `JsonException`, `InvalidOperationException` and `ArgumentException` are mapped to a kind with its fixed text; no exception message reaches a result.
- Depth at most 64. Memory: the input (at most 16 MiB), one rented 64 KiB buffer and the model at the bounds (fact 11).
- The SHA-256 is an identity, not a security control (record 0055).

## Decision records

- `docs/decisions/0055-catalog-document-refers-to-icons-by-content-hash.md` (Proposed)
- `docs/decisions/0056-catalog-document-read-strictly-by-exact-schema-version.md` (Proposed)

## Out of scope / follow-ups

Sending, storing and showing the catalog and the images (#27, #42), as the spec's *Out of scope* lists. For #42: the receiving side must bound a message's size while receiving it, before the reader runs, and how many documents it reads at once. No follow-up issue.

## Challenge

- DA 1 (minor): accepted; the converter keeps an entry only when its GUID is not `0` and `catalog.Find` returns it; new AC1 test; spec AC1 amended.
- DA 2 (minor): accepted; the writer refuses another schema version as *unsupported version*, own AC4 test; spec AC4 amended.
- DA 3 (minor): accepted; the Tester creates the fixture README (task 8).

## Security review

- 1 (blocking, model memory): resolved. Step 2 counts entries (16,384), names per entry (16) and region types per entry (32) before deserializing; the document bound drops to 16 MiB. The real catalog has 1,040 entries and 2 names. Spec E5 and AC7 amended; tests at bound and bound plus one, and one that a count is refused before deserialization; fact 11 measures the peak.
- 2: `InvalidOperationException` → *malformed*; `schemaVersion` found with `ValueTextEquals` at depth 1; AC4 rows.
- 3: ASCII-only rules in *Approach*; AC6 rows; spec E4 says ASCII digits.
- 4: `AsSpan(0, MaxStringBytes)`, `clearArray: true`.
- 5: noted under *Out of scope / follow-ups*. Facts confirmed by Security are merged into fact 9.
