namespace Censio.Server.Data;

/// <summary>
/// One icon of the <c>Icons</c> table.
/// </summary>
internal sealed class IconRow
{
    #region Properties

    /// <summary>
    /// Gets or sets the icon reference key.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PNG image.
    /// </summary>
    public byte[] Png { get; set; } = [];

    #endregion // Properties
}