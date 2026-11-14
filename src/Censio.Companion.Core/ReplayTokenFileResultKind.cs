namespace Censio.Companion.Core;

/// <summary>
/// The outcome kind of reading a token file.
/// </summary>
public enum ReplayTokenFileResultKind
{
    /// <summary>
    /// The file holds a valid token.
    /// </summary>
    Read,

    /// <summary>
    /// The file cannot be read.
    /// </summary>
    CannotBeRead,

    /// <summary>
    /// The file holds no valid token.
    /// </summary>
    NoValidToken
}