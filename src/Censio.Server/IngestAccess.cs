namespace Censio.Server;

/// <summary>
/// Decides whether a Companion's token is accepted; keeps no token text.
/// </summary>
public sealed class IngestAccess
{
    #region Properties

    /// <summary>
    /// Gets a value indicating whether a token is required.
    /// </summary>
    public bool IsTokenRequired { get; }

    #endregion // Properties

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S2325

    /// <summary>
    /// Creates an access that accepts every token, including none.
    /// </summary>
    /// <returns>The access</returns>
    public static IngestAccess CreateOpen()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates an access that refuses every token.
    /// </summary>
    /// <returns>The access</returns>
    public static IngestAccess CreateRefusingAll()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates an access that accepts exactly one token.
    /// </summary>
    /// <param name="token">The accepted token</param>
    /// <returns>The access</returns>
    public static IngestAccess CreateForToken(string token)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks a presented token.
    /// </summary>
    /// <param name="presentedToken">The token the Companion presented, or <c>null</c></param>
    /// <returns><c>true</c> when the token is accepted</returns>
    public bool IsTokenAccepted(string? presentedToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325

    #endregion // Methods
}