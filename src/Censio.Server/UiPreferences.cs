using Microsoft.Net.Http.Headers;

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

    /// <summary>
    /// The stored value of the light theme.
    /// </summary>
    private const string LightValue = "light";

    /// <summary>
    /// The stored value of the dark theme.
    /// </summary>
    private const string DarkValue = "dark";

    /// <summary>
    /// The stored value of English.
    /// </summary>
    private const string EnglishValue = "en";

    /// <summary>
    /// The stored value of German.
    /// </summary>
    private const string GermanValue = "de";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the theme of a stored cookie value.
    /// </summary>
    /// <param name="storedValue">The stored value, or <c>null</c> when the cookie is absent.</param>
    /// <returns>The dark theme for the value <c>dark</c>; otherwise, the light theme.</returns>
    public static UiTheme ReadTheme(string? storedValue)
    {
        return string.Equals(storedValue, DarkValue, StringComparison.Ordinal) ? UiTheme.Dark : UiTheme.Light;
    }

    /// <summary>
    /// Reads the language of a stored cookie value or, without a known one, of the language header.
    /// </summary>
    /// <param name="storedValue">The stored value, or <c>null</c> when the cookie is absent.</param>
    /// <param name="acceptLanguage">The value of the <c>Accept-Language</c> header, or <c>null</c>.</param>
    /// <returns>The language to show; English when none is known.</returns>
    public static UiLanguage ReadLanguage(string? storedValue, string? acceptLanguage)
    {
        if (string.Equals(storedValue, GermanValue, StringComparison.Ordinal))
        {
            return UiLanguage.German;
        }

        if (string.Equals(storedValue, EnglishValue, StringComparison.Ordinal))
        {
            return UiLanguage.English;
        }

        if (string.IsNullOrEmpty(acceptLanguage) || StringWithQualityHeaderValue.TryParseList([acceptLanguage], out var entries) == false)
        {
            return UiLanguage.English;
        }

        var ranked = entries.Select((entry, index) => (Entry: entry, Index: index))
                            .Where(item => (item.Entry.Quality ?? 1.0) > 0.0)
                            .OrderByDescending(item => item.Entry.Quality ?? 1.0)
                            .ThenBy(item => item.Index);

        foreach (var (entry, _) in ranked)
        {
            var tag = entry.Value.AsSpan();
            var separator = tag.IndexOf('-');
            var primary = separator < 0 ? tag : tag[..separator];

            if (primary.Equals(GermanValue, StringComparison.OrdinalIgnoreCase))
            {
                return UiLanguage.German;
            }

            if (primary.Equals(EnglishValue, StringComparison.OrdinalIgnoreCase))
            {
                return UiLanguage.English;
            }
        }

        return UiLanguage.English;
    }

    /// <summary>
    /// Gets the value stored for a theme.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The stored value.</returns>
    public static string StoredValue(UiTheme theme)
    {
        return theme == UiTheme.Dark ? DarkValue : LightValue;
    }

    /// <summary>
    /// Gets the value stored for a language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The stored value.</returns>
    public static string StoredValue(UiLanguage language)
    {
        return language == UiLanguage.German ? GermanValue : EnglishValue;
    }

    #endregion // Methods
}