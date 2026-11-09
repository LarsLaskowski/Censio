using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// Reads the install records of Ubisoft Connect from the registry.
/// </summary>
internal sealed class RegistryUbisoftInstallRecords : IUbisoftInstallRecords
{
    #region IUbisoftInstallRecords

    /// <inheritdoc />
    public IReadOnlyList<UbisoftInstallRecord> Read()
    {
        throw new NotImplementedException();
    }

    #endregion // IUbisoftInstallRecords
}