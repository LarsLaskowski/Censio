using System.Security.Cryptography;
using System.Text;

namespace Censio.Server;

/// <summary>
/// Decides whether a Companion's token is accepted; keeps no token text.
/// </summary>
public sealed class IngestAccess
{
    #region Fields

    /// <summary>
    /// The SHA-256 hash of the accepted token, or <c>null</c> when no token is accepted.
    /// </summary>
    private readonly byte[]? _tokenHash;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestAccess" /> class.
    /// </summary>
    /// <param name="isTokenRequired">A value indicating whether a token is required</param>
    /// <param name="tokenHash">The SHA-256 hash of the accepted token, or <c>null</c> when none is accepted</param>
    private IngestAccess(bool isTokenRequired, byte[]? tokenHash)
    {
        IsTokenRequired = isTokenRequired;
        _tokenHash = tokenHash;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets a value indicating whether a token is required.
    /// </summary>
    public bool IsTokenRequired { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates an access that accepts every token, including none.
    /// </summary>
    /// <returns>The access</returns>
    public static IngestAccess CreateOpen()
    {
        return new IngestAccess(false, null);
    }

    /// <summary>
    /// Creates an access that refuses every token.
    /// </summary>
    /// <returns>The access</returns>
    public static IngestAccess CreateRefusingAll()
    {
        return new IngestAccess(true, null);
    }

    /// <summary>
    /// Creates an access that accepts exactly one token.
    /// </summary>
    /// <param name="token">The accepted token</param>
    /// <returns>The access</returns>
    public static IngestAccess CreateForToken(string token)
    {
        return new IngestAccess(true, Hash(token));
    }

    /// <summary>
    /// Checks a presented token.
    /// </summary>
    /// <param name="presentedToken">The token the Companion presented, or <c>null</c></param>
    /// <returns><c>true</c> when the token is accepted</returns>
    public bool IsTokenAccepted(string? presentedToken)
    {
        if (IsTokenRequired == false)
        {
            return true;
        }

        if (_tokenHash is null || presentedToken is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(_tokenHash, Hash(presentedToken));
    }

    /// <summary>
    /// Hashes a token.
    /// </summary>
    /// <param name="token">The token</param>
    /// <returns>The SHA-256 hash of the token's UTF-8 bytes</returns>
    private static byte[] Hash(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    #endregion // Methods
}