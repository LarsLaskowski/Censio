namespace Censio.GameData.Tests;

/// <summary>
/// A read-only memory stream that writes an entry to a log when it is disposed, so a test can show when a reader
/// releases a stream.
/// </summary>
public sealed class TrackingStream : MemoryStream
{
    #region Fields

    /// <summary>
    /// The entry written when the stream is disposed.
    /// </summary>
    private readonly string _entry;

    /// <summary>
    /// The log.
    /// </summary>
    private readonly List<string> _log;

    /// <summary>
    /// Whether the stream was disposed.
    /// </summary>
    private bool _isDisposed;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingStream" /> class.
    /// </summary>
    /// <param name="bytes">The content</param>
    /// <param name="entry">The entry written when the stream is disposed</param>
    /// <param name="log">The log</param>
    public TrackingStream(byte[] bytes, string entry, List<string> log)
        : base(bytes, false)
    {
        _entry = entry;
        _log = log;
    }

    #endregion // Constructors

    #region Stream

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && _isDisposed == false)
        {
            _isDisposed = true;
            _log.Add(_entry);
        }

        base.Dispose(disposing);
    }

    #endregion // Stream
}