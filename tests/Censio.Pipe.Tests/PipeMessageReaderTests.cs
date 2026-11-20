using System.Globalization;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeMessageReader" />.
/// </summary>
[TestClass]
public sealed class PipeMessageReaderTests
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeMessageReaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PipeMessageReaderTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    /// <summary>
    /// Gets every cut offset of the sample sequence.
    /// </summary>
    private static IEnumerable<object[]> CutOffsets => Enumerable.Range(0, CreateSequence().Length + 1).Select(offset => new object[] { offset });

    /// <summary>
    /// Gets every offset of the sample sequence combined with the replacement bytes.
    /// </summary>
    private static IEnumerable<object[]> CorruptedOffsets => Enumerable.Range(0, CreateSequence().Length)
                                                                       .SelectMany(offset => new[] { (byte)0x00, (byte)0xFF }, (offset, value) => new object[] { offset, value });

    /// <summary>
    /// Gets the cases of a result built from a whole frame: the bytes before the frame, how many results they give, the
    /// frame, and the kind of the result.
    /// </summary>
    private static IEnumerable<object[]> FrameCases
    {
        get
        {
            yield return [VersionFrame(2), 1, new byte[] { 2 }, PipeReadResultKind.Message];
            yield return [Array.Empty<byte>(), 0, new byte[] { 0, 2, 0, 0, 0 }, PipeReadResultKind.Message];
            yield return [Array.Empty<byte>(), 0, Array.Empty<byte>(), PipeReadResultKind.Error];
            yield return [VersionFrame(2), 1, new byte[] { 99, 1, 2 }, PipeReadResultKind.Error];
            yield return [VersionFrame(2), 1, new byte[] { 3, 1 }, PipeReadResultKind.Error];
            yield return [Array.Empty<byte>(), 0, new byte[] { 0, 4, 0, 0, 0 }, PipeReadResultKind.Error];
            yield return [Array.Empty<byte>(), 0, new byte[] { 2 }, PipeReadResultKind.Error];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A result built from a whole frame (message, empty, unknown type, malformed, protocol mismatch) carries that frame.
    /// </summary>
    /// <param name="before">The bytes before the frame, with their length prefixes</param>
    /// <param name="beforeResults">The number of results the bytes before the frame give</param>
    /// <param name="frame">The frame bytes</param>
    /// <param name="kind">The expected kind of the result</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DynamicData(nameof(FrameCases))]
    public async Task PipeMessageReaderResultCarriesWholeFrame(byte[] before, int beforeResults, byte[] frame, PipeReadResultKind kind)
    {
        // Arrange
        using var stream = new ChunkedStream([.. before, .. PipeFrameBuilder.WithPrefix(frame)], 3);
        var reader = new PipeMessageReader(stream);

        await SkipResultsAsync(reader, beforeResults);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(kind, result.Kind, "The result should have the expected kind");
        Assert.IsNotNull(result.Frame, "The result should carry the frame it came from");
        Assert.AreSequenceEqual(frame, result.Frame.Value.ToArray(), "The result should carry the whole frame, type byte and payload");
    }

    /// <summary>
    /// Two session-end frames after the version give two messages.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderTwoSessionEndFramesReturnTwoMessages()
    {
        // Arrange
        using var stream = new ChunkedStream([.. VersionFrame(2), .. SessionEndFrame(), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        // Act
        var version = await reader.ReadAsync(TestContext.CancellationToken);
        var first = await reader.ReadAsync(TestContext.CancellationToken);
        var second = await reader.ReadAsync(TestContext.CancellationToken);
        var end = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.IsInstanceOfType<VersionMessage>(version.Message, "The first message should be the version");
        Assert.IsInstanceOfType<SessionEndMessage>(first.Message, "The second message should be a session end");
        Assert.IsInstanceOfType<SessionEndMessage>(second.Message, "The third message should be a session end");
        Assert.AreEqual(PipeReadResultKind.EndOfStream, end.Kind, "The stream should then end");
    }

    /// <summary>
    /// The chunk size of the stream does not change the results.
    /// </summary>
    /// <param name="chunkSize">The most bytes one read returns</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(int.MaxValue)]
    public async Task PipeMessageReaderChunkedStreamReturnsSameResults(int chunkSize)
    {
        // Arrange
        using var stream = new ChunkedStream(CreateSequence(), chunkSize);
        var reader = new PipeMessageReader(stream);

        // Act
        var results = await ReadToEndAsync(reader);

        // Assert
        Assert.HasCount(5, results, "The sequence should give four messages and the end of the stream");
        Assert.AreEqual(2, (results[0].Message as VersionMessage)?.Version, "The version should be decoded");
        Assert.AreEqual("Head", (results[1].Message as SessionStartMessage)?.Headline, "The headline should be decoded");

        var statistics = results[2].Message as AreaProductionStatisticsMessage;

        Assert.AreEqual("Area", statistics?.AreaName, "The area name should be decoded");
        Assert.HasCount(1, statistics?.Entries ?? [], "The entry should be decoded");
        Assert.IsInstanceOfType<SessionEndMessage>(results[3].Message, "The session end should be decoded");
        Assert.AreEqual(PipeReadResultKind.EndOfStream, results[4].Kind, "The sequence should end");
    }

    /// <summary>
    /// An oversized length gives the oversized error, after the version and before it.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderOversizedLengthReturnsOversized()
    {
        // Arrange
        using var stream = new ChunkedStream([.. VersionFrame(2), .. PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1)]);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);
        var after = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeReadResultKind.Error, result.Kind, "An oversized length should give an error");
        Assert.AreEqual(PipeErrorKind.OversizedFrame, result.Error?.Kind, "The error should be an oversized frame");
        Assert.AreEqual(PipeProtocol.MaxFrameLength + 1, result.Error?.Length, "The error should carry the announced length");
        Assert.AreEqual(PipeReadResultKind.EndOfStream, after.Kind, "The reader should then be at the end");
    }

    /// <summary>
    /// The end of the stream and the errors that end it carry no frame.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderEndAndStreamEndingErrorCarryNoFrame()
    {
        // Arrange
        using var endStream = new ChunkedStream([]);
        using var truncatedStream = new ChunkedStream([.. PipeFrameBuilder.RawLength(5), 1, 2]);
        using var oversizedStream = new ChunkedStream(PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1));
        using var failedStream = new ChunkedStream([], ioErrorOffset: 0);

        // Act
        var end = await new PipeMessageReader(endStream).ReadAsync(TestContext.CancellationToken);
        var truncated = await new PipeMessageReader(truncatedStream).ReadAsync(TestContext.CancellationToken);
        var oversized = await new PipeMessageReader(oversizedStream).ReadAsync(TestContext.CancellationToken);
        var failed = await new PipeMessageReader(failedStream).ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeReadResultKind.EndOfStream, end.Kind, "An empty stream should end");
        Assert.AreEqual(PipeErrorKind.TruncatedFrame, truncated.Error?.Kind, "A cut frame should be truncated");
        Assert.AreEqual(PipeErrorKind.OversizedFrame, oversized.Error?.Kind, "A too long frame should be oversized");
        Assert.AreEqual(PipeErrorKind.ReadFailed, failed.Error?.Kind, "A failing read should be a read failure");
        Assert.IsNull(end.Frame, "The end should carry no frame");
        Assert.IsNull(truncated.Frame, "A truncated frame should carry no frame");
        Assert.IsNull(oversized.Frame, "An oversized frame should carry no frame");
        Assert.IsNull(failed.Frame, "A failed read should carry no frame");
    }

    /// <summary>
    /// An oversized length before any version is passed through, not turned into a mismatch.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderOversizedLengthBeforeVersionReturnsOversized()
    {
        // Arrange
        using var stream = new ChunkedStream(PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1));
        var reader = new PipeMessageReader(stream);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.OversizedFrame, result.Error?.Kind, "The frame error should be passed through");
    }

    /// <summary>
    /// An empty frame gives the empty-frame error and the next frame is read.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderEmptyFrameThenFrameReturnsNextMessage()
    {
        // Arrange
        using var stream = new ChunkedStream([.. VersionFrame(2), .. PipeFrameBuilder.WithPrefix([]), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var empty = await reader.ReadAsync(TestContext.CancellationToken);
        var next = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.EmptyFrame, empty.Error?.Kind, "The empty frame should give its error");
        Assert.IsInstanceOfType<SessionEndMessage>(next.Message, "The next frame should be decoded");
    }

    /// <summary>
    /// An unknown type gives the unknown-type error and the next frame is read.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderUnknownTypeThenFrameReturnsNextMessage()
    {
        // Arrange
        using var stream = new ChunkedStream([.. VersionFrame(2), .. new PipeFrameBuilder().Byte(6).BuildWithPrefix(), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var unknown = await reader.ReadAsync(TestContext.CancellationToken);
        var next = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.UnknownType, unknown.Error?.Kind, "The unknown type should give its error");
        Assert.AreEqual((byte)6, unknown.Error?.TypeValue, "The error should carry the type value");
        Assert.IsInstanceOfType<SessionEndMessage>(next.Message, "The next frame should be decoded");
    }

    /// <summary>
    /// A malformed frame gives the malformed error and the next frame is read.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderMalformedFrameThenFrameReturnsNextMessage()
    {
        // Arrange
        using var stream = new ChunkedStream([.. VersionFrame(2), .. new PipeFrameBuilder().Byte(1).Byte(50).BuildWithPrefix(), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var malformed = await reader.ReadAsync(TestContext.CancellationToken);
        var next = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.MalformedMessage, malformed.Error?.Kind, "The malformed frame should give its error");
        Assert.IsInstanceOfType<SessionEndMessage>(next.Message, "The next frame should be decoded");
    }

    /// <summary>
    /// A malformed version leaves the gate unchanged.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderMalformedVersionKeepsGateUnknown()
    {
        // Arrange
        using var stream = new ChunkedStream([.. new PipeFrameBuilder().Byte(0).Raw(2, 0, 0).BuildWithPrefix(), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        // Act
        var malformed = await reader.ReadAsync(TestContext.CancellationToken);
        var next = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.MalformedMessage, malformed.Error?.Kind, "The malformed version should give its error");
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, next.Error?.Kind, "The gate should still be unknown");
        Assert.IsNull(next.Error?.ReceivedVersion, "No version should be recorded");
    }

    /// <summary>
    /// A supported version (2 or 3) is delivered as a version message, and the session start, island and session end
    /// frames after it are decoded.
    /// </summary>
    /// <param name="version">The announced version</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(2)]
    [DataRow(3)]
    public async Task PipeMessageReaderSupportedVersionDecodesFollowingFrames(int version)
    {
        // Arrange
        using var stream = new ChunkedStream(CreateSequence(version));
        var reader = new PipeMessageReader(stream);

        // Act
        var results = await ReadToEndAsync(reader);

        // Assert
        Assert.HasCount(5, results, $"The sequence after version {version} should give four messages and the end of the stream");
        Assert.AreEqual(version, (results[0].Message as VersionMessage)?.Version, $"Version {version} should be delivered as a version message");
        Assert.AreEqual("Head", (results[1].Message as SessionStartMessage)?.Headline, $"The session start after version {version} should be decoded");
        Assert.AreEqual("Area", (results[2].Message as AreaProductionStatisticsMessage)?.AreaName, $"The island after version {version} should be decoded");
        Assert.IsInstanceOfType<SessionEndMessage>(results[3].Message, $"The session end after version {version} should be decoded");
        Assert.AreEqual(PipeReadResultKind.EndOfStream, results[4].Kind, "The sequence should end");
    }

    /// <summary>
    /// A version other than 2 or 3 gives a protocol mismatch with the received version and the newest supported version
    /// as the expected one.
    /// </summary>
    /// <param name="version">The announced version</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(-1)]
    public async Task PipeMessageReaderUnsupportedVersionReturnsMismatchWithNewestVersion(int version)
    {
        // Arrange
        using var stream = new ChunkedStream(VersionFrame(version));
        var reader = new PipeMessageReader(stream);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, result.Error?.Kind, $"Version {version} should give a mismatch");
        Assert.AreEqual(version, result.Error?.ReceivedVersion, "The error should carry the received version");
        Assert.AreEqual(3, result.Error?.ExpectedVersion, "The error should carry version 3 as the expected version");
    }

    /// <summary>
    /// Every frame after an unsupported version gives a mismatch without decoding.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderFrameAfterUnsupportedVersionReturnsMismatch()
    {
        // Arrange
        using var stream = new ChunkedStream([.. VersionFrame(4), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, result.Error?.Kind, "A frame after an unsupported version should give a mismatch");
        Assert.AreEqual(4, result.Error?.ReceivedVersion, "The error should carry the received version");
        Assert.AreEqual(PipeProtocol.NewestSupportedVersion, result.Error?.ExpectedVersion, "The error should carry the expected version");
        Assert.AreEqual((byte)2, result.Error?.TypeValue, "The error should carry the type of the frame");
    }

    /// <summary>
    /// A supported version after a mismatch opens the gate.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderSupportedVersionAfterMismatchDecodesFrames()
    {
        // Arrange
        using var stream = new ChunkedStream([.. VersionFrame(4), .. VersionFrame(2), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        // Act
        var mismatch = await reader.ReadAsync(TestContext.CancellationToken);
        var version = await reader.ReadAsync(TestContext.CancellationToken);
        var next = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, mismatch.Error?.Kind, "The unsupported version should give a mismatch");
        Assert.AreEqual(2, (version.Message as VersionMessage)?.Version, "The supported version should be returned");
        Assert.IsInstanceOfType<SessionEndMessage>(next.Message, "Frames should be decoded after the supported version");
    }

    /// <summary>
    /// A frame before any version gives a mismatch without a received version.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderFrameBeforeVersionReturnsMismatchWithoutVersion()
    {
        // Arrange
        using var stream = new ChunkedStream(SessionEndFrame());
        var reader = new PipeMessageReader(stream);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, result.Error?.Kind, "A frame before the version should give a mismatch");
        Assert.IsNull(result.Error?.ReceivedVersion, "No version was received");
        Assert.AreEqual(PipeProtocol.NewestSupportedVersion, result.Error?.ExpectedVersion, "The error should carry the expected version");
        Assert.AreEqual((byte)2, result.Error?.TypeValue, "The error should carry the type of the frame");
    }

    /// <summary>
    /// An empty frame before any version gives the empty-frame error.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderEmptyFrameBeforeVersionReturnsEmptyFrame()
    {
        // Arrange
        using var stream = new ChunkedStream(PipeFrameBuilder.WithPrefix([]));
        var reader = new PipeMessageReader(stream);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.EmptyFrame, result.Error?.Kind, "An empty frame should not consult the gate");
    }

    /// <summary>
    /// An unknown type (6) before any version gives a mismatch.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task PipeMessageReaderUnknownTypeBeforeVersionReturnsMismatch()
    {
        // Arrange
        using var stream = new ChunkedStream(new PipeFrameBuilder().Byte(6).BuildWithPrefix());
        var reader = new PipeMessageReader(stream);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, result.Error?.Kind, "An unknown type 6 before the version should give a mismatch");
        Assert.AreEqual((byte)6, result.Error?.TypeValue, "The error should carry the type of the frame");
        Assert.IsNull(result.Error?.ReceivedVersion, "No version was received");
    }

    /// <summary>
    /// A trade-route or item-offer frame before any version gives a mismatch, as every non-version frame does.
    /// </summary>
    /// <param name="type">The type byte of the frame</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow((byte)4)]
    [DataRow((byte)5)]
    public async Task PipeMessageReaderTradeRoutesOrItemOffersBeforeVersionReturnsMismatch(byte type)
    {
        // Arrange
        using var stream = new ChunkedStream(PipeFrameBuilder.WithPrefix(TradeRoutesOrItemOffersFrame(type)));
        var reader = new PipeMessageReader(stream);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, result.Error?.Kind, $"A type {type} frame before the version should give a mismatch");
        Assert.AreEqual(type, result.Error?.TypeValue, "The error should carry the type of the frame");
        Assert.IsNull(result.Error?.ReceivedVersion, "No version was received");
        Assert.AreEqual(3, result.Error?.ExpectedVersion, "The error should carry version 3 as the expected version");
    }

    /// <summary>
    /// A trade-route frame after version 3 is delivered as a trade-routes message with its routes, carrying its whole
    /// frame, and the frame after it is decoded.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PipeMessageReaderTradeRoutesAfterVersionThreeReturnsTradeRoutesMessage()
    {
        // Arrange
        var frame = TradeRoutesFrame();
        using var stream = new ChunkedStream([.. VersionFrame(3), .. PipeFrameBuilder.WithPrefix(frame), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);
        var next = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        AssertTradeRoutes(result, frame);
        Assert.IsInstanceOfType<SessionEndMessage>(next.Message, "The frame after the trade routes should be decoded");
    }

    /// <summary>
    /// A trade-route frame after version 2 is decoded as well: the parser has no version.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task PipeMessageReaderTradeRoutesAfterVersionTwoReturnsTradeRoutesMessage()
    {
        // Arrange
        var frame = TradeRoutesFrame();
        using var stream = new ChunkedStream([.. VersionFrame(2), .. PipeFrameBuilder.WithPrefix(frame)]);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        AssertTradeRoutes(result, frame);
    }

    /// <summary>
    /// A failed read gives the read-failed error, then the end of the stream.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderReadFailedThenReturnsEndOfStream()
    {
        // Arrange
        var data = VersionFrame(2);
        using var stream = new ChunkedStream(data, ioErrorOffset: data.Length);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var failed = await reader.ReadAsync(TestContext.CancellationToken);
        var after = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ReadFailed, failed.Error?.Kind, "The failed read should give its error");
        Assert.AreEqual(PipeReadResultKind.EndOfStream, after.Kind, "The reader should then be at the end");
    }

    /// <summary>
    /// A cancelled read ends with cancellation.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderCancelledReadThrowsOperationCanceled()
    {
        // Arrange
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var stream = new ChunkedStream(VersionFrame(2), cancelOffset: 0, cancellationSource: source);
        var reader = new PipeMessageReader(stream);

        // Act
        async Task Act()
        {
            await reader.ReadAsync(source.Token);
        }

        // Assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(Act, "A cancelled read should end with an OperationCanceledException");
    }

    /// <summary>
    /// A sequence cut at any offset never throws and ends with the end of the stream.
    /// </summary>
    /// <param name="offset">The number of bytes kept</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DynamicData(nameof(CutOffsets))]
    public async Task PipeMessageReaderTruncatedSequenceNeverThrows(int offset)
    {
        // Arrange
        using var stream = new ChunkedStream([.. CreateSequence().Take(offset)]);
        var reader = new PipeMessageReader(stream);

        // Act
        var results = await ReadToEndAsync(reader);

        // Assert
        Assert.AreEqual(PipeReadResultKind.EndOfStream, results[^1].Kind, "The last result should be the end of the stream");
    }

    /// <summary>
    /// A sequence with one byte replaced never throws and ends with the end of the stream.
    /// </summary>
    /// <param name="offset">The offset of the replaced byte</param>
    /// <param name="value">The replacement byte</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DynamicData(nameof(CorruptedOffsets))]
    public async Task PipeMessageReaderCorruptedSequenceNeverThrows(int offset, byte value)
    {
        // Arrange
        var data = CreateSequence();

        data[offset] = value;

        using var stream = new ChunkedStream(data);
        var reader = new PipeMessageReader(stream);

        // Act
        var results = await ReadToEndAsync(reader);

        // Assert
        Assert.AreEqual(PipeReadResultKind.EndOfStream, results[^1].Kind, "The last result should be the end of the stream");
    }

    /// <summary>
    /// Each fixture, replayed through the message reader, gives the frame counts its README states, without an error or a
    /// problem of the file.
    /// </summary>
    /// <param name="fixture">The fixture file name</param>
    /// <param name="expected">The counts the README states</param>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC6")]
    [TestCategory("AC7")]
    [TestCategory("AC11")]
    [DataRow("long-session.jsonl", "Version=1 SessionStart=2 SessionEnd=2 AreaProductionStatistics=789 TradeRoutes=0 ItemOffers=0 Error=0 entries=21091")]
    [DataRow("protocol-3-session.jsonl", "Version=1 SessionStart=1 SessionEnd=1 AreaProductionStatistics=101 TradeRoutes=5 ItemOffers=4 Error=0 entries=2752")]
    [DataRow("protocol-3-trade-routes.jsonl", "Version=1 SessionStart=1 SessionEnd=0 AreaProductionStatistics=300 TradeRoutes=11 ItemOffers=12 Error=0 entries=7679")]
    public async Task PipeMessageReaderFixturesGiveReadmeFrameCounts(string fixture, string expected)
    {
        // Arrange
        using var replay = new PipeReplayStream(File.OpenRead(Path.Combine(FixtureDenyList.FixturesFolder, fixture)), PipeReplaySpeed.Maximum, new PipeReplayClock(DateTimeOffset.UnixEpoch), TimeProvider.System);
        var reader = new PipeMessageReader(replay);

        // Act
        var results = await ReadFixtureToEndAsync(reader);

        // Assert
        Assert.AreEqual(expected, CountKinds(results), $"The fixture {fixture} should give the frame counts of its README");
        Assert.AreEqual(PipeReadResultKind.EndOfStream, results[^1].Kind, "The replay should end with the end of the stream");
        Assert.IsNull(replay.Problem, "The fixture should be a valid recording");
    }

    /// <summary>
    /// The trade-route and item-offer frames of the protocol 3 fixture, emptied by the anonymiser that made it, decode as
    /// messages without routes or traders.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task PipeMessageReaderProtocolThreeFixtureGivesEmptyTradeRoutesAndItemOffers()
    {
        // Arrange
        using var replay = new PipeReplayStream(File.OpenRead(Path.Combine(FixtureDenyList.FixturesFolder, "protocol-3-session.jsonl")), PipeReplaySpeed.Maximum, new PipeReplayClock(DateTimeOffset.UnixEpoch), TimeProvider.System);
        var reader = new PipeMessageReader(replay);

        // Act
        var results = await ReadFixtureToEndAsync(reader);

        // Assert
        var messages = results.Select(result => result.Message).ToList();
        var routeCounts = string.Join(' ', messages.OfType<TradeRoutesMessage>().Select(message => message.Routes.Count));
        var offerCounts = string.Join(' ', messages.OfType<ItemOffersMessage>().Select(message => message.Offers.Count));

        Assert.AreEqual("0 0 0 0 0", routeCounts, "The five trade-route frames should decode as messages without routes");
        Assert.AreEqual("0 0 0 0", offerCounts, "The four item-offer frames should decode as messages without traders");
        Assert.IsNull(replay.Problem, "The fixture should be a valid recording");
    }

    /// <summary>
    /// Counts the read results by message type (trade routes and item offers included), the errors and the production
    /// entries.
    /// </summary>
    /// <param name="results">The results</param>
    /// <returns>The counts as text</returns>
    private static string CountKinds(List<PipeReadResult> results)
    {
        int Count(PipeMessageType type)
        {
            return results.Count(result => result.Message?.MessageType == type);
        }

        var errors = results.Count(result => result.Kind == PipeReadResultKind.Error);
        var entries = results.Select(result => result.Message).OfType<AreaProductionStatisticsMessage>().Sum(message => message.Entries.Count);

        return string.Create(CultureInfo.InvariantCulture,
                             $"Version={Count(PipeMessageType.Version)} SessionStart={Count(PipeMessageType.SessionStart)} SessionEnd={Count(PipeMessageType.SessionEnd)} AreaProductionStatistics={Count(PipeMessageType.AreaProductionStatistics)} TradeRoutes={Count(PipeMessageType.TradeRoutes)} ItemOffers={Count(PipeMessageType.ItemOffers)} Error={errors} entries={entries}");
    }

    /// <summary>
    /// Creates a version frame with its length prefix.
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns>The bytes</returns>
    private static byte[] VersionFrame(int version)
    {
        return new PipeFrameBuilder().Byte(0).Int32Value(version).BuildWithPrefix();
    }

    /// <summary>
    /// Creates a session-end frame with its length prefix.
    /// </summary>
    /// <returns>The bytes</returns>
    private static byte[] SessionEndFrame()
    {
        return new PipeFrameBuilder().Byte(2).BuildWithPrefix();
    }

    /// <summary>
    /// Creates a trade-route frame in the reference layout with one route of one station of one good, without its length
    /// prefix.
    /// </summary>
    /// <returns>The frame bytes</returns>
    private static byte[] TradeRoutesFrame()
    {
        var good = new TradeRouteGood
                   {
                       ProductGuid = 1010,
                       Amount = 5,
                       Loading = true
                   };
        var station = new TradeRouteStation
                      {
                          Id = 11,
                          SessionId = 1,
                          IslandId = 2,
                          AreaIndex = 0,
                          SessionGuid = 180023,
                          Owner = 7,
                          ThrowOverboard = false,
                          WaitUntilFull = false,
                          WaitUntilEmpty = true,
                          Goods = [good]
                      };
        var route = new TradeRoute
                    {
                        Id = 42,
                        Name = "Route",
                        Folder = "Folder",
                        MajorProblems = 0,
                        MinorProblems = 1,
                        Ships = 2,
                        LostShips = 0,
                        Stations = [station]
                    };

        return new PipeFrameBuilder().Byte((byte)PipeMessageType.TradeRoutes).TradeRoutes([route]).Build();
    }

    /// <summary>
    /// Creates a trade-route or item-offer frame in the reference layout with one entry, without its length prefix.
    /// </summary>
    /// <param name="type">The type byte, 4 or 5</param>
    /// <returns>The frame bytes</returns>
    private static byte[] TradeRoutesOrItemOffersFrame(byte type)
    {
        var offer = new ItemOffer
                    {
                        TraderId = 8,
                        ItemGuids = [1000]
                    };

        return type == (byte)PipeMessageType.TradeRoutes
                   ? TradeRoutesFrame()
                   : new PipeFrameBuilder().Byte(type).ItemOffers([offer]).Build();
    }

    /// <summary>
    /// Asserts that a result is the trade-routes message of <see cref="TradeRoutesFrame" />, carrying that frame.
    /// </summary>
    /// <param name="result">The result</param>
    /// <param name="frame">The frame it was read from</param>
    private static void AssertTradeRoutes(PipeReadResult result, byte[] frame)
    {
        Assert.AreEqual(PipeReadResultKind.Message, result.Kind, "The trade-route frame should give a message");

        var message = result.Message as TradeRoutesMessage;

        Assert.IsNotNull(message, "The trade-route frame should give a trade-routes message");
        Assert.HasCount(1, message.Routes, "The route should be delivered");
        Assert.AreEqual(42, message.Routes[0].Id, "The route ID should be delivered");
        Assert.AreEqual("Folder", message.Routes[0].Folder, "The folder should be delivered");
        Assert.AreEqual(2, message.Routes[0].Ships, "The ship count should be delivered");
        Assert.HasCount(1, message.Routes[0].Stations, "The station should be delivered");
        Assert.IsTrue(message.Routes[0].Stations[0].WaitUntilEmpty, "The station's flag should be delivered");
        Assert.HasCount(1, message.Routes[0].Stations[0].Goods, "The good should be delivered");
        Assert.AreEqual(5, message.Routes[0].Stations[0].Goods[0].Amount, "The good's amount should be delivered");
        Assert.AreSequenceEqual(frame, result.Frame?.ToArray() ?? [], "The result should carry the whole frame");
    }

    /// <summary>
    /// Creates a sequence of a version 2, a session start, a statistics frame and a session end.
    /// </summary>
    /// <returns>The bytes</returns>
    private static byte[] CreateSequence()
    {
        return CreateSequence(2);
    }

    /// <summary>
    /// Creates a sequence of a version, a session start, a statistics frame and a session end.
    /// </summary>
    /// <param name="version">The version of the first frame</param>
    /// <returns>The bytes</returns>
    private static byte[] CreateSequence(int version)
    {
        var statistics = new PipeFrameBuilder().Byte(3)
                                               .Byte(200)
                                               .Byte(255)
                                               .Byte(128)
                                               .Int32Value(-77)
                                               .StringValue("Area")
                                               .Int64Value(123456789012L)
                                               .Int32Value(1)
                                               .Entry(10, [new(1, 2)], [new(3, 4)])
                                               .BuildWithPrefix();

        return
        [
            .. VersionFrame(version),
            .. new PipeFrameBuilder().Byte(1).StringValue("Head").BuildWithPrefix(),
            .. statistics,
            .. SessionEndFrame()
        ];
    }

    /// <summary>
    /// Reads until the end of the stream.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns>All results, the end of the stream last</returns>
    private async Task<List<PipeReadResult>> ReadToEndAsync(PipeMessageReader reader)
    {
        var results = new List<PipeReadResult>();

        for (var count = 0; count < 100; count++)
        {
            var result = await reader.ReadAsync(TestContext.CancellationToken);

            results.Add(result);

            if (result.Kind == PipeReadResultKind.EndOfStream)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Reads and discards a number of results.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <param name="count">The number of results</param>
    /// <returns>The task</returns>
    private async Task SkipResultsAsync(PipeMessageReader reader, int count)
    {
        for (var index = 0; index < count; index++)
        {
            await reader.ReadAsync(TestContext.CancellationToken);
        }
    }

    /// <summary>
    /// Reads a message reader to its end, for a fixture of up to 100,000 frames.
    /// </summary>
    /// <param name="reader">The reader</param>
    /// <returns>All results, the end last</returns>
    private async Task<List<PipeReadResult>> ReadFixtureToEndAsync(PipeMessageReader reader)
    {
        var results = new List<PipeReadResult>();
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        results.Add(result);

        while (result.Kind != PipeReadResultKind.EndOfStream && results.Count < 100000)
        {
            result = await reader.ReadAsync(TestContext.CancellationToken);

            results.Add(result);
        }

        return results;
    }

    #endregion // Methods
}