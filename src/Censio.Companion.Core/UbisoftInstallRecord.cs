namespace Censio.Companion.Core;

/// <summary>
/// One install record of Ubisoft Connect.
/// </summary>
public sealed record UbisoftInstallRecord
{
    #region Properties

    /// <summary>
    /// Gets the name of the record.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the install folder of the record, if it has one.
    /// </summary>
    public string? InstallDir { get; init; }

    #endregion // Properties
}