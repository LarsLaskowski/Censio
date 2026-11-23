namespace Censio.Companion.Core;

/// <summary>
/// The server a live sender sends to.
/// </summary>
internal sealed class LiveSendTarget
{
    #region Properties

    /// <summary>
    /// Gets the address of the server.
    /// </summary>
    public required Uri Address { get; init; }

    /// <summary>
    /// Gets the reader of the token, or <see langword="null" /> when no token is presented.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? ReadToken { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server is the local one.
    /// </summary>
    public bool IsLocal { get; init; }

    #endregion // Properties
}