namespace Censio.Companion.Core;

/// <summary>
/// Reads the install records of Ubisoft Connect.
/// </summary>
public interface IUbisoftInstallRecords
{
    #region Methods

    /// <summary>
    /// Reads the install records.
    /// </summary>
    /// <returns>The install records</returns>
    IReadOnlyList<UbisoftInstallRecord> Read();

    #endregion // Methods
}