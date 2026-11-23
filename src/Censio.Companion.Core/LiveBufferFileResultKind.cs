namespace Censio.Companion.Core;

/// <summary>
/// The outcome kind of reading a live buffer file.
/// </summary>
public enum LiveBufferFileResultKind
{
    /// <summary>
    /// The file or header is valid.
    /// </summary>
    Read,

    /// <summary>
    /// The file or header cannot be read.
    /// </summary>
    Unreadable
}