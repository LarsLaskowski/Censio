using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Server.Data.Tests;

/// <summary>
/// An <see cref="ILoggerProvider" /> whose loggers of every category write into one <see cref="RecordingLogger" />.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    #region Properties

    /// <summary>
    /// Gets the logger that keeps the entries of every category.
    /// </summary>
    public RecordingLogger Logger { get; } = new();

    #endregion // Properties

    #region ILoggerProvider

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return Logger;
    }

    #endregion // ILoggerProvider

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        // The recorded entries stay readable after the factory is disposed
    }

    #endregion // IDisposable
}