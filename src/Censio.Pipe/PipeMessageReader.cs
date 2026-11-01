namespace Censio.Pipe;

/// <summary>
/// Reads typed messages from one pipe stream and applies the protocol version gate.
/// </summary>
public sealed class PipeMessageReader : IPipeMessageSource
{
    #region Fields

    /// <summary>
    /// The frame reader that owns the stream.
    /// </summary>
    private readonly PipeFrameReader _frameReader;

    /// <summary>
    /// Indicates whether a supported version has arrived.
    /// </summary>
    private bool _supported;

    /// <summary>
    /// The unsupported version received last, or <see langword="null" /> while no version has been rejected.
    /// </summary>
    private int? _mismatchVersion;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeMessageReader" /> class.
    /// </summary>
    /// <param name="stream">The stream to read from</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream" /> cannot be read.</exception>
    public PipeMessageReader(Stream stream)
    {
        _frameReader = new PipeFrameReader(stream);
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Applies the version gate to a parsed version frame.
    /// </summary>
    /// <param name="parsed">The parse result of the version frame</param>
    /// <returns>The version message, a mismatch error, or the parse error</returns>
    private PipeReadResult ApplyVersion(PipeReadResult parsed)
    {
        if (parsed.Message is not VersionMessage version)
        {
            return parsed;
        }

        if (version.Version == PipeProtocol.SupportedVersion)
        {
            _supported = true;
            _mismatchVersion = null;

            return parsed;
        }

        _supported = false;
        _mismatchVersion = version.Version;

        return PipeReadResult.FromError(new PipeError
                                        {
                                            Kind = PipeErrorKind.ProtocolMismatch,
                                            ReceivedVersion = version.Version,
                                            ExpectedVersion = PipeProtocol.SupportedVersion
                                        });
    }

    #endregion // Methods

    #region IPipeMessageSource

    /// <inheritdoc />
    public async ValueTask<PipeReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        var frameResult = await _frameReader.ReadFrameAsync(cancellationToken).ConfigureAwait(false);

        switch (frameResult.Kind)
        {
            case PipeFrameResultKind.EndOfStream:
                return PipeReadResult.End;

            case PipeFrameResultKind.Error:
                return PipeReadResult.FromError(frameResult.Error!);
        }

        var frame = frameResult.Frame;

        if (frame.IsEmpty)
        {
            return PipeMessageParser.Parse(frame.Span);
        }

        var typeValue = frame.Span[0];

        if (typeValue == (byte)PipeMessageType.Version)
        {
            return ApplyVersion(PipeMessageParser.Parse(frame.Span));
        }

        if (_supported == false)
        {
            return PipeReadResult.FromError(new PipeError
                                            {
                                                Kind = PipeErrorKind.ProtocolMismatch,
                                                ReceivedVersion = _mismatchVersion,
                                                ExpectedVersion = PipeProtocol.SupportedVersion,
                                                TypeValue = typeValue
                                            });
        }

        return PipeMessageParser.Parse(frame.Span);
    }

    #endregion // IPipeMessageSource
}