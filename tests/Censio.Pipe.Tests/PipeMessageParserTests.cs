using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeMessageParser" />.
/// </summary>
[TestClass]
public sealed class PipeMessageParserTests
{
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
            var version = new PipeFrameBuilder().Byte(0).Int32(2).Build();

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

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A version frame gives the version.
    /// </summary>
    [TestMethod]
    public void PipeMessageParserVersionFrameReturnsVersionMessage()
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(0).Int32(2).Build();

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
        var frame = new PipeFrameBuilder().Byte(1).String(headline).Build();

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
    /// A type byte above 3 gives the unknown-type error with the value.
    /// </summary>
    /// <param name="type">The type byte</param>
    [TestMethod]
    [DataRow((byte)4)]
    [DataRow((byte)255)]
    public void PipeMessageParserUnknownTypeReturnsUnknownType(byte type)
    {
        // Arrange
        var frame = new PipeFrameBuilder().Byte(type).Int32(1).Build();

        // Act
        var result = PipeMessageParser.Parse(frame);

        // Assert
        Assert.AreEqual(PipeReadResultKind.Error, result.Kind, "An unknown type should give an error");
        Assert.AreEqual(PipeErrorKind.UnknownType, result.Error?.Kind, "The error should be an unknown type");
        Assert.AreEqual(type, result.Error?.TypeValue, "The error should carry the type value");
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
        byte[] frame;

        if (position == 0)
        {
            frame = CreateStatistics(-1).Build();
        }
        else if (position == 1)
        {
            frame = CreateStatistics(1).EntryFields(1).Int32(-1).Int32(0).Build();
        }
        else
        {
            frame = CreateStatistics(1).EntryFields(1).Int32(0).Int32(-1).Build();
        }

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
        byte[] frame;

        if (position == 0)
        {
            frame = CreateStatistics(1000).Entry(1, _noPairs, _noPairs).Build();
        }
        else if (position == 1)
        {
            frame = CreateStatistics(1).EntryFields(1).Int32(1000).Int32(1).Int32(1).Build();
        }
        else
        {
            frame = CreateStatistics(1).EntryFields(1).Int32(0).Int32(1000).Int32(1).Int32(1).Build();
        }

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
                        ? CreateStatistics(1).EntryFields(1).Int32(int.MaxValue).Raw(new byte[64]).Build()
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
        var versionFrame = new PipeFrameBuilder().Byte(0).Int32(2).Raw(9, 9, 9).Build();
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
        var frame = new PipeFrameBuilder().Byte(1).String(text).Build();

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
    /// Starts a statistics frame with the header values of the tests and the given entry count.
    /// </summary>
    /// <param name="entryCount">The entry count to write</param>
    /// <returns>The builder positioned at the first entry</returns>
    private static PipeFrameBuilder CreateStatistics(int entryCount)
    {
        return new PipeFrameBuilder().Byte(3).Byte(200).Byte(255).Byte(128).Int32(-77).String("Area").Int64(123456789012L).Int32(entryCount);
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