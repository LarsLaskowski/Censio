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

    /// <summary>
    /// Gets the stored values that are no known range and no known axis: other case, an encoded or control character,
    /// an overlong text, the empty text and none.
    /// </summary>
    private static IEnumerable<object?[]> UnknownChartValues => [
                                                                    ["Week"],
                                                                    ["%3Cb%3E"],
                                                                    ["week\u0001"],
                                                                    [new string('a', 2000)],
                                                                    [string.Empty],
                                                                    [null]
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

    /// <summary>
    /// The known stored values give their range and axis.
    /// </summary>
    /// <param name="storedRange">The stored range</param>
    /// <param name="storedAxis">The stored axis</param>
    /// <param name="expectedRange">The expected range</param>
    /// <param name="expectedAxis">The expected axis</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("hour", "real", ChartRange.LastHour, ChartAxis.RealTime)]
    [DataRow("day", "game", ChartRange.LastDay, ChartAxis.GameTime)]
    [DataRow("week", "real", ChartRange.LastWeek, ChartAxis.RealTime)]
    [DataRow("all", "game", ChartRange.All, ChartAxis.GameTime)]
    public void UiPreferencesReadChartChoiceKnownValues(string storedRange, string storedAxis, ChartRange expectedRange, ChartAxis expectedAxis)
    {
        // Act
        var choice = UiPreferences.ReadChartChoice(storedRange, storedAxis);

        // Assert
        Assert.AreEqual(expectedRange, choice.Range, $"The stored range {storedRange} should be read");
        Assert.AreEqual(expectedAxis, choice.Axis, $"The stored axis {storedAxis} should be read");
    }

    /// <summary>
    /// A stored value that is no exact known value gives the default range and axis.
    /// </summary>
    /// <param name="storedValue">The stored value</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DynamicData(nameof(UnknownChartValues))]
    public void UiPreferencesReadChartChoiceUnknownValueGivesDefault(string? storedValue)
    {
        // Act
        var choice = UiPreferences.ReadChartChoice(storedValue, storedValue);

        // Assert
        Assert.AreEqual(ChartChoice.Default, choice, "An absent or unknown stored value should give the default choice");
    }

    /// <summary>
    /// An unknown or absent value of one cookie gives the default of its own part and leaves the other part as stored.
    /// </summary>
    /// <param name="storedRange">The stored range</param>
    /// <param name="storedAxis">The stored axis</param>
    /// <param name="expectedRange">The expected range</param>
    /// <param name="expectedAxis">The expected axis</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow("Week", "game", ChartRange.LastDay, ChartAxis.GameTime)]
    [DataRow(null, "game", ChartRange.LastDay, ChartAxis.GameTime)]
    [DataRow("week", "Game", ChartRange.LastWeek, ChartAxis.RealTime)]
    [DataRow("all", null, ChartRange.All, ChartAxis.RealTime)]
    public void UiPreferencesReadChartChoiceUnknownPartKeepsOtherPart(string? storedRange, string? storedAxis, ChartRange expectedRange, ChartAxis expectedAxis)
    {
        // Act
        var choice = UiPreferences.ReadChartChoice(storedRange, storedAxis);

        // Assert
        Assert.AreEqual(expectedRange, choice.Range, "The range should be the stored one or the default of its part");
        Assert.AreEqual(expectedAxis, choice.Axis, "The axis should be the stored one or the default of its part");
    }

    /// <summary>
    /// The stored value of a range and of an axis is a fixed text, and reading it gives the choice back.
    /// </summary>
    /// <param name="range">The range</param>
    /// <param name="axis">The axis</param>
    /// <param name="expectedRange">The stored range</param>
    /// <param name="expectedAxis">The stored axis</param>
    [TestMethod]
    [TestCategory("AC5")]
    [DataRow(ChartRange.LastHour, ChartAxis.RealTime, "hour", "real")]
    [DataRow(ChartRange.LastDay, ChartAxis.GameTime, "day", "game")]
    [DataRow(ChartRange.LastWeek, ChartAxis.RealTime, "week", "real")]
    [DataRow(ChartRange.All, ChartAxis.GameTime, "all", "game")]
    public void UiPreferencesStoredValueOfChartChoiceRoundTrips(ChartRange range, ChartAxis axis, string expectedRange, string expectedAxis)
    {
        // Act
        var storedRange = UiPreferences.StoredValue(range);
        var storedAxis = UiPreferences.StoredValue(axis);
        var choice = UiPreferences.ReadChartChoice(storedRange, storedAxis);

        // Assert
        Assert.AreEqual(expectedRange, storedRange, "The range should be stored as its fixed value");
        Assert.AreEqual(expectedAxis, storedAxis, "The axis should be stored as its fixed value");
        Assert.AreEqual(range, choice.Range, "The stored range should read back");
        Assert.AreEqual(axis, choice.Axis, "The stored axis should read back");
    }

    #endregion // Methods
}