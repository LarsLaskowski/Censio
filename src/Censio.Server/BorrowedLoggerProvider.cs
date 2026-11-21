namespace Censio.Server;

/// <summary>
/// Passes the loggers of a logger provider on and never disposes the provider.
/// </summary>
internal sealed class BorrowedLoggerProvider : ILoggerProvider
{
    #region Fields

    /// <summary>
    /// The provider the loggers come from.
    /// </summary>
    private readonly ILoggerProvider _provider;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BorrowedLoggerProvider" /> class.
    /// </summary>
    /// <param name="provider">The provider the loggers come from.</param>
    public BorrowedLoggerProvider(ILoggerProvider provider)
    {
        _provider = provider;
    }

    #endregion // Constructors

    #region ILoggerProvider

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return _provider.CreateLogger(categoryName);
    }

    #endregion // ILoggerProvider

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        // The provider belongs to the caller, who disposes it.
    }

    #endregion // IDisposable
}