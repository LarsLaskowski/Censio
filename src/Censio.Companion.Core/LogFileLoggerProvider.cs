using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Writes log entries to rotating files in a folder.
/// </summary>
public sealed class LogFileLoggerProvider : ILoggerProvider
{
    #region Constants

    /// <summary>
    /// The maximum size of a log file in bytes.
    /// </summary>
    public const long MaxFileBytes = 10485760;

    /// <summary>
    /// The maximum number of log files kept.
    /// </summary>
    public const int MaxFiles = 10;

    /// <summary>
    /// The maximum number of characters queued for writing.
    /// </summary>
    public const int MaxQueuedChars = 1048576;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The log folder.
    /// </summary>
    private readonly string _folder;

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LogFileLoggerProvider" /> class.
    /// </summary>
    /// <param name="folder">The log folder</param>
    /// <param name="timeProvider">The time provider</param>
    public LogFileLoggerProvider(string folder, TimeProvider timeProvider)
    {
        _folder = folder;
        _timeProvider = timeProvider;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the log folder.
    /// </summary>
    internal string Folder => _folder;

    /// <summary>
    /// Gets the time provider.
    /// </summary>
    internal TimeProvider TimeProvider => _timeProvider;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Waits until the queue is written or the timeout has passed.
    /// </summary>
    /// <param name="timeout">The timeout</param>
    /// <returns>The task</returns>
    public Task FlushAsync(TimeSpan timeout)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods

    #region ILoggerProvider

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        throw new NotImplementedException();
    }

    #endregion // ILoggerProvider

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    #endregion // IDisposable
}