namespace Censio.GameData;

/// <summary>
/// Reads the template and property default files, each in one forward-only pass.
/// </summary>
internal static class GameCatalogDefaults
{
    #region Methods

    /// <summary>
    /// Reads the default region type of the <c>Region</c> property.
    /// </summary>
    /// <param name="stream">The property defaults file</param>
    /// <returns>The default <c>RegionID</c>, or <c>null</c> when none is stated</returns>
    internal static string? ReadDefaultRegionId(Stream stream)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads the region type each template with a <c>Region</c> property states.
    /// </summary>
    /// <param name="stream">The templates file</param>
    /// <returns>The stated <c>RegionID</c>, or <c>null</c>, by template name</returns>
    internal static Dictionary<string, string?> ReadTemplateRegionIds(Stream stream)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}