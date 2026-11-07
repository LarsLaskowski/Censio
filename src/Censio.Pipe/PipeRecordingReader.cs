namespace Censio.Pipe;

/// <summary>
/// Reads the lines of a pipe recording one by one with the line rules of the format.
/// </summary>
public sealed class PipeRecordingReader
{
    #region Fields

    /// <summary>
    /// The recording to read.
    /// </summary>
    private readonly Stream _recording;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingReader"/> class.
    /// </summary>
    /// <param name="recording">The recording to read</param>
    public PipeRecordingReader(Stream recording)
    {
        _recording = recording;
    }

    #endregion // Constructors

    #region Properties

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S2325

    /// <summary>
    /// Gets the start time of the header once the header is read, or <see langword="null" /> when it is missing or unusable.
    /// </summary>
    public DateTimeOffset? Started => throw new NotImplementedException();

#pragma warning restore RH2003, S2325

    #endregion // Properties

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003

    /// <summary>
    /// Reads the next line; the first call reads and checks the header.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the reads</param>
    /// <returns>The frame line, the end of the recording or the problem that ends the reading</returns>
    public ValueTask<PipeRecordingLine> ReadAsync(CancellationToken cancellationToken)
    {
        _ = _recording;

        throw new NotImplementedException();
    }

#pragma warning restore RH2003

    #endregion // Methods
}