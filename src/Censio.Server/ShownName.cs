namespace Censio.Server;

/// <summary>
/// A name in both languages.
/// </summary>
public sealed class ShownName
{
    #region Properties

    /// <summary>
    /// Gets the English name.
    /// </summary>
    public required string English { get; init; }

    /// <summary>
    /// Gets the German name.
    /// </summary>
    public required string German { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Gets the name in a language.
    /// </summary>
    /// <param name="language">The language</param>
    /// <returns>The name</returns>
#pragma warning disable S1172, S2325, RH2003 // Skeleton: removed with the implementation
    public string For(UiLanguage language)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, S2325, RH2003

    #endregion // Methods
}