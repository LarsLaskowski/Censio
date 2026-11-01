namespace Censio.Companion.Core;

/// <summary>
/// The result of one attempt to connect to the statistics pipe.
/// </summary>
public sealed class PipeConnectResult
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeConnectResult" /> class.
    /// </summary>
    /// <param name="kind">The outcome kind</param>
    /// <param name="stream">The connected stream, if any</param>
    /// <param name="errorCode">The error code, if any</param>
    private PipeConnectResult(PipeConnectResultKind kind, Stream? stream, int? errorCode)
    {
        Kind = kind;
        Stream = stream;
        ErrorCode = errorCode;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the outcome kind.
    /// </summary>
    public PipeConnectResultKind Kind { get; }

    /// <summary>
    /// Gets the connected stream; <c>null</c> unless the kind is <see cref="PipeConnectResultKind.Connected" />.
    /// </summary>
    public Stream? Stream { get; }

    /// <summary>
    /// Gets the error code of a failed attempt, if any.
    /// </summary>
    public int? ErrorCode { get; }

    /// <summary>
    /// Gets the result for a pipe that does not exist.
    /// </summary>
    public static PipeConnectResult NotFound => throw new NotImplementedException();

    /// <summary>
    /// Gets the result for a pipe held by another reader.
    /// </summary>
    public static PipeConnectResult Busy => throw new NotImplementedException();

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the result of a connected pipe.
    /// </summary>
    /// <param name="stream">The connected stream</param>
    /// <returns>The result</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream" /> is <c>null</c></exception>
    public static PipeConnectResult FromStream(Stream stream)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Classifies a failed attempt.
    /// </summary>
    /// <param name="exception">The exception of the attempt</param>
    /// <param name="pipeExists">Whether the pipe is listed</param>
    /// <returns>The result</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception" /> is <c>null</c></exception>
    public static PipeConnectResult FromFailure(Exception exception, bool pipeExists)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}