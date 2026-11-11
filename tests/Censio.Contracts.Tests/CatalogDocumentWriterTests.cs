using System.Text;

namespace Censio.Contracts.Tests;

/// <summary>
/// Tests for <see cref="CatalogDocumentWriter" />.
/// </summary>
[TestClass]
public sealed class CatalogDocumentWriterTests
{
    #region Constants

    /// <summary>
    /// A valid icon reference: 64 lowercase hex digits.
    /// </summary>
    private const string Icon = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// The fixed text of a document that breaks a value rule.
    /// </summary>
    private const string InvalidText = "Catalog document breaks a rule of schema version 1";

    /// <summary>
    /// The fixed text of a document above a size bound.
    /// </summary>
    private const string TooLargeText = "Catalog document exceeds a size bound";

    /// <summary>
    /// A 36 character language tag, one above the bound.
    /// </summary>
    private const string Tag36 = "en-abcdefgh-abcdefgh-abcdefgh-abcdef";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the documents to round trip: the name of the case and the document.
    /// </summary>
    private static IEnumerable<object[]> RoundTripRows
    {
        get
        {
            yield return new object[] { "two languages, region, types, icon, game version", Document("1.2.3.4", Entry(5, CatalogEntryKind.Region, Names(("en", "Wood"), ("de", "Holz")), 7, ["Coast", "Land"], Icon)) };
            yield return new object[] { "one language", Document(null, Entry(5, CatalogEntryKind.Good, Names(("de", "Holz")))) };
            yield return new object[] { "no language", Document(null, Entry(5, CatalogEntryKind.Workforce, Names())) };
            yield return new object[] { "non-ASCII names", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", "Bäume"), ("de", "Größe 木材 🌲")), null, null, null)) };
            yield return new object[] { "negative GUID and region GUID", Document(null, Entry(-5, CatalogEntryKind.Region, Names(("en", "Wood")), -7, null, null)) };
            yield return new object[] { "extreme GUIDs", Document(null, Entry(int.MinValue, CatalogEntryKind.Region, Names(("en", "Wood")), int.MaxValue, null, null), Entry(int.MaxValue, CatalogEntryKind.Good, Names())) };
            yield return new object[] { "region types not stated", Document(null, Entry(5, CatalogEntryKind.Region, Names(("en", "Wood")), 7, null, null)) };
            yield return new object[] { "region types none", Document(null, Entry(5, CatalogEntryKind.Region, Names(("en", "Wood")), 7, [], null)) };
            yield return new object[] { "region types in their order", Document(null, Entry(5, CatalogEntryKind.Region, Names(("en", "Wood")), 7, ["Zeta", "Alpha", "mid"], null)) };
            yield return new object[] { "game version null", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", "Wood")))) };
            yield return new object[] { "highest game version", Document("65535.65535.65535.65535") };
            yield return new object[] { "entry with and without an icon", Document("0.0.0.1", Entry(1, CatalogEntryKind.Good, Names(("en", "A")), null, null, Icon), Entry(2, CatalogEntryKind.Good, Names(("en", "B")), null, null, null)) };
            yield return new object[] { "no entries", Document(null) };
            yield return new object[] { "all kinds in their order", Document("1.0.0.0", Entry(4, CatalogEntryKind.Building, Names(("en", "D"))), Entry(3, CatalogEntryKind.Workforce, Names(("en", "C"))), Entry(2, CatalogEntryKind.Good, Names(("en", "B"))), Entry(1, CatalogEntryKind.Region, Names(("en", "A")), 9, ["T"], null)) };
            yield return new object[] { "characters JSON escapes", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", "Quote \" back\\slash tab\t line\n <>&'")))) };
            yield return new object[] { "long tags", Document(null, Entry(5, CatalogEntryKind.Good, Names(("pt-br", "A"), ("zh-hans-cn", "B"), ("en-abcdefgh-abcdefgh-abcdefgh-abcde", "C")))) };
        }
    }

    /// <summary>
    /// Gets the documents that break a value rule or hold a null: the name of the case and the document.
    /// </summary>
    private static IEnumerable<object[]> InvalidRows
    {
        get
        {
            yield return new object[] { "GUID 0", Document(null, Entry(0, CatalogEntryKind.Good, Names(("en", "A")))) };
            yield return new object[] { "GUID on two entries", Document(null, Entry(5, CatalogEntryKind.Good, Names()), Entry(5, CatalogEntryKind.Workforce, Names())) };
            yield return new object[] { "region without region GUID", Document(null, Entry(5, CatalogEntryKind.Region, Names(("en", "A")), null, null, null)) };
            yield return new object[] { "region with region GUID 0", Document(null, Entry(5, CatalogEntryKind.Region, Names(("en", "A")), 0, null, null)) };
            yield return new object[] { "region GUID on a good", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", "A")), 7, null, null)) };
            yield return new object[] { "region GUID on a workforce", Document(null, Entry(5, CatalogEntryKind.Workforce, Names(("en", "A")), 7, null, null)) };
            yield return new object[] { "region GUID on a building", Document(null, Entry(5, CatalogEntryKind.Building, Names(("en", "A")), 7, null, null)) };
            yield return new object[] { "undefined kind", Document(null, Entry(5, (CatalogEntryKind)99, Names(("en", "A")))) };
            yield return new object[] { "empty name", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", string.Empty)))) };
            yield return new object[] { "tag EN", Document(null, Entry(5, CatalogEntryKind.Good, Names(("EN", "A")))) };
            yield return new object[] { "tag pt-BR", Document(null, Entry(5, CatalogEntryKind.Good, Names(("pt-BR", "A")))) };
            yield return new object[] { "tag e", Document(null, Entry(5, CatalogEntryKind.Good, Names(("e", "A")))) };
            yield return new object[] { "empty tag", Document(null, Entry(5, CatalogEntryKind.Good, Names((string.Empty, "A")))) };
            yield return new object[] { "tag of 36 characters", Document(null, Entry(5, CatalogEntryKind.Good, Names((Tag36, "A")))) };
            yield return new object[] { "tag with sharp s", Document(null, Entry(5, CatalogEntryKind.Good, Names(("straße", "A")))) };
            yield return new object[] { "tag with underscore", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en_us", "A")))) };
            yield return new object[] { "tag with trailing hyphen", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en-", "A")))) };
            yield return new object[] { "tag with a digit as primary subtag", Document(null, Entry(5, CatalogEntryKind.Good, Names(("e1", "A")))) };
            yield return new object[] { "empty region type", Document(null, Entry(5, CatalogEntryKind.Region, Names(("en", "A")), 7, ["Coast", string.Empty], null)) };
            yield return new object[] { "icon with an uppercase digit", Document(null, Entry(5, CatalogEntryKind.Good, Names(), null, null, Icon[..63] + "F")) };
            yield return new object[] { "icon of 63 digits", Document(null, Entry(5, CatalogEntryKind.Good, Names(), null, null, Icon[..63])) };
            yield return new object[] { "icon of 65 digits", Document(null, Entry(5, CatalogEntryKind.Good, Names(), null, null, Icon + "0")) };
            yield return new object[] { "empty icon", Document(null, Entry(5, CatalogEntryKind.Good, Names(), null, null, string.Empty)) };
            yield return new object[] { "icon with a non-ASCII digit", Document(null, Entry(5, CatalogEntryKind.Good, Names(), null, null, Icon[..63] + "١")) };
            yield return new object[] { "empty game version", Document(string.Empty) };
            yield return new object[] { "game version all zero", Document("0.0.0.0") };
            yield return new object[] { "game version of three numbers", Document("1.2.3") };
            yield return new object[] { "game version of five numbers", Document("1.2.3.4.5") };
            yield return new object[] { "game version with a leading zero", Document("01.2.3.4") };
            yield return new object[] { "game version above 65535", Document("65536.0.0.0") };
            yield return new object[] { "game version with a sign", Document("+1.0.0.0") };
            yield return new object[] { "game version with a space", Document("1.0.0.0 ") };
            yield return new object[] { "game version with a non-ASCII digit", Document("١.0.0.0") };
            yield return new object[] { "null name", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", null!)))) };
            yield return new object[] { "null region type", Document(null, Entry(5, CatalogEntryKind.Region, Names(("en", "A")), 7, ["Coast", null!], null)) };
            yield return new object[] { "null entry", Document(null, null!) };
        }
    }

    /// <summary>
    /// Gets the documents with a string that is no well-formed Unicode text: the name of the case and the document.
    /// </summary>
    private static IEnumerable<object[]> LoneSurrogateRows
    {
        get
        {
            yield return new object[] { "high surrogate at the end of a name", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", "ab\ud800")))) };
            yield return new object[] { "high surrogate before a letter", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", "\ud800a")))) };
            yield return new object[] { "low surrogate alone", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", "\udc00")))) };
            yield return new object[] { "reversed pair", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", "\udc00\ud800")))) };
            yield return new object[] { "lone surrogate in a region type", Document(null, Entry(5, CatalogEntryKind.Region, Names(("en", "A")), 7, ["Co\ud800ast"], null)) };
            yield return new object[] { "lone surrogate in a tag", Document(null, Entry(5, CatalogEntryKind.Good, Names(("e\ud800", "A")))) };
            yield return new object[] { "lone surrogate in the game version", Document("1.2.3.\ud800") };
            yield return new object[] { "lone surrogate in the icon", Document(null, Entry(5, CatalogEntryKind.Good, Names(), null, null, Icon[..63] + "\ud800")) };
        }
    }

    /// <summary>
    /// Gets the documents with a string above the bound: the name of the case and the document.
    /// </summary>
    private static IEnumerable<object[]> LongStringRows
    {
        get
        {
            yield return new object[] { "name of 65,537 bytes", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", new string('a', CatalogDocumentReader.MaxStringBytes + 1))))) };
            yield return new object[] { "name of 65,538 bytes in two-byte characters", Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", new string('ä', (CatalogDocumentReader.MaxStringBytes / 2) + 1))))) };
            yield return new object[] { "region type of 65,537 bytes", Document(null, Entry(5, CatalogEntryKind.Region, Names(("en", "A")), 7, [new string('a', CatalogDocumentReader.MaxStringBytes + 1)], null)) };
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A written document reads back equal in every field and order.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="document">The document</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DynamicData(nameof(RoundTripRows))]
    public void CatalogDocumentWriterWriteRoundTripsEveryField(string label, CatalogDocument document)
    {
        // Arrange
        var written = CatalogDocumentWriter.Write(document);

        // Act
        var read = CatalogDocumentReader.Read(written.Utf8Json.Span);

        // Assert
        Assert.IsNull(written.Error, "The writer should accept the document: " + label);
        Assert.IsNull(read.Error, "The reader should accept what the writer wrote: " + label);
        AssertEqual(document, read.Document!, label);
    }

    /// <summary>
    /// Writing the same document twice, and writing the document read back, give identical bytes.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="document">The document</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DynamicData(nameof(RoundTripRows))]
    public void CatalogDocumentWriterWriteTwiceGivesIdenticalBytes(string label, CatalogDocument document)
    {
        // Arrange
        var first = CatalogDocumentWriter.Write(document);
        var read = CatalogDocumentReader.Read(first.Utf8Json.Span);

        // Act
        var second = CatalogDocumentWriter.Write(document);
        var again = CatalogDocumentWriter.Write(read.Document!);

        // Assert
        Assert.IsNull(first.Error, "The writer should accept the document: " + label);
        Assert.AreSequenceEqual(first.Utf8Json.ToArray(), second.Utf8Json.ToArray(), "The same document should give the same bytes: " + label);
        Assert.AreSequenceEqual(first.Utf8Json.ToArray(), again.Utf8Json.ToArray(), "The document read back should give the same bytes: " + label);
    }

    /// <summary>
    /// A document is written compact, with every property always present in a fixed order, null where there is no value, and no byte order mark.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CatalogDocumentWriterWriteProducesCompactCanonicalJson()
    {
        // Arrange
        var document = Document("1.2.3.4", Entry(5, CatalogEntryKind.Good, Names(("en", "Wood"))), Entry(-6, CatalogEntryKind.Region, Names(), 7, [], Icon));

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        var expected = "{\"schemaVersion\":1,\"gameVersion\":\"1.2.3.4\",\"entries\":["
                       + "{\"guid\":5,\"kind\":\"good\",\"names\":{\"en\":\"Wood\"},\"regionGuid\":null,\"regionTypes\":null,\"icon\":null},"
                       + "{\"guid\":-6,\"kind\":\"region\",\"names\":{},\"regionGuid\":7,\"regionTypes\":[],\"icon\":\"" + Icon + "\"}]}";

        Assert.IsNull(result.Error, "The document should be written");
        Assert.AreEqual(expected, Encoding.UTF8.GetString(result.Utf8Json.Span), "The JSON should be compact with every property in the documented order");
    }

    /// <summary>
    /// The languages of an entry are written in ordinal order of their tag, whatever their order in the document.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CatalogDocumentWriterWriteOrdersLanguagesOrdinally()
    {
        // Arrange
        var document = Document(null, Entry(5, CatalogEntryKind.Good, Names(("enb", "3"), ("pt-br", "5"), ("en-zz", "2"), ("de", "1"), ("en", "0"), ("zh", "6"))));

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        Assert.IsNull(result.Error, "The document should be written");
        Assert.Contains("\"names\":{\"de\":\"1\",\"en\":\"0\",\"en-zz\":\"2\",\"enb\":\"3\",\"pt-br\":\"5\",\"zh\":\"6\"}", Encoding.UTF8.GetString(result.Utf8Json.Span), "The languages should be in ordinal order of their tag");
    }

    /// <summary>
    /// Non-ASCII characters are written as escapes: every byte of the output is ASCII.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CatalogDocumentWriterWriteWritesOnlyAsciiBytes()
    {
        // Arrange
        var document = Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", "Bäume 木材 🌲"), ("de", "Größe")), null, null, null));

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        Assert.IsNull(result.Error, "The document should be written");
        Assert.AreEqual(0, result.Utf8Json.ToArray().Count(value => value >= 0x80), "Every byte of the JSON should be ASCII");
        Assert.AreEqual((byte)'{', result.Utf8Json.Span[0], "The JSON should start with its first brace, without a byte order mark");
    }

    /// <summary>
    /// An empty catalog is written with an empty entries list and read back empty.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CatalogDocumentWriterWriteRoundTripsEmptyCatalog()
    {
        // Arrange
        var document = Document(null);

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        Assert.IsNull(result.Error, "An empty catalog should be written");
        Assert.AreEqual("{\"schemaVersion\":1,\"gameVersion\":null,\"entries\":[]}", Encoding.UTF8.GetString(result.Utf8Json.Span), "An empty catalog should be written with an empty entries list");
        Assert.IsEmpty(CatalogDocumentReader.Read(result.Utf8Json.Span).Document!.Entries, "An empty catalog should read back empty");
    }

    /// <summary>
    /// A schema version other than 1 is refused as unsupported, with the text of the reader, and nothing is written.
    /// </summary>
    /// <param name="version">The schema version</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(2)]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MaxValue)]
    public void CatalogDocumentWriterWriteRefusesOtherSchemaVersionAsUnsupported(int version)
    {
        // Arrange
        var document = new CatalogDocument
                       {
                           SchemaVersion = version,
                           GameVersion = null,
                           Entries = []
                       };

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.UnsupportedVersion, $"Catalog schema version {version} is not supported; supported: 1", $"version {version}");
    }

    /// <summary>
    /// A document that breaks a value rule, or holds a null the version allows none of, is refused as invalid and nothing is written.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="document">The document</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DynamicData(nameof(InvalidRows))]
    public void CatalogDocumentWriterWriteRefusesInvalidDocument(string label, CatalogDocument document)
    {
        // Arrange
        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Invalid, InvalidText, label);
    }

    /// <summary>
    /// More than 16,384 entries, 16 names or 32 region types in one entry are refused as too large and nothing is written.
    /// </summary>
    /// <param name="entries">The number of entries</param>
    /// <param name="names">The number of names of the first entry</param>
    /// <param name="regionTypes">The number of region types of the first entry</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(16385, 1, 1)]
    [DataRow(1, 17, 1)]
    [DataRow(1, 1, 33)]
    public void CatalogDocumentWriterWriteRefusesCountsAboveBound(int entries, int names, int regionTypes)
    {
        // Arrange
        var document = BuildCountsDocument(entries, names, regionTypes);

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, $"{entries} entries, {names} names, {regionTypes} region types");
    }

    /// <summary>
    /// A document with every count at its bound is written and read back whole.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentWriterWriteAcceptsCountsAtBound()
    {
        // Arrange
        var document = BuildCountsDocument(CatalogDocumentReader.MaxEntries, CatalogDocumentReader.MaxNamesPerEntry, CatalogDocumentReader.MaxRegionTypesPerEntry);

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        Assert.IsNull(result.Error, "A document with every count at its bound should be written");

        var read = CatalogDocumentReader.Read(result.Utf8Json.Span);

        Assert.IsNull(read.Error, "The reader should accept what the writer wrote");
        AssertEqual(document, read.Document!, "counts at the bound");
    }

    /// <summary>
    /// A string above 65,536 UTF-8 bytes is refused as too large and nothing is written.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="document">The document</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DynamicData(nameof(LongStringRows))]
    public void CatalogDocumentWriterWriteRefusesStringAboveBound(string label, CatalogDocument document)
    {
        // Arrange
        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, label);
    }

    /// <summary>
    /// A name of exactly 65,536 bytes is written and read back whole.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentWriterWriteAcceptsStringAtBound()
    {
        // Arrange
        var name = new string('a', CatalogDocumentReader.MaxStringBytes);
        var document = Document(null, Entry(5, CatalogEntryKind.Good, Names(("en", name))));

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        Assert.IsNull(result.Error, "A name at the bound should be written");
        Assert.AreEqual(name, CatalogDocumentReader.Read(result.Utf8Json.Span).Document!.Entries[0].Names["en"], "The name should read back whole");
    }

    /// <summary>
    /// A string that is no well-formed Unicode text is refused as invalid and nothing is written.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="document">The document</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DynamicData(nameof(LoneSurrogateRows))]
    public void CatalogDocumentWriterWriteRefusesLoneSurrogate(string label, CatalogDocument document)
    {
        // Arrange
        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Invalid, InvalidText, $"lone surrogate: {label}");
    }

    /// <summary>
    /// A document that is above 16 MiB once written is refused as too large, although each count and string is within its bound.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentWriterWriteRefusesDocumentAboveBound()
    {
        // Arrange
        var name = new string('a', 1100);
        var entries = Enumerable.Range(1, CatalogDocumentReader.MaxEntries).Select(guid => Entry(guid, CatalogEntryKind.Good, Names(("en", name)))).ToArray();
        var document = Document(null, entries);

        // Act
        var result = CatalogDocumentWriter.Write(document);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, "more than 16 MiB written");
    }

    /// <summary>
    /// Builds a document.
    /// </summary>
    /// <param name="gameVersion">The game version</param>
    /// <param name="entries">The entries</param>
    /// <returns>The document of schema version 1</returns>
    private static CatalogDocument Document(string? gameVersion, params CatalogDocumentEntry[] entries)
    {
        return new CatalogDocument
               {
                   SchemaVersion = CatalogDocument.CurrentSchemaVersion,
                   GameVersion = gameVersion,
                   Entries = entries
               };
    }

    /// <summary>
    /// Builds an entry.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="kind">The kind</param>
    /// <param name="names">The names</param>
    /// <param name="regionGuid">The region GUID</param>
    /// <param name="regionTypes">The region types</param>
    /// <param name="icon">The icon reference</param>
    /// <returns>The entry</returns>
    private static CatalogDocumentEntry Entry(int guid, CatalogEntryKind kind, IReadOnlyDictionary<string, string> names, int? regionGuid = null, IReadOnlyList<string>? regionTypes = null, string? icon = null)
    {
        return new CatalogDocumentEntry
               {
                   AssetGuid = guid,
                   Kind = kind,
                   Names = names,
                   RegionGuid = regionGuid,
                   RegionTypes = regionTypes,
                   IconReference = icon
               };
    }

    /// <summary>
    /// Builds a names dictionary in the order given.
    /// </summary>
    /// <param name="names">The pairs of tag and name</param>
    /// <returns>The dictionary</returns>
    private static Dictionary<string, string> Names(params (string Tag, string Name)[] names)
    {
        return names.ToDictionary(pair => pair.Tag, pair => pair.Name);
    }

    /// <summary>
    /// Builds a document whose first entry is a region with a number of names and region types, followed by good entries.
    /// </summary>
    /// <param name="entries">The number of entries</param>
    /// <param name="names">The number of names of the first entry</param>
    /// <param name="regionTypes">The number of region types of the first entry</param>
    /// <returns>The document</returns>
    private static CatalogDocument BuildCountsDocument(int entries, int names, int regionTypes)
    {
        var first = Entry(1, CatalogEntryKind.Region, Enumerable.Range(0, names).ToDictionary(index => new string([(char)('a' + (index / 26)), (char)('a' + (index % 26))]), _ => "x"), 1, Enumerable.Range(0, regionTypes).Select(index => "T" + index).ToArray(), null);
        var others = Enumerable.Range(2, Math.Max(0, entries - 1)).Select(guid => Entry(guid, CatalogEntryKind.Good, Names(("en", "x"))));

        return Document(null, [first, .. others]);
    }

    /// <summary>
    /// Asserts that a result is a refusal of a kind and fixed text, with nothing written.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="kind">The expected kind</param>
    /// <param name="text">The expected text</param>
    /// <param name="context">What the case was, for the messages</param>
    private static void AssertRefusal(CatalogDocumentWriteResult result, CatalogDocumentErrorKind kind, string text, string context)
    {
        Assert.IsNotNull(result.Error, "The document should be refused: " + context);
        Assert.AreEqual(kind, result.Error.Kind, "The refusal should have the expected kind: " + context);
        Assert.AreEqual(text, result.Error.Text, "The refusal should have the fixed text of its kind: " + context);
        Assert.IsTrue(result.Utf8Json.IsEmpty, "A refused document should write nothing: " + context);
    }

    /// <summary>
    /// Asserts that two documents are equal in every field and order.
    /// </summary>
    /// <param name="expected">The expected document</param>
    /// <param name="actual">The actual document</param>
    /// <param name="context">What the case was, for the messages</param>
    private static void AssertEqual(CatalogDocument expected, CatalogDocument actual, string context)
    {
        Assert.AreEqual(expected.SchemaVersion, actual.SchemaVersion, "The schema version should be equal: " + context);
        Assert.AreEqual(expected.GameVersion, actual.GameVersion, "The game version should be equal: " + context);
        Assert.HasCount(expected.Entries.Count, actual.Entries, "The number of entries should be equal: " + context);

        for (var index = 0; index < expected.Entries.Count; index++)
        {
            var want = expected.Entries[index];
            var got = actual.Entries[index];

            Assert.AreEqual(want.AssetGuid, got.AssetGuid, $"The GUID of entry {index} should be equal: {context}");
            Assert.AreEqual(want.Kind, got.Kind, $"The kind of entry {index} should be equal: {context}");
            Assert.AreEqual(want.RegionGuid, got.RegionGuid, $"The region GUID of entry {index} should be equal: {context}");
            Assert.AreEqual(want.IconReference, got.IconReference, $"The icon of entry {index} should be equal: {context}");
            Assert.HasCount(want.Names.Count, got.Names, $"The number of names of entry {index} should be equal: {context}");

            foreach (var pair in want.Names)
            {
                Assert.IsTrue(got.Names.TryGetValue(pair.Key, out var name), $"The language {pair.Key} of entry {index} should be present: {context}");
                Assert.AreEqual(pair.Value, name, $"The name in {pair.Key} of entry {index} should be equal: {context}");
            }

            Assert.AreEqual(want.RegionTypes is null, got.RegionTypes is null, $"Region types not stated should stay not stated for entry {index}: {context}");

            if (want.RegionTypes is not null)
            {
                Assert.AreSequenceEqual(want.RegionTypes.ToArray(), got.RegionTypes!.ToArray(), $"The region types of entry {index} should be equal and in order: {context}");
            }
        }
    }

    #endregion // Methods
}