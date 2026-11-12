namespace Censio.Server;

/// <summary>
/// Creates the ingest access of the server from its configuration and the token file in the data directory.
/// </summary>
public static partial class IngestTokenSetup
{
    #region Constants

    /// <summary>
    /// The configuration key that switches the ingest token requirement.
    /// </summary>
    public const string RequireTokenKey = "Censio:Ingest:RequireToken";

    /// <summary>
    /// The name of the token file in the data directory.
    /// </summary>
    public const string TokenFileName = "ingest-token";

    /// <summary>
    /// The maximum length of a token in bytes.
    /// </summary>
    public const int MaxTokenLength = 1024;

    #endregion // Constants

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003

    /// <summary>
    /// Creates the ingest access, reading or creating the token file, and reports a failure to the log.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The access, or <c>null</c> when the server must stop.</returns>
    public static IngestAccess? TryCreateAccess(IConfiguration configuration, ILogger logger)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Generates a new token.
    /// </summary>
    /// <returns>The token as Base64Url text without padding.</returns>
    public static string GenerateToken()
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003

    #endregion // Methods
}