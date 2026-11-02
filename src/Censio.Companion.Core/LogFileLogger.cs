using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// A logger that hands its entries to a <see cref="LogFileLoggerProvider" />.
/// </summary>
internal sealed class LogFileLogger : ILogger
{
    #region Fields

    /// <summary>
    /// The provider.
    /// </summary>
    private readonly LogFileLoggerProvider _provider;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LogFileLogger" /> class.
    /// </summary>
    /// <param name="provider">The provider</param>
    internal LogFileLogger(LogFileLoggerProvider provider)
    {
        _provider = provider;
    }

    #endregion // Constructor

    #region ILogger

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        throw new NotImplementedException();
    }

    #endregion // ILogger
}