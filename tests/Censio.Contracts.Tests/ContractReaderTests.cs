using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Censio.Contracts.Tests;

/// <summary>
/// Tests for <see cref="ContractReader" />.
/// </summary>
[TestClass]
public sealed class ContractReaderTests
{
    #region Constants

    /// <summary>
    /// The fixed text of a malformed message.
    /// </summary>
    private const string MalformedText = "Contract message is malformed";

    /// <summary>
    /// The fixed text of a message that breaks a value rule.
    /// </summary>
    private const string InvalidText = "Contract message breaks a rule of contract version 1";

    /// <summary>
    /// The fixed text of a message above a size bound.
    /// </summary>
    private const string TooLargeText = "Contract message exceeds a size bound";

    /// <summary>
    /// A valid message identity.
    /// </summary>
    private const string Id = "11111111-2222-4333-8444-555555555555";

    /// <summary>
    /// A valid profile identity.
    /// </summary>
    private const string Profile = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";

    /// <summary>
    /// The all-zero UUID.
    /// </summary>
    private const string ZeroId = "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// A valid receive time with seven fractional digits.
    /// </summary>
    private const string Time = "2026-11-12T10:20:30.1234567Z";

    /// <summary>
    /// A valid icon reference or catalog identity: 64 lowercase hex digits.
    /// </summary>
    private const string Icon = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// A second valid icon reference, above <see cref="Icon" /> in ordinal order.
    /// </summary>
    private const string IconTwo = "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// The Base64 text of the four PNG bytes 89 50 4E 47.
    /// </summary>
    private const string PngBase64 = "iVBORw==";

    /// <summary>
    /// The SHA-256 of the four PNG bytes 89 50 4E 47 in lowercase hex.
    /// </summary>
    private const string PngReference = "0f4636c78f65d3639ece5a064b5ae753e3408614a14fb18ab4d7540d2c248543";

    /// <summary>
    /// The Base64 text of the catalog document <c>{"schemaVersion":1,"gameVersion":null,"entries":[]}</c>.
    /// </summary>
    private const string CatalogBase64 = "eyJzY2hlbWFWZXJzaW9uIjoxLCJnYW1lVmVyc2lvbiI6bnVsbCwiZW50cmllcyI6W119";

    /// <summary>
    /// A valid entry in single-quote notation.
    /// </summary>
    private const string EntryNotation = "{'productGuid':1010017,'generation':1.5,'consumption':0.25,'delta':1.25,'perfectGeneration':2,'perfectConsumption':0,'buildingCount':3,'maintenance':-20,'income':null,'profit':7,'summedProductivity':300,'averageProductivity':1,'workforceAmounts':[[1010052,40]],'buildingAmounts':[]}";

    /// <summary>
    /// A valid island in single-quote notation.
    /// </summary>
    private const string IslandNotation = $"{{'sessionGuid':180023,'islandId':1,'areaIndex':0,'sessionId':2,'name':'Isle','entries':[{EntryNotation}]}}";

    /// <summary>
    /// A valid tick in single-quote notation.
    /// </summary>
    private const string TickNotation = $"{{'type':'tick','contractVersion':1,'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}','gameTime':42,'islands':[{IslandNotation}]}}";

    /// <summary>
    /// A valid hello in single-quote notation.
    /// </summary>
    private const string HelloNotation = $"{{'type':'hello','contractVersion':1,'token':'secret-token','companionVersion':'0.3.0','profileId':'{Profile}','profileName':'Alice'}}";

    /// <summary>
    /// A valid accepted hello reply in single-quote notation.
    /// </summary>
    private const string HelloAcceptedNotation = "{'type':'helloAccepted','contractVersion':1,'serverVersion':'0.3.0','catalogIdentity':null}";

    /// <summary>
    /// A valid refused hello reply in single-quote notation.
    /// </summary>
    private const string HelloRefusedNotation = "{'type':'helloRefused','contractVersion':1,'reason':'tokenRefused','receivedVersion':1,'supportedVersions':[1]}";

    /// <summary>
    /// A valid catalog upload in single-quote notation.
    /// </summary>
    private const string CatalogUploadNotation = $"{{'type':'catalogUpload','contractVersion':1,'id':'{Id}','document':'{CatalogBase64}'}}";

    /// <summary>
    /// A valid icon image in single-quote notation.
    /// </summary>
    private const string IconImageNotation = $"{{'type':'iconImage','contractVersion':1,'id':'{Id}','reference':'{PngReference}','png':'{PngBase64}'}}";

    /// <summary>
    /// A valid session start in single-quote notation.
    /// </summary>
    private const string SessionStartNotation = $"{{'type':'sessionStart','contractVersion':1,'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}'}}";

    /// <summary>
    /// A valid session end in single-quote notation.
    /// </summary>
    private const string SessionEndNotation = $"{{'type':'sessionEnd','contractVersion':1,'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}'}}";

    /// <summary>
    /// A valid status in single-quote notation.
    /// </summary>
    private const string StatusNotation = "{'type':'status','contractVersion':1,'pipeState':'receiving'}";

    /// <summary>
    /// A valid acknowledgement in single-quote notation.
    /// </summary>
    private const string AcknowledgementNotation = $"{{'type':'acknowledgement','contractVersion':1,'id':'{Id}','refusal':null,'refusedVersion':null,'missingIcons':['{Icon}']}}";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The inputs of the size and count bound cases, built on demand: the name of the case and its builder.
    /// </summary>
    private static readonly Dictionary<string, Func<byte[]>> _tooLargeInputs = new()
                                                                               {
                                                                                   ["257 islands in a tick"] = () => Utf8(TickWithIslands(IslandsNotation(257), false)),
                                                                                   ["257 islands with type last and islands first"] = () => Utf8(TickWithIslands(IslandsNotation(257), true)),
                                                                                   ["257 islands in a status message"] = () => Utf8($"{{'type':'status','contractVersion':1,'pipeState':'receiving','islands':[{IslandsNotation(257)}]}}"),
                                                                                   ["257 islands in a version 2 tick"] = () => Utf8(TickWithIslands(IslandsNotation(257), false).Replace("'contractVersion':1", "'contractVersion':2", StringComparison.Ordinal)),
                                                                                   ["524,289 entries and pairs in a tick"] = () => Utf8(TickWithIslands(IslandWithEntries(EntriesAndPairs(ContractReader.MaxEntriesAndPairs + 1)), false)),
                                                                                   ["1,025 workforce pairs"] = () => Utf8(TickWithIslands(IslandWithEntries(EntryWithPairs(ContractReader.MaxPairsPerMap + 1, 0)), false)),
                                                                                   ["1,025 building pairs"] = () => Utf8(TickWithIslands(IslandWithEntries(EntryWithPairs(0, ContractReader.MaxPairsPerMap + 1)), false)),
                                                                                   ["17 supported versions"] = () => Utf8($"{{'type':'helloRefused','contractVersion':1,'reason':'unsupportedVersion','receivedVersion':2,'supportedVersions':[{Numbers(17)}]}}"),
                                                                                   ["17 supported versions in a version 2 refusal"] = () => Utf8($"{{'type':'helloRefused','contractVersion':2,'reason':'unsupportedVersion','receivedVersion':1,'supportedVersions':[{Numbers(17)}]}}"),
                                                                                   ["16,385 missing icons"] = () => Utf8($"{{'type':'acknowledgement','contractVersion':1,'id':'{Id}','refusal':null,'refusedVersion':null,'missingIcons':[{MissingIcons(ContractReader.MaxMissingIcons + 1)}]}}"),
                                                                                   ["16,385 missing icons in a tick"] = () => Utf8(Substitute(TickNotation, "'gameTime':42", $"'gameTime':42,'missingIcons':[{MissingIcons(ContractReader.MaxMissingIcons + 1)}]")),
                                                                                   ["token of 1,025 bytes"] = () => Utf8(Substitute(HelloNotation, "'secret-token'", $"'{new string('a', 1025)}'")),
                                                                                   ["token of 1,026 bytes in two-byte characters"] = () => Utf8(Substitute(HelloNotation, "'secret-token'", $"'{new string('ä', 513)}'")),
                                                                                   ["token of 1,025 bytes in a version 2 hello"] = () => Utf8(Substitute(HelloNotation, "'contractVersion':1,'token':'secret-token'", $"'contractVersion':2,'token':'{new string('a', 1025)}'")),
                                                                                   ["profile name of 1,025 bytes"] = () => Utf8(Substitute(HelloNotation, "'Alice'", $"'{new string('a', 1025)}'")),
                                                                                   ["companion version of 1,025 bytes"] = () => Utf8(Substitute(HelloNotation, "'0.3.0'", $"'{new string('a', 1025)}'")),
                                                                                   ["server version of 1,025 bytes"] = () => Utf8(Substitute(HelloAcceptedNotation, "'0.3.0'", $"'{new string('a', 1025)}'")),
                                                                                   ["reference of 1,025 bytes"] = () => Utf8(Substitute(IconImageNotation, $"'{PngReference}'", $"'{new string('a', 1025)}'")),
                                                                                   ["island name of 766 bytes"] = () => Utf8(Substitute(TickNotation, "'Isle'", $"'{new string('a', 766)}'")),
                                                                                   ["island name of 256 replacement characters"] = () => Utf8(Substitute(TickNotation, "'Isle'", $"'{new string((char)0xFFFD, 256)}'")),
                                                                                   ["island name of 766 bytes in a status message"] = () => Utf8($"{{'type':'status','contractVersion':1,'pipeState':'receiving','islands':[{{'name':'{new string('a', 766)}'}}]}}"),
                                                                                   ["property name of 1,025 bytes"] = () => Utf8(Substitute(StatusNotation, "'pipeState'", $"'{new string('a', 1025)}':1,'pipeState'")),
                                                                                   ["property name of 1,025 bytes in a version 2 message"] = () => Utf8(Substitute(StatusNotation, "'contractVersion':1", $"'contractVersion':2,'{new string('a', 1025)}':1")),
                                                                                   ["string of 1,025 bytes in an unknown property"] = () => Utf8(Substitute(StatusNotation, "'pipeState'", $"'future':'{new string('a', 1025)}','pipeState'")),
                                                                                   ["png of 1,025 characters inside an island"] = () => Utf8(Substitute(TickNotation, "'name':'Isle'", $"'name':'Isle','png':'{new string('A', 1028)}'")),
                                                                                   ["png above the binary bound"] = () => Utf8(IconJson(new byte[ContractReader.MaxBinaryBytes + 1])),
                                                                                   ["document above the binary bound"] = () => Utf8($"{{'type':'catalogUpload','contractVersion':1,'id':'{Id}','document':'{Convert.ToBase64String(new byte[ContractReader.MaxBinaryBytes + 1])}'}}")
                                                                               };

    /// <summary>
    /// The inputs of the cases at a size or count bound, built on demand: the name of the case and its builder.
    /// </summary>
    private static readonly Dictionary<string, Func<byte[]>> _atBoundInputs = new()
                                                                              {
                                                                                  ["256 islands"] = () => Utf8(TickWithIslands(IslandsNotation(256), false)),
                                                                                  ["524,288 entries and pairs"] = () => Utf8(TickWithIslands(IslandWithEntries(EntriesAndPairs(ContractReader.MaxEntriesAndPairs)), false)),
                                                                                  ["1,024 pairs in each map"] = () => Utf8(TickWithIslands(IslandWithEntries(EntryWithPairs(ContractReader.MaxPairsPerMap, ContractReader.MaxPairsPerMap)), false)),
                                                                                  ["16 supported versions"] = () => Utf8($"{{'type':'helloRefused','contractVersion':1,'reason':'unsupportedVersion','receivedVersion':17,'supportedVersions':[{Numbers(16)}]}}"),
                                                                                  ["16,384 missing icons"] = () => Utf8($"{{'type':'acknowledgement','contractVersion':1,'id':'{Id}','refusal':null,'refusedVersion':null,'missingIcons':[{MissingIcons(ContractReader.MaxMissingIcons)}]}}"),
                                                                                  ["token of 1,024 bytes"] = () => Utf8(Substitute(HelloNotation, "'secret-token'", $"'{new string('a', 1024)}'")),
                                                                                  ["profile name of 1,024 bytes in three-byte characters"] = () => Utf8(Substitute(HelloNotation, "'Alice'", $"'{new string('木', 341)}a'")),
                                                                                  ["island name of 765 bytes"] = () => Utf8(Substitute(TickNotation, "'Isle'", $"'{new string('a', 765)}'")),
                                                                                  ["island name of 255 replacement characters"] = () => Utf8(Substitute(TickNotation, "'Isle'", $"'{new string((char)0xFFFD, 255)}'")),
                                                                                  ["png at the binary bound"] = () => Utf8(IconJson(BinaryOfLength(ContractReader.MaxBinaryBytes)))
                                                                              };

    /// <summary>
    /// The catalog documents the catalog reader refuses, built on demand: the name of the case and its builder.
    /// </summary>
    private static readonly Dictionary<string, Func<byte[]>> _refusedCatalogs = new()
                                                                                {
                                                                                    ["catalog version 2"] = () => Encoding.UTF8.GetBytes("{\"schemaVersion\":2,\"gameVersion\":null,\"entries\":[]}"),
                                                                                    ["catalog malformed"] = () => Encoding.UTF8.GetBytes("not json"),
                                                                                    ["catalog invalid"] = () => Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"gameVersion\":\"0.0.0.0\",\"entries\":[]}"),
                                                                                    ["catalog too large"] = () => new byte[CatalogDocumentReader.MaxDocumentBytes + 1]
                                                                                };

    /// <summary>
    /// The messages of the refused <c>id</c> cases, built on demand: the name of the case and the builder of its JSON text.
    /// </summary>
    private static readonly Dictionary<string, Func<string>> _refusedIdInputs = new()
                                                                                {
                                                                                    ["tick receive time without fraction"] = () => Substitute(TickNotation, Time, "2026-11-12T10:20:30Z"),
                                                                                    ["tick without islands"] = () => Substitute(TickNotation, $"[{IslandNotation}]", "[]"),
                                                                                    ["session start profile all zero"] = () => Substitute(SessionStartNotation, Profile, ZeroId),
                                                                                    ["session end profile no UUID"] = () => Substitute(SessionEndNotation, Profile, "profile-a"),
                                                                                    ["icon reference not the PNG hash"] = () => Substitute(IconImageNotation, PngReference, Icon),
                                                                                    ["catalog version 2"] = () => CatalogUploadJson("catalog version 2"),
                                                                                    ["catalog malformed"] = () => CatalogUploadJson("catalog malformed"),
                                                                                    ["catalog invalid"] = () => CatalogUploadJson("catalog invalid"),
                                                                                    ["catalog too large"] = () => CatalogUploadJson("catalog too large")
                                                                                };

    /// <summary>
    /// The messages of the cases without a refused <c>id</c>, built on demand: the name of the case and the builder of its
    /// JSON text.
    /// </summary>
    private static readonly Dictionary<string, Func<string>> _noRefusedIdInputs = new()
                                                                                  {
                                                                                      ["tick game time as text"] = () => Substitute(TickNotation, "'gameTime':42", "'gameTime':'42'"),
                                                                                      ["tick without game time"] = () => Substitute(TickNotation, "'gameTime':42,", string.Empty),
                                                                                      ["tick id null"] = () => Substitute(TickNotation, $"'{Id}'", "null"),
                                                                                      ["tick island name too long"] = () => Substitute(TickNotation, "'Isle'", $"'{new string('a', 766)}'"),
                                                                                      ["tick version 2"] = () => Substitute(TickNotation, "'contractVersion':1", "'contractVersion':2"),
                                                                                      ["tick id all zero"] = () => Substitute(TickNotation, Id, ZeroId),
                                                                                      ["tick id no UUID"] = () => Substitute(TickNotation, Id, "tick-a"),
                                                                                      ["tick id uppercase"] = () => Substitute(TickNotation, Id, Profile.ToUpperInvariant()),
                                                                                      ["catalog upload id all zero"] = () => Substitute(CatalogUploadJson("catalog invalid"), Id, ZeroId),
                                                                                      ["acknowledgement with valid id"] = () => Substitute(AcknowledgementNotation, $"['{Icon}']", "['XYZ']"),
                                                                                      ["hello profile all zero"] = () => Substitute(HelloNotation, Profile, ZeroId)
                                                                                  };

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets a valid message of every type: the message in single-quote notation and its type.
    /// </summary>
    private static IEnumerable<object[]> WireFormRows
    {
        get
        {
            yield return new object[] { HelloNotation, ContractMessageType.Hello };
            yield return new object[] { Substitute(HelloNotation, "'secret-token'", "null"), ContractMessageType.Hello };
            yield return new object[] { HelloAcceptedNotation, ContractMessageType.HelloAccepted };
            yield return new object[] { Substitute(HelloAcceptedNotation, "null", $"'{Icon}'"), ContractMessageType.HelloAccepted };
            yield return new object[] { HelloRefusedNotation, ContractMessageType.HelloRefused };
            yield return new object[] { CatalogUploadNotation, ContractMessageType.CatalogUpload };
            yield return new object[] { IconImageNotation, ContractMessageType.IconImage };
            yield return new object[] { TickNotation, ContractMessageType.Tick };
            yield return new object[] { SessionStartNotation, ContractMessageType.SessionStart };
            yield return new object[] { SessionEndNotation, ContractMessageType.SessionEnd };
            yield return new object[] { StatusNotation, ContractMessageType.Status };
            yield return new object[] { AcknowledgementNotation, ContractMessageType.Acknowledgement };
            yield return new object[] { Substitute(AcknowledgementNotation, $"'refusal':null,'refusedVersion':null,'missingIcons':['{Icon}']", "'refusal':'catalogUnsupportedVersion','refusedVersion':2,'missingIcons':[]"), ContractMessageType.Acknowledgement };
        }
    }

    /// <summary>
    /// Gets the version cases: the contract version and the body after it in single-quote notation.
    /// </summary>
    private static IEnumerable<object[]> OtherVersionRows => new[] { 0, 2, -1, int.MaxValue, int.MinValue }.SelectMany(version => OtherVersionBodies().Select(body => new object[] { version, body }));

    /// <summary>
    /// Gets the malformed inputs: the name of the case and the bytes.
    /// </summary>
    private static IEnumerable<object[]> MalformedRows => MalformedInputs().Select(pair => new object[] { pair.Label, pair.Json })
                                                                           .Concat(Enumerable.Range(0, Utf8(TickNotation).Length).Select(length => new object[] { $"tick cut at {length}", Utf8(TickNotation).AsSpan(0, length).ToArray() }));

    /// <summary>
    /// Gets the inputs that break a value rule: the name of the case and the JSON text.
    /// </summary>
    private static IEnumerable<object[]> InvalidRows => InvalidInputs().Select(pair => new object[] { pair.Label, pair.Json });

    /// <summary>
    /// Gets the names of the cases above a size or count bound.
    /// </summary>
    private static IEnumerable<object[]> TooLargeRows => _tooLargeInputs.Keys.Select(label => new object[] { label });

    /// <summary>
    /// Gets the names of the cases at a size or count bound.
    /// </summary>
    private static IEnumerable<object[]> AtBoundRows => _atBoundInputs.Keys.Select(label => new object[] { label });

    /// <summary>
    /// Gets messages that hold marker texts in the token, the profile name, an island name, the catalog and the image data:
    /// the name of the case, the JSON text and the kind it is refused with.
    /// </summary>
    private static IEnumerable<object[]> MarkerRows
    {
        get
        {
            const string markerHello = "{'type':'hello','contractVersion':1,'token':'MARKERTOKEN','companionVersion':'0.3.0','profileId':'" + Profile + "','profileName':'MARKERPROFILE'}";
            const string markerTick = "{'type':'tick','contractVersion':1,'id':'" + Id + "','profileId':'" + Profile + "','receiveTime':'" + Time + "','gameTime':42,'islands':[{'sessionGuid':1,'islandId':1,'areaIndex':0,'sessionId':2,'name':'MARKERISLAND','entries':[]}]}";

            yield return new object[] { "malformed hello", Json(markerHello.Replace("'profileName'", "'MARKERTOKEN':1,'profileName'", StringComparison.Ordinal)), ContractErrorKind.Malformed };
            yield return new object[] { "invalid hello", Json(markerHello.Replace(Profile, ZeroId, StringComparison.Ordinal)), ContractErrorKind.Invalid };
            yield return new object[] { "too large hello", Json(markerHello.Replace("'0.3.0'", $"'{new string('a', 1025)}'", StringComparison.Ordinal)), ContractErrorKind.TooLarge };
            yield return new object[] { "version 2 hello", Json(markerHello.Replace("'contractVersion':1", "'contractVersion':2", StringComparison.Ordinal)), ContractErrorKind.UnsupportedVersion };
            yield return new object[] { "malformed tick", Json(markerTick.Replace("'gameTime':42", "'gameTime':'MARKERISLAND'", StringComparison.Ordinal)), ContractErrorKind.Malformed };
            yield return new object[] { "invalid tick", Json(markerTick.Replace(Id, ZeroId, StringComparison.Ordinal)), ContractErrorKind.Invalid };
            yield return new object[] { "too large tick", Json(markerTick.Replace("'MARKERISLAND'", $"'MARKERISLAND{new string('a', 760)}'", StringComparison.Ordinal)), ContractErrorKind.TooLarge };
            yield return new object[] { "catalog refused", Json($"{{'type':'catalogUpload','contractVersion':1,'id':'{Id}','document':'{Base64("{\"schemaVersion\":1,\"gameVersion\":\"MARKERCATALOG\",\"entries\":[]}")}'}}"), ContractErrorKind.CatalogRefused };
            yield return new object[] { "malformed catalog upload", Json($"{{'type':'catalogUpload','contractVersion':1,'id':'{Id}','document':'{Base64("{\"schemaVersion\":1,\"gameVersion\":\"MARKERCATALOG\",\"entries\":[]}")}','MARKERCATALOG':1}}"), ContractErrorKind.Malformed };
            yield return new object[] { "invalid icon image", Json($"{{'type':'iconImage','contractVersion':1,'id':'{Id}','reference':'{Icon}','png':'{Base64("MARKERIMAGEDATA!!")}'}}"), ContractErrorKind.Invalid };
        }
    }

    /// <summary>
    /// Gets the catalog documents the catalog reader refuses: the name of the case, the catalog kind and text.
    /// </summary>
    private static IEnumerable<object[]> CatalogRefusalRows
    {
        get
        {
            yield return new object[] { "catalog version 2", CatalogDocumentErrorKind.UnsupportedVersion, "Catalog schema version 2 is not supported; supported: 1" };
            yield return new object[] { "catalog malformed", CatalogDocumentErrorKind.Malformed, "Catalog document is malformed" };
            yield return new object[] { "catalog invalid", CatalogDocumentErrorKind.Invalid, "Catalog document breaks a rule of schema version 1" };
            yield return new object[] { "catalog too large", CatalogDocumentErrorKind.TooLarge, "Catalog document exceeds a size bound" };
        }
    }

    /// <summary>
    /// Gets the Companion-to-server messages the reader builds and then refuses in its rules or catalog step, each with the
    /// valid <c>id</c> <see cref="Id" />: the name of the case (a key of <see cref="_refusedIdInputs" />) and the kind it is
    /// refused with.
    /// </summary>
    private static IEnumerable<object[]> RefusedIdRows
    {
        get
        {
            yield return new object[] { "tick receive time without fraction", ContractErrorKind.Invalid };
            yield return new object[] { "tick without islands", ContractErrorKind.Invalid };
            yield return new object[] { "session start profile all zero", ContractErrorKind.Invalid };
            yield return new object[] { "session end profile no UUID", ContractErrorKind.Invalid };
            yield return new object[] { "icon reference not the PNG hash", ContractErrorKind.Invalid };
            yield return new object[] { "catalog version 2", ContractErrorKind.CatalogRefused };
            yield return new object[] { "catalog malformed", ContractErrorKind.CatalogRefused };
            yield return new object[] { "catalog invalid", ContractErrorKind.CatalogRefused };
            yield return new object[] { "catalog too large", ContractErrorKind.CatalogRefused };
        }
    }

    /// <summary>
    /// Gets the refused messages that return no <c>id</c>: malformed, too large, of another version, with an unusable
    /// <c>id</c>, a server-to-Companion type with a valid <c>id</c> and a hello. The name of the case (a key of
    /// <see cref="_noRefusedIdInputs" />) and the kind it is refused with.
    /// </summary>
    private static IEnumerable<object[]> NoRefusedIdRows
    {
        get
        {
            yield return new object[] { "tick game time as text", ContractErrorKind.Malformed };
            yield return new object[] { "tick without game time", ContractErrorKind.Malformed };
            yield return new object[] { "tick id null", ContractErrorKind.Malformed };
            yield return new object[] { "tick island name too long", ContractErrorKind.TooLarge };
            yield return new object[] { "tick version 2", ContractErrorKind.UnsupportedVersion };
            yield return new object[] { "tick id all zero", ContractErrorKind.Invalid };
            yield return new object[] { "tick id no UUID", ContractErrorKind.Invalid };
            yield return new object[] { "tick id uppercase", ContractErrorKind.Invalid };
            yield return new object[] { "catalog upload id all zero", ContractErrorKind.Invalid };
            yield return new object[] { "acknowledgement with valid id", ContractErrorKind.Invalid };
            yield return new object[] { "hello profile all zero", ContractErrorKind.Invalid };
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// The wire form of every message type, as the plan fixes it, is read without a refusal.
    /// </summary>
    /// <param name="notation">The message in single-quote notation</param>
    /// <param name="type">The expected message type</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DynamicData(nameof(WireFormRows))]
    public void ContractReaderWireFormOfEveryTypeIsRead(string notation, ContractMessageType type)
    {
        // Arrange
        var json = Utf8(notation);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, $"The message should be read: {notation}");
        Assert.IsNotNull(result.Message, $"The message should be returned: {notation}");
        Assert.AreEqual(type, result.Message.Type, $"The message should have its type: {notation}");
    }

    /// <summary>
    /// A tick in the wire form reads back every value: key, session ID, name, the product GUID, the eleven values with
    /// no value and integer-written floats, both maps, the receive time to seven digits and the game time.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void ContractReaderTickWireFormReadsEveryValue()
    {
        // Arrange
        var json = Utf8(TickNotation);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        var tick = AssertRead<ContractTick>(result, "wire tick");
        var island = tick.Islands[0];
        var entry = island.Entries[0];

        Assert.AreEqual(Guid.Parse(Id), tick.Id, "The identity should be read");
        Assert.AreEqual(Guid.Parse(Profile), tick.ProfileId, "The profile should be read");
        Assert.AreEqual(new DateTimeOffset(2026, 11, 12, 10, 20, 30, TimeSpan.Zero).AddTicks(1234567), tick.ReceiveTime, "The receive time should be read to seven digits");
        Assert.AreEqual(TimeSpan.Zero, tick.ReceiveTime.Offset, "The receive time should be UTC");
        Assert.AreEqual(42L, tick.GameTime, "The game time should be read");
        Assert.HasCount(1, tick.Islands, "The island should be read");
        Assert.AreEqual(180023, island.SessionGuid, "The session GUID should be read");
        Assert.AreEqual((byte)1, island.IslandId, "The island ID should be read");
        Assert.AreEqual((byte)0, island.AreaIndex, "The area index should be read");
        Assert.AreEqual((byte)2, island.SessionId, "The session ID should be read");
        Assert.AreEqual("Isle", island.Name, "The name should be read");
        Assert.HasCount(1, island.Entries, "The entry should be read");
        Assert.AreEqual(1010017, entry.ProductGuid, "The product GUID should be read");
        Assert.AreEqual(1.5f, entry.Generation, "The generation should be read");
        Assert.AreEqual(0.25f, entry.Consumption, "The consumption should be read");
        Assert.AreEqual(1.25f, entry.Delta, "The delta should be read");
        Assert.AreEqual(2f, entry.PerfectGeneration, "An integer-written float should be read");
        Assert.AreEqual(0f, entry.PerfectConsumption, "A zero should be read as zero");
        Assert.AreEqual(3, entry.BuildingCount, "The building count should be read");
        Assert.AreEqual(-20, entry.Maintenance, "The maintenance should be read");
        Assert.IsNull(entry.Income, "A null float should be read as no value");
        Assert.AreEqual(7, entry.Profit, "The profit should be read");
        Assert.AreEqual(300f, entry.SummedProductivity, "The summed productivity should be read");
        Assert.AreEqual(1f, entry.AverageProductivity, "The average productivity should be read");
        Assert.HasCount(1, entry.WorkforceAmounts, "The workforce map should hold its pair");
        Assert.AreEqual(40, entry.WorkforceAmounts[1010052], "The workforce amount should be read under its GUID");
        Assert.IsEmpty(entry.BuildingAmounts, "The empty building map should be read empty");
    }

    /// <summary>
    /// A session start that carries a text, or a message with a figure name, is malformed: no message has a place for it.
    /// </summary>
    /// <param name="notation">The message in single-quote notation</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow($"{{'type':'sessionStart','contractVersion':1,'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}','figureName':'Marcus'}}")]
    [DataRow($"{{'type':'sessionStart','contractVersion':1,'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}','headline':'Marcus'}}")]
    [DataRow($"{{'type':'sessionStart','contractVersion':1,'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}','text':'Marcus'}}")]
    [DataRow($"{{'type':'sessionEnd','contractVersion':1,'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}','figureName':'Marcus'}}")]
    [DataRow($"{{'type':'tick','contractVersion':1,'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}','gameTime':42,'islands':[],'figureName':'Marcus'}}")]
    [DataRow($"{{'type':'hello','contractVersion':1,'token':null,'companionVersion':'0.3.0','profileId':'{Profile}','profileName':'Alice','figureName':'Marcus'}}")]
    public void ContractReaderSessionStartWithTextIsMalformed(string notation)
    {
        // Arrange
        var json = Utf8(notation);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        AssertRefusal(result, ContractErrorKind.Malformed, MalformedText, notation);
    }

    /// <summary>
    /// A message of a contract version other than 1 is refused as unsupported with the version found, whatever it holds.
    /// </summary>
    /// <param name="version">The contract version</param>
    /// <param name="body">The rest of the message after the version, in single-quote notation</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(OtherVersionRows))]
    public void ContractReaderOtherVersionIsUnsupportedVersion(int version, string body)
    {
        // Arrange
        var notation = $"{{'contractVersion':{version}{body}";
        var json = Utf8(notation);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        AssertUnsupportedVersion(result, version, notation);
    }

    /// <summary>
    /// A version property name written with an escape is still the version.
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void ContractReaderEscapedVersionNameIsFound()
    {
        // Arrange
        var text = Json("""{'type':'status','contract\u0056ersion':2,'pipeState':'receiving'}""");
        var json = Encoding.UTF8.GetBytes(text);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.Contains("\\u0056", text, "The input should still hold the backslash of the escape");
        AssertUnsupportedVersion(result, 2, "escaped version name");
    }

    /// <summary>
    /// A hello of another version is read for its version and refused as unsupported, whatever else it holds.
    /// </summary>
    /// <param name="notation">The hello in single-quote notation</param>
    /// <param name="version">The version of the hello</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow($"{{'type':'hello','contractVersion':2,'token':'secret-token','companionVersion':'0.4.0','profileId':'{Profile}','profileName':'Alice'}}", 2)]
    [DataRow($"{{'type':'hello','contractVersion':2,'token':'secret-token','companionVersion':'0.4.0','profileId':'{Profile}','profileName':'Alice','futureProperty':[1]}}", 2)]
    [DataRow("{'type':'hello','contractVersion':3}", 3)]
    [DataRow("{'contractVersion':0,'type':'hello','profileId':7}", 0)]
    public void ContractReaderHelloOfOtherVersionIsRefusedWithItsVersion(string notation, int version)
    {
        // Arrange
        var json = Utf8(notation);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        AssertUnsupportedVersion(result, version, notation);
    }

    /// <summary>
    /// A refused hello reply of the frozen form is read in any version with its kind, received and supported versions.
    /// </summary>
    /// <param name="notation">The reply in single-quote notation</param>
    /// <param name="reason">The expected refusal kind</param>
    /// <param name="received">The expected received version</param>
    /// <param name="supported">The expected supported versions</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("{'type':'helloRefused','contractVersion':2,'reason':'unsupportedVersion','receivedVersion':1,'supportedVersions':[2]}", ContractHelloRefusal.UnsupportedVersion, 1, new[] { 2 })]
    [DataRow("{'supportedVersions':[2],'receivedVersion':1,'reason':'unsupportedVersion','contractVersion':2,'type':'helloRefused'}", ContractHelloRefusal.UnsupportedVersion, 1, new[] { 2 })]
    [DataRow("{'type':'helloRefused','contractVersion':2,'reason':'tokenRefused','receivedVersion':2,'supportedVersions':[2]}", ContractHelloRefusal.TokenRefused, 2, new[] { 2 })]
    [DataRow("{'type':'helloRefused','contractVersion':3,'reason':'companionConnected','receivedVersion':3,'supportedVersions':[2,3]}", ContractHelloRefusal.CompanionConnected, 3, new[] { 2, 3 })]
    [DataRow("{'type':'helloRefused','contractVersion':1,'reason':'tokenRefused','receivedVersion':1,'supportedVersions':[1]}", ContractHelloRefusal.TokenRefused, 1, new[] { 1 })]
    [DataRow("{'type':'helloRefused','contractVersion':1,'reason':'companionConnected','receivedVersion':1,'supportedVersions':[1]}", ContractHelloRefusal.CompanionConnected, 1, new[] { 1 })]
    [DataRow("{'type':'helloRefused','contractVersion':1,'reason':'unsupportedVersion','receivedVersion':0,'supportedVersions':[1]}", ContractHelloRefusal.UnsupportedVersion, 0, new[] { 1 })]
    public void ContractReaderFutureServerRefusalReadsVersionsBack(string notation, ContractHelloRefusal reason, int received, int[] supported)
    {
        // Arrange
        var json = Utf8(notation);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        var refused = AssertRead<ContractHelloRefused>(result, notation);

        Assert.AreEqual(reason, refused.Reason, $"The refusal kind should be read: {notation}");
        Assert.AreEqual(received, refused.ReceivedVersion, $"The received version should be read: {notation}");
        Assert.AreSequenceEqual(supported, refused.SupportedVersions.ToArray(), $"The supported versions should be read: {notation}");
    }

    /// <summary>
    /// A refused hello reply of another version that is not of the frozen form is refused as unsupported.
    /// </summary>
    /// <param name="notation">The reply in single-quote notation</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("{'type':'helloRefused','contractVersion':2,'reason':'unsupportedVersion','receivedVersion':1,'supportedVersions':[2],'retryAfter':5}")]
    [DataRow("{'type':'helloRefused','contractVersion':2,'reason':'rateLimited','receivedVersion':1,'supportedVersions':[2]}")]
    [DataRow("{'type':'helloRefused','contractVersion':2,'reason':'unsupportedVersion','receivedVersion':1}")]
    [DataRow("{'type':'helloRefused','contractVersion':2,'reason':'unsupportedVersion','receivedVersion':'1','supportedVersions':[2]}")]
    [DataRow("{'type':'helloRefused','contractVersion':2,'reason':'unsupportedVersion','receivedVersion':1,'supportedVersions':'2'}")]
    public void ContractReaderOtherVersionRefusalOfUnknownFormIsUnsupportedVersion(string notation)
    {
        // Arrange
        var json = Utf8(notation);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        AssertUnsupportedVersion(result, 2, notation);
    }

    /// <summary>
    /// An input that is no JSON, is truncated, has an unknown type, an unknown, repeated or missing property, a wrong JSON
    /// type, a lone surrogate, a byte order mark, Base64 that is not canonical, or a number out of range is malformed.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="json">The input</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DynamicData(nameof(MalformedRows))]
    public void ContractReaderMalformedInputIsMalformed(string label, byte[] json)
    {
        // Arrange
        // Act
        var result = ContractReader.Read(json);

        // Assert
        AssertRefusal(result, ContractErrorKind.Malformed, MalformedText, label);
    }

    /// <summary>
    /// A lone surrogate written as an escape is malformed, in a string and in a property name.
    /// </summary>
    /// <param name="find">The text to replace in the valid tick</param>
    /// <param name="replace">The replacement, holding the escape</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("'Isle'", """'Is\ud800le'""")]
    [DataRow("'Isle'", """'\udc00'""")]
    [DataRow("'Isle'", """'\ud800\ud800'""")]
    [DataRow("'name'", """'na\ud800me'""")]
    public void ContractReaderEscapedLoneSurrogateIsMalformed(string find, string replace)
    {
        // Arrange
        var text = Substitute(TickNotation, find, replace);
        var json = Encoding.UTF8.GetBytes(text);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.Contains("\\ud", text, "The input should still hold the backslash of the escape");
        AssertRefusal(result, ContractErrorKind.Malformed, MalformedText, text);
    }

    /// <summary>
    /// Base64 with an escape inside the string is malformed, also where the escape names a Base64 character.
    /// </summary>
    /// <param name="png">The text of the <c>png</c> string, holding the escape</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow("""iVBO\nRw==""")]
    [DataRow("""\u0069VBORw==""")]
    [DataRow("""iVBORw\u003d=""")]
    public void ContractReaderEscapedBase64IsMalformed(string png)
    {
        // Arrange
        var text = Substitute(IconImageNotation, PngBase64, png);
        var json = Encoding.UTF8.GetBytes(text);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.Contains("\\", text, "The input should still hold the backslash of the escape");
        AssertRefusal(result, ContractErrorKind.Malformed, MalformedText, text);
    }

    /// <summary>
    /// A message that breaks a value rule of version 1 is invalid.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="text">The JSON text</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DynamicData(nameof(InvalidRows))]
    public void ContractReaderRuleBreakIsInvalid(string label, string text)
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(text);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        AssertRefusal(result, ContractErrorKind.Invalid, InvalidText, label);
    }

    /// <summary>
    /// A refusal text holds no token, profile name, island name, catalog content or image data, in any kind.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="text">The JSON text</param>
    /// <param name="kind">The kind it is refused with</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DynamicData(nameof(MarkerRows))]
    public void ContractReaderRefusalTextHoldsNoContent(string label, string text, ContractErrorKind kind)
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(text);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.IsNotNull(result.Error, $"The message should be refused: {label}");
        Assert.AreEqual(kind, result.Error.Kind, $"The message should be refused with the kind of its row: {label}");
        Assert.DoesNotContain("MARKERTOKEN", result.Error.Text, $"The text should hold no token: {label}");
        Assert.DoesNotContain("MARKERPROFILE", result.Error.Text, $"The text should hold no profile name: {label}");
        Assert.DoesNotContain("MARKERISLAND", result.Error.Text, $"The text should hold no island name: {label}");
        Assert.DoesNotContain("MARKERCATALOG", result.Error.Text, $"The text should hold no catalog content: {label}");
        Assert.DoesNotContain("MARKERIMAGEDATA", result.Error.Text, $"The text should hold no image data: {label}");
        Assert.DoesNotContain(Base64("MARKERIMAGEDATA!!"), result.Error.Text, $"The text should hold no Base64 image data: {label}");
        Assert.DoesNotContain(Profile, result.Error.Text, $"The text should hold no profile identity: {label}");
    }

    /// <summary>
    /// A message of exactly the byte bound, padded with JSON whitespace, is read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void ContractReaderMessageAtByteBoundIsRead()
    {
        // Arrange
        var json = Padded(StatusNotation, ContractReader.MaxMessageBytes);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.HasCount(ContractReader.MaxMessageBytes, json, "The input should be exactly at the byte bound");
        Assert.AreEqual(ContractPipeState.Receiving, AssertRead<ContractStatus>(result, "message at the byte bound").PipeState, "The status should be read");
    }

    /// <summary>
    /// An input of one byte above the byte bound is too large before anything is parsed, valid, not JSON or of another version.
    /// </summary>
    /// <param name="notation">The message in single-quote notation, padded with spaces</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(StatusNotation)]
    [DataRow("not json")]
    [DataRow("{'type':'status','contractVersion':2,'pipeState':'receiving'}")]
    [DataRow("{'type':'status',")]
    public void ContractReaderInputAboveByteBoundIsTooLarge(string notation)
    {
        // Arrange
        var json = Padded(notation, ContractReader.MaxMessageBytes + 1);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        AssertRefusal(result, ContractErrorKind.TooLarge, TooLargeText, notation);
    }

    /// <summary>
    /// A count or string above its bound is too large, at every position, whatever the type, wherever <c>type</c> stands
    /// and in a message of another version.
    /// </summary>
    /// <param name="label">The name of the case</param>
    [TestMethod]
    [TestCategory("AC6")]
    [TestCategory("AC7")]
    [DynamicData(nameof(TooLargeRows))]
    public void ContractReaderCountAboveBoundIsTooLarge(string label)
    {
        // Arrange
        var json = _tooLargeInputs[label]();

        // Act
        var result = ContractReader.Read(json);

        // Assert
        AssertRefusal(result, ContractErrorKind.TooLarge, TooLargeText, label);
    }

    /// <summary>
    /// A count or string exactly at its bound is read.
    /// </summary>
    /// <param name="label">The name of the case</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DynamicData(nameof(AtBoundRows))]
    public void ContractReaderCountAtBoundIsRead(string label)
    {
        // Arrange
        var json = _atBoundInputs[label]();

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.IsNull(result.Error, $"A message at the bound should be read: {label}");
        Assert.IsNotNull(result.Message, $"A message at the bound should be returned: {label}");
    }

    /// <summary>
    /// String bounds apply after unescaping: 1,024 escaped characters are read, 1,025 are too large.
    /// </summary>
    /// <param name="count">The number of escaped characters in the token</param>
    /// <param name="text">The expected refusal text, or <see langword="null" /> when the hello is read</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(1024, null)]
    [DataRow(1025, TooLargeText)]
    public void ContractReaderEscapedStringIsBoundAfterUnescaping(int count, string? text)
    {
        // Arrange
        var message = Substitute(HelloNotation, "'secret-token'", $"'{string.Concat(Enumerable.Repeat("""\u0061""", count))}'");
        var json = Encoding.UTF8.GetBytes(message);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.Contains("\\u0061", message, "The input should still hold the backslash of the escape");
        Assert.AreEqual(text, result.Error?.Text, $"A token of {count} escaped characters should be refused only above the bound");
    }

    /// <summary>
    /// A Base64 text one character above the bound of 23,767,724 characters after unescaping is too large, also where the
    /// character is an escape and the bytes it decodes to stay within the binary bound; the message stays below the byte bound.
    /// </summary>
    /// <param name="notation">The message that holds the Base64 text</param>
    /// <param name="find">The Base64 text of the message to replace</param>
    /// <param name="escape">The escape inserted into the Base64 text of the binary bound</param>
    [TestMethod]
    [TestCategory("AC7")]
    [DataRow(IconImageNotation, PngBase64, """\n""")]
    [DataRow(CatalogUploadNotation, CatalogBase64, """\n""")]
    [DataRow(IconImageNotation, PngBase64, """\u0020""")]
    public void ContractReaderBase64AboveCharacterBoundIsTooLarge(string notation, string find, string escape)
    {
        // Arrange
        var base64 = Convert.ToBase64String(BinaryOfLength(ContractReader.MaxBinaryBytes));
        var text = string.Concat(base64.AsSpan(0, 4), escape, base64.AsSpan(4));
        var json = Encoding.UTF8.GetBytes(Substitute(notation, find, text));

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.AreEqual(23767724, base64.Length, "The Base64 text of the binary bound should be at the character bound");
        Assert.StartsWith("\\", escape, "The escape should still hold its backslash");
        Assert.IsLessThan(ContractReader.MaxMessageBytes, json.Length, "The message should stay below the byte bound");
        AssertRefusal(result, ContractErrorKind.TooLarge, TooLargeText, $"{find} with {escape}");
    }

    /// <summary>
    /// An island name of 255 replacement characters written as escapes (765 bytes, 1,530 raw) is read whole.
    /// </summary>
    [TestMethod]
    [TestCategory("AC7")]
    public void ContractReaderEscapedReplacementCharacterNameIsRead()
    {
        // Arrange
        var text = Substitute(TickNotation, "'Isle'", $"'{string.Concat(Enumerable.Repeat("""\ufffd""", 255))}'");
        var json = Encoding.UTF8.GetBytes(text);

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.Contains("\\ufffd", text, "The input should still hold the backslash of the escape");
        Assert.AreEqual(new string((char)0xFFFD, 255), AssertRead<ContractTick>(result, "escaped replacement characters").Islands[0].Name, "The name should read back whole");
    }

    /// <summary>
    /// A catalog upload whose catalog document the catalog reader refuses is refused with the catalog's kind and text.
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="catalogKind">The kind of the catalog refusal</param>
    /// <param name="text">The text of the catalog refusal</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DynamicData(nameof(CatalogRefusalRows))]
    public void ContractReaderCatalogUploadWithRefusedDocumentIsCatalogRefused(string label, CatalogDocumentErrorKind catalogKind, string text)
    {
        // Arrange
        var json = Utf8($"{{'type':'catalogUpload','contractVersion':1,'id':'{Id}','document':'{Convert.ToBase64String(_refusedCatalogs[label]())}'}}");

        // Act
        var result = ContractReader.Read(json);

        // Assert
        AssertRefusal(result, ContractErrorKind.CatalogRefused, text, label);
        Assert.IsNotNull(result.Error!.CatalogError, $"The catalog error should be returned: {label}");
        Assert.AreEqual(catalogKind, result.Error.CatalogError.Kind, $"The catalog error should have the catalog's kind: {label}");
        Assert.AreEqual(text, result.Error.CatalogError.Text, $"The catalog error should have the catalog's text: {label}");
    }

    /// <summary>
    /// A tick, session start, session end, icon image or catalog upload the reader built and then refused as invalid or with
    /// a catalog kind returns its <c>id</c>, so the server can acknowledge the refusal (spec E5).
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="kind">The kind it is refused with</param>
    [TestMethod]
    [TestCategory("AC10")]
    [DynamicData(nameof(RefusedIdRows))]
    public void ContractReaderReadInvalidMessageReturnsRefusedId(string label, ContractErrorKind kind)
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(_refusedIdInputs[label]());

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.IsNull(result.Message, $"A refused message should return no message: {label}");
        Assert.IsNotNull(result.Error, $"The message should be refused: {label}");
        Assert.AreEqual(kind, result.Error.Kind, $"The message should be refused with the kind of its row: {label}");
        Assert.AreEqual(Guid.Parse(Id, CultureInfo.InvariantCulture), result.RefusedId, $"The refusal should return the id of the message: {label}");
        Assert.IsNull(result.CatalogDocument, $"A refused message should return no catalog document: {label}");
    }

    /// <summary>
    /// A malformed, too large or other-version message, a message with an unusable <c>id</c>, a server-to-Companion type
    /// with a valid <c>id</c> and a hello return no <c>id</c>, so the server never acknowledges them (spec E5, E6).
    /// </summary>
    /// <param name="label">The name of the case</param>
    /// <param name="kind">The kind it is refused with</param>
    [TestMethod]
    [TestCategory("AC10")]
    [DynamicData(nameof(NoRefusedIdRows))]
    public void ContractReaderReadMalformedMessageReturnsNoRefusedId(string label, ContractErrorKind kind)
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(_noRefusedIdInputs[label]());

        // Act
        var result = ContractReader.Read(json);

        // Assert
        Assert.IsNull(result.Message, $"A refused message should return no message: {label}");
        Assert.IsNotNull(result.Error, $"The message should be refused: {label}");
        Assert.AreEqual(kind, result.Error.Kind, $"The message should be refused with the kind of its row: {label}");
        Assert.IsNull(result.RefusedId, $"The refusal should return no id: {label}");
        Assert.IsNull(result.CatalogDocument, $"A refused message should return no catalog document: {label}");
    }

    /// <summary>
    /// An accepted catalog upload returns the catalog document the reader read, with every field of its entry.
    /// </summary>
    [TestMethod]
    [TestCategory("AC10")]
    public void ContractReaderReadCatalogUploadReturnsCatalogDocument()
    {
        // Arrange
        var document = Json($"{{'schemaVersion':1,'gameVersion':'1.2.3.4','entries':[{{'guid':5,'kind':'region','names':{{'en':'Wood','de':'Holz'}},'regionGuid':7,'regionTypes':['Coast','Land'],'icon':'{Icon}'}}]}}");
        var json = Utf8($"{{'type':'catalogUpload','contractVersion':1,'id':'{Id}','document':'{Base64(document)}'}}");

        // Act
        var result = ContractReader.Read(json);

        // Assert
        var upload = AssertRead<ContractCatalogUpload>(result, "catalog upload with one entry");

        Assert.AreEqual(Guid.Parse(Id, CultureInfo.InvariantCulture), upload.Id, "The upload should keep its id");
        Assert.IsNull(result.RefusedId, "An accepted message should return no refused id");
        Assert.IsNotNull(result.CatalogDocument, "An accepted catalog upload should return its catalog document");
        Assert.AreEqual(1, result.CatalogDocument.SchemaVersion, "The schema version of the catalog should be returned");
        Assert.AreEqual("1.2.3.4", result.CatalogDocument.GameVersion, "The game version of the catalog should be returned");
        Assert.HasCount(1, result.CatalogDocument.Entries, "The entry of the catalog should be returned");

        var entry = result.CatalogDocument.Entries[0];

        Assert.AreEqual(5, entry.AssetGuid, "The GUID of the entry should be returned");
        Assert.AreEqual(CatalogEntryKind.Region, entry.Kind, "The kind of the entry should be returned");
        Assert.AreEqual("Wood", entry.Names["en"], "The English name of the entry should be returned");
        Assert.AreEqual("Holz", entry.Names["de"], "The German name of the entry should be returned");
        Assert.AreEqual(7, entry.RegionGuid, "The region GUID of the entry should be returned");
        Assert.AreEqual(Icon, entry.IconReference, "The icon reference of the entry should be returned");
    }

    /// <summary>
    /// Builds a catalog upload with the valid <c>id</c> <see cref="Id" /> around a catalog document the catalog reader refuses.
    /// </summary>
    /// <param name="label">The name of the refused catalog document</param>
    /// <returns>The JSON text of the upload</returns>
    private static string CatalogUploadJson(string label)
    {
        return Json($"{{'type':'catalogUpload','contractVersion':1,'id':'{Id}','document':'{Convert.ToBase64String(_refusedCatalogs[label]())}'}}");
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
    /// Returns the Base64 text of the UTF-8 bytes of a text.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The Base64 text</returns>
    private static string Base64(string text)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Replaces a part of a message and returns the JSON; the part must exist in it.
    /// </summary>
    /// <param name="notation">The message in single-quote notation</param>
    /// <param name="find">The text to replace, in single-quote notation</param>
    /// <param name="replace">The replacement, in single-quote notation</param>
    /// <returns>The JSON text of the changed message</returns>
    private static string Substitute(string notation, string find, string replace)
    {
        var message = Json(notation);
        var part = Json(find);

        if (message.Contains(part, StringComparison.Ordinal) == false)
        {
            throw new InvalidOperationException("The message does not hold " + part);
        }

        return message.Replace(part, Json(replace), StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns a message padded with trailing spaces to a length.
    /// </summary>
    /// <param name="notation">The message in single-quote notation</param>
    /// <param name="length">The length in bytes</param>
    /// <returns>The bytes of the padded message</returns>
    private static byte[] Padded(string notation, int length)
    {
        var json = new byte[length];

        json.AsSpan().Fill((byte)' ');
        Utf8(notation).CopyTo(json, 0);

        return json;
    }

    /// <summary>
    /// Returns bytes of a length in a repeating pattern.
    /// </summary>
    /// <param name="length">The length</param>
    /// <returns>The bytes</returns>
    private static byte[] BinaryOfLength(int length)
    {
        var bytes = new byte[length];

        for (var index = 0; index < length; index++)
        {
            bytes[index] = (byte)(index * 7);
        }

        return bytes;
    }

    /// <summary>
    /// Builds an icon image of PNG data with its reference, in single-quote notation.
    /// </summary>
    /// <param name="png">The PNG data</param>
    /// <returns>The icon image</returns>
    private static string IconJson(byte[] png)
    {
        return $"{{'type':'iconImage','contractVersion':1,'id':'{Id}','reference':'{Convert.ToHexStringLower(SHA256.HashData(png))}','png':'{Convert.ToBase64String(png)}'}}";
    }

    /// <summary>
    /// Builds a number of islands with distinct keys and no entries, in single-quote notation.
    /// </summary>
    /// <param name="count">The number of islands</param>
    /// <returns>The islands, separated by commas</returns>
    private static string IslandsNotation(int count)
    {
        return string.Join(",", Enumerable.Range(0, count).Select(index => $"{{'sessionGuid':180023,'islandId':{index % 256},'areaIndex':{index / 256},'sessionId':2,'name':'Isle {index}','entries':[]}}"));
    }

    /// <summary>
    /// Builds one island holding entries, in single-quote notation.
    /// </summary>
    /// <param name="entries">The entries, separated by commas</param>
    /// <returns>The island</returns>
    private static string IslandWithEntries(string entries)
    {
        return $"{{'sessionGuid':180023,'islandId':1,'areaIndex':0,'sessionId':2,'name':'Isle','entries':[{entries}]}}";
    }

    /// <summary>
    /// Builds a tick of islands, in single-quote notation.
    /// </summary>
    /// <param name="islands">The islands, separated by commas</param>
    /// <param name="typeLast">Whether the islands come first and the type last</param>
    /// <returns>The tick</returns>
    private static string TickWithIslands(string islands, bool typeLast)
    {
        return typeLast
                   ? $"{{'islands':[{islands}],'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}','gameTime':42,'contractVersion':1,'type':'tick'}}"
                   : $"{{'type':'tick','contractVersion':1,'id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}','gameTime':42,'islands':[{islands}]}}";
    }

    /// <summary>
    /// Builds an entry with a number of workforce and building pairs of ascending GUIDs, in single-quote notation.
    /// </summary>
    /// <param name="workforce">The number of workforce pairs</param>
    /// <param name="building">The number of building pairs</param>
    /// <returns>The entry</returns>
    private static string EntryWithPairs(int workforce, int building)
    {
        return $"{{'productGuid':1010017,'generation':1.5,'consumption':0.25,'delta':1.25,'perfectGeneration':2,'perfectConsumption':0,'buildingCount':3,'maintenance':-20,'income':null,'profit':7,'summedProductivity':300,'averageProductivity':1,'workforceAmounts':[{Pairs(workforce)}],'buildingAmounts':[{Pairs(building)}]}}";
    }

    /// <summary>
    /// Builds map pairs of ascending GUIDs, in single-quote notation.
    /// </summary>
    /// <param name="count">The number of pairs</param>
    /// <returns>The pairs, separated by commas</returns>
    private static string Pairs(int count)
    {
        return string.Join(",", Enumerable.Range(1, count).Select(guid => $"[{guid},7]"));
    }

    /// <summary>
    /// Builds entries whose entries and pairs add up to a total: entries of 1,024 workforce and 1,023 building pairs, and
    /// one last entry for the rest.
    /// </summary>
    /// <param name="total">The number of entries and pairs</param>
    /// <returns>The entries, separated by commas</returns>
    private static string EntriesAndPairs(int total)
    {
        const int full = 1 + 1024 + 1023;
        var fullEntries = total / full;
        var rest = total % full;
        var entries = Enumerable.Repeat(EntryWithPairs(1024, 1023), fullEntries).ToList();

        if (rest > 0)
        {
            entries.Add(EntryWithPairs(Math.Min(rest - 1, 1024), Math.Max(rest - 1 - 1024, 0)));
        }

        return string.Join(",", entries);
    }

    /// <summary>
    /// Builds the numbers 1 to a count.
    /// </summary>
    /// <param name="count">The count</param>
    /// <returns>The numbers, separated by commas</returns>
    private static string Numbers(int count)
    {
        return string.Join(",", Enumerable.Range(1, count));
    }

    /// <summary>
    /// Builds distinct, ascending icon references of 64 lowercase hex digits, in single-quote notation.
    /// </summary>
    /// <param name="count">The number of references</param>
    /// <returns>The references, separated by commas</returns>
    private static string MissingIcons(int count)
    {
        return string.Join(",", Enumerable.Range(1, count).Select(index => $"'{index.ToString("x64", CultureInfo.InvariantCulture)}'"));
    }

    /// <summary>
    /// Asserts that a result is a refusal of a kind and fixed text, without a message.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="kind">The expected kind</param>
    /// <param name="text">The expected text</param>
    /// <param name="context">What the case was, for the messages</param>
    private static void AssertRefusal(ContractReadResult result, ContractErrorKind kind, string text, string context)
    {
        Assert.IsNull(result.Message, "A refused message should return no message: " + context);
        Assert.IsNotNull(result.Error, "The message should be refused: " + context);
        Assert.AreEqual(kind, result.Error.Kind, "The refusal should have the expected kind: " + context);
        Assert.AreEqual(text, result.Error.Text, "The refusal should have the fixed text of its kind: " + context);
    }

    /// <summary>
    /// Asserts that a result is a refusal as unsupported version with the version found.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="version">The version found</param>
    /// <param name="context">What the case was, for the messages</param>
    private static void AssertUnsupportedVersion(ContractReadResult result, int version, string context)
    {
        AssertRefusal(result, ContractErrorKind.UnsupportedVersion, $"Contract version {version} is not supported; supported: 1", context);
        Assert.AreEqual(version, result.Error!.Version, "The refusal should carry the version found: " + context);
    }

    /// <summary>
    /// Asserts that a result holds a message of a type and no error, and returns it.
    /// </summary>
    /// <typeparam name="T">The expected message type</typeparam>
    /// <param name="result">The result</param>
    /// <param name="context">What the case was, for the messages</param>
    /// <returns>The message</returns>
    private static T AssertRead<T>(ContractReadResult result, string context)
        where T : ContractMessage
    {
        Assert.IsNull(result.Error, "The message should be read: " + context);

        return Assert.IsInstanceOfType<T>(result.Message, "The message should have its type: " + context);
    }

    /// <summary>
    /// Returns the bodies after the version of the version cases: valid, with an unknown property, a wrong JSON type, an
    /// unknown type and a structure of another version.
    /// </summary>
    /// <returns>The bodies in single-quote notation</returns>
    private static IEnumerable<string> OtherVersionBodies()
    {
        yield return ",'type':'status','pipeState':'receiving'}";
        yield return ",'type':'status','pipeState':'receiving','futureProperty':1}";
        yield return ",'type':'status','pipeState':5}";
        yield return ",'type':'futureType','payload':{'a':[1,2]}}";
        yield return ",'type':'tick','islands':'none'}";
        yield return $",'type':'tick','id':'{Id}','profileId':'{Profile}','receiveTime':'{Time}','gameTime':42,'islands':[]}}";
        yield return $",'type':'acknowledgement','id':'{ZeroId}','refusal':'futureRefusal','refusedVersion':null,'missingIcons':['XYZ']}}";
    }

    /// <summary>
    /// Returns the malformed inputs.
    /// </summary>
    /// <returns>The name of each case and its bytes</returns>
    private static IEnumerable<(string Label, byte[] Json)> MalformedInputs()
    {
        yield return ("empty input", []);
        yield return ("not json", Utf8("not json"));
        yield return ("null", Utf8("null"));
        yield return ("root array", Utf8("[" + StatusNotation + "]"));
        yield return ("root string", Utf8("'status'"));
        yield return ("byte order mark", [0xEF, 0xBB, 0xBF, .. Utf8(StatusNotation)]);
        yield return ("content after the root", Utf8(StatusNotation + "{}"));
        yield return ("text after the root", Utf8(StatusNotation + " x"));
        yield return ("trailing comma", Utf8(Substitute(StatusNotation, "'receiving'", "'receiving',")));
        yield return ("single quotes", Encoding.UTF8.GetBytes(StatusNotation));
        yield return ("comment", Utf8(Substitute(StatusNotation, "'pipeState'", "/* c */'pipeState'")));
        yield return ("depth 65", Utf8(Substitute(StatusNotation, "'pipeState'", $"'future':{new string('[', 64)}{new string(']', 64)},'pipeState'")));
        yield return ("truncated version 2", Utf8("{'type':'status','contractVersion':2,'pipeState':'receiving'"));
        yield return ("depth 65 in version 2", Utf8($"{{'type':'status','contractVersion':2,'future':{new string('[', 64)}{new string(']', 64)}}}"));
        yield return ("invalid UTF-8 in a string", [.. Utf8("{'type':'status','contractVersion':1,'pipeState':'rec"), 0xC3, 0x28, .. Utf8("'}")]);
        yield return ("invalid UTF-8 in an island name", [.. Utf8(TickNotation[..TickNotation.IndexOf("Isle", StringComparison.Ordinal)]), 0xED, 0xA0, 0x80, .. Utf8(TickNotation[TickNotation.IndexOf("Isle", StringComparison.Ordinal)..])]);
        yield return ("version missing", Utf8("{'type':'status','pipeState':'receiving'}"));
        yield return ("version twice", Utf8("{'type':'status','contractVersion':1,'contractVersion':1,'pipeState':'receiving'}"));
        yield return ("version as text", Utf8(Substitute(StatusNotation, "'contractVersion':1", "'contractVersion':'1'")));
        yield return ("version with a fraction", Utf8(Substitute(StatusNotation, "'contractVersion':1", "'contractVersion':1.0")));
        yield return ("version with an exponent", Utf8(Substitute(StatusNotation, "'contractVersion':1", "'contractVersion':1e0")));
        yield return ("version above 32 bits", Utf8(Substitute(StatusNotation, "'contractVersion':1", "'contractVersion':4294967297")));
        yield return ("version null", Utf8(Substitute(StatusNotation, "'contractVersion':1", "'contractVersion':null")));
        yield return ("version only inside an island", Utf8(Substitute(TickNotation, "'contractVersion':1,", string.Empty).Replace("\"sessionGuid\"", "\"contractVersion\":1,\"sessionGuid\"", StringComparison.Ordinal)));
        yield return ("version in another case", Utf8(Substitute(StatusNotation, "'contractVersion'", "'ContractVersion'")));
        yield return ("type missing", Utf8("{'contractVersion':1,'pipeState':'receiving'}"));
        yield return ("type twice", Utf8("{'type':'status','type':'status','contractVersion':1,'pipeState':'receiving'}"));
        yield return ("type a number", Utf8(Substitute(StatusNotation, "'type':'status'", "'type':9")));
        yield return ("type null", Utf8(Substitute(StatusNotation, "'type':'status'", "'type':null")));
        yield return ("unknown type", Utf8(Substitute(StatusNotation, "'type':'status'", "'type':'futureType'")));
        yield return ("type in another case", Utf8(Substitute(StatusNotation, "'type':'status'", "'type':'Status'")));
        yield return ("unknown property", Utf8(Substitute(StatusNotation, "'pipeState'", "'future':1,'pipeState'")));
        yield return ("unknown property in an island", Utf8(Substitute(TickNotation, "'name':'Isle'", "'name':'Isle','future':1")));
        yield return ("unknown property in an entry", Utf8(Substitute(TickNotation, "'profit':7", "'profit':7,'future':1")));
        yield return ("property in another case", Utf8(Substitute(StatusNotation, "'pipeState'", "'PipeState'")));
        yield return ("repeated property", Utf8(Substitute(TickNotation, "'gameTime':42", "'gameTime':42,'gameTime':42")));
        yield return ("missing property", Utf8(Substitute(TickNotation, "'gameTime':42,", string.Empty)));
        yield return ("missing nullable token", Utf8(Substitute(HelloNotation, "'token':'secret-token',", string.Empty)));
        yield return ("missing nullable float", Utf8(Substitute(TickNotation, "'income':null,", string.Empty)));
        yield return ("missing catalog identity", Utf8(Substitute(HelloAcceptedNotation, ",'catalogIdentity':null", string.Empty)));
        yield return ("missing refused version", Utf8(Substitute(AcknowledgementNotation, "'refusedVersion':null,", string.Empty)));
        yield return ("version 1 refusal with a future property", Utf8(Substitute(HelloRefusedNotation, "'supportedVersions':[1]", "'supportedVersions':[1],'retryAfter':5")));
        yield return ("game time as text", Utf8(Substitute(TickNotation, "'gameTime':42", "'gameTime':'42'")));
        yield return ("game time above 64 bits", Utf8(Substitute(TickNotation, "'gameTime':42", "'gameTime':9223372036854775808")));
        yield return ("game time with a fraction", Utf8(Substitute(TickNotation, "'gameTime':42", "'gameTime':42.5")));
        yield return ("session GUID above 32 bits", Utf8(Substitute(TickNotation, "'sessionGuid':180023", "'sessionGuid':2147483648")));
        yield return ("island ID of 256", Utf8(Substitute(TickNotation, "'islandId':1", "'islandId':256")));
        yield return ("island ID of -1", Utf8(Substitute(TickNotation, "'islandId':1", "'islandId':-1")));
        yield return ("area index of 256", Utf8(Substitute(TickNotation, "'areaIndex':0", "'areaIndex':256")));
        yield return ("session ID of 256", Utf8(Substitute(TickNotation, "'sessionId':2", "'sessionId':256")));
        yield return ("building count with a fraction", Utf8(Substitute(TickNotation, "'buildingCount':3", "'buildingCount':3.5")));
        yield return ("float as text", Utf8(Substitute(TickNotation, "'generation':1.5", "'generation':'1.5'")));
        yield return ("float 1e39", Utf8(Substitute(TickNotation, "'generation':1.5", "'generation':1e39")));
        yield return ("float -1e39", Utf8(Substitute(TickNotation, "'delta':1.25", "'delta':-1e39")));
        yield return ("float 3.4028236E+38", Utf8(Substitute(TickNotation, "'income':null", "'income':3.4028236E+38")));
        yield return ("float 1e39 written as an integer", Utf8(Substitute(TickNotation, "'consumption':0.25", "'consumption':1000000000000000000000000000000000000000")));
        yield return ("float 1e39 in the average productivity", Utf8(Substitute(TickNotation, "'averageProductivity':1", "'averageProductivity':1e39")));
        yield return ("map pair of one number", Utf8(Substitute(TickNotation, "[[1010052,40]]", "[[1010052]]")));
        yield return ("map pair of three numbers", Utf8(Substitute(TickNotation, "[[1010052,40]]", "[[1010052,40,1]]")));
        yield return ("map pair as object", Utf8(Substitute(TickNotation, "[[1010052,40]]", "[{'guid':1010052,'amount':40}]")));
        yield return ("map as object", Utf8(Substitute(TickNotation, "[[1010052,40]]", "{'1010052':40}")));
        yield return ("map pair with a fraction", Utf8(Substitute(TickNotation, "[[1010052,40]]", "[[1010052,40.5]]")));
        yield return ("map pair with text", Utf8(Substitute(TickNotation, "[[1010052,40]]", "[['1010052',40]]")));
        yield return ("map pair null", Utf8(Substitute(TickNotation, "[[1010052,40]]", "[null]")));
        yield return ("map null", Utf8(Substitute(TickNotation, "'buildingAmounts':[]", "'buildingAmounts':null")));
        yield return ("islands null", Utf8(Substitute(TickNotation, $"[{IslandNotation}]", "null")));
        yield return ("islands [null]", Utf8(Substitute(TickNotation, $"[{IslandNotation}]", "[null]")));
        yield return ("entries [null]", Utf8(Substitute(TickNotation, $"[{EntryNotation}]", "[null]")));
        yield return ("missingIcons [null]", Utf8(Substitute(AcknowledgementNotation, $"['{Icon}']", "[null]")));
        yield return ("supportedVersions [null]", Utf8(Substitute(HelloRefusedNotation, "[1]", "[null]")));
        yield return ("island name null", Utf8(Substitute(TickNotation, "'Isle'", "null")));
        yield return ("profile name null", Utf8(Substitute(HelloNotation, "'Alice'", "null")));
        yield return ("identity null", Utf8(Substitute(TickNotation, $"'id':'{Id}'", "'id':null")));
        yield return ("identity a number", Utf8(Substitute(TickNotation, $"'id':'{Id}'", "'id':7")));
        yield return ("receive time a number", Utf8(Substitute(TickNotation, $"'{Time}'", "638000000000000000")));
        yield return ("pipe state unknown", Utf8(Substitute(StatusNotation, "'receiving'", "'sleeping'")));
        yield return ("pipe state a number", Utf8(Substitute(StatusNotation, "'receiving'", "1")));
        yield return ("pipe state in another case", Utf8(Substitute(StatusNotation, "'receiving'", "'Receiving'")));
        yield return ("refusal reason unknown", Utf8(Substitute(HelloRefusedNotation, "'tokenRefused'", "'rateLimited'")));
        yield return ("acknowledgement refusal a number", Utf8(Substitute(AcknowledgementNotation, "'refusal':null", "'refusal':2")));
        yield return ("Base64 with a space", Utf8(Substitute(IconImageNotation, PngBase64, "iVBO Rw==")));
        yield return ("Base64 without padding", Utf8(Substitute(IconImageNotation, PngBase64, "iVBORw")));
        yield return ("Base64 with too much padding", Utf8(Substitute(IconImageNotation, PngBase64, "iVBORw===")));
        yield return ("Base64 not canonical", Utf8(Substitute(IconImageNotation, PngBase64, "iVBORx==")));
        yield return ("Base64 URL alphabet", Utf8(Substitute(IconImageNotation, PngBase64, "iVBOR-==")));
        yield return ("Base64 with a line break", [.. Utf8(IconImageNotation[..IconImageNotation.IndexOf("Rw==", StringComparison.Ordinal)]), 0x0A, .. Utf8(IconImageNotation[IconImageNotation.IndexOf("Rw==", StringComparison.Ordinal)..])]);
        yield return ("invalid UTF-8 in a Base64 png", [.. Utf8(IconImageNotation[..IconImageNotation.IndexOf("Rw==", StringComparison.Ordinal)]), 0xC3, 0x28, .. Utf8(IconImageNotation[IconImageNotation.IndexOf("Rw==", StringComparison.Ordinal)..])]);
        yield return ("missing icons a string", Utf8(Substitute(AcknowledgementNotation, $"['{Icon}']", $"'{Icon}'")));
        yield return ("Base64 png a number", Utf8(Substitute(IconImageNotation, $"'{PngBase64}'", "7")));
        yield return ("Base64 document with a space", Utf8(Substitute(CatalogUploadNotation, "eyJzY2hl", "eyJz Y2hl")));
        yield return ("Base64 document without padding", Utf8($"{{'type':'catalogUpload','contractVersion':1,'id':'{Id}','document':'{Base64("not json").TrimEnd('=')}'}}"));
        yield return ("root name of 800 bytes is no island name", Utf8(Substitute(TickNotation, "'gameTime':42", $"'gameTime':42,'name':'{new string('a', 800)}'")));
        yield return ("root png of 1,100 Base64 characters in a status message", Utf8(Substitute(StatusNotation, "'pipeState'", $"'png':'{new string('A', 1100)}','pipeState'")));
    }

    /// <summary>
    /// Returns the inputs that break a value rule of version 1.
    /// </summary>
    /// <returns>The name of each case and its JSON text</returns>
    private static IEnumerable<(string Label, string Json)> InvalidInputs()
    {
        yield return ("token empty", Substitute(HelloNotation, "'secret-token'", "''"));
        yield return ("companion version empty", Substitute(HelloNotation, "'0.3.0'", "''"));
        yield return ("profile name empty", Substitute(HelloNotation, "'Alice'", "''"));
        yield return ("profile identity all zero", Substitute(HelloNotation, Profile, ZeroId));
        yield return ("profile identity uppercase", Substitute(HelloNotation, Profile, Profile.ToUpperInvariant()));
        yield return ("profile identity no UUID", Substitute(HelloNotation, Profile, "not-a-uuid"));
        yield return ("profile identity in braces", Substitute(HelloNotation, Profile, "{" + Profile + "}"));
        yield return ("profile identity without hyphens", Substitute(HelloNotation, Profile, Profile.Replace("-", string.Empty, StringComparison.Ordinal)));
        yield return ("server version empty", Substitute(HelloAcceptedNotation, "'0.3.0'", "''"));
        yield return ("catalog identity uppercase", Substitute(HelloAcceptedNotation, "null", $"'{Icon.ToUpperInvariant()}'"));
        yield return ("catalog identity of 63 digits", Substitute(HelloAcceptedNotation, "null", $"'{Icon[..63]}'"));
        yield return ("catalog identity of 65 digits", Substitute(HelloAcceptedNotation, "null", $"'{Icon}0'"));
        yield return ("catalog identity empty", Substitute(HelloAcceptedNotation, "null", "''"));
        yield return ("catalog identity not hex", Substitute(HelloAcceptedNotation, "null", $"'{Icon[..63]}g'"));
        yield return ("supported versions empty", Substitute(HelloRefusedNotation, "[1]", "[]"));
        yield return ("supported versions descending", Substitute(HelloRefusedNotation, "[1]", "[2,1]"));
        yield return ("supported versions repeated", Substitute(HelloRefusedNotation, "[1]", "[1,1]"));
        yield return ("supported version zero", Substitute(HelloRefusedNotation, "[1]", "[0]"));
        yield return ("supported version negative", Substitute(HelloRefusedNotation, "[1]", "[-1,1]"));
        yield return ("catalog upload identity all zero", Substitute(CatalogUploadNotation, Id, ZeroId));
        yield return ("icon identity all zero", Substitute(IconImageNotation, Id, ZeroId));
        yield return ("icon reference not the PNG hash", Substitute(IconImageNotation, PngReference, Icon));
        yield return ("icon reference uppercase", Substitute(IconImageNotation, PngReference, PngReference.ToUpperInvariant()));
        yield return ("icon reference of 63 digits", Substitute(IconImageNotation, PngReference, PngReference[..63]));
        yield return ("icon PNG empty", Substitute(IconImageNotation, $"'{PngReference}','png':'{PngBase64}'", $"'{Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>()))}','png':''"));
        yield return ("tick identity all zero", Substitute(TickNotation, $"'id':'{Id}'", $"'id':'{ZeroId}'"));
        yield return ("tick identity no UUID", Substitute(TickNotation, $"'id':'{Id}'", "'id':'tick-1'"));
        yield return ("tick profile all zero", Substitute(TickNotation, Profile, ZeroId));
        yield return ("receive time without fraction", Substitute(TickNotation, Time, "2026-11-12T10:20:30Z"));
        yield return ("receive time of six digits", Substitute(TickNotation, Time, "2026-11-12T10:20:30.123456Z"));
        yield return ("receive time of eight digits", Substitute(TickNotation, Time, "2026-11-12T10:20:30.12345678Z"));
        yield return ("receive time with an offset", Substitute(TickNotation, Time, "2026-11-12T10:20:30.1234567+01:00"));
        yield return ("receive time with a zero offset", Substitute(TickNotation, Time, "2026-11-12T10:20:30.1234567+00:00"));
        yield return ("receive time without zone", Substitute(TickNotation, Time, "2026-11-12T10:20:30.1234567"));
        yield return ("receive time no time", Substitute(TickNotation, Time, "yesterday"));
        yield return ("receive time with a space for the T", Substitute(TickNotation, Time, "2026-11-12 10:20:30.1234567Z"));
        yield return ("receive time with a letter for a digit", Substitute(TickNotation, Time, "2026-11-12T10:2a:30.1234567Z"));
        yield return ("receive time of a day that does not exist", Substitute(TickNotation, Time, "2026-02-30T10:20:30.1234567Z"));
        yield return ("tick without islands", Substitute(TickNotation, $"[{IslandNotation}]", "[]"));
        yield return ("island key twice", Substitute(TickNotation, $"[{IslandNotation}]", $"[{IslandNotation},{IslandNotation.Replace("'sessionId':2,'name':'Isle'", "'sessionId':3,'name':'Other'", StringComparison.Ordinal)}]"));
        yield return ("map GUIDs descending", Substitute(TickNotation, "[[1010052,40]]", "[[2,1],[1,1]]"));
        yield return ("map GUID twice", Substitute(TickNotation, "[[1010052,40]]", "[[1,1],[1,2]]"));
        yield return ("building map GUIDs descending", Substitute(TickNotation, "'buildingAmounts':[]", "'buildingAmounts':[[5,1],[-5,1]]"));
        yield return ("session start identity all zero", Substitute(SessionStartNotation, Id, ZeroId));
        yield return ("session start profile all zero", Substitute(SessionStartNotation, Profile, ZeroId));
        yield return ("session start receive time without fraction", Substitute(SessionStartNotation, Time, "2026-11-12T10:20:30Z"));
        yield return ("session end identity all zero", Substitute(SessionEndNotation, Id, ZeroId));
        yield return ("session end profile no UUID", Substitute(SessionEndNotation, Profile, "profile-a"));
        yield return ("acknowledgement identity all zero", Substitute(AcknowledgementNotation, Id, ZeroId));
        yield return ("accepted with a refused version", Substitute(AcknowledgementNotation, "'refusedVersion':null", "'refusedVersion':2"));
        yield return ("unsupported version without a refused version", Substitute(AcknowledgementNotation, $"'refusal':null,'refusedVersion':null,'missingIcons':['{Icon}']", "'refusal':'unsupportedVersion','refusedVersion':null,'missingIcons':[]"));
        yield return ("catalog unsupported version without a refused version", Substitute(AcknowledgementNotation, $"'refusal':null,'refusedVersion':null,'missingIcons':['{Icon}']", "'refusal':'catalogUnsupportedVersion','refusedVersion':null,'missingIcons':[]"));
        yield return ("invalid with a refused version", Substitute(AcknowledgementNotation, $"'refusal':null,'refusedVersion':null,'missingIcons':['{Icon}']", "'refusal':'invalid','refusedVersion':2,'missingIcons':[]"));
        yield return ("refusal with missing icons", Substitute(AcknowledgementNotation, "'refusal':null", "'refusal':'malformed'"));
        yield return ("missing icon not hex", Substitute(AcknowledgementNotation, $"['{Icon}']", "['XYZ']"));
        yield return ("missing icon uppercase", Substitute(AcknowledgementNotation, $"['{Icon}']", $"['{Icon.ToUpperInvariant()}']"));
        yield return ("missing icons descending", Substitute(AcknowledgementNotation, $"['{Icon}']", $"['{IconTwo}','{Icon}']"));
        yield return ("missing icon twice", Substitute(AcknowledgementNotation, $"['{Icon}']", $"['{Icon}','{Icon}']"));
    }

    #endregion // Methods
}