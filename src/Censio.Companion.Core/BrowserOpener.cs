using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Opens an address in the browser.
/// </summary>
public sealed class BrowserOpener
{
    #region Fields

    /// <summary>
    /// The delegate that launches the address.
    /// </summary>
    private readonly Action<Uri> _launch;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserOpener" /> class.
    /// </summary>
    /// <param name="launch">The delegate that launches the address</param>
    /// <param name="logger">The logger</param>
    public BrowserOpener(Action<Uri> launch, ILogger logger)
    {
        _launch = launch;
        _logger = logger;
    }

    #endregion // Constructor

    #region Methods

    /// <summary>
    /// Opens an address in the browser.
    /// </summary>
    /// <param name="address">The address</param>
    public void Open(Uri address)
    {
        try
        {
            _launch(address);
        }
        catch (Exception exception)
        {
            CompanionLog.BrowserOpenFailed(_logger, exception);
        }
    }

    #endregion // Methods
}