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
    public string For(UiLanguage language)
    {
        return language == UiLanguage.German
                   ? German
                   : English;
    }

    #endregion // Methods
}