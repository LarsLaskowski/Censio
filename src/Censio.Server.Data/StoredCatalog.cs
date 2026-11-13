namespace Censio.Server.Data;

/// <summary>
/// The stored catalog.
/// </summary>
public sealed class StoredCatalog
{
    #region Properties

    /// <summary>
    /// Gets the catalog identity.
    /// </summary>
    public required string Identity { get; init; }

    /// <summary>
    /// Gets the catalog document.
    /// </summary>
    public required ReadOnlyMemory<byte> Document { get; init; }

    #endregion // Properties
}