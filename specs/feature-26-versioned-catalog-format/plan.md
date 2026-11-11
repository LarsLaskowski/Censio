# Plan: Versioned catalog format

Source: Issue #26 | [spec.md](spec.md)
Tier: standard — a new format with its reader and writer: input handling for a later network reader (size bounds, a parser's validation), no security control created or changed.
Security areas touched: Parsing of external input (the new catalog document reader in `src/Censio.Contracts`, which the server will run on WebSocket input, #27/#42); build configuration (two test project files: a fixture link and a runtime feature switch); `.squad/project.md` (*Security areas*, an entry extended).

## Problem / root cause

Feature: see [spec.md](spec.md); its criteria AC1–AC8 are the plan's.

## Verified facts

| # | Claim | Evidence | Status |
| - | ----- | -------- | ------ |
| 1 | `Censio.Contracts` holds only `ProductVersion` and references no project | `src/Censio.Contracts/Censio.Contracts.csproj`, `docs/ARCHITECTURE.md:19` | confirmed |
| 2 | The builder can produce an entry of GUID `0` | `GameCatalogAssets.cs:74` skips only a missing GUID; `GameCatalogBuilder.cs:36,90` `GetValueOrDefault()`, no filter | confirmed |
| 3 | `GameCatalog` accepts repeated GUIDs and `Find` answers the first; the builder's entries are unique and exclude ambiguous GUIDs | `GameCatalog.cs` constructor (`TryAdd`); `GameCatalogBuilder.cs:37-58` | confirmed |
| 4 | Region entries carry a non-zero `RegionGuid`, other kinds none | `GameCatalogBuilder.cs:92-121` | confirmed |
| 5 | A missing name is `null`; `GameCatalog.Check` treats an empty one as missing too | `GameCatalog.cs` `Check` (`IsNullOrEmpty`); `game-catalog.md` *Names* | confirmed |
| 6 | The build is `null` (unknown) or four non-negative numbers, not all zero, formatted invariantly without leading zeros | `GameInstallation.cs:57-65` `FormatBuild` | confirmed |
| 7 | A real file version has 16-bit parts, so a real build fits the game-version rule | Win32 `VS_FIXEDFILEINFO` (two 32-bit fields per pair) | confirmed |
| 8 | Entries that name one icon file share one `GameIcon`; `GameIconSet.Find` gives the icon per GUID | `GameIconSet.cs`; `game-catalog-icons.md` *Entries* | confirmed |
| 9 | .NET 11 has `JsonSourceGenerationOptionsAttribute.AllowDuplicateProperties`, `RespectNullableAnnotations`, `UnmappedMemberHandling`, and `Convert.ToHexStringLower` | `/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/11.0.0/ref/net11.0/System.Text.Json.xml`, `System.Runtime.xml` | confirmed |
| 10 | `AllowDuplicateProperties = false` also refuses a repeated dictionary key | to verify: AC5 duplicate-language case, Tester, step 5; if it fails, the Dev adds a per-object name check to step 2 | unverified |
| 11 | `Utf8JsonReader.CopyString` throws `ArgumentException` when the unescaped value exceeds the buffer, and an escaped lone surrogate (`\uD800`) is refused by it or by the UTF-8 check after it | to verify: AC5 lone-surrogate and AC7 escaped-string cases, Tester, step 5 | unverified |
| 12 | Deserializing `1.0`, `1e0`, `"1"` or 2147483648 into an `int` throws `JsonException` under strict number handling | to verify: AC4 and AC5 cases, Tester, step 5 | unverified |
| 13 | Switching reflection-based serialization off in `Censio.Companion.Core.Tests` would break `GameInstallationSearchTests.cs:871` (`JsonSerializer.Serialize` without a context) | `tests/Censio.Companion.Core.Tests/GameInstallationSearchTests.cs:871` | confirmed |
| 14 | `Censio.Companion.Core.Tests` copies only its own `Fixtures\**`; the catalog fixture lies in `tests/Censio.GameData.Tests/Fixtures/GameCatalog/` | `Censio.Companion.Core.Tests.csproj:19` | confirmed |
| 15 | `GameCatalogReader.Read` builds a catalog from six stream sources, usable on the loose fixture files | `GameCatalogReader.cs:72`, `GameCatalogSource.FromStream` | confirmed |
| 16 | The README already says nothing is sent to the server | `README.md`, *Status* | confirmed |

## Tests per criterion

| AC | Test(s) | Test file |
| -- | ------- | --------- |
| AC1 | `GameCatalogDocumentConverterToDocumentMapsEveryField`, `GameCatalogDocumentConverterToDocumentSharesReferenceOfSharedImage`, `GameCatalogDocumentConverterToDocumentWithoutIconSetHasNoReferences`, `GameCatalogDocumentConverterToDocumentLeavesOutGuidZero`, `GameCatalogDocumentConverterToDocumentKeepsFirstOfRepeatedGuid`, `GameCatalogDocumentConverterToDocumentWritesUnknownBuildAsNull`, `GameCatalogDocumentConverterToDocumentIsAcceptedByWriter` | `tests/Censio.Companion.Core.Tests/GameCatalogDocumentConverterTests.cs` |
| AC2 | `CatalogDocumentWriterWriteRoundTripsEveryField` (rows of the spec's cases), `CatalogDocumentWriterWriteTwiceGivesIdenticalBytes`, `CatalogDocumentWriterWriteOrdersLanguagesOrdinally`, `CatalogDocumentWriterWriteRoundTripsEmptyCatalog`, `CatalogDocumentWriterReflectionSerializationIsDisabled` | `tests/Censio.Contracts.Tests/CatalogDocumentWriterTests.cs` |
| AC3 | `GameCatalogDocumentConverterFixtureCatalogRoundTripsValid` | `tests/Censio.Companion.Core.Tests/GameCatalogDocumentConverterTests.cs` |
| AC4 | `CatalogDocumentReaderReadRefusesOtherVersionAsUnsupported` (0, 2, -1), `CatalogDocumentReaderReadRefusesLaterVersionWithUnknownPropertyAsUnsupported`, `CatalogDocumentReaderReadRefusesMalformedVersion` (missing, `1.0`, `1e0`, `"1"`, 2147483648, repeated), `CatalogDocumentReaderReadRefusesTruncatedVersionTwoAsMalformed` | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |
| AC5 | `CatalogDocumentReaderReadRefusesMalformedSyntax` (E1 rows), `CatalogDocumentReaderReadRefusesMalformedStructure` (E2 rows: unknown, repeated, missing, other-case property, wrong type, `null`, GUID 2147483648 and `5.0`, kind `Good` and `castle`, repeated language), `CatalogDocumentReaderReadRefusesInvalidUtf8`, `CatalogDocumentReaderReadRefusesLoneSurrogate` | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |
| AC6 | `CatalogDocumentReaderReadRefusesInvalidValue` (E4 rows, `EN`, `pt-BR`, `e`, a 36-character tag), `CatalogDocumentReaderReadAcceptsLanguageTag` (`en`, `de`, `pt-br`), `CatalogDocumentReaderReadAcceptsNegativeGuid` | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |
| AC7 | `CatalogDocumentReaderReadRefusesDocumentAboveBound`, `CatalogDocumentReaderReadAcceptsDocumentAtBound`, `CatalogDocumentReaderReadRefusesStringAboveBound`, `CatalogDocumentReaderReadAcceptsStringAtBound`, `CatalogDocumentReaderReadAcceptsEscapedStringWithinBound` | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |
| AC7 | `CatalogDocumentWriterWriteRefusesInvalidDocument` (E4 rows, undefined kind, schema version 2), `CatalogDocumentWriterWriteRefusesStringAboveBound`, `CatalogDocumentWriterWriteRefusesLoneSurrogate`, `CatalogDocumentWriterWriteRefusesDocumentAboveBound` | `tests/Censio.Contracts.Tests/CatalogDocumentWriterTests.cs` |
| AC8 | `CatalogDocumentReaderReadReturnsFixedTextPerKind`, `CatalogDocumentReaderReadTextHoldsNoContent`, `CatalogDocumentReaderReadNeverThrows` (every refusal row of AC4–AC7 plus an empty input, a BOM, a top-level array and depth 65) | `tests/Censio.Contracts.Tests/CatalogDocumentReaderTests.cs` |

The document at the bound is a valid document padded with spaces to exactly 67,108,864 bytes; only the kinds are asserted (spec AC7). `CatalogDocumentReaderReadTextHoldsNoContent` refuses documents holding a marker name, region type and reference in each kind and asserts the text contains none of them.

## Approach

**Document (`Censio.Contracts`).** JSON, UTF-8 without BOM, compact. Top level `schemaVersion` (1), `gameVersion` (string or `null`), `entries` (array). Each entry: `guid`, `kind` (`region`, `good`, `workforce`, `building`), `names` (object, language tag → name, only languages with a name), `regionGuid` (number or `null`), `regionTypes` (array or `null`), `icon` (64 lowercase hex or `null`). Every property is required and always written. The model is plain sealed classes with `required` `init` properties (*Signatures*); `kind` goes through an internal `JsonConverter<CatalogEntryKind>` that reads exactly the four lowercase words (anything else throws `JsonException` → *malformed*) and writes them. An internal `CatalogDocumentJsonContext : JsonSerializerContext` with `[JsonSerializable(typeof(CatalogDocument))]` and `[JsonSourceGenerationOptions]`: camel case, `UnmappedMemberHandling = Disallow`, `AllowDuplicateProperties = false`, `RespectNullableAnnotations = true`, strict number handling, case-sensitive names, no comments or trailing commas. Every serializer call passes the context's type info, never options alone.

**Reader** (`CatalogDocumentReader.Read`, bytes in, result out, the spec's five steps):
1. Length above `MaxDocumentBytes` → *too large*.
2. One pass of `Utf8JsonReader` over the whole input (default depth 64). For each string and property-name token: not escaped — raw length above `MaxStringBytes` → *too large*, `Utf8.IsValid` false → *malformed*; escaped — `CopyString` into one rented buffer of `MaxStringBytes` bytes (`ArgumentException` → *too large*), then `Utf8.IsValid` on the result. The same pass finds the root's `schemaVersion`: a root that is no object, a missing or repeated `schemaVersion`, or a value that is not a number token with `TryGetInt32` → *malformed*.
3. Version ≠ 1 → *unsupported version*, text with the number (invariant culture).
4. `JsonSerializer.Deserialize` with the context → `JsonException` → *malformed*.
5. Internal `CatalogDocumentRules`: first a sweep for `null` in places the model forbids that the serializer does not check (an entry, a name value, a region type) → *malformed*; then the value rules of E4 → *invalid*.

`JsonException`, `InvalidOperationException` and `ArgumentException` are caught in steps 2 and 4 and mapped as above; an out-of-memory condition is not caught. The result holds the document or an error, never both.

**Writer** (`CatalogDocumentWriter.Write`): `SchemaVersion` ≠ 1 → *unsupported version*; then the same `CatalogDocumentRules` (*invalid*, `null` included here, as the writer has no *malformed* input), an undefined kind → *invalid*, every string encoded with a throwing `UTF8Encoding` (a lone surrogate → *invalid*, more than `MaxStringBytes` bytes → *too large*). It serializes a normalized copy (names in a `SortedDictionary` with `StringComparer.Ordinal`) to a byte array with the default `JavaScriptEncoder` (non-ASCII escaped; deterministic), and refuses a result above `MaxDocumentBytes` (*too large*); on any refusal it returns no bytes.

**Language tag rule**, in `CatalogDocumentRules`: 1 to 35 characters; a primary subtag of 2 or 3 of `a`–`z`, then subtags of 1 to 8 of `a`–`z` or `0`–`9`, each after one `-`. **Game version**: four parts of ASCII digits joined by `.`, each 0 to 65,535, `0` or no leading zero, not all four `0`. **Icon**: exactly 64 of `0`–`9`, `a`–`f`.

**Converter (`Censio.Companion.Core`)** `GameCatalogDocumentConverter.ToDocument`: for each entry of `catalog.Entries` in order, skip GUID `0` and a GUID already taken; map the kind; names `en` from `EnglishName`, `de` from `GermanName`, each left out when null or empty; `RegionGuid` and `RegionTypes` as they are (the list copied); `icon` = `Convert.ToHexStringLower(SHA256.HashData(png))` of `icons?.Find(guid)`, computed once per `GameIcon` instance; `GameVersion` = the build. It validates nothing; the writer does.

No user-visible output.

## Options considered

- Icon data in the document, references by index or by file name: lost to the content hash (record 0055).
- A tolerant reader or a major/minor version: lost to exact versions and strict reading (record 0056).
- Reflection-based serialization: excluded by the issue (source generation).
- `JsonStringEnumConverter` for `kind`: reads case-insensitively and accepts numbers, against strict reading; one small converter instead.
- Two layers (wire DTOs with string kinds, then a typed model): no rule needs them once the kind converter refuses unknown words; the spec moved an unknown kind to *malformed*.
- The converter in `Censio.GameData` (referencing `Censio.Contracts`): `Censio.GameData` would depend on the contract only for this; `Censio.Companion.Core` already references both.
- Own duplicate-name detection in step 2: unneeded while fact 10 holds; it is the fallback.

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
- `public const int MaxDocumentBytes = 67108864;`
- `public const int MaxStringBytes = 65536;`
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

- `docs/areas/game-catalog-format.md` (new, from `_template.md`): the content of *Approach* as rules (no type names as contract), a JSON example of invented values (two entries, one with an icon), the kind table with the spec's exact texts, the five-step order, the bounds, the compatibility rules (spec N2), privacy (spec N5); *Related decisions* 0055, 0056, 0018, 0033.
- `docs/areas/README.md`: the new row; in the rows of game-catalog and game-catalog-cache, "the catalog format for the server (#26)" → "the catalog format for the server (see game-catalog-format)".
- `docs/areas/game-catalog.md:224`, `docs/areas/game-catalog-cache.md:94`: "The catalog format for the server and the upload (#26, #42)." → "The catalog format for the server: [`game-catalog-format.md`](game-catalog-format.md); the upload (#42)."; `docs/areas/game-files.md:144`: "the catalog format (#26)" → "the catalog format ([`game-catalog-format.md`](game-catalog-format.md))".
- `docs/ARCHITECTURE.md:19`: Contracts row → "The types shared by Companion and server: the catalog document with its reader and writer ([`areas/game-catalog-format.md`](areas/game-catalog-format.md)), and the product version"; row 24: add "the conversion of the catalog into the catalog document"; after the paragraph at line 59 one sentence: the catalog converts into the versioned catalog document of `Censio.Contracts`, which nothing sends yet (#42) (records 0055, 0056).
- `tests/Censio.Companion.Core.Tests/Fixtures/README.md` (Tester): the linked `GameCatalog/*.xml` files, their source folder and that they are the GameData fixture, not copies.
- `.squad/project.md` (the Lead reports, the orchestrator writes), in *Security areas*, line *Parsing of external input*: after "the catalog cache file)," insert "the catalog document (`CatalogDocumentReader` in `src/Censio.Contracts`)," and after "[game-catalog-cache](../docs/areas/game-catalog-cache.md)," insert "[game-catalog-format](../docs/areas/game-catalog-format.md),"; in *Integration surface*, *A new external API call or DTO*, "the ingest messages in `Censio.Contracts`" → "the catalog document and the ingest messages in `Censio.Contracts`".
- `README.md`, `docs/USER_GUIDE.md`: none (spec, *Open questions*). No changelog exists.

## Architecture check

`Censio.Contracts` keeps no project reference; `Censio.Companion.Core` already references Contracts and GameData. No guarantee of `.squad/project.md` changes: nothing is sent, stored or shown; the document stays in memory. Record 0018's rule (refuse an unsupported version) is applied, record 0033's strictness kept, record 0023's privacy kept (no content in texts or logs).

## Security considerations

- [x] The document bound applies to the raw bytes before parsing; the string bound to the unescaped UTF-8 bytes, checked before a .NET string is created; invalid UTF-8 is refused, never replaced by U+FFFD; the writer counts the same unescaped UTF-8 bytes.
- [x] Exceptions: `JsonException`, `InvalidOperationException`, `ArgumentException` of the reader and serializer are mapped to a kind with its fixed text; none of their messages reaches a result. Nothing in this feature logs or shows a text yet.
- Depth: at most 64 (the reader's default), deeper is *malformed*. Memory: the input, one rented 64 KiB buffer and the model.
- The SHA-256 is an identity of the image, not a security control (record 0055).

## Decision records

- `docs/decisions/0055-catalog-document-refers-to-icons-by-content-hash.md` (Proposed)
- `docs/decisions/0056-catalog-document-read-strictly-by-exact-schema-version.md` (Proposed)

## Out of scope / follow-ups

Sending, storing and showing the catalog and the images (#27, #42), as the spec's *Out of scope* lists. No follow-up issue.
