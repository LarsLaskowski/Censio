using System.Net;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// A system proxy setting that sends every request, loopback addresses included, to one proxy; a <see cref="WebProxy" />
/// always bypasses loopback addresses, whatever <see cref="WebProxy.BypassProxyOnLocal" /> says.
/// </summary>
public sealed class LoopbackWebProxy : IWebProxy
{
    #region Fields

    /// <summary>
    /// The address of the proxy.
    /// </summary>
    private readonly Uri _address;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopbackWebProxy" /> class.
    /// </summary>
    /// <param name="address">The address of the proxy</param>
    public LoopbackWebProxy(Uri address)
    {
        _address = address;
    }

    #endregion // Constructors

    #region IWebProxy

    /// <inheritdoc />
    public ICredentials? Credentials { get; set; }

    /// <inheritdoc />
    public Uri GetProxy(Uri destination)
    {
        return _address;
    }

    /// <inheritdoc />
    public bool IsBypassed(Uri host)
    {
        return false;
    }

    #endregion // IWebProxy
}