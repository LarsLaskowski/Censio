namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="UiPreferences" />.
/// </summary>
[TestClass]
public sealed class UiPreferencesTests
{
    #region Constants

    /// <summary>
    /// A language list that ranks German first, so a result that follows it is German, not the English default.
    /// </summary>
    private const string GermanLanguageList = "de";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the stored values that are neither a known theme nor a known language.
    /// </summary>
    private static IEnumerable<object[]> UnknownStoredValues => [
                                                                    ["DARK"],
                                                                    ["fr"],
                                                                    ["De"],
                                                                    [" dark"],
                                                                    ["dark;x"],
                                                                    [new string('a', 4000)],
                                                                    ["<script>"],
                                                                    ["\ndark"],
                                                                    [string.Empty]
                                                                ];

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Without a stored value the language is German exactly when a German variant ranks above English in the language
    /// list, by weight and then by position, weight 0 excluded; otherwise it is English.
    /// </summary>
    /// <param name="acceptLanguage">The value of the language header</param>
    /// <param name="expected">The expected language</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow("de-DE,en;q=0.8", UiLanguage.German)]
    [DataRow("en-US,de", UiLanguage.English)]
    [DataRow("fr,de", UiLanguage.German)]
    [DataRow("fr", UiLanguage.English)]
    [DataRow("", UiLanguage.English)]
    [DataRow(null, UiLanguage.English)]
    [DataRow("en;q=0.5, de", UiLanguage.German)]
    [DataRow("de;q=0, en", UiLanguage.English)]
    [DataRow("de;q=0", UiLanguage.English)]
    [DataRow("*", UiLanguage.English)]
    [DataRow("DE-at", UiLanguage.German)]
    [DataRow("de;q=0.5, en;q=0.5", UiLanguage.German)]
    [DataRow("de;q=abc, en", UiLanguage.English)]
    [DataRow(";", UiLanguage.English)]
    public void UiPreferencesReadLanguageWithoutStoredValueFollowsLanguageList(string? acceptLanguage, UiLanguage expected)
    {
        // Act
        var language = UiPreferences.ReadLanguage(null, acceptLanguage);

        // Assert
        Assert.AreEqual(expected, language, $"The language list '{acceptLanguage}' should give {expected}");
    }

    /// <summary>
    /// A stored theme value that is not the exact value <c>dark</c> gives the light theme.
    /// </summary>
    /// <param name="storedValue">The stored value</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DynamicData(nameof(UnknownStoredValues))]
    public void UiPreferencesReadThemeWithUnknownValueReturnsLight(string storedValue)
    {
        // Act
        var theme = UiPreferences.ReadTheme(storedValue);

        // Assert
        Assert.AreEqual(UiTheme.Light, theme, "An unknown stored theme should give the light theme");
    }

    /// <summary>
    /// A stored language value that is not a known language is ignored, and the language list decides.
    /// </summary>
    /// <param name="storedValue">The stored value</param>
    [TestMethod]
    [TestCategory("AC9")]
    [DynamicData(nameof(UnknownStoredValues))]
    public void UiPreferencesReadLanguageWithUnknownStoredValueFollowsLanguageList(string storedValue)
    {
        // Act
        var language = UiPreferences.ReadLanguage(storedValue, GermanLanguageList);

        // Assert
        Assert.AreEqual(UiLanguage.German, language, "An unknown stored language should leave the choice to the language list");
    }

    #endregion // Methods
}