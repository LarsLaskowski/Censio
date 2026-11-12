using System.Text;

namespace Censio.Contracts.Tests;

/// <summary>
/// Tests for <see cref="CatalogDocumentReader" />.
/// </summary>
[TestClass]
public sealed class CatalogDocumentReaderTests
{
    #region Constants

    /// <summary>
    /// A valid icon reference: 64 lowercase hex digits.
    /// </summary>
    private const string Icon = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// The icon property of the valid document in single-quote notation.
    /// </summary>
    private const string IconField = $"'icon':'{Icon}'";

    /// <summary>
    /// A valid document of one region entry in single-quote notation (<see cref="Json" /> turns the quotes into double quotes).
    /// </summary>
    private const string ValidDocument = $"{{'schemaVersion':1,'gameVersion':'1.2.3.4','entries':[{{'guid':5,'kind':'region','names':{{'en':'Wood','de':'Holz'}},'regionGuid':7,'regionTypes':['Coast','Land'],{IconField}}}]}}";

    /// <summary>
    /// The fixed text of a malformed document.
    /// </summary>
    private const string MalformedText = "Catalog document is malformed";

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

    /// <summary>
    /// A 35 character language tag, exactly at the bound.
    /// </summary>
    private const string Tag35 = "en-abcdefgh-abcdefgh-abcdefgh-abcde";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The region types of the valid document.
    /// </summary>
    private static readonly string[] _expectedTypes = ["Coast", "Land"];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the documents that are no well-formed JSON text (E1): one column, the document.
    /// </summary>
    private static IEnumerable<object[]> SyntaxRows => SyntaxDocuments().Select(document => new object[] { document });

    /// <summary>
    /// Gets the documents of a wrong root shape (E2): one column, the document.
    /// </summary>
    private static IEnumerable<object[]> RootRows => RootDocuments().Select(document => new object[] { document });

    /// <summary>
    /// Gets the documents with a malformed schema version (AC4): one column, the document.
    /// </summary>
    private static IEnumerable<object[]> MalformedVersionRows => MalformedVersionDocuments().Select(document => new object[] { document });

    /// <summary>
    /// Gets the structure cases (E2): the text to find in the valid document and its replacement.
    /// </summary>
    private static IEnumerable<object[]> StructureRows => StructurePairs().Select(pair => new object[] { pair.Find, pair.Replace });

    /// <summary>
    /// Gets the value cases (E4): the text to find in the valid document and its replacement.
    /// </summary>
    private static IEnumerable<object[]> InvalidValueRows => InvalidPairs().Select(pair => new object[] { pair.Find, pair.Replace });

    /// <summary>
    /// Gets every document a reader must refuse without throwing: one column, the document.
    /// </summary>
    private static IEnumerable<object[]> RefusalRows => SyntaxDocuments().Concat(RootDocuments())
                                                                         .Concat(MalformedVersionDocuments())
                                                                         .Concat(StructurePairs().Select(pair => Substitute(pair.Find, pair.Replace)))
                                                                         .Concat(InvalidPairs().Select(pair => Substitute(pair.Find, pair.Replace)))
                                                                         .Concat(OddDocuments())
                                                                         .Select(document => new object[] { document });

    /// <summary>
    /// Gets the cut positions of the valid document: one column, the number of bytes kept.
    /// </summary>
    private static IEnumerable<object[]> CutRows => Enumerable.Range(0, Json(ValidDocument).Length).Select(length => new object[] { length });

    /// <summary>
    /// Gets the string cases of the string bound: the text to find, and the replacement with the placeholder {S}.
    /// </summary>
    private static IEnumerable<object[]> StringPlaceRows
    {
        get
        {
            yield return new object[] { "'en':'Wood'", "'en':'{S}'" };
            yield return new object[] { "'en':'Wood'", "'{S}':'Wood'" };
            yield return new object[] { "'guid':5", "'{S}':5" };
            yield return new object[] { "'1.2.3.4'", "'{S}'" };
            yield return new object[] { "'Coast'", "'{S}'" };
            yield return new object[] { "'region'", "'{S}'" };
            yield return new object[] { "'icon':'" + Icon + "'", "'icon':'{S}'" };
        }
    }

    /// <summary>
    /// Gets the cases of the fixed texts: the document, the kind and the text.
    /// </summary>
    private static IEnumerable<object[]> FixedTextRows
    {
        get
        {
            yield return new object[] { "not json", CatalogDocumentErrorKind.Malformed, MalformedText };
            yield return new object[] { "{'schemaVersion':1}", CatalogDocumentErrorKind.Malformed, MalformedText };
            yield return new object[] { "{'schemaVersion':1,'gameVersion':'0.0.0.0','entries':[]}", CatalogDocumentErrorKind.Invalid, InvalidText };
            yield return new object[] { "{'schemaVersion':2,'gameVersion':null,'entries':[]}", CatalogDocumentErrorKind.UnsupportedVersion, "Catalog schema version 2 is not supported; supported: 1" };
            yield return new object[] { "{'schemaVersion':-1,'gameVersion':null,'entries':[]}", CatalogDocumentErrorKind.UnsupportedVersion, "Catalog schema version -1 is not supported; supported: 1" };
            yield return new object[] { BuildCountsDocument(1, 17, 0), CatalogDocumentErrorKind.TooLarge, TooLargeText };
            yield return new object[] { BuildCountsDocument(1, 1, 33), CatalogDocumentErrorKind.TooLarge, TooLargeText };
        }
    }

    /// <summary>
    /// Gets documents that hold marker texts in a name, a region type and an icon reference: the document and the kind it is refused with.
    /// </summary>
    private static IEnumerable<object[]> MarkerRows
    {
        get
        {
            yield return new object[] { "{'schemaVersion':1,'gameVersion':null,'entries':[{'guid':5,'kind':'region','names':{'en':'MARKERNAME'},'regionGuid':7,'regionTypes':['MARKERTYPE'],'icon':'REFMARKER'}]}", CatalogDocumentErrorKind.Invalid };
            yield return new object[] { "{'schemaVersion':1,'gameVersion':null,'entries':[{'guid':5,'kind':'region','names':{'en':'MARKERNAME'},'regionGuid':7,'regionTypes':['MARKERTYPE'],'icon':'REFMARKER','MARKERNAME':1}]}", CatalogDocumentErrorKind.Malformed };
            yield return new object[] { "{'schemaVersion':1,'gameVersion':null,'entries':[{'guid':5,'kind':'MARKERNAME','names':{'en':'MARKERNAME'},'regionGuid':7,'regionTypes':['MARKERTYPE'],'icon':'REFMARKER'}]}", CatalogDocumentErrorKind.Malformed };
            yield return new object[] { "{'schemaVersion':7,'gameVersion':'MARKERNAME','entries':[{'MARKERTYPE':'REFMARKER','names':{'en':'MARKERNAME'}}]}", CatalogDocumentErrorKind.UnsupportedVersion };
            yield return new object[] { "{'schemaVersion':1,'gameVersion':null,'entries':[{'guid':5,'kind':'region','names':{" + MarkerNames(17) + "},'regionGuid':7,'regionTypes':['MARKERTYPE'],'icon':'REFMARKER'}]}", CatalogDocumentErrorKind.TooLarge };
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A schema version other than 1 is refused as unsupported, with its number in the text and nothing of the document used.
    /// </summary>
    /// <param name="version">The schema version of the document</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(-1)]
    [DataRow(int.MaxValue)]
    [DataRow(int.MinValue)]
    public void CatalogDocumentReaderReadRefusesOtherVersionAsUnsupported(int version)
    {
        // Arrange
        var json = Utf8($"{{'schemaVersion':{version},'gameVersion':null,'entries':[]}}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.UnsupportedVersion, $"Catalog schema version {version} is not supported; supported: 1", $"version {version}");
    }

    /// <summary>
    /// The refusal of an unsupported schema version carries the version found, so the server can name it in its
    /// acknowledgement (spec E5).
    /// </summary>
    /// <param name="version">The schema version of the document</param>
    [TestMethod]
    [TestCategory("AC10")]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(-1)]
    [DataRow(int.MaxValue)]
    [DataRow(int.MinValue)]
    public void CatalogDocumentReaderReadUnsupportedVersionReturnsVersion(int version)
    {
        // Arrange
        var json = Utf8($"{{'schemaVersion':{version},'gameVersion':null,'entries':[]}}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNotNull(result.Error, $"The document should be refused: version {version}");
        Assert.AreEqual(CatalogDocumentErrorKind.UnsupportedVersion, result.Error.Kind, $"The document should be refused as unsupported: version {version}");
        Assert.AreEqual(version, result.Error.Version, $"The refusal should carry the version found: version {version}");
    }

    /// <summary>
    /// A document of another version is unsupported whatever else it holds, a property of a later version included.
    /// </summary>
    /// <param name="document">The document in single-quote notation</param>
    /// <param name="version">The schema version of the document</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow("{'schemaVersion':2,'gameVersion':null,'entries':[],'futureProperty':[1,2]}", 2)]
    [DataRow("{'schemaVersion':3,'futureProperty':{'a':null}}", 3)]
    [DataRow("{'schemaVersion':2}", 2)]
    [DataRow("{'schemaVersion':2,'entries':[{'guid':'text','kind':'castle','extra':true}]}", 2)]
    [DataRow("{'futureProperty':1,'schemaVersion':0,'entries':'x'}", 0)]
    public void CatalogDocumentReaderReadRefusesLaterVersionWithUnknownPropertyAsUnsupported(string document, int version)
    {
        // Arrange
        var json = Utf8(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.UnsupportedVersion, $"Catalog schema version {version} is not supported; supported: 1", document);
    }

    /// <summary>
    /// A schema version that is missing, repeated or no signed 32-bit integer is malformed.
    /// </summary>
    /// <param name="document">The document in single-quote notation</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DynamicData(nameof(MalformedVersionRows))]
    public void CatalogDocumentReaderReadRefusesMalformedVersion(string document)
    {
        // Arrange
        var json = Utf8(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, document);
    }

    /// <summary>
    /// A truncated document with schema version 2 is malformed: the whole document is checked before the version.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void CatalogDocumentReaderReadRefusesTruncatedVersionTwoAsMalformed()
    {
        // Arrange
        var json = Utf8("{'schemaVersion':2,'gameVersion':null,'entries':[");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, "truncated version 2");
    }

    /// <summary>
    /// A property name written with an escape is still the schema version.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void CatalogDocumentReaderReadFindsEscapedVersionName()
    {
        // Arrange
        var text = Json("{'schema\\u0056ersion':2,'gameVersion':null,'entries':[]}");
        var json = Encoding.UTF8.GetBytes(text);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.Contains("\\u0056", text, "The input should still hold the backslash of the escape");
        AssertRefusal(result, CatalogDocumentErrorKind.UnsupportedVersion, "Catalog schema version 2 is not supported; supported: 1", "escaped version name");
    }

    /// <summary>
    /// A schema version inside an entry is no version of the document.
    /// </summary>
    [TestMethod]
    [TestCategory("AC4")]
    public void CatalogDocumentReaderReadIgnoresVersionInsideEntry()
    {
        // Arrange
        var json = Utf8("{'gameVersion':null,'entries':[{'schemaVersion':2,'guid':5}]}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, "version only inside an entry");
    }

    /// <summary>
    /// A text that is no complete JSON document is malformed.
    /// </summary>
    /// <param name="document">The document in single-quote notation</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(SyntaxRows))]
    public void CatalogDocumentReaderReadRefusesMalformedSyntax(string document)
    {
        // Arrange
        var json = Utf8(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, $"syntax: {document}");
    }

    /// <summary>
    /// Every strict prefix of a valid document is malformed.
    /// </summary>
    /// <param name="length">The number of bytes kept</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(CutRows))]
    public void CatalogDocumentReaderReadRefusesTruncatedDocument(int length)
    {
        // Arrange
        var json = Utf8(ValidDocument).AsSpan(0, length).ToArray();

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, $"cut at {length}");
    }

    /// <summary>
    /// A structure the version does not allow is malformed: unknown, repeated, missing or other-case property, wrong type, null, number or kind.
    /// </summary>
    /// <param name="find">The text to replace in the valid document</param>
    /// <param name="replace">The replacement</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(StructureRows))]
    public void CatalogDocumentReaderReadRefusesMalformedStructure(string find, string replace)
    {
        // Arrange
        var document = Substitute(find, replace);
        var json = Encoding.UTF8.GetBytes(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, document);
    }

    /// <summary>
    /// A root or an entries list of the wrong shape is malformed.
    /// </summary>
    /// <param name="document">The document in single-quote notation</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(RootRows))]
    public void CatalogDocumentReaderReadRefusesMalformedRoot(string document)
    {
        // Arrange
        var json = Utf8(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, $"root: {document}");
    }

    /// <summary>
    /// Bytes that are no UTF-8 inside a name are malformed, never replaced.
    /// </summary>
    /// <param name="hex">The bytes put between the quotes of the name</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("C328")]
    [DataRow("FF")]
    [DataRow("EDA080")]
    [DataRow("EDB080")]
    [DataRow("C0AF")]
    [DataRow("F4908080")]
    [DataRow("E282")]
    public void CatalogDocumentReaderReadRefusesInvalidUtf8(string hex)
    {
        // Arrange
        var json = WithBytes("{'schemaVersion':1,'gameVersion':null,'entries':[{'guid':5,'kind':'good','names':{'en':'a", hex, "b'},'regionGuid':null,'regionTypes':null,'icon':null}]}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, hex);
    }

    /// <summary>
    /// Bytes that are no UTF-8 inside a property name are malformed.
    /// </summary>
    /// <param name="hex">The bytes put between the quotes of the property name</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("C328")]
    [DataRow("EDA080")]
    public void CatalogDocumentReaderReadRefusesInvalidUtf8InPropertyName(string hex)
    {
        // Arrange
        var json = WithBytes("{'schemaVersion':1,'gameVersion':null,'entries':[],'a", hex, "b':1}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, hex);
    }

    /// <summary>
    /// A lone surrogate written as an escape in a string or a property name is malformed.
    /// </summary>
    /// <param name="find">The text to replace in the valid document</param>
    /// <param name="replace">The replacement, holding the escape</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("'en':'Wood'", "'en':'\\ud800'")]
    [DataRow("'en':'Wood'", "'en':'a\\ude00'")]
    [DataRow("'en':'Wood'", "'en':'\\ud800a'")]
    [DataRow("'en':'Wood'", "'en':'\\ud800\\ud800'")]
    [DataRow("'en':'Wood'", "'en\\ud800':'Wood'")]
    [DataRow("'1.2.3.4'", "'\\ud800'")]
    [DataRow("'Coast'", "'\\udc00'")]
    [DataRow("'guid':5", "'g\\ud800':5")]
    public void CatalogDocumentReaderReadRefusesLoneSurrogate(string find, string replace)
    {
        // Arrange
        var document = Substitute(find, replace);
        var json = Encoding.UTF8.GetBytes(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.Contains("\\ud", document, "The input should still hold the backslash of the escape");
        AssertRefusal(result, CatalogDocumentErrorKind.Malformed, MalformedText, document);
    }

    /// <summary>
    /// A surrogate pair written as two escapes is a valid name.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void CatalogDocumentReaderReadAcceptsEscapedSurrogatePair()
    {
        // Arrange
        var document = Substitute("'en':'Wood'", "'en':'\\ud83d\\ude00'");
        var json = Encoding.UTF8.GetBytes(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.Contains("\\ud83d", document, "The input should still hold the backslash of the escape");
        Assert.IsNull(result.Error, "A surrogate pair should be accepted");
        Assert.AreEqual("😀", result.Document!.Entries[0].Names["en"], "The pair should be read as one character");
    }

    /// <summary>
    /// A value that breaks a rule of the version is invalid.
    /// </summary>
    /// <param name="find">The text to replace in the valid document</param>
    /// <param name="replace">The replacement</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DynamicData(nameof(InvalidValueRows))]
    public void CatalogDocumentReaderReadRefusesInvalidValue(string find, string replace)
    {
        // Arrange
        var document = Substitute(find, replace);
        var json = Encoding.UTF8.GetBytes(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.Invalid, InvalidText, document);
    }

    /// <summary>
    /// A language tag with a non-ASCII letter written as an escape is invalid.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void CatalogDocumentReaderReadRefusesEscapedNonAsciiLanguageTag()
    {
        // Arrange
        var document = Substitute("'en':'Wood'", "'stra\\u00dfe':'Wood'");
        var json = Encoding.UTF8.GetBytes(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.Contains("\\u00df", document, "The input should still hold the backslash of the escape");
        AssertRefusal(result, CatalogDocumentErrorKind.Invalid, InvalidText, "escaped non-ASCII tag");
    }

    /// <summary>
    /// A lowercase BCP 47 language tag of at most 35 characters is accepted.
    /// </summary>
    /// <param name="tag">The language tag</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("en")]
    [DataRow("de")]
    [DataRow("pt-br")]
    [DataRow("abc")]
    [DataRow("zh-hans-cn")]
    [DataRow("en-1a")]
    [DataRow("de-1996")]
    [DataRow("en-12345678")]
    [DataRow(Tag35)]
    public void CatalogDocumentReaderReadAcceptsLanguageTag(string tag)
    {
        // Arrange
        var json = Utf8($"{{'schemaVersion':1,'gameVersion':null,'entries':[{{'guid':5,'kind':'good','names':{{'{tag}':'Wood'}},'regionGuid':null,'regionTypes':null,'icon':null}}]}}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, $"The language tag {tag} should be accepted");
        Assert.AreEqual("Wood", result.Document!.Entries[0].Names[tag], "The name should be read under its tag");
    }

    /// <summary>
    /// Any non-zero signed 32-bit GUID, negative ones included, is accepted, also as region GUID.
    /// </summary>
    /// <param name="entryGuid">The GUID of the entry</param>
    /// <param name="regionGuid">The region GUID of the entry</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(-5, -7)]
    [DataRow(int.MinValue, int.MinValue)]
    [DataRow(int.MaxValue, int.MaxValue)]
    [DataRow(1, -1)]
    public void CatalogDocumentReaderReadAcceptsNegativeGuid(int entryGuid, int regionGuid)
    {
        // Arrange
        var json = Utf8($"{{'schemaVersion':1,'gameVersion':null,'entries':[{{'guid':{entryGuid},'kind':'region','names':{{'en':'Wood'}},'regionGuid':{regionGuid},'regionTypes':null,'icon':null}}]}}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, "A non-zero GUID should be accepted");
        Assert.AreEqual(entryGuid, result.Document!.Entries[0].AssetGuid, "The GUID should be read unchanged");
        Assert.AreEqual(regionGuid, result.Document.Entries[0].RegionGuid, "The region GUID should be read unchanged");
    }

    /// <summary>
    /// A game version of four ASCII numbers from 0 to 65,535, not all zero, is accepted.
    /// </summary>
    /// <param name="version">The game version</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("65535.65535.65535.65535")]
    [DataRow("0.0.0.1")]
    [DataRow("1.0.0.0")]
    [DataRow("10.20.30.40")]
    [DataRow("0.10.0.100")]
    public void CatalogDocumentReaderReadAcceptsGameVersion(string version)
    {
        // Arrange
        var json = Utf8($"{{'schemaVersion':1,'gameVersion':'{version}','entries':[]}}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, $"The game version {version} should be accepted");
        Assert.AreEqual(version, result.Document!.GameVersion, "The game version should be read unchanged");
    }

    /// <summary>
    /// A valid document is read with every field and the order of its lists.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CatalogDocumentReaderReadReturnsDocumentWithEveryField()
    {
        // Arrange
        var json = Utf8(ValidDocument);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, "A valid document should not be refused");
        Assert.IsNotNull(result.Document, "A valid document should be returned");
        Assert.AreEqual(1, result.Document.SchemaVersion, "The schema version should be read");
        Assert.AreEqual("1.2.3.4", result.Document.GameVersion, "The game version should be read");
        Assert.HasCount(1, result.Document.Entries, "The entry should be read");

        var entry = result.Document.Entries[0];

        Assert.AreEqual(5, entry.AssetGuid, "The GUID should be read");
        Assert.AreEqual(CatalogEntryKind.Region, entry.Kind, "The kind should be read");
        Assert.HasCount(2, entry.Names, "Both names should be read");
        Assert.AreEqual("Wood", entry.Names["en"], "The English name should be read");
        Assert.AreEqual("Holz", entry.Names["de"], "The German name should be read");
        Assert.AreEqual(7, entry.RegionGuid, "The region GUID should be read");
        Assert.IsNotNull(entry.RegionTypes, "The region types should be read");
        Assert.AreSequenceEqual(_expectedTypes, entry.RegionTypes.ToArray(), "The region types should keep their order");
        Assert.AreEqual(Icon, entry.IconReference, "The icon reference should be read");
    }

    /// <summary>
    /// The four kinds, null values and region types not stated, none and a list are read as they are written.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void CatalogDocumentReaderReadReturnsKindsAndNullValues()
    {
        // Arrange
        var json = Utf8("{'schemaVersion':1,'gameVersion':null,'entries':["
                        + "{'guid':1,'kind':'good','names':{'de':'Holz'},'regionGuid':null,'regionTypes':null,'icon':null},"
                        + "{'guid':2,'kind':'workforce','names':{},'regionGuid':null,'regionTypes':[],'icon':null},"
                        + "{'guid':3,'kind':'building','names':{'en':'Hut'},'regionGuid':null,'regionTypes':['Coast'],'icon':'" + Icon + "'}]}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, "The document should be accepted");

        var entries = result.Document!.Entries;

        Assert.IsNull(result.Document.GameVersion, "A null game version should be read as null");
        Assert.AreEqual(CatalogEntryKind.Good, entries[0].Kind, "The first kind should be good");
        Assert.AreEqual(CatalogEntryKind.Workforce, entries[1].Kind, "The second kind should be workforce");
        Assert.AreEqual(CatalogEntryKind.Building, entries[2].Kind, "The third kind should be building");
        Assert.IsNull(entries[0].RegionTypes, "Region types not stated should be null");
        Assert.IsNotNull(entries[1].RegionTypes, "No region types should be an empty list");
        Assert.IsEmpty(entries[1].RegionTypes!, "No region types should be an empty list");
        Assert.HasCount(1, entries[2].RegionTypes!, "A list of one should keep its element");
        Assert.IsNull(entries[0].IconReference, "A null icon should be read as null");
        Assert.IsNull(entries[0].RegionGuid, "A null region GUID should be read as null");
        Assert.IsEmpty(entries[1].Names, "An entry without a name should have an empty names object");
    }

    /// <summary>
    /// A catalog without entries, and an entry with no name or one name, are valid.
    /// </summary>
    /// <param name="document">The document in single-quote notation</param>
    /// <param name="entries">The number of entries</param>
    /// <param name="names">The number of names of the first entry</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("{'schemaVersion':1,'gameVersion':null,'entries':[]}", 0, 0)]
    [DataRow("{'schemaVersion':1,'gameVersion':null,'entries':[{'guid':1,'kind':'good','names':{},'regionGuid':null,'regionTypes':null,'icon':null}]}", 1, 0)]
    [DataRow("{'schemaVersion':1,'gameVersion':null,'entries':[{'guid':1,'kind':'good','names':{'de':'Holz'},'regionGuid':null,'regionTypes':null,'icon':null}]}", 1, 1)]
    public void CatalogDocumentReaderReadAcceptsEmptyCatalogAndMissingNames(string document, int entries, int names)
    {
        // Arrange
        var json = Utf8(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, "The document should be accepted");
        Assert.HasCount(entries, result.Document!.Entries, "The entries should be read");
        Assert.AreEqual(names, result.Document.Entries.Sum(entry => entry.Names.Count), "The names should be read");
    }

    /// <summary>
    /// A document above 16 MiB is too large, although everything else about it is valid.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadRefusesDocumentAboveBound()
    {
        // Arrange
        var json = Padded(CatalogDocumentReader.MaxDocumentBytes + 1);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, "16 MiB plus one");
    }

    /// <summary>
    /// The length is checked before anything is parsed: a text above the bound that is no JSON is too large, not malformed.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadRefusesOversizedGarbageByLengthFirst()
    {
        // Arrange
        var json = new byte[CatalogDocumentReader.MaxDocumentBytes + 1];

        json.AsSpan().Fill((byte)'x');

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, "garbage above the bound");
    }

    /// <summary>
    /// A valid document of exactly 16 MiB is read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadAcceptsDocumentAtBound()
    {
        // Arrange
        var json = Padded(CatalogDocumentReader.MaxDocumentBytes);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.HasCount(CatalogDocumentReader.MaxDocumentBytes, json, "The document should have exactly the bound");
        Assert.IsNull(result.Error, "A document at the bound should not be refused");
        Assert.IsNotNull(result.Document, "A document at the bound should be returned");
    }

    /// <summary>
    /// A string of 65,537 bytes is too large wherever it stands: name, tag, property name, game version, region type, kind or icon.
    /// </summary>
    /// <param name="find">The text to replace in the valid document</param>
    /// <param name="replace">The replacement with the placeholder {S}</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DynamicData(nameof(StringPlaceRows))]
    public void CatalogDocumentReaderReadRefusesStringAboveBound(string find, string replace)
    {
        // Arrange
        var document = Substitute(find, replace.Replace("{S}", new string('a', CatalogDocumentReader.MaxStringBytes + 1)));
        var json = Encoding.UTF8.GetBytes(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, $"string of 65,537 bytes in {replace}");
    }

    /// <summary>
    /// A string of exactly 65,536 bytes is not refused for its size, wherever it stands.
    /// </summary>
    /// <param name="find">The text to replace in the valid document</param>
    /// <param name="replace">The replacement with the placeholder {S}</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DynamicData(nameof(StringPlaceRows))]
    public void CatalogDocumentReaderReadDoesNotRefuseStringAtBoundForItsSize(string find, string replace)
    {
        // Arrange
        var document = Substitute(find, replace.Replace("{S}", new string('a', CatalogDocumentReader.MaxStringBytes)));
        var json = Encoding.UTF8.GetBytes(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.AreNotEqual(CatalogDocumentErrorKind.TooLarge, result.Error?.Kind, $"A string of 65,536 bytes in {replace} should not be too large");
    }

    /// <summary>
    /// A name of exactly 65,536 bytes is read whole.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadAcceptsStringAtBound()
    {
        // Arrange
        var name = new string('a', CatalogDocumentReader.MaxStringBytes);
        var json = Encoding.UTF8.GetBytes(Substitute("'en':'Wood'", $"'en':'{name}'"));

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, "A name at the bound should be accepted");
        Assert.AreEqual(name, result.Document!.Entries[0].Names["en"], "The name should be read whole");
    }

    /// <summary>
    /// The bound counts UTF-8 bytes, not characters: 32,769 two-byte characters are too large.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadRefusesMultiByteStringAboveBound()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(Substitute("'en':'Wood'", $"'en':'{new string('ä', (CatalogDocumentReader.MaxStringBytes / 2) + 1)}'"));

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, "65,538 bytes of two-byte characters");
    }

    /// <summary>
    /// The bound counts UTF-8 bytes, not characters: 32,768 two-byte characters are accepted.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadAcceptsMultiByteStringAtBound()
    {
        // Arrange
        var name = new string('ä', CatalogDocumentReader.MaxStringBytes / 2);
        var json = Encoding.UTF8.GetBytes(Substitute("'en':'Wood'", $"'en':'{name}'"));

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, "A name of 65,536 bytes should be accepted");
        Assert.AreEqual(name, result.Document!.Entries[0].Names["en"], "The name should be read whole");
    }

    /// <summary>
    /// A name that is 65,536 bytes after unescaping is accepted, although its text is far longer.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadAcceptsEscapedStringWithinBound()
    {
        // Arrange
        var document = Substitute("'en':'Wood'", $"'en':'{string.Concat(Enumerable.Repeat("\\u0041", CatalogDocumentReader.MaxStringBytes))}'");
        var json = Encoding.UTF8.GetBytes(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.Contains("\\u0041", document, "The input should still hold the backslash of the escape");
        Assert.IsNull(result.Error, "An escaped name of 65,536 bytes should be accepted");
        Assert.AreEqual(new string('A', CatalogDocumentReader.MaxStringBytes), result.Document!.Entries[0].Names["en"], "The name should be read unescaped");
    }

    /// <summary>
    /// A name that is 65,537 bytes after unescaping is too large.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadRefusesEscapedStringAboveBound()
    {
        // Arrange
        var document = Substitute("'en':'Wood'", $"'en':'{string.Concat(Enumerable.Repeat("\\u0041", CatalogDocumentReader.MaxStringBytes + 1))}'");
        var json = Encoding.UTF8.GetBytes(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.Contains("\\u0041", document, "The input should still hold the backslash of the escape");
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, "escaped string of 65,537 bytes");
    }

    /// <summary>
    /// More than 16,384 entries, 16 names or 32 region types in one entry are too large.
    /// </summary>
    /// <param name="entries">The number of entries</param>
    /// <param name="names">The number of names of the first entry</param>
    /// <param name="regionTypes">The number of region types of the first entry</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(16385, 1, 1)]
    [DataRow(1, 17, 1)]
    [DataRow(1, 1, 33)]
    [DataRow(1, 100, 100)]
    public void CatalogDocumentReaderReadRefusesCountsAboveBound(int entries, int names, int regionTypes)
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(BuildCountsDocument(entries, names, regionTypes));

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, $"{entries} entries, {names} names, {regionTypes} region types");
    }

    /// <summary>
    /// A document of 16,384 entries, one with 16 names and 32 region types, is read whole.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadAcceptsCountsAtBound()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(BuildCountsDocument(CatalogDocumentReader.MaxEntries, CatalogDocumentReader.MaxNamesPerEntry, CatalogDocumentReader.MaxRegionTypesPerEntry));

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, "A document with every count at its bound should be accepted");
        Assert.HasCount(CatalogDocumentReader.MaxEntries, result.Document!.Entries, "Every entry should be read");
        Assert.HasCount(CatalogDocumentReader.MaxNamesPerEntry, result.Document.Entries[0].Names, "Every name of the first entry should be read");
        Assert.HasCount(CatalogDocumentReader.MaxRegionTypesPerEntry, result.Document.Entries[0].RegionTypes!, "Every region type of the first entry should be read");
    }

    /// <summary>
    /// The counts are checked before anything is deserialized: 17 names in an entry that also breaks a structure rule are too large, not malformed.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadRefusesCountsBeforeDeserializing()
    {
        // Arrange
        var json = Utf8($"{{'schemaVersion':1,'gameVersion':null,'entries':[{{'guid':'text','unknown':true,'kind':'castle','names':{{{MarkerNames(17)}}}}}]}}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, "17 names in a broken entry");
    }

    /// <summary>
    /// The counts are checked whatever the schema version: 17 names in a document of version 2 are too large, not unsupported.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadRefusesCountsOfLaterVersionAsTooLarge()
    {
        // Arrange
        var json = Utf8($"{{'schemaVersion':2,'gameVersion':null,'entries':[{{'guid':5,'names':{{{MarkerNames(17)}}}}}]}}");

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, "17 names in version 2");
    }

    /// <summary>
    /// The counts also see properties whose names are written with escapes.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void CatalogDocumentReaderReadCountsEscapedPropertyNames()
    {
        // Arrange
        var text = Json($"{{'schemaVersion':1,'gameVersion':null,'entries':[{{'guid':5,'kind':'good','n\\u0061mes':{{{MarkerNames(17)}}},'regionGuid':null,'regionTypes':null,'icon':null}}]}}");
        var json = Encoding.UTF8.GetBytes(text);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.Contains("\\u0061", text, "The input should still hold the backslash of the escape");
        AssertRefusal(result, CatalogDocumentErrorKind.TooLarge, TooLargeText, "17 names under an escaped property name");
    }

    /// <summary>
    /// Every refusal returns its kind and the fixed text of its kind.
    /// </summary>
    /// <param name="document">The document in single-quote notation</param>
    /// <param name="kind">The expected kind</param>
    /// <param name="text">The expected text</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DynamicData(nameof(FixedTextRows))]
    public void CatalogDocumentReaderReadReturnsFixedTextPerKind(string document, CatalogDocumentErrorKind kind, string text)
    {
        // Arrange
        var json = Utf8(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        AssertRefusal(result, kind, text, kind.ToString());
    }

    /// <summary>
    /// A refusal text holds no name, region type or reference of the document, in any kind.
    /// </summary>
    /// <param name="document">The document in single-quote notation</param>
    /// <param name="kind">The kind it is refused with</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DynamicData(nameof(MarkerRows))]
    public void CatalogDocumentReaderReadTextHoldsNoContent(string document, CatalogDocumentErrorKind kind)
    {
        // Arrange
        var json = Utf8(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNotNull(result.Error, "The document should be refused");
        Assert.AreEqual(kind, result.Error.Kind, "The document should be refused with the kind of its row");
        Assert.DoesNotContain("MARKERNAME", result.Error.Text, "The text should hold no name");
        Assert.DoesNotContain("MARKERTYPE", result.Error.Text, "The text should hold no region type");
        Assert.DoesNotContain("REFMARKER", result.Error.Text, "The text should hold no reference");
    }

    /// <summary>
    /// Every refusal row of the other tests, an empty input, a byte order mark, a top-level array and a depth of 65 give a result and no exception.
    /// </summary>
    /// <param name="document">The document in single-quote notation</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DynamicData(nameof(RefusalRows))]
    public void CatalogDocumentReaderReadNeverThrows(string document)
    {
        // Arrange
        var json = Utf8(document);

        // Act
        var result = CatalogDocumentReader.Read(json);

        // Assert
        Assert.IsNull(result.Document, "A refused document should return no document");
        Assert.IsNotNull(result.Error, "A refused document should return an error");
        Assert.IsFalse(string.IsNullOrEmpty(result.Error.Text), "A refusal should carry its text");
    }

    /// <summary>
    /// Turns the single-quote notation into JSON.
    /// </summary>
    /// <param name="notation">The text with single quotes</param>
    /// <returns>The text with double quotes</returns>
    private static string Json(string notation)
    {
        return notation.Replace('\'', '"');
    }

    /// <summary>
    /// Turns the single-quote notation into UTF-8 bytes.
    /// </summary>
    /// <param name="notation">The text with single quotes</param>
    /// <returns>The UTF-8 bytes of the JSON</returns>
    private static byte[] Utf8(string notation)
    {
        return Encoding.UTF8.GetBytes(Json(notation));
    }

    /// <summary>
    /// Builds a document from three parts of single-quote notation with bytes (hex) in between.
    /// </summary>
    /// <param name="before">The notation before the bytes</param>
    /// <param name="hex">The bytes in hex</param>
    /// <param name="after">The notation after the bytes</param>
    /// <returns>The bytes of the document</returns>
    private static byte[] WithBytes(string before, string hex, string after)
    {
        return [.. Utf8(before), .. Convert.FromHexString(hex), .. Utf8(after)];
    }

    /// <summary>
    /// Replaces a part of the valid document and returns the JSON; the part must exist in it.
    /// </summary>
    /// <param name="find">The text to replace, in single-quote notation</param>
    /// <param name="replace">The replacement, in single-quote notation</param>
    /// <returns>The JSON text of the changed document</returns>
    private static string Substitute(string find, string replace)
    {
        var document = Json(ValidDocument);
        var part = Json(find);

        if (document.Contains(part, StringComparison.Ordinal) == false)
        {
            throw new InvalidOperationException("The valid document does not hold " + part);
        }

        return document.Replace(part, Json(replace), StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the valid document padded with trailing spaces to a length.
    /// </summary>
    /// <param name="length">The length in bytes</param>
    /// <returns>The bytes of the padded document</returns>
    private static byte[] Padded(int length)
    {
        var json = new byte[length];
        var document = Utf8(ValidDocument);

        json.AsSpan().Fill((byte)' ');
        document.CopyTo(json, 0);

        return json;
    }

    /// <summary>
    /// Builds a document whose first entry is a region with a number of names and region types, followed by good entries.
    /// </summary>
    /// <param name="entries">The number of entries</param>
    /// <param name="names">The number of names of the first entry</param>
    /// <param name="regionTypes">The number of region types of the first entry</param>
    /// <returns>The JSON text</returns>
    private static string BuildCountsDocument(int entries, int names, int regionTypes)
    {
        var builder = new StringBuilder("{\"schemaVersion\":1,\"gameVersion\":null,\"entries\":[");

        builder.Append("{\"guid\":1,\"kind\":\"region\",\"names\":{");

        for (var index = 0; index < names; index++)
        {
            builder.Append(index == 0 ? string.Empty : ",").Append('"').Append(Tag(index)).Append("\":\"x\"");
        }

        builder.Append("},\"regionGuid\":1,\"regionTypes\":[");

        for (var index = 0; index < regionTypes; index++)
        {
            builder.Append(index == 0 ? string.Empty : ",").Append("\"T").Append(index).Append('"');
        }

        builder.Append("],\"icon\":null}");

        for (var index = 2; index <= entries; index++)
        {
            builder.Append(",{\"guid\":").Append(index).Append(",\"kind\":\"good\",\"names\":{\"en\":\"x\"},\"regionGuid\":null,\"regionTypes\":null,\"icon\":null}");
        }

        return builder.Append("]}").ToString();
    }

    /// <summary>
    /// Builds a names object body (single-quote notation) of a number of languages, each named by a marker.
    /// </summary>
    /// <param name="count">The number of languages</param>
    /// <returns>The notation of the names</returns>
    private static string MarkerNames(int count)
    {
        return string.Join(",", Enumerable.Range(0, count).Select(index => $"'{Tag(index)}':'MARKERNAME'"));
    }

    /// <summary>
    /// Returns a valid, distinct language tag of two letters for an index below 676.
    /// </summary>
    /// <param name="index">The index</param>
    /// <returns>The tag</returns>
    private static string Tag(int index)
    {
        return new string([(char)('a' + (index / 26)), (char)('a' + (index % 26))]);
    }

    /// <summary>
    /// Asserts that a result is a refusal of a kind and fixed text, without a document.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="kind">The expected kind</param>
    /// <param name="text">The expected text</param>
    /// <param name="context">What the case was, for the messages</param>
    private static void AssertRefusal(CatalogDocumentReadResult result, CatalogDocumentErrorKind kind, string text, string context)
    {
        Assert.IsNull(result.Document, "A refused document should return no document: " + context);
        Assert.IsNotNull(result.Error, "The document should be refused: " + context);
        Assert.AreEqual(kind, result.Error.Kind, "The refusal should have the expected kind: " + context);
        Assert.AreEqual(text, result.Error.Text, "The refusal should have the fixed text of its kind: " + context);
    }

    /// <summary>
    /// Returns documents that are no complete JSON text.
    /// </summary>
    /// <returns>The documents in single-quote notation</returns>
    private static IEnumerable<string> SyntaxDocuments()
    {
        yield return "not json";
        yield return "{";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[]";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[],}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[1,]}";
        yield return "{schemaVersion:1}";
        yield return "{'schemaVersion':01,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':1,'gameVersion':NaN,'entries':[]}";
        yield return "{'schemaVersion':1,'gameVersion':'a\tb','entries':[]}";
        yield return "{'schemaVersion':1,'gameVersion':'a\\qb','entries':[]}";
        yield return "{'schemaVersion':1,'gameVersion':'a\\u12','entries':[]}";
        yield return "/* c */{'schemaVersion':1,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':1,/* c */'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[]} // c";
        yield return Json(ValidDocument) + " x";
        yield return Json(ValidDocument) + "{}";
        yield return Json(ValidDocument) + Json(ValidDocument);
        yield return Json(ValidDocument) + ",";
        yield return Json(ValidDocument) + "]";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[]}}";
    }

    /// <summary>
    /// Returns documents whose root or entries list has the wrong shape.
    /// </summary>
    /// <returns>The documents in single-quote notation</returns>
    private static IEnumerable<string> RootDocuments()
    {
        yield return "[]";
        yield return "[1,2]";
        yield return "null";
        yield return "5";
        yield return "'text'";
        yield return "true";
        yield return "{}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':null}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':{}}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':'x'}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[null]}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[5]}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[[]]}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':['x']}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[{}]}";
        yield return "{'schemaVersion':1,'gameVersion':null,'entries':[],'entries':[]}";
    }

    /// <summary>
    /// Returns documents whose schema version is missing, repeated or no signed 32-bit integer.
    /// </summary>
    /// <returns>The documents in single-quote notation</returns>
    private static IEnumerable<string> MalformedVersionDocuments()
    {
        yield return "{'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':1.0,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':1e0,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':1E0,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':'1','gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':2147483648,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':-2147483649,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':2.0,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':2e0,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':1,'schemaVersion':1,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':2,'schemaVersion':1,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':null,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':true,'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':[1],'gameVersion':null,'entries':[]}";
        yield return "{'schemaVersion':{},'gameVersion':null,'entries':[]}";
        yield return "{'SchemaVersion':1,'gameVersion':null,'entries':[]}";
        yield return "{'schemaversion':1,'gameVersion':null,'entries':[]}";
    }

    /// <summary>
    /// Returns documents that are odd as a whole: empty, a byte order mark, a top-level array and a nesting of 65.
    /// </summary>
    /// <returns>The documents in single-quote notation</returns>
    private static IEnumerable<string> OddDocuments()
    {
        yield return string.Empty;
        yield return "   ";
        yield return "﻿" + Json(ValidDocument);
        yield return "﻿";
        yield return "[]";
        yield return new string('[', 65) + new string(']', 65);
        yield return new string('[', 65);
        yield return $"{{'schemaVersion':1,'gameVersion':null,'entries':{new string('[', 65)}{new string(']', 65)}}}";
        yield return $"{{'a':{string.Concat(Enumerable.Repeat("{'a':", 70))}1{new string('}', 70)}}}";
    }

    /// <summary>
    /// Returns the structure cases: a part of the valid document and its replacement, each giving a malformed document.
    /// </summary>
    /// <returns>The pairs in single-quote notation</returns>
    private static IEnumerable<(string Find, string Replace)> StructurePairs()
    {
        // Missing properties
        yield return ("'gameVersion':'1.2.3.4',", string.Empty);
        yield return ("'entries':[", "'entriez':[");
        yield return ("'guid':5,", string.Empty);
        yield return ("'kind':'region',", string.Empty);
        yield return ("'names':{'en':'Wood','de':'Holz'},", string.Empty);
        yield return ("'regionGuid':7,", string.Empty);
        yield return ("'regionTypes':['Coast','Land'],", string.Empty);
        yield return ("," + IconField, string.Empty);

        // Unknown and repeated properties
        yield return ("'entries':[", "'extra':1,'entries':[");
        yield return ("'guid':5,", "'guid':5,'extra':true,");
        yield return ("'gameVersion':'1.2.3.4',", "'gameVersion':'1.2.3.4','gameVersion':null,");
        yield return ("'guid':5,", "'guid':5,'guid':6,");
        yield return ("'kind':'region',", "'kind':'region','kind':'region',");
        yield return ("'en':'Wood'", "'en':'Wood','en':'Other'");
        yield return ("'en':'Wood','de':'Holz'", "'en':'Wood','de':'Holz','en':'Wood'");
        yield return ("'de':'Holz'", "'de':'Holz','de':'Holz'");

        // Property names in another case
        yield return ("'guid':", "'GUID':");
        yield return ("'guid':", "'Guid':");
        yield return ("'kind':", "'Kind':");
        yield return ("'names':", "'NAMES':");
        yield return ("'regionGuid':", "'regionguid':");
        yield return ("'regionTypes':", "'RegionTypes':");
        yield return ("'icon':", "'Icon':");
        yield return ("'gameVersion':", "'gameversion':");
        yield return ("'entries':", "'Entries':");

        // Values of the wrong JSON type
        yield return ("'gameVersion':'1.2.3.4'", "'gameVersion':1");
        yield return ("'gameVersion':'1.2.3.4'", "'gameVersion':true");
        yield return ("'gameVersion':'1.2.3.4'", "'gameVersion':['1.2.3.4']");
        yield return ("'guid':5", "'guid':'5'");
        yield return ("'guid':5", "'guid':true");
        yield return ("'guid':5", "'guid':[5]");
        yield return ("'kind':'region'", "'kind':1");
        yield return ("'kind':'region'", "'kind':0");
        yield return ("'kind':'region'", "'kind':['region']");
        yield return ("'names':{'en':'Wood','de':'Holz'}", "'names':['Wood']");
        yield return ("'names':{'en':'Wood','de':'Holz'}", "'names':'Wood'");
        yield return ("'en':'Wood'", "'en':5");
        yield return ("'en':'Wood'", "'en':true");
        yield return ("'en':'Wood'", "'en':['Wood']");
        yield return ("'en':'Wood'", "'en':{}");
        yield return ("'regionGuid':7", "'regionGuid':'7'");
        yield return ("'regionGuid':7", "'regionGuid':true");
        yield return ("['Coast','Land']", "'Coast'");
        yield return ("['Coast','Land']", "[1]");
        yield return ("['Coast','Land']", "[['Coast']]");
        yield return ("['Coast','Land']", "{}");
        yield return (IconField, "'icon':5");
        yield return (IconField, "'icon':true");
        yield return (IconField, "'icon':[]");

        // Null where the version allows none
        yield return ("'names':{'en':'Wood','de':'Holz'}", "'names':null");
        yield return ("'guid':5", "'guid':null");
        yield return ("'kind':'region'", "'kind':null");
        yield return ("'en':'Wood'", "'en':null");
        yield return ("['Coast','Land']", "['Coast',null]");
        yield return ("['Coast','Land']", "[null]");

        // Numbers outside the signed 32-bit range, with a fraction or an exponent
        yield return ("'guid':5", "'guid':2147483648");
        yield return ("'guid':5", "'guid':-2147483649");
        yield return ("'guid':5", "'guid':5.0");
        yield return ("'guid':5", "'guid':5.5");
        yield return ("'guid':5", "'guid':5e0");
        yield return ("'guid':5", "'guid':5E0");
        yield return ("'guid':5", "'guid':1e2");
        yield return ("'guid':5", "'guid':99999999999999999999");
        yield return ("'regionGuid':7", "'regionGuid':2147483648");
        yield return ("'regionGuid':7", "'regionGuid':7.0");
        yield return ("'regionGuid':7", "'regionGuid':7e0");

        // Kinds other than the four lowercase words
        yield return ("'kind':'region'", "'kind':'Good'");
        yield return ("'kind':'region'", "'kind':'castle'");
        yield return ("'kind':'region'", "'kind':'GOOD'");
        yield return ("'kind':'region'", "'kind':'Region'");
        yield return ("'kind':'region'", "'kind':''");
        yield return ("'kind':'region'", "'kind':'good '");
        yield return ("'kind':'region'", "'kind':'goods'");
    }

    /// <summary>
    /// Returns the value cases: a part of the valid document and its replacement, each giving an invalid document.
    /// </summary>
    /// <returns>The pairs in single-quote notation</returns>
    private static IEnumerable<(string Find, string Replace)> InvalidPairs()
    {
        // GUIDs
        yield return ("'guid':5", "'guid':0");
        yield return ("}]}", "},{'guid':5,'kind':'good','names':{},'regionGuid':null,'regionTypes':null,'icon':null}]}");
        yield return ("}]}", "},{'guid':-5,'kind':'good','names':{},'regionGuid':null,'regionTypes':null,'icon':null},{'guid':-5,'kind':'good','names':{},'regionGuid':null,'regionTypes':null,'icon':null}]}");
        yield return ("'regionGuid':7", "'regionGuid':null");
        yield return ("'regionGuid':7", "'regionGuid':0");
        yield return ("'kind':'region'", "'kind':'good'");
        yield return ("'kind':'region'", "'kind':'workforce'");
        yield return ("'kind':'region'", "'kind':'building'");

        // Names and language tags
        yield return ("'en':'Wood'", "'en':''");
        yield return ("'de':'Holz'", "'de':''");
        yield return ("'en':'Wood'", "'EN':'Wood'");
        yield return ("'en':'Wood'", "'pt-BR':'Wood'");
        yield return ("'en':'Wood'", "'e':'Wood'");
        yield return ("'en':'Wood'", "'':'Wood'");
        yield return ("'en':'Wood'", "'" + Tag36 + "':'Wood'");
        yield return ("'en':'Wood'", "'english':'Wood'");
        yield return ("'en':'Wood'", "'ens1':'Wood'");
        yield return ("'en':'Wood'", "'e1':'Wood'");
        yield return ("'en':'Wood'", "'1n':'Wood'");
        yield return ("'en':'Wood'", "'en-':'Wood'");
        yield return ("'en':'Wood'", "'-en':'Wood'");
        yield return ("'en':'Wood'", "'en--us':'Wood'");
        yield return ("'en':'Wood'", "'en_us':'Wood'");
        yield return ("'en':'Wood'", "'en us':'Wood'");
        yield return ("'en':'Wood'", "'en-abcdefghi':'Wood'");
        yield return ("'en':'Wood'", "'en-us-':'Wood'");
        yield return ("'en':'Wood'", "'straße':'Wood'");
        yield return ("'en':'Wood'", "'én':'Wood'");
        yield return ("'en':'Wood'", "'en-١':'Wood'");

        // Region types
        yield return ("['Coast','Land']", "['Coast','']");
        yield return ("['Coast','Land']", "['']");

        // Icon references
        yield return (IconField, "'icon':''");
        yield return (IconField, $"'icon':'{Icon[..63]}F'");
        yield return (IconField, $"'icon':'{Icon[..63]}'");
        yield return (IconField, $"'icon':'{Icon}0'");
        yield return (IconField, $"'icon':'{Icon[..63]}g'");
        yield return (IconField, $"'icon':' {Icon[..63]}'");
        yield return (IconField, $"'icon':'{Icon[..63]}\u0661'");
        yield return (IconField, $"'icon':'{Icon.ToUpperInvariant()}'");

        // Game versions
        yield return ("'1.2.3.4'", "''");
        yield return ("'1.2.3.4'", "'0.0.0.0'");
        yield return ("'1.2.3.4'", "'1.2.3'");
        yield return ("'1.2.3.4'", "'1.2.3.4.5'");
        yield return ("'1.2.3.4'", "'01.2.3.4'");
        yield return ("'1.2.3.4'", "'1.2.3.04'");
        yield return ("'1.2.3.4'", "'65536.0.0.0'");
        yield return ("'1.2.3.4'", "'1.2.3.65536'");
        yield return ("'1.2.3.4'", "'+1.0.0.0'");
        yield return ("'1.2.3.4'", "'-1.0.0.0'");
        yield return ("'1.2.3.4'", "' 1.0.0.0'");
        yield return ("'1.2.3.4'", "'1.0.0.0 '");
        yield return ("'1.2.3.4'", "'1..3.4'");
        yield return ("'1.2.3.4'", "'a.b.c.d'");
        yield return ("'1.2.3.4'", "'1,2,3,4'");
        yield return ("'1.2.3.4'", "'١.0.0.0'");
        yield return ("'1.2.3.4'", "'1.0.0.١'");
        yield return ("'1.2.3.4'", "'1.2.3.4\\n'");
    }

    #endregion // Methods
}