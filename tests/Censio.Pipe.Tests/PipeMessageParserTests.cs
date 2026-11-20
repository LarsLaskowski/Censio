using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeMessageParser" />.
/// </summary>
[TestClass]
public sealed class PipeMessageParserTests
{
    #region Constants

    /// <summary>
    /// The level of the routes of a trade-route frame.
    /// </summary>
    private const string RoutesLevel = "routes";

    /// <summary>
    /// The level of the stations of a route.
    /// </summary>
    private const string StationsLevel = "stations";

    /// <summary>
    /// The level of the goods of a station.
    /// </summary>
    private const string GoodsLevel = "goods";

    /// <summary>
    /// The level of the offers of an item-offer frame.
    /// </summary>
    private const string OffersLevel = "offers";

    /// <summary>
    /// The level of the items of an offer.
    /// </summary>
    private const string ItemsLevel = "items";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// An empty list of map pairs.
    /// </summary>
    private static readonly KeyValuePair<int, int>[] _noPairs = [];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the frames of a version message and of a statistics message, cut at every length.
    /// </summary>
    public static IEnumerable<object[]> TruncatedFrames
    {
        get
        {
            var version = new PipeFrameBuilder().Byte(0).Int32Value(2).Build();

            for (var length = 1; length < version.Length; length++)
            {
                yield return [version[..length]];
            }

            var statistics = CreateStatistics(1).Entry(10, [new(1, 2)], [new(3, 4)]).Build();

            for (var length = 1; length < statistics.Length; length++)
            {
                yield return [statistics[..length]];
            }
        }
    }

    /// <summary>
    /// Gets frames of each type, with and without trailing bytes, and refused frames, with the expected consumed length.
    /// </summary>
    public static IEnumerable<object[]> ConsumedLengthFrames
    {
        get
        {
            var statistics = CreateStatistics(2).Entry(10, [new(1, 2), new(1, 3)], [new(3, 4)]).Entry(11, _noPairs, _noPairs).Build();

            yield return ["version", new PipeFrameBuilder().Byte(0).Int32Value(2).Build(), 5];
            yield return ["version with trailing bytes", new PipeFrameBuilder().Byte(0).Int32Value(2).Raw(0xAA, 0xBB).Build(), 5];
            yield return ["session start", new PipeFrameBuilder().Byte(1).StringValue("abc").Build(), 5];
            yield return ["session start with trailing bytes", new PipeFrameBuilder().Byte(1).StringValue("abc").Raw(0xAA).Build(), 5];
            yield return ["empty session start", new PipeFrameBuilder().Byte(1).StringValue(string.Empty).Build(), 2];
            yield return ["session end", new PipeFrameBuilder().Byte(2).Build(), 1];
            yield return ["session end with trailing bytes", new PipeFrameBuilder().Byte(2).Raw(0xAA, 0xBB, 0xCC).Build(), 1];
            yield return ["statistics", statistics, statistics.Length];
            yield return ["statistics with trailing bytes", Concat(statistics, [0xAA, 0xBB]), statistics.Length];
            yield return ["empty frame", Array.Empty<byte>(), 0];
            yield return ["unknown type", new PipeFrameBuilder().Byte(9).Int32Value(2).Build(), 0];
            yield return ["truncated version", new PipeFrameBuilder().Byte(0).Raw(2, 0).Build(), 0];
            yield return ["truncated statistics", statistics[..^1], 0];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A version frame gives the version.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserVersionFrameReturnsVersionMessage()
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(0).Int32Value(2).Build();

        // Act
        var message = ParseMessage<VersionMessage>(frame);

        // Assert
        Assert.AreEqual(2, message.Version, "The version should be read");
        Assert.AreEqual(PipeMessageType.Version, message.MessageType, "The message type should be Version");
    }

    /// <summary>
    /// A session start frame gives the headline, a 200-byte length byte read as unsigned.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserSessionStartFrameReturnsHeadline()
    {
        // Arrange
        var headline = new string('h', 200);
        var frame = new PipeFrameBuilder().Byte(1).StringValue(headline).Build();

        // Act
        var message = ParseMessage<SessionStartMessage>(frame);

        // Assert
        Assert.AreEqual(headline, message.Headline, "The 200-byte headline should be read in full");
        Assert.AreEqual(PipeMessageType.SessionStart, message.MessageType, "The message type should be SessionStart");
    }

    /// <summary>
    /// A session end frame gives a session end message.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserSessionEndFrameReturnsSessionEndMessage()
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(2).Build();

        // Act
        var message = ParseMessage<SessionEndMessage>(frame);

        // Assert
        Assert.AreEqual(PipeMessageType.SessionEnd, message.MessageType, "The message type should be SessionEnd");
    }

    /// <summary>
    /// A statistics frame gives every header value and both entries with all values and maps.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserStatisticsFrameReturnsAllFields()
    {
        // Arrange
        KeyValuePair<int, int>[] workforce = [new(11, 5), new(12, -6)];
        KeyValuePair<int, int>[] buildings = [new(21, 7)];
        var frame = CreateStatistics(2).Entry(1000, workforce, buildings).Entry(2000, _noPairs, workforce).Build();

        // Act
        var message = ParseMessage<AreaProductionStatisticsMessage>(frame);

        // Assert
        Assert.AreEqual(PipeMessageType.AreaProductionStatistics, message.MessageType, "The message type should be AreaProductionStatistics");
        Assert.AreEqual((byte)200, message.SessionId, "The session ID should be read as unsigned");
        Assert.AreEqual((byte)255, message.IslandId, "The island ID should be read as unsigned");
        Assert.AreEqual((byte)128, message.AreaIndex, "The area index should be read as unsigned");
        Assert.AreEqual(-77, message.SessionGuid, "The session GUID should be read");
        Assert.AreEqual("Area", message.AreaName, "The area name should be read");
        Assert.AreEqual(123456789012L, message.Timestamp, "The timestamp should be read");
        Assert.HasCount(2, message.Entries, "Both entries should be returned");

        AssertEntry(message.Entries[0], 1000, workforce, buildings);
        AssertEntry(message.Entries[1], 2000, _noPairs, workforce);
    }

    /// <summary>
    /// An entry count of zero gives no entries.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserStatisticsFrameWithoutEntriesReturnsNoEntries()
    {
        // Arrange
        var frame = CreateStatistics(0).Build();

        // Act
        var message = ParseMessage<AreaProductionStatisticsMessage>(frame);

        // Assert
        Assert.AreEqual((byte)200, message.SessionId, "The header should still be read");
        Assert.IsEmpty(message.Entries, "An entry count of zero should give no entries");
    }

    /// <summary>
    /// A GUID repeated in the workforce map is summed.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserDuplicateWorkforceGuidSumsAmounts()
    {
        // Arrange
        var frame = CreateStatistics(1).Entry(5, [new(9, 3), new(8, 1), new(9, 4)], _noPairs).Build();

        // Act
        var message = ParseMessage<AreaProductionStatisticsMessage>(frame);

        // Assert
        Assert.AreSequenceEqual(new[] { new KeyValuePair<int, int>(9, 7), new KeyValuePair<int, int>(8, 1) }.OrderBy(pair => pair.Key).ThenBy(pair => pair.Value).ToList(),
                                message.Entries[0].WorkforceAmounts.OrderBy(pair => pair.Key).ThenBy(pair => pair.Value).ToList(),
                                "The duplicate GUID should be held once with the sum");
        Assert.IsEmpty(message.Entries[0].BuildingAmounts, "The building map should stay empty");
    }

    /// <summary>
    /// A GUID repeated in the building map is summed.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserDuplicateBuildingGuidSumsAmounts()
    {
        // Arrange
        var frame = CreateStatistics(1).Entry(5, _noPairs, [new(9, 3), new(9, 4)]).Build();

        // Act
        var message = ParseMessage<AreaProductionStatisticsMessage>(frame);

        // Assert
        Assert.AreSequenceEqual(new[] { new KeyValuePair<int, int>(9, 7) }.OrderBy(pair => pair.Key).ThenBy(pair => pair.Value).ToList(),
                                message.Entries[0].BuildingAmounts.OrderBy(pair => pair.Key).ThenBy(pair => pair.Value).ToList(),
                                "The duplicate GUID should be held once with the sum");
        Assert.IsEmpty(message.Entries[0].WorkforceAmounts, "The workforce map should stay empty");
    }

    /// <summary>
    /// A 1,024-pair map of one GUID holds one pair and keeps no space for the wire count.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserDuplicateMapKeysKeepNoSpareCapacity()
    {
        // Arrange
        var duplicates = Enumerable.Repeat(new KeyValuePair<int, int>(9, 1), PipeProtocol.MaxMapPairs).ToArray();
        var frame = CreateStatistics(1).Entry(5, duplicates, duplicates).Build();

        // Act
        var message = ParseMessage<AreaProductionStatisticsMessage>(frame);

        // Assert
        var workforce = message.Entries[0].WorkforceAmounts as Dictionary<int, int>;
        var buildings = message.Entries[0].BuildingAmounts as Dictionary<int, int>;

        Assert.IsNotNull(workforce, "The workforce map should be a dictionary");
        Assert.IsNotNull(buildings, "The building map should be a dictionary");
        Assert.HasCount(1, workforce, "The workforce map should count the GUID once");
        Assert.AreEqual(PipeProtocol.MaxMapPairs, workforce[9], "The workforce amounts should be summed");
        Assert.IsLessThan(8, workforce.Capacity, "The workforce map should keep space for its distinct GUIDs only");
        Assert.HasCount(1, buildings, "The building map should count the GUID once");
        Assert.IsLessThan(8, buildings.Capacity, "The building map should keep space for its distinct GUIDs only");
    }

    /// <summary>
    /// A duplicate sum outside the int32 range rejects the frame.
    /// </summary>
    /// <param name="first">The first amount</param>
    /// <param name="second">The second amount</param>
    [TestMethod]
    [DataRow(int.MaxValue, 1)]
    [DataRow(int.MinValue, -1)]
    public void PipeMessageParserDuplicateGuidSumOverflowReturnsMalformed(int first, int second)
    {
        // Arrange
        var frame = CreateStatistics(1).Entry(5, [new(9, first), new(9, second)], _noPairs).Build();

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.AreEqual((byte)3, error.TypeValue, "The error should carry the statistics type");
    }

    /// <summary>
    /// An empty frame gives the empty-frame error.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserEmptyFrameReturnsEmptyFrame()
    {
        // Act
        var result = PipeMessageParser.Parse([]);

        // Assert
        Assert.AreEqual(PipeReadResultKind.Error, result.Kind, "An empty frame should give an error");
        Assert.AreEqual(PipeErrorKind.EmptyFrame, result.Error?.Kind, "The error should be an empty frame");
    }

    /// <summary>
    /// A type byte above 5 gives the unknown-type error with the value.
    /// </summary>
    /// <param name="type">The type byte</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow((byte)6)]
    [DataRow((byte)255)]
    public void PipeMessageParserUnknownTypeReturnsUnknownType(byte type)
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(type).Int32Value(1).Build();

        // Act
        var result = PipeMessageParser.Parse(frame);

        // Assert
        Assert.AreEqual(PipeReadResultKind.Error, result.Kind, "An unknown type should give an error");
        Assert.AreEqual(PipeErrorKind.UnknownType, result.Error?.Kind, "The error should be an unknown type");
        Assert.AreEqual(type, result.Error?.TypeValue, "The error should carry the type value");
    }

    /// <summary>
    /// A trade-route frame of the reference layout gives every field of every route, station and good in frame order.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void PipeMessageParserTradeRoutesReturnsEveryField()
    {
        // Arrange
        var expected = new TradeRoutesMessage
                       {
                           Routes = [
                                        .. ReferenceRoutes(),
                                        CreateRoute(-8, "Second", "Other", CreateStation(13, 250, false, false, false))
                                    ]
                       };
        var frame = FrameOf(expected);

        Assert.HasCount(166, frame, "The frame should hold the reference layout: 109 bytes of the first route, 57 of the second");

        // Act
        var result = PipeMessageParser.Parse(frame);

        // Assert
        Assert.AreEqual(Describe(expected), Describe(result.Message), "Every field of every route, station and good should be delivered in frame order");
    }

    /// <summary>
    /// A repeated route ID, station ID or product GUID is delivered twice, in its place.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void PipeMessageParserTradeRoutesKeepsRepeatedIds()
    {
        // Arrange
        var expected = new TradeRoutesMessage
                       {
                           Routes = [
                                        CreateRoute(7,
                                                    "First",
                                                    "Folder",
                                                    CreateStation(11, 200, true, false, false, CreateGood(1010, 5, true), CreateGood(1010, 6, false)),
                                                    CreateStation(11, 201, false, true, false)),
                                        CreateRoute(8, "Between", "Folder"),
                                        CreateRoute(7, "Again", "Folder", CreateStation(11, 202, false, false, true))
                                    ]
                       };

        // Act
        var result = PipeMessageParser.Parse(FrameOf(expected));

        // Assert
        Assert.AreEqual(Describe(expected), Describe(result.Message), "Repeated IDs should be delivered in their place, nothing merged or dropped");
    }

    /// <summary>
    /// Route and folder names follow the string rules: not trimmed, underscores and non-ASCII text kept, an empty text
    /// allowed and an invalid UTF-8 byte delivered as U+FFFD.
    /// </summary>
    [TestMethod]
    [TestCategory("AC1")]
    public void PipeMessageParserTradeRoutesKeepsNamesAsDecoded()
    {
        // Arrange
        var replacement = ((char)0xFFFD).ToString();
        var plain = CreateRoute(7, "Zycada ", string.Empty);
        var marked = CreateRoute(8, "R_mische K_ste", " Köln ");
        var invalid = CreateRoute(9, "A" + replacement, replacement);
        var expected = new TradeRoutesMessage
                       {
                           Routes = [plain, marked, invalid]
                       };
        var frame = new PipeFrameBuilder().Byte(4)
                                          .Int32Value(3)
                                          .Route(plain)
                                          .Route(marked)
                                          .Int32Value(invalid.Id)
                                          .Byte(2)
                                          .Raw(0x41, 0xFF)
                                          .Byte(1)
                                          .Raw(0xC3)
                                          .Int32Value(invalid.MajorProblems)
                                          .Int32Value(invalid.MinorProblems)
                                          .Int32Value(invalid.Ships)
                                          .Int32Value(invalid.LostShips)
                                          .Int32Value(0)
                                          .Build();

        // Act
        var result = PipeMessageParser.Parse(frame);

        // Assert
        Assert.AreEqual(Describe(expected), Describe(result.Message), "The names should be delivered under the string rules");
    }

    /// <summary>
    /// Bytes after the last field of a trade-route or item-offer frame are ignored: the message is delivered in full and
    /// the consumed length ends behind its last field.
    /// </summary>
    /// <param name="type">The type byte</param>
    [TestMethod]
    [TestCategory("AC1")]
    [TestCategory("AC2")]
    [DataRow((byte)4)]
    [DataRow((byte)5)]
    public void PipeMessageParserTradeRoutesOrItemOffersIgnoresTrailingBytes(byte type)
    {
        // Arrange
        var expected = ReferenceMessage(type);
        var frame = FrameOf(expected);

        // Act
        var result = PipeMessageParser.Parse(Concat(frame, [0xAA, 0xBB]), out var consumed);

        // Assert
        Assert.AreEqual(Describe(expected), Describe(result.Message), $"The type {type} frame should be delivered despite the trailing bytes");
        Assert.AreEqual(frame.Length, consumed, $"The consumed length of the type {type} frame should end behind its last field");
    }

    /// <summary>
    /// An item-offer frame of the reference layout gives every trader and item in frame order.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void PipeMessageParserItemOffersReturnsEveryOffer()
    {
        // Arrange
        var expected = new ItemOffersMessage
                       {
                           Offers = [CreateOffer(7, 100, -5, int.MaxValue), CreateOffer(-8, 42), CreateOffer(9, 0, int.MinValue)]
                       };
        var frame = FrameOf(expected);

        Assert.HasCount(53, frame, "The frame should hold the reference layout: type, count, three traders and six items");

        // Act
        var result = PipeMessageParser.Parse(frame);

        // Assert
        Assert.AreEqual(Describe(expected), Describe(result.Message), "Every trader and item should be delivered in frame order");
    }

    /// <summary>
    /// A repeated trader ID or item GUID is delivered twice, in its place.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void PipeMessageParserItemOffersKeepsRepeatedTradersAndItems()
    {
        // Arrange
        var expected = new ItemOffersMessage
                       {
                           Offers = [CreateOffer(7, 1, 1, 2), CreateOffer(8, 3), CreateOffer(7, 4)]
                       };

        // Act
        var result = PipeMessageParser.Parse(FrameOf(expected));

        // Assert
        Assert.AreEqual(Describe(expected), Describe(result.Message), "A repeated trader and a repeated item should be delivered in their place");
    }

    /// <summary>
    /// A count of 0 at each of the five levels gives an empty list there.
    /// </summary>
    /// <param name="level">The level whose count is 0</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DataRow(RoutesLevel)]
    [DataRow(StationsLevel)]
    [DataRow(GoodsLevel)]
    [DataRow(OffersLevel)]
    [DataRow(ItemsLevel)]
    public void PipeMessageParserTradeRoutesOrItemOffersZeroCountGivesEmptyList(string level)
    {
        // Arrange
        var expected = ZeroCountMessage(level);

        // Act
        var result = PipeMessageParser.Parse(FrameOf(expected));

        // Assert
        Assert.AreEqual(Describe(expected), Describe(result.Message), $"A count of 0 in the {level} should give an empty list");
    }

    /// <summary>
    /// A count with fewer than 4 bytes left, at each of the five levels, gives the malformed-message error at the count.
    /// </summary>
    /// <param name="type">The type byte</param>
    /// <param name="length">The length the reference frame is cut to</param>
    /// <param name="expectedOffset">The offset of the count that is missing</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow((byte)4, 1, 1)]
    [DataRow((byte)4, 4, 1)]
    [DataRow((byte)4, 38, 38)]
    [DataRow((byte)4, 41, 38)]
    [DataRow((byte)4, 96, 96)]
    [DataRow((byte)4, 99, 96)]
    [DataRow((byte)5, 1, 1)]
    [DataRow((byte)5, 4, 1)]
    [DataRow((byte)5, 25, 25)]
    [DataRow((byte)5, 28, 25)]
    public void PipeMessageParserTradeRoutesOrItemOffersMissingCountReturnsMalformed(byte type, int length, int expectedOffset)
    {
        // Arrange
        var frame = FrameOf(ReferenceMessage(type))[..length];

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.AreEqual(expectedOffset, error.Offset, $"The offset should be the position of the missing count (type {type}, cut at {length})");
    }

    /// <summary>
    /// A negative count at each of the five levels gives the malformed-message error at the count.
    /// </summary>
    /// <param name="type">The type byte</param>
    /// <param name="countOffset">The offset of the count in the reference frame</param>
    /// <param name="count">The negative count</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow((byte)4, 1, -1)]
    [DataRow((byte)4, 1, int.MinValue)]
    [DataRow((byte)4, 38, -1)]
    [DataRow((byte)4, 38, int.MinValue)]
    [DataRow((byte)4, 58, -1)]
    [DataRow((byte)4, 58, int.MinValue)]
    [DataRow((byte)5, 1, -1)]
    [DataRow((byte)5, 1, int.MinValue)]
    [DataRow((byte)5, 9, -1)]
    [DataRow((byte)5, 9, int.MinValue)]
    public void PipeMessageParserTradeRoutesOrItemOffersNegativeCountReturnsMalformed(byte type, int countOffset, int count)
    {
        // Arrange
        var frame = WithInt(FrameOf(ReferenceMessage(type)), countOffset, count);

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.AreEqual(countOffset, error.Offset, $"The offset should be the position of the count {count} (type {type})");
    }

    /// <summary>
    /// A field past the end of the frame (a route value, a station ID, a station's key byte, session GUID, owner or flags
    /// byte, a good's loading byte) gives the malformed-message error. A short field of the last good is found at its goods
    /// count, which the bytes left cannot hold.
    /// </summary>
    /// <param name="length">The length the reference trade-route frame is cut to</param>
    /// <param name="expectedOffset">The offset of the field that does not fit</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(33, 30)]
    [DataRow(82, 80)]
    [DataRow(86, 86)]
    [DataRow(89, 87)]
    [DataRow(93, 91)]
    [DataRow(95, 95)]
    [DataRow(108, 96)]
    public void PipeMessageParserTradeRoutesShortFieldReturnsMalformed(int length, int expectedOffset)
    {
        // Arrange
        var frame = FrameOf(ReferenceMessage(4))[..length];

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.AreEqual(expectedOffset, error.Offset, $"The offset should be the position of the field that does not fit (cut at {length})");
    }

    /// <summary>
    /// A route name or folder whose length byte points past the frame gives the malformed-message error at the string.
    /// </summary>
    /// <param name="emptyStrings">The number of empty strings before the long one: 0 for the name, 1 for the folder</param>
    /// <param name="expectedOffset">The offset of the long string</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(0, 9)]
    [DataRow(1, 10)]
    public void PipeMessageParserTradeRoutesStringPastFrameReturnsMalformed(int emptyStrings, int expectedOffset)
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(4).Int32Value(1).Int32Value(7).Raw(new byte[emptyStrings]).Byte(200).Raw(new byte[30]).Build();

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.AreEqual(expectedOffset, error.Offset, "The offset should be the position of the string that does not fit");
    }

    /// <summary>
    /// A count one above its cap, in a frame that holds that many complete elements, gives the malformed-message error at
    /// the count.
    /// </summary>
    /// <param name="level">The level of the count</param>
    /// <param name="count">The count, one above the cap of its level</param>
    /// <param name="expectedOffset">The offset of the count</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(RoutesLevel, PipeProtocol.MaxTradeRoutes + 1, 1)]
    [DataRow(StationsLevel, PipeProtocol.MaxRouteStations + 1, 27)]
    [DataRow(GoodsLevel, PipeProtocol.MaxStationGoods + 1, 47)]
    [DataRow(OffersLevel, PipeProtocol.MaxItemOffers + 1, 1)]
    [DataRow(ItemsLevel, PipeProtocol.MaxOfferItems + 1, 9)]
    public void PipeMessageParserTradeRoutesOrItemOffersCountAboveCapReturnsMalformed(string level, int count, int expectedOffset)
    {
        // Arrange
        var frame = LevelFrame(level, count, count);

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.AreEqual(expectedOffset, error.Offset, $"The {level} count {count} should be rejected at its own offset");
    }

    /// <summary>
    /// A count at its cap is delivered with every element.
    /// </summary>
    /// <param name="level">The level of the count</param>
    /// <param name="count">The count, the cap of its level</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(RoutesLevel, PipeProtocol.MaxTradeRoutes)]
    [DataRow(StationsLevel, PipeProtocol.MaxRouteStations)]
    [DataRow(GoodsLevel, PipeProtocol.MaxStationGoods)]
    [DataRow(OffersLevel, PipeProtocol.MaxItemOffers)]
    [DataRow(ItemsLevel, PipeProtocol.MaxOfferItems)]
    public void PipeMessageParserTradeRoutesOrItemOffersCountAtCapReturnsMessage(string level, int count)
    {
        // Arrange
        var frame = LevelFrame(level, count, count);

        // Act
        var result = PipeMessageParser.Parse(frame);

        // Assert
        Assert.AreEqual(PipeReadResultKind.Message, result.Kind, $"The {level} count at its cap should give a message");
        Assert.AreEqual(count, LevelCount(level, result.Message), $"Every one of the {count} {level} should be delivered");
    }

    /// <summary>
    /// A count the rest of the frame cannot hold, with one complete element after it, gives the malformed-message error at
    /// the count, before any collection of that size is created.
    /// </summary>
    /// <param name="level">The level of the count</param>
    /// <param name="count">The count</param>
    /// <param name="expectedOffset">The offset of the count</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(RoutesLevel, 2, 1)]
    [DataRow(RoutesLevel, int.MaxValue, 1)]
    [DataRow(StationsLevel, 2, 27)]
    [DataRow(StationsLevel, int.MaxValue, 27)]
    [DataRow(GoodsLevel, 2, 47)]
    [DataRow(GoodsLevel, int.MaxValue, 47)]
    [DataRow(OffersLevel, 2, 1)]
    [DataRow(OffersLevel, int.MaxValue, 1)]
    [DataRow(ItemsLevel, 2, 9)]
    [DataRow(ItemsLevel, int.MaxValue, 9)]
    public void PipeMessageParserTradeRoutesOrItemOffersCountBeyondFrameReturnsMalformed(string level, int count, int expectedOffset)
    {
        // Arrange
        var frame = LevelFrame(level, count, 1);

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.AreEqual(expectedOffset, error.Offset, $"The {level} count {count} should be rejected at its own offset");
    }

    /// <summary>
    /// Flag bits the reference does not name (bits 3 to 7 of a station, 1 to 7 of a good) change nothing: the named flags
    /// beside them are delivered as set, and the rest of the frame is read as before.
    /// </summary>
    /// <param name="stationFlags">The raw flags byte of the station</param>
    /// <param name="goodFlags">The raw flags byte of the good</param>
    /// <param name="throwOverboard">The expected throw-overboard flag</param>
    /// <param name="waitUntilFull">The expected wait-until-full flag</param>
    /// <param name="waitUntilEmpty">The expected wait-until-empty flag</param>
    /// <param name="loading">The expected loading flag</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow((byte)0xF8, (byte)0xFE, false, false, false, false)]
    [DataRow((byte)0xFF, (byte)0xFF, true, true, true, true)]
    [DataRow((byte)0xFD, (byte)0xFF, true, false, true, true)]
    [DataRow((byte)0xFA, (byte)0xFE, false, true, false, false)]
    public void PipeMessageParserTradeRoutesUnnamedFlagBitsAreIgnored(byte stationFlags, byte goodFlags, bool throwOverboard, bool waitUntilFull, bool waitUntilEmpty, bool loading)
    {
        // Arrange
        var good = CreateGood(1010, 5, loading);
        var flagged = CreateStation(11, 200, throwOverboard, waitUntilFull, waitUntilEmpty, good);
        var plain = CreateStation(12, 130, false, true, false, CreateGood(1020, 50, true));
        var route = CreateRoute(7, "Route", "Folder", flagged, plain);
        var expected = new TradeRoutesMessage
                       {
                           Routes = [route]
                       };
        var frame = new PipeFrameBuilder().Byte(4)
                                          .Int32Value(1)
                                          .RouteFields(route)
                                          .Int32Value(2)
                                          .StationFields(flagged, stationFlags)
                                          .Int32Value(1)
                                          .Good(good, goodFlags)
                                          .Station(plain)
                                          .Build();

        // Act
        var result = PipeMessageParser.Parse(frame);

        // Assert
        Assert.AreEqual(Describe(expected), Describe(result.Message), $"The station flags 0x{stationFlags:X2} and good flags 0x{goodFlags:X2} should give their named bits only");
    }

    /// <summary>
    /// A payload cut inside any field gives the malformed-message error.
    /// </summary>
    /// <param name="frame">The cut frame</param>
    [TestMethod]
    [DynamicData(nameof(TruncatedFrames))]
    public void PipeMessageParserTruncatedPayloadReturnsMalformed(byte[] frame)
    {
        // Act and Assert
        ParseMalformed(frame);
    }

    /// <summary>
    /// The offset names the field that did not fit.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserTruncatedVersionReportsOffsetOfField()
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(0).Raw(2, 0, 0).Build();

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.AreEqual(1, error.Offset, "The offset should be the position of the version field");
    }

    /// <summary>
    /// A string length beyond the frame gives the malformed-message error.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserStringBeyondFrameReturnsMalformed()
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(1).Byte(10).Raw(1, 2, 3).Build();

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.AreEqual((byte)1, error.TypeValue, "The error should carry the session start type");
    }

    /// <summary>
    /// A negative count in the entries, the workforce map or the building map gives the malformed-message error.
    /// </summary>
    /// <param name="position">0 for the entry count, 1 for the workforce count, 2 for the building count</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void PipeMessageParserNegativeCountReturnsMalformed(int position)
    {
        // Arrange
        var frame = position switch
                    {
                        0 => CreateStatistics(-1).Build(),
                        1 => CreateStatistics(1).EntryFields(1).Int32Value(-1).Int32Value(0).Build(),
                        _ => CreateStatistics(1).EntryFields(1).Int32Value(0).Int32Value(-1).Build(),
                    };

        // Act and Assert
        ParseMalformed(frame);
    }

    /// <summary>
    /// A count larger than the rest of the frame gives the malformed-message error.
    /// </summary>
    /// <param name="position">0 for the entry count, 1 for the workforce count, 2 for the building count</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void PipeMessageParserCountBeyondFrameReturnsMalformed(int position)
    {
        // Arrange
        var frame = position switch
                    {
                        0 => CreateStatistics(1000).Entry(1, _noPairs, _noPairs).Build(),
                        1 => CreateStatistics(1).EntryFields(1).Int32Value(1000).Int32Value(1).Int32Value(1).Build(),
                        _ => CreateStatistics(1).EntryFields(1).Int32Value(0).Int32Value(1000).Int32Value(1).Int32Value(1).Build(),
                    };

        // Act and Assert
        ParseMalformed(frame);
    }

    /// <summary>
    /// A huge count is rejected without taking memory for the count.
    /// </summary>
    /// <param name="inPairs">True for a huge pair count, false for a huge entry count</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PipeMessageParserHugeCountAllocatesNoMemoryForCount(bool inPairs)
    {
        // Arrange
        var frame = inPairs
                        ? CreateStatistics(1).EntryFields(1).Int32Value(int.MaxValue).Raw(new byte[64]).Build()
                        : CreateStatistics(int.MaxValue).Raw(new byte[64]).Build();

        Assert.IsLessThan(1024, frame.Length, "The frame should be under 1 KiB");

        var warmUp = PipeMessageParser.Parse(frame);

        Assert.AreEqual(PipeReadResultKind.Error, warmUp.Kind, "The huge count should give an error");

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = PipeMessageParser.Parse(frame);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.AreEqual(PipeErrorKind.MalformedMessage, result.Error?.Kind, "The huge count should give the malformed-message error");
        Assert.IsLessThan(64 * 1024, allocated, "The parse should allocate nothing for the count");
    }

    /// <summary>
    /// Bytes after the last field are ignored.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserTrailingBytesReturnsMessage()
    {
        // Arrange
        var versionFrame = new PipeFrameBuilder().Byte(0).Int32Value(2).Raw(9, 9, 9).Build();
        var statisticsFrame = CreateStatistics(0).Raw(1, 2, 3).Build();

        // Act
        var version = ParseMessage<VersionMessage>(versionFrame);
        var statistics = ParseMessage<AreaProductionStatisticsMessage>(statisticsFrame);

        // Assert
        Assert.AreEqual(2, version.Version, "The version should be read despite the extra bytes");
        Assert.AreEqual("Area", statistics.AreaName, "The statistics should be read despite the extra bytes");
    }

    /// <summary>
    /// An invalid UTF-8 byte becomes U+FFFD.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserInvalidUtf8ReturnsReplacementCharacter()
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(1).Byte(1).Byte(0xFF).Build();
        var replacement = ((char)0xFFFD).ToString();

        // Act
        var message = ParseMessage<SessionStartMessage>(frame);

        // Assert
        Assert.AreEqual(replacement, message.Headline, "The invalid byte should become one replacement character");
    }

    /// <summary>
    /// A string is delivered untrimmed and with underscores kept.
    /// </summary>
    /// <param name="text">The text</param>
    [TestMethod]
    [DataRow("Zycada ")]
    [DataRow("R_mische K_ste")]
    public void PipeMessageParserStringKeptAsDelivered(string text)
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(1).StringValue(text).Build();

        // Act
        var message = ParseMessage<SessionStartMessage>(frame);

        // Assert
        Assert.AreEqual(text, message.Headline, "The string should be delivered as it was written");
    }

    /// <summary>
    /// A map of exactly 1,024 pairs is accepted.
    /// </summary>
    /// <param name="inWorkforce">True for the workforce map, false for the building map</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PipeMessageParserMapAtPairLimitReturnsMessage(bool inWorkforce)
    {
        // Arrange
        var pairs = CreatePairs(1024);
        var frame = CreateStatistics(1).Entry(1, inWorkforce ? pairs : _noPairs, inWorkforce ? _noPairs : pairs).Build();

        // Act
        var message = ParseMessage<AreaProductionStatisticsMessage>(frame);

        // Assert
        var map = inWorkforce ? message.Entries[0].WorkforceAmounts : message.Entries[0].BuildingAmounts;

        Assert.HasCount(1024, map, "All 1,024 pairs should be kept");
    }

    /// <summary>
    /// A map of 1,025 pairs is rejected although the frame holds them.
    /// </summary>
    /// <param name="inWorkforce">True for the workforce map, false for the building map</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PipeMessageParserMapOverPairLimitReturnsMalformed(bool inWorkforce)
    {
        // Arrange
        var pairs = CreatePairs(1025);
        var frame = CreateStatistics(1).Entry(1, inWorkforce ? pairs : _noPairs, inWorkforce ? _noPairs : pairs).Build();

        // Act and Assert
        ParseMalformed(frame);
    }

    /// <summary>
    /// A malformed error carries no payload text.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserMalformedErrorCarriesNoPayloadText()
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(1).Byte(10).Raw(Encoding.UTF8.GetBytes("secret")).Build();

        // Act
        var error = ParseMalformed(frame);

        // Assert
        Assert.DoesNotContain("secret", error.ToString(), "The string form should not contain payload text");
    }

    /// <summary>
    /// The overload with the consumed length reports the position behind the last field read, so trailing bytes are not
    /// counted, gives the result of <see cref="PipeMessageParser.Parse(ReadOnlySpan{byte})" /> and reports 0 on an error.
    /// </summary>
    /// <param name="description">What the frame holds</param>
    /// <param name="frame">The frame</param>
    /// <param name="expected">The expected consumed length</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DynamicData(nameof(ConsumedLengthFrames))]
    public void PipeMessageParserParseReportsConsumedLength(string description, byte[] frame, int expected)
    {
        // Arrange
        var plain = PipeMessageParser.Parse(frame);

        // Act
        var result = PipeMessageParser.Parse(frame, out var consumed);

        // Assert
        Assert.AreEqual(expected, consumed, $"The consumed length should end after the last field read ({description})");
        Assert.AreEqual(plain.Kind, result.Kind, $"The result should be the one of the plain parse ({description})");
        Assert.AreEqual(plain.Message?.MessageType, result.Message?.MessageType, $"The message type should be the one of the plain parse ({description})");
        Assert.AreEqual(plain.Error?.ToString(), result.Error?.ToString(), $"The error should be the one of the plain parse ({description})");
    }

    /// <summary>
    /// Creates a trade route whose problem, ship and lost-ship counts are derived from its ID.
    /// </summary>
    /// <param name="id">The route ID</param>
    /// <param name="name">The route name</param>
    /// <param name="folder">The folder name</param>
    /// <param name="stations">The stations</param>
    /// <returns>The route</returns>
    private static TradeRoute CreateRoute(int id, string name, string folder, params TradeRouteStation[] stations)
    {
        return new TradeRoute
               {
                   Id = id,
                   Name = name,
                   Folder = folder,
                   MajorProblems = (id * 10) + 1,
                   MinorProblems = (id * 10) + 2,
                   Ships = (id * 10) + 3,
                   LostShips = (id * 10) + 4,
                   Stations = stations
               };
    }

    /// <summary>
    /// Creates a station whose island key, session GUID and owner are derived from its ID and session ID.
    /// </summary>
    /// <param name="id">The station ID</param>
    /// <param name="sessionId">The session ID, at most 253</param>
    /// <param name="throwOverboard">The throw-overboard flag</param>
    /// <param name="waitUntilFull">The wait-until-full flag</param>
    /// <param name="waitUntilEmpty">The wait-until-empty flag</param>
    /// <param name="goods">The goods</param>
    /// <returns>The station</returns>
    private static TradeRouteStation CreateStation(int id, byte sessionId, bool throwOverboard, bool waitUntilFull, bool waitUntilEmpty, params TradeRouteGood[] goods)
    {
        return new TradeRouteStation
               {
                   Id = id,
                   SessionId = sessionId,
                   IslandId = (byte)(sessionId + 1),
                   AreaIndex = (byte)(sessionId + 2),
                   SessionGuid = -100 * id,
                   Owner = (1000 * id) + 7,
                   ThrowOverboard = throwOverboard,
                   WaitUntilFull = waitUntilFull,
                   WaitUntilEmpty = waitUntilEmpty,
                   Goods = goods
               };
    }

    /// <summary>
    /// Creates a good.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="amount">The amount</param>
    /// <param name="loading">Whether the good is loaded</param>
    /// <returns>The good</returns>
    private static TradeRouteGood CreateGood(int productGuid, int amount, bool loading)
    {
        return new TradeRouteGood
               {
                   ProductGuid = productGuid,
                   Amount = amount,
                   Loading = loading
               };
    }

    /// <summary>
    /// Creates an item offer.
    /// </summary>
    /// <param name="traderId">The trader ID</param>
    /// <param name="itemGuids">The item GUIDs</param>
    /// <returns>The offer</returns>
    private static ItemOffer CreateOffer(int traderId, params int[] itemGuids)
    {
        return new ItemOffer
               {
                   TraderId = traderId,
                   ItemGuids = itemGuids
               };
    }

    /// <summary>
    /// Gets the routes of the reference trade-route frame: one route (ID at 5, station count at 38) with two stations,
    /// the first (goods count at 58) with two goods, the second (starting at 80, goods count at 96) with one good; 109
    /// bytes in all.
    /// </summary>
    /// <returns>The routes</returns>
    private static TradeRoute[] ReferenceRoutes()
    {
        return
        [
            CreateRoute(7,
                        "Route",
                        "Folder",
                        CreateStation(11, 200, true, false, true, CreateGood(1010, 5, true), CreateGood(1011, -3, false)),
                        CreateStation(12, 130, false, true, false, CreateGood(1020, 50, true)))
        ];
    }

    /// <summary>
    /// Gets the message of a reference frame: the routes of <see cref="ReferenceRoutes" /> for type 4, and for type 5 two
    /// offers, the first (item count at 9) with two items, the second (item count at 25) with one item; 33 bytes in all.
    /// </summary>
    /// <param name="type">The type byte, 4 or 5</param>
    /// <returns>The message</returns>
    private static PipeMessage ReferenceMessage(byte type)
    {
        return type == (byte)PipeMessageType.TradeRoutes
                   ? new TradeRoutesMessage
                     {
                         Routes = ReferenceRoutes()
                     }
                   : new ItemOffersMessage
                     {
                         Offers = [CreateOffer(7, 100, 101), CreateOffer(8, 102)]
                     };
    }

    /// <summary>
    /// Gets a message with a count of 0 at one level and one element at every level above it.
    /// </summary>
    /// <param name="level">The level whose count is 0</param>
    /// <returns>The message</returns>
    private static PipeMessage ZeroCountMessage(string level)
    {
        return level switch
               {
                   RoutesLevel => new TradeRoutesMessage
                                  {
                                      Routes = []
                                  },
                   StationsLevel => new TradeRoutesMessage
                                    {
                                        Routes = [CreateRoute(7, "Route", "Folder")]
                                    },
                   GoodsLevel => new TradeRoutesMessage
                                 {
                                     Routes = [CreateRoute(7, "Route", "Folder", CreateStation(11, 200, false, false, false))]
                                 },
                   OffersLevel => new ItemOffersMessage
                                  {
                                      Offers = []
                                  },
                   _ => new ItemOffersMessage
                        {
                            Offers = [CreateOffer(7)]
                        }
               };
    }

    /// <summary>
    /// Writes the frame of a trade-route or item-offer message in the reference layout.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The frame bytes</returns>
    private static byte[] FrameOf(PipeMessage message)
    {
        return message is TradeRoutesMessage routes
                   ? new PipeFrameBuilder().Byte(4).TradeRoutes(routes.Routes).Build()
                   : new PipeFrameBuilder().Byte(5).ItemOffers(((ItemOffersMessage)message).Offers).Build();
    }

    /// <summary>
    /// Writes a frame with a count at one level, one element at every level above it (empty names, no flags) and a number of
    /// minimal complete elements after the count. The count lies at offset 1 for routes and offers, 27 for stations, 47
    /// for goods and 9 for items.
    /// </summary>
    /// <param name="level">The level of the count</param>
    /// <param name="count">The count written</param>
    /// <param name="elements">The number of elements written after it</param>
    /// <returns>The frame bytes</returns>
    private static byte[] LevelFrame(string level, int count, int elements)
    {
        var route = CreateRoute(7, string.Empty, string.Empty);
        var station = CreateStation(11, 200, false, false, false);
        var good = CreateGood(1010, 5, false);
        var offer = CreateOffer(8);

        var builder = level switch
                      {
                          RoutesLevel => Repeat(new PipeFrameBuilder().Byte(4).Int32Value(count), elements, current => current.Route(route)),
                          StationsLevel => Repeat(new PipeFrameBuilder().Byte(4).Int32Value(1).RouteFields(route).Int32Value(count), elements, current => current.Station(station)),
                          GoodsLevel => Repeat(new PipeFrameBuilder().Byte(4).Int32Value(1).RouteFields(route).Int32Value(1).StationFields(station, 0).Int32Value(count), elements, current => current.Good(good)),
                          OffersLevel => Repeat(new PipeFrameBuilder().Byte(5).Int32Value(count), elements, current => current.Offer(offer)),
                          _ => Repeat(new PipeFrameBuilder().Byte(5).Int32Value(1).Int32Value(7).Int32Value(count), elements, current => current.Int32Value(100))
                      };

        return builder.Build();
    }

    /// <summary>
    /// Appends the same element a number of times.
    /// </summary>
    /// <param name="builder">The builder</param>
    /// <param name="times">The number of elements</param>
    /// <param name="append">Appends one element</param>
    /// <returns>The builder</returns>
    private static PipeFrameBuilder Repeat(PipeFrameBuilder builder, int times, Func<PipeFrameBuilder, PipeFrameBuilder> append)
    {
        return Enumerable.Range(0, times).Aggregate(builder, (current, _) => append(current));
    }

    /// <summary>
    /// Gets the number of elements of the first list at a level of a frame built by <see cref="LevelFrame" />.
    /// </summary>
    /// <param name="level">The level</param>
    /// <param name="message">The message</param>
    /// <returns>The number of elements, or <c>null</c> when the message or the list above the level is missing</returns>
    private static int? LevelCount(string level, PipeMessage? message)
    {
        var routes = (message as TradeRoutesMessage)?.Routes;
        var offers = (message as ItemOffersMessage)?.Offers;

        return level switch
               {
                   RoutesLevel => routes?.Count,
                   StationsLevel => FirstOrNull(routes)?.Stations.Count,
                   GoodsLevel => FirstOrNull(FirstOrNull(routes)?.Stations)?.Goods.Count,
                   OffersLevel => offers?.Count,
                   _ => FirstOrNull(offers)?.ItemGuids.Count
               };
    }

    /// <summary>
    /// Gets the first element of a list.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="list">The list, or <c>null</c></param>
    /// <returns>The first element, or <c>null</c> when the list is missing or empty</returns>
    private static T? FirstOrNull<T>(IReadOnlyList<T>? list)
        where T : class
    {
        return list is { Count: > 0 } ? list[0] : null;
    }

    /// <summary>
    /// Overwrites a little-endian 32-bit integer of a frame.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="offset">The offset of the integer</param>
    /// <param name="value">The new value</param>
    /// <returns>A copy of the frame with the new value</returns>
    private static byte[] WithInt(byte[] frame, int offset, int value)
    {
        var copy = frame.ToArray();

        BinaryPrimitives.WriteInt32LittleEndian(copy.AsSpan(offset), value);

        return copy;
    }

    /// <summary>
    /// Describes a decoded message with every field of its routes, stations, goods and offers, in frame order.
    /// </summary>
    /// <param name="message">The message, or <c>null</c> for an error</param>
    /// <returns>The description</returns>
    private static string Describe(PipeMessage? message)
    {
        return message switch
               {
                   TradeRoutesMessage routes => string.Create(CultureInfo.InvariantCulture, $"trade routes ({routes.Routes.Count}): {string.Join("; ", routes.Routes.Select(DescribeRoute))}"),
                   ItemOffersMessage offers => string.Create(CultureInfo.InvariantCulture, $"item offers ({offers.Offers.Count}): {string.Join("; ", offers.Offers.Select(DescribeOffer))}"),
                   null => "no message",
                   _ => message.MessageType.ToString()
               };
    }

    /// <summary>
    /// Describes a route with every field.
    /// </summary>
    /// <param name="route">The route</param>
    /// <returns>The description</returns>
    private static string DescribeRoute(TradeRoute route)
    {
        return string.Create(CultureInfo.InvariantCulture,
                             $"route {route.Id} name '{route.Name}' folder '{route.Folder}' problems {route.MajorProblems}/{route.MinorProblems} ships {route.Ships} lost {route.LostShips} stations [{string.Join(", ", route.Stations.Select(DescribeStation))}]");
    }

    /// <summary>
    /// Describes a station with every field.
    /// </summary>
    /// <param name="station">The station</param>
    /// <returns>The description</returns>
    private static string DescribeStation(TradeRouteStation station)
    {
        return string.Create(CultureInfo.InvariantCulture,
                             $"station {station.Id} island {station.SessionId}/{station.IslandId}/{station.AreaIndex}/{station.SessionGuid} owner {station.Owner} throw {station.ThrowOverboard} full {station.WaitUntilFull} empty {station.WaitUntilEmpty} goods [{string.Join(", ", station.Goods.Select(DescribeGood))}]");
    }

    /// <summary>
    /// Describes a good with every field.
    /// </summary>
    /// <param name="good">The good</param>
    /// <returns>The description</returns>
    private static string DescribeGood(TradeRouteGood good)
    {
        return string.Create(CultureInfo.InvariantCulture, $"good {good.ProductGuid} amount {good.Amount} loading {good.Loading}");
    }

    /// <summary>
    /// Describes an offer with every field.
    /// </summary>
    /// <param name="offer">The offer</param>
    /// <returns>The description</returns>
    private static string DescribeOffer(ItemOffer offer)
    {
        return string.Create(CultureInfo.InvariantCulture, $"trader {offer.TraderId} items [{string.Join(", ", offer.ItemGuids.Select(guid => guid.ToString(CultureInfo.InvariantCulture)))}]");
    }

    /// <summary>
    /// Joins two byte arrays.
    /// </summary>
    /// <param name="first">The first array</param>
    /// <param name="second">The second array</param>
    /// <returns>The bytes of both</returns>
    private static byte[] Concat(byte[] first, byte[] second)
    {
        return [.. first, .. second];
    }

    /// <summary>
    /// Starts a statistics frame with the header values of the tests and the given entry count.
    /// </summary>
    /// <param name="entryCount">The entry count to write</param>
    /// <returns>The builder positioned at the first entry</returns>
    private static PipeFrameBuilder CreateStatistics(int entryCount)
    {
        return new PipeFrameBuilder().Byte(3).Byte(200).Byte(255).Byte(128).Int32Value(-77).StringValue("Area").Int64Value(123456789012L).Int32Value(entryCount);
    }

    /// <summary>
    /// Creates the pairs (1, 1) to (count, 1).
    /// </summary>
    /// <param name="count">The number of pairs</param>
    /// <returns>The pairs with distinct keys</returns>
    private static KeyValuePair<int, int>[] CreatePairs(int count)
    {
        return [.. Enumerable.Range(1, count).Select(key => new KeyValuePair<int, int>(key, 1))];
    }

    /// <summary>
    /// Parses a frame and returns the message of the given type.
    /// </summary>
    /// <typeparam name="TMessage">The expected message type</typeparam>
    /// <param name="frame">The frame</param>
    /// <returns>The message</returns>
    private static TMessage ParseMessage<TMessage>(byte[] frame)
        where TMessage : PipeMessage
    {
        var result = PipeMessageParser.Parse(frame);

        Assert.AreEqual(PipeReadResultKind.Message, result.Kind, "The frame should give a message");

        var message = result.Message as TMessage;

        Assert.IsNotNull(message, "The message should have the expected type");

        return message;
    }

    /// <summary>
    /// Parses a frame and asserts a malformed-message error.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns>The error</returns>
    private static PipeError ParseMalformed(byte[] frame)
    {
        var result = PipeMessageParser.Parse(frame);

        Assert.AreEqual(PipeReadResultKind.Error, result.Kind, "The frame should give an error");
        Assert.IsNotNull(result.Error, "The result should carry the error");
        Assert.AreEqual(PipeErrorKind.MalformedMessage, result.Error.Kind, "The error should be a malformed message");
        Assert.AreEqual(frame[0], result.Error.TypeValue, "The error should carry the type value");
        Assert.IsNotNull(result.Error.Offset, "The error should carry the offset of the field that did not fit");

        return result.Error;
    }

    /// <summary>
    /// Asserts the values of an entry built by <see cref="PipeFrameBuilder.Entry" />.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <param name="guid">The product GUID it was built with</param>
    /// <param name="workforce">The workforce pairs it was built with</param>
    /// <param name="buildings">The building pairs it was built with</param>
    private static void AssertEntry(ProductionEntry entry, int guid, KeyValuePair<int, int>[] workforce, KeyValuePair<int, int>[] buildings)
    {
        Assert.AreEqual(guid, entry.ProductGuid, "The product GUID should be read");
        Assert.AreEqual(guid + 0.25f, entry.Generation, 0f, "The generation should be read");
        Assert.AreEqual(guid + 0.5f, entry.Consumption, 0f, "The consumption should be read");
        Assert.AreEqual(guid + 0.75f, entry.Delta, 0f, "The delta should be read");
        Assert.AreEqual(guid + 1f, entry.PerfectGeneration, 0f, "The perfect generation should be read");
        Assert.AreEqual(guid + 1.25f, entry.PerfectConsumption, 0f, "The perfect consumption should be read");
        Assert.AreEqual(guid + 6, entry.BuildingCount, "The building count should be read");
        Assert.AreEqual(guid + 7, entry.Maintenance, "The maintenance should be read");
        Assert.AreEqual(guid + 8.5f, entry.Income, 0f, "The income should be read");
        Assert.AreEqual(guid + 9, entry.Profit, "The profit should be read");
        Assert.AreEqual(guid + 10.25f, entry.SummedProductivity, 0f, "The summed productivity should be read");
        Assert.AreEqual(guid + 11.75f, entry.AverageProductivity, 0f, "The average productivity should be read");
        Assert.AreSequenceEqual(workforce.OrderBy(pair => pair.Key).ThenBy(pair => pair.Value).ToList(),
                                entry.WorkforceAmounts.OrderBy(pair => pair.Key).ThenBy(pair => pair.Value).ToList(),
                                "The workforce map should hold the written pairs");
        Assert.AreSequenceEqual(buildings.OrderBy(pair => pair.Key).ThenBy(pair => pair.Value).ToList(),
                                entry.BuildingAmounts.OrderBy(pair => pair.Key).ThenBy(pair => pair.Value).ToList(),
                                "The building map should hold the written pairs");
    }

    #endregion // Methods
}