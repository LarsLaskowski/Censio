namespace Censio.Server;

/// <summary>
/// Parses the segments of a data page address.
/// </summary>
public static class PageAddress
{
    #region Methods

    /// <summary>
    /// Tries to parse a profile identity.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="profileId">The identity</param>
    /// <returns><see langword="true"/> when the text is an identity</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static bool TryParseProfile(string? text, out Guid profileId)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Tries to parse a session GUID.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="sessionGuid">The session GUID</param>
    /// <returns><see langword="true"/> when the text is a session GUID</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static bool TryParseSession(string? text, out int sessionGuid)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    /// <summary>
    /// Tries to parse a byte.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="value">The value</param>
    /// <returns><see langword="true"/> when the text is a byte</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static bool TryParseByte(string? text, out byte value)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    #endregion // Methods
}