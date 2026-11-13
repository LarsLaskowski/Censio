using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// The result of storing a catalog.
/// </summary>
public sealed class CatalogStoreResult
{
    #region Properties

    /// <summary>
    /// Gets the result of the store.
    /// </summary>
    public required ServerStoreResult Result { get; init; }

    /// <summary>
    /// Gets the references of the icons that are not stored yet.
    /// </summary>
    public required IReadOnlyList<string> MissingIcons { get; init; }

    #endregion // Properties
}