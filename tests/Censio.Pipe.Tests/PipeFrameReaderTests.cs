namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeFrameReader" />.
/// </summary>
[TestClass]
public sealed class PipeFrameReaderTests
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeFrameReaderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public PipeFrameReaderTests(TestContext testContext)
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
    /// Gets the streams that end after a first read in a way that must keep every later read at the end.
    /// </summary>
    private static IEnumerable<object[]> EndedStreams => [
                                                             [Array.Empty<byte>()],
                                                             [new byte[] { 1, 0 }],
                                                             [PipeFrameBuilder.RawLength(8)],
                                                             [PipeFrameBuilder.RawLength(PipeProtocol.MaxFrameLength + 1)]
                                                         ];

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A stream that ends right after a frame gives the end of the stream.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeFrameReaderEndAtFrameBoundaryReturnsEndOfStream()
    {
        // Arrange
        using var stream = new ChunkedStream(new PipeFrameBuilder().Byte(2).BuildWithPrefix());
        var reader = new PipeFrameReader(stream);

        await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.EndOfStream, result.Kind, "A stream that ends at a frame boundary should give the end of the stream");
    }

    /// <summary>
    /// Every read after the end of the stream, a truncated frame or an oversized frame gives the end of the stream.
    /// </summary>
    /// <param name="data">The bytes of the stream</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DynamicData(nameof(EndedStreams))]
    public async Task PipeFrameReaderReadAfterEndReturnsEndOfStream(byte[] data)
    {
        // Arrange
        using var stream = new ChunkedStream(data);
        var reader = new PipeFrameReader(stream);

        await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.EndOfStream, result.Kind, "A read after the end of the stream should give the end of the stream");
    }

    /// <summary>
    /// A stream that ends inside the 4-byte length prefix gives a truncated-frame error.
    /// </summary>
    /// <param name="prefixBytes">The number of prefix bytes before the end</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task PipeFrameReaderStreamEndsInPrefixReturnsTruncated(int prefixBytes)
    {
        // Arrange
        using var stream = new ChunkedStream(PipeFrameBuilder.RawLength(8).Take(prefixBytes).ToArray());
        var reader = new PipeFrameReader(stream);

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.Error, result.Kind, "A stream that ends inside the prefix should give an error");
        Assert.AreEqual(PipeErrorKind.TruncatedFrame, result.Error?.Kind, "The error should be a truncated frame");
    }

    /// <summary>
    /// A stream that ends inside a payload gives a truncated-frame error with the announced length.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeFrameReaderStreamEndsInPayloadReturnsTruncated()
    {
        // Arrange
        using var stream = new ChunkedStream([.. PipeFrameBuilder.RawLength(10), 1, 2, 3, 4]);
        var reader = new PipeFrameReader(stream);

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.Error, result.Kind, "A stream that ends inside a payload should give an error");
        Assert.AreEqual(PipeErrorKind.TruncatedFrame, result.Error?.Kind, "The error should be a truncated frame");
        Assert.AreEqual(10, result.Error?.Length, "The error should carry the announced length");
    }

    /// <summary>
    /// A length over the limit or a negative length gives an oversized-frame error, and nothing more is read.
    /// </summary>
    /// <param name="length">The announced length</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DataRow(1048577)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public async Task PipeFrameReaderOversizedLengthReturnsOversized(int length)
    {
        // Arrange
        using var stream = new ChunkedStream([.. PipeFrameBuilder.RawLength(length), 1, 2, 3, 4, 5, 6, 7, 8]);
        var reader = new PipeFrameReader(stream);

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.Error, result.Kind, "An oversized length should give an error");
        Assert.AreEqual(PipeErrorKind.OversizedFrame, result.Error?.Kind, "The error should be an oversized frame");
        Assert.AreEqual(length, result.Error?.Length, "The error should carry the announced length");
        Assert.AreEqual(4, stream.Offset, "Nothing but the prefix should be read from the stream");
    }

    /// <summary>
    /// A frame of exactly the maximum length is read.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeFrameReaderMaximumLengthReturnsFrame()
    {
        // Arrange
        var frame = Enumerable.Range(0, PipeProtocol.MaxFrameLength).Select(index => (byte)(index % 251)).ToArray();
        using var stream = new ChunkedStream(PipeFrameBuilder.WithPrefix(frame));
        var reader = new PipeFrameReader(stream);

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.Frame, result.Kind, "A frame of the maximum length should be read");
        CollectionAssert.AreEqual(frame, result.Frame.ToArray(), "The frame should hold the delivered bytes");
    }

    /// <summary>
    /// An I/O error of the stream gives a read-failed error.
    /// </summary>
    /// <param name="errorOffset">The offset at which the stream fails</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DataRow(2)]
    [DataRow(6)]
    public async Task PipeFrameReaderStreamIoErrorReturnsReadFailed(int errorOffset)
    {
        // Arrange
        using var stream = new ChunkedStream(new PipeFrameBuilder().Byte(2).Raw(1, 2, 3, 4, 5).BuildWithPrefix(), ioErrorOffset: errorOffset);
        var reader = new PipeFrameReader(stream);

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.Error, result.Kind, "An I/O error should give an error result");
        Assert.AreEqual(PipeErrorKind.ReadFailed, result.Error?.Kind, "The error should be a failed read");
    }

    /// <summary>
    /// A stream disposed before the read gives a read-failed error.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeFrameReaderDisposedStreamReturnsReadFailed()
    {
        // Arrange
        var stream = new MemoryStream(new PipeFrameBuilder().Byte(2).BuildWithPrefix());
        var reader = new PipeFrameReader(stream);

        await stream.DisposeAsync();

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.Error, result.Kind, "A disposed stream should give an error result");
        Assert.AreEqual(PipeErrorKind.ReadFailed, result.Error?.Kind, "The error should be a failed read");
    }

    /// <summary>
    /// After a failed read, every read gives the end of the stream.
    /// </summary>
    /// <returns>The task of the test</returns>
    [TestMethod]
    public async Task PipeFrameReaderReadAfterReadFailedReturnsEndOfStream()
    {
        // Arrange
        using var stream = new ChunkedStream(new PipeFrameBuilder().Byte(2).BuildWithPrefix(), ioErrorOffset: 0);
        var reader = new PipeFrameReader(stream);

        await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.EndOfStream, result.Kind, "A read after a failed read should give the end of the stream");
    }

    /// <summary>
    /// A cancelled read ends with cancellation, before or after the prefix.
    /// </summary>
    /// <param name="cancelOffset">The offset at which the token is cancelled</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DataRow(0)]
    [DataRow(4)]
    public async Task PipeFrameReaderCancelledReadThrowsOperationCanceled(int cancelOffset)
    {
        // Arrange
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var stream = new ChunkedStream(new PipeFrameBuilder().Byte(2).Raw(1, 2, 3).BuildWithPrefix(), cancelOffset: cancelOffset, cancellationSource: source);
        var reader = new PipeFrameReader(stream);

        // Act
        async Task Act()
        {
            await reader.ReadFrameAsync(source.Token);
        }

        // Assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(Act, "A cancelled read should end with an OperationCanceledException");
    }

    /// <summary>
    /// After a cancelled read, every read gives the end of the stream.
    /// </summary>
    /// <param name="cancelOffset">The offset at which the token is cancelled</param>
    /// <returns>The task of the test</returns>
    [TestMethod]
    [DataRow(0)]
    [DataRow(4)]
    public async Task PipeFrameReaderReadAfterCancelledReadReturnsEndOfStream(int cancelOffset)
    {
        // Arrange
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var stream = new ChunkedStream(new PipeFrameBuilder().Byte(2).Raw(1, 2, 3).BuildWithPrefix(), cancelOffset: cancelOffset, cancellationSource: source);
        var reader = new PipeFrameReader(stream);

        async Task Cancelled()
        {
            await reader.ReadFrameAsync(source.Token);
        }

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(Cancelled, "The first read should be cancelled");

        // Act
        var result = await reader.ReadFrameAsync(TestContext.CancellationToken);

        // Assert
        Assert.AreEqual(PipeFrameResultKind.EndOfStream, result.Kind, "A read after a cancelled read should give the end of the stream");
    }

    /// <summary>
    /// A stream that cannot be read is rejected by the constructor.
    /// </summary>
    [TestMethod]
    public void PipeFrameReaderUnreadableStreamThrowsArgumentException()
    {
        // Arrange
        using var stream = new ChunkedStream([], readable: false);

        // Act
        void Act()
        {
            _ = new PipeFrameReader(stream);
        }

        // Assert
        Assert.ThrowsExactly<ArgumentException>(Act, "A stream that cannot be read should be rejected");
    }

    #endregion // Methods
}