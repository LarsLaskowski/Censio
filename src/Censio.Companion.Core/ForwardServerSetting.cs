namespace Censio.Companion.Core;

/// <summary>
/// The server setting of forward mode: where live data is sent and the token that is presented.
/// </summary>
public sealed class ForwardServerSetting
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardServerSetting" /> class.
    /// </summary>
    /// <param name="address">The address of the server</param>
    /// <param name="token">The token, or <see langword="null" /> when the setting names none</param>
    public ForwardServerSetting(Uri address, string? token)
    {
        Address = address;
        Token = token;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the address of the server.
    /// </summary>
    public Uri Address { get; }

    /// <summary>
    /// Gets the token, or <see langword="null" /> when the setting names none.
    /// </summary>
    public string? Token { get; }

    #endregion // Properties
}