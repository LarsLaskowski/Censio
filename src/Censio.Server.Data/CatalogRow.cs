namespace Censio.Server.Data;

/// <summary>
/// The single catalog row of the <c>Catalog</c> table.
/// </summary>
internal sealed class CatalogRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the row number, always 1.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the catalog identity.
    /// </summary>
    public string Identity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the catalog document.
    /// </summary>
    public byte[] Document { get; set; } = [];

    #endregion // Properties
}