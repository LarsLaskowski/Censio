using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// Builds the view of a profile from its state.
/// </summary>
public static class ProfileViewBuilder
{
    #region Methods

    /// <summary>
    /// Builds the view of a profile.
    /// </summary>
    /// <param name="state">The profile state</param>
    /// <param name="catalog">The catalog lookup, or <see langword="null"/> when none is usable</param>
    /// <param name="missingIcons">The references of the icons not stored, or <see langword="null"/> when unknown</param>
    /// <param name="island">The shown island, or <see langword="null"/> for none</param>
    /// <returns>The view</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static ProfileView Build(StoredProfileState state, PageCatalog? catalog, IReadOnlySet<string>? missingIcons, IslandKey? island)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    #endregion // Methods
}