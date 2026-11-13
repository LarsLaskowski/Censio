namespace Censio.Server;

/// <summary>
/// Reads and writes the web UI's preference values of the browser's cookies and language header.
/// </summary>
public static class UiPreferences
{
    #region Constants

    /// <summary>
    /// The name of the cookie that holds the theme.
    /// </summary>
    public const string ThemeCookieName = "censio-theme";

    /// <summary>
    /// The name of the cookie that holds the language.
    /// </summary>
    public const string LanguageCookieName = "censio-language";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the theme of a stored cookie value.
    /// </summary>
    /// <param name="storedValue">The stored value, or <c>null</c> when the cookie is absent.</param>
    /// <returns>The dark theme for the value <c>dark</c>; otherwise, the light theme.</returns>
    public static UiTheme ReadTheme(string? storedValue)
    {
#pragma warning disable RH2003
        // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <summary>
    /// Reads the language of a stored cookie value or, without a known one, of the language header.
    /// </summary>
    /// <param name="storedValue">The stored value, or <c>null</c> when the cookie is absent.</param>
    /// <param name="acceptLanguage">The value of the <c>Accept-Language</c> header, or <c>null</c>.</param>
    /// <returns>The language to show; English when none is known.</returns>
    public static UiLanguage ReadLanguage(string? storedValue, string? acceptLanguage)
    {
#pragma warning disable RH2003
        // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <summary>
    /// Gets the value stored for a theme.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The stored value.</returns>
    public static string StoredValue(UiTheme theme)
    {
#pragma warning disable RH2003
        // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <summary>
    /// Gets the value stored for a language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The stored value.</returns>
    public static string StoredValue(UiLanguage language)
    {
#pragma warning disable RH2003
        // Skeleton: removed with the implementation
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    #endregion // Methods
}