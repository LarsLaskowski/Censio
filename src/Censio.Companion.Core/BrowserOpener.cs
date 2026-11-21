using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Opens an address in the browser.
/// </summary>
public sealed class BrowserOpener
{
    #region Fields

#pragma warning disable S4487 // Skeleton: removed with the implementation

    /// <summary>
    /// The delegate that launches the address.
    /// </summary>
    private readonly Action<Uri> _launch;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

#pragma warning restore S4487 // Skeleton: removed with the implementation

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
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public void Open(Uri address)
    {
#pragma warning disable RH2003, S1172 // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003, S1172 // Skeleton: removed with the implementation
    }
#pragma warning restore S2325 // Skeleton: removed with the implementation

    #endregion // Methods
}