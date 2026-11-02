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
            yield return [Array.Empty<byte>(), 0, new byte[] { 0, 3, 0, 0, 0 }, PipeReadResultKind.Error];
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
        using var stream = new ChunkedStream([.. VersionFrame(2), .. new PipeFrameBuilder().Byte(4).BuildWithPrefix(), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var unknown = await reader.ReadAsync(TestContext.CancellationToken);
        var next = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.UnknownType, unknown.Error?.Kind, "The unknown type should give its error");
        Assert.AreEqual((byte)4, unknown.Error?.TypeValue, "The error should carry the type value");
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
    /// An unsupported version gives a protocol mismatch.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderUnsupportedVersionReturnsMismatch()
    {
        // Arrange
        using var stream = new ChunkedStream(VersionFrame(3));
        var reader = new PipeMessageReader(stream);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, result.Error?.Kind, "An unsupported version should give a mismatch");
        Assert.AreEqual(3, result.Error?.ReceivedVersion, "The error should carry the received version");
        Assert.AreEqual(PipeProtocol.SupportedVersion, result.Error?.ExpectedVersion, "The error should carry the expected version");
    }

    /// <summary>
    /// Every frame after an unsupported version gives a mismatch without decoding.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderFrameAfterUnsupportedVersionReturnsMismatch()
    {
        // Arrange
        using var stream = new ChunkedStream([.. VersionFrame(3), .. SessionEndFrame()]);
        var reader = new PipeMessageReader(stream);

        await reader.ReadAsync(TestContext.CancellationToken);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, result.Error?.Kind, "A frame after an unsupported version should give a mismatch");
        Assert.AreEqual(3, result.Error?.ReceivedVersion, "The error should carry the received version");
        Assert.AreEqual(PipeProtocol.SupportedVersion, result.Error?.ExpectedVersion, "The error should carry the expected version");
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
        using var stream = new ChunkedStream([.. VersionFrame(3), .. VersionFrame(2), .. SessionEndFrame()]);
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
        Assert.AreEqual(PipeProtocol.SupportedVersion, result.Error?.ExpectedVersion, "The error should carry the expected version");
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
    /// An unknown type before any version gives a mismatch.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeMessageReaderUnknownTypeBeforeVersionReturnsMismatch()
    {
        // Arrange
        using var stream = new ChunkedStream(new PipeFrameBuilder().Byte(4).BuildWithPrefix());
        var reader = new PipeMessageReader(stream);

        // Act
        var result = await reader.ReadAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeErrorKind.ProtocolMismatch, result.Error?.Kind, "An unknown type before the version should give a mismatch");
        Assert.AreEqual((byte)4, result.Error?.TypeValue, "The error should carry the type of the frame");
        Assert.IsNull(result.Error?.ReceivedVersion, "No version was received");
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
    /// Creates a version frame with its length prefix.
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns>The bytes</returns>
    private static byte[] VersionFrame(int version)
    {
        return new PipeFrameBuilder().Byte(0).Int32(version).BuildWithPrefix();
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
    /// Creates a sequence of a version, a session start, a statistics frame and a session end.
    /// </summary>
    /// <returns>The bytes</returns>
    private static byte[] CreateSequence()
    {
        var statistics = new PipeFrameBuilder().Byte(3)
                                               .Byte(200)
                                               .Byte(255)
                                               .Byte(128)
                                               .Int32(-77)
                                               .String("Area")
                                               .Int64(123456789012L)
                                               .Int32(1)
                                               .Entry(10, [new(1, 2)], [new(3, 4)])
                                               .BuildWithPrefix();

        return
        [
            .. VersionFrame(2),
            .. new PipeFrameBuilder().Byte(1).String("Head").BuildWithPrefix(),
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

    #endregion // Methods
}