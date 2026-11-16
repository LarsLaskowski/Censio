namespace Censio.Server;

/// <summary>
/// Decides whether a page opened by a host may delete a profile's history.
/// </summary>
public static class DeletionHostRule
{
    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable S1172, RH2003
    /// <summary>
    /// Determines whether a host is trusted.
    /// </summary>
    /// <param name="host">The host of the request, brackets of an IPv6 address kept</param>
    /// <param name="allowedHosts">The AllowedHosts setting</param>
    /// <returns><see langword="true"/> when the host is trusted</returns>
    public static bool IsTrusted(string? host, string? allowedHosts)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    #endregion // Methods
}