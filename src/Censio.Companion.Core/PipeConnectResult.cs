namespace Censio.Companion.Core;

/// <summary>
/// The result of one attempt to connect to the statistics pipe.
/// </summary>
public sealed class PipeConnectResult
{
    #region Constants

    /// <summary>
    /// The mask of the facility bits of an HRESULT.
    /// </summary>
    private const uint Win32HResultMask = 0xFFFF0000;

    /// <summary>
    /// The facility bits of an HRESULT that wraps a Win32 error code.
    /// </summary>
    private const uint Win32HResultPrefix = 0x80070000;

    /// <summary>
    /// The mask of the Win32 error code in an HRESULT.
    /// </summary>
    private const uint Win32CodeMask = 0xFFFF;

    #endregion // Constants

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
    /// Gets the result for a pipe that does not exist.
    /// </summary>
    public static PipeConnectResult NotFound { get; } = new(PipeConnectResultKind.NotFound, null, null);

    /// <summary>
    /// Gets the result for a pipe held by another reader.
    /// </summary>
    public static PipeConnectResult Busy { get; } = new(PipeConnectResultKind.Busy, null, null);

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
        ArgumentNullException.ThrowIfNull(stream);

        return new PipeConnectResult(PipeConnectResultKind.Connected, stream, null);
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
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is FileNotFoundException)
        {
            return NotFound;
        }

        if (exception is TimeoutException)
        {
            return pipeExists ? Busy : NotFound;
        }

        var hresult = unchecked((uint)exception.HResult);
        int? errorCode = (hresult & Win32HResultMask) == Win32HResultPrefix ? (int)(hresult & Win32CodeMask) : null;

        return new PipeConnectResult(PipeConnectResultKind.Failed, null, errorCode);
    }

    #endregion // Methods
}