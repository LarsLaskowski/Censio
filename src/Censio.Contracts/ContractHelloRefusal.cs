namespace Censio.Contracts;

/// <summary>
/// The reason the server refuses a hello.
/// </summary>
public enum ContractHelloRefusal
{
    #region Values

    /// <summary>
    /// The contract version is not supported.
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// The token is refused.
    /// </summary>
    TokenRefused,

    /// <summary>
    /// Another Companion is connected.
    /// </summary>
    CompanionConnected

    #endregion // Values
}