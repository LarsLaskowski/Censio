using Microsoft.Extensions.Logging;

namespace Censio.Server.Tests;

/// <summary>
/// An <see cref="ILogger" /> that keeps every entry written to it: level, formatted message and exception.
/// </summary>
public sealed class RecordingLogger : ILogger
{
    #region Fields

    /// <summary>
    /// The entries written so far.
    /// </summary>
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the entries written so far, in the order they were written.
    /// </summary>
    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries => _entries;

    #endregion // Properties

    #region ILogger

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add((logLevel, formatter(state, exception), exception));
    }

    #endregion // ILogger
}