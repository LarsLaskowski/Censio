using Censio.GameData;

namespace Censio.Companion.Core;

/// <summary>
/// Identifies the build of the Companion for the catalog cache.
/// </summary>
public static class CompanionIdentity
{
    #region Properties

    /// <summary>
    /// Gets the identity of this build: the module version ids of the assemblies that shape the catalog.
    /// </summary>
    public static string Current { get; } = string.Join(';', typeof(GameCatalog).Module.ModuleVersionId.ToString("D"), typeof(CompanionIdentity).Module.ModuleVersionId.ToString("D"));

    #endregion // Properties
}