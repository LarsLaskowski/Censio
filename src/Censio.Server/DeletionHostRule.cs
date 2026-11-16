namespace Censio.Server;

/// <summary>
/// Decides whether a page opened by a host may delete a profile's history.
/// </summary>
public static class DeletionHostRule
{
    #region Constants

    /// <summary>
    /// The host name a page may be opened by without an entry in the AllowedHosts setting.
    /// </summary>
    private const string LocalHost = "localhost";

    /// <summary>
    /// The setting entry that turns host filtering off.
    /// </summary>
    private const string AnyHost = "*";

    /// <summary>
    /// The start of a setting entry that lists every host of a domain.
    /// </summary>
    private const string DomainPrefix = "*.";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Determines whether a host is trusted: an IP address, <c>localhost</c> or a host name the AllowedHosts setting lists.
    /// </summary>
    /// <param name="host">The host of the request, brackets of an IPv6 address kept</param>
    /// <param name="allowedHosts">The AllowedHosts setting</param>
    /// <returns><see langword="true"/> when the host is trusted</returns>
    public static bool IsTrusted(string? host, string? allowedHosts)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        if (IsAddress(host) || string.Equals(host, LocalHost, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Trim() == AnyHost)
        {
            return false;
        }

        return allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(entry => IsListed(host, entry));
    }

    /// <summary>
    /// Determines whether a host is an IPv4 or IPv6 address.
    /// </summary>
    /// <param name="host">The host, brackets of an IPv6 address kept</param>
    /// <returns><see langword="true"/> when the host is an address</returns>
    private static bool IsAddress(string host)
    {
        var bare = host.Length > 2 && host[0] == '[' && host[^1] == ']'
                       ? host[1..^1]
                       : host;

        return Uri.CheckHostName(bare) is UriHostNameType.IPv4 or UriHostNameType.IPv6;
    }

    /// <summary>
    /// Determines whether an entry of the AllowedHosts setting names a host.
    /// </summary>
    /// <param name="host">The host</param>
    /// <param name="entry">The entry, a host name or <c>*.</c> and a domain</param>
    /// <returns><see langword="true"/> when the entry names the host</returns>
    private static bool IsListed(string host, string entry)
    {
        if (entry.StartsWith(DomainPrefix, StringComparison.Ordinal))
        {
            var suffix = entry[1..];

            return suffix.Length > 1 && host.Length > suffix.Length && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(host, entry, StringComparison.OrdinalIgnoreCase);
    }

    #endregion // Methods
}