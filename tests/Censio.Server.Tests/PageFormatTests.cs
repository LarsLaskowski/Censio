namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="PageFormat" />.
/// </summary>
[TestClass]
public sealed class PageFormatTests
{
    #region Constants

    /// <summary>
    /// The text of a value the tick holds as no value (spec E6).
    /// </summary>
    private const string Dash = "–";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The instant the time tests format: 2026-11-13 12:34:56 UTC.
    /// </summary>
    private static readonly DateTimeOffset _instant = new(2026, 11, 13, 12, 34, 56, TimeSpan.Zero);

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Rates show two decimals with grouped thousands, the separators of the language, and a dash for no value.
    /// </summary>
    /// <param name="value">The rate, or <see langword="null" /></param>
    /// <param name="language">The language</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(1234.5f, UiLanguage.English, "1,234.50")]
    [DataRow(1234.5f, UiLanguage.German, "1.234,50")]
    [DataRow(-1234567.25f, UiLanguage.English, "-1,234,567.25")]
    [DataRow(-1234567.25f, UiLanguage.German, "-1.234.567,25")]
    [DataRow(0f, UiLanguage.English, "0.00")]
    [DataRow(0.5f, UiLanguage.German, "0,50")]
    [DataRow(null, UiLanguage.English, Dash)]
    [DataRow(null, UiLanguage.German, Dash)]
    public void PageFormatFormatsRatesPerLanguage(float? value, UiLanguage language, string expected)
    {
        // Act
        var text = PageFormat.Rate(value, language);

        // Assert
        Assert.AreEqual(expected, text, "The rate should be formatted with two decimals and the language's separators");
    }

    /// <summary>
    /// Deltas show two decimals with their sign, following the value rounded to two decimals: a value that rounds to zero,
    /// negative zero included, shows <c>0.00</c> without a sign.
    /// </summary>
    /// <param name="value">The delta, or <see langword="null" /></param>
    /// <param name="language">The language</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(1.25f, UiLanguage.English, "+1.25")]
    [DataRow(-0.5f, UiLanguage.English, "-0.50")]
    [DataRow(0f, UiLanguage.English, "0.00")]
    [DataRow(-0f, UiLanguage.English, "0.00")]
    [DataRow(-0.004f, UiLanguage.English, "0.00")]
    [DataRow(0.004f, UiLanguage.English, "0.00")]
    [DataRow(-0.006f, UiLanguage.English, "-0.01")]
    [DataRow(1234.5f, UiLanguage.German, "+1.234,50")]
    [DataRow(-0.5f, UiLanguage.German, "-0,50")]
    [DataRow(-0.004f, UiLanguage.German, "0,00")]
    [DataRow(null, UiLanguage.English, Dash)]
    public void PageFormatFormatsDeltaWithSign(float? value, UiLanguage language, string expected)
    {
        // Act
        var text = PageFormat.Delta(value, language);

        // Assert
        Assert.AreEqual(expected, text, "The delta should carry its sign after rounding to two decimals");
    }

    /// <summary>
    /// Productivity is stored as a percent and shown rounded to a whole percent, not multiplied, with the language's
    /// spacing.
    /// </summary>
    /// <param name="value">The productivity, or <see langword="null" /></param>
    /// <param name="language">The language</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC4")]
    [DataRow(87.4f, UiLanguage.English, "87%")]
    [DataRow(87.4f, UiLanguage.German, "87 %")]
    [DataRow(435f, UiLanguage.English, "435%")]
    [DataRow(435f, UiLanguage.German, "435 %")]
    [DataRow(0f, UiLanguage.English, "0%")]
    [DataRow(null, UiLanguage.German, Dash)]
    public void PageFormatFormatsPercentAsStored(float? value, UiLanguage language, string expected)
    {
        // Act
        var text = PageFormat.Percent(value, language);

        // Assert
        Assert.AreEqual(expected, text, "The productivity should be shown as the stored percent, rounded to a whole number");
    }

    /// <summary>
    /// A count shows its number, and no value a dash.
    /// </summary>
    /// <param name="value">The count, or <see langword="null" /></param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC2")]
    [DataRow(3, "3")]
    [DataRow(0, "0")]
    [DataRow(null, Dash)]
    public void PageFormatFormatsCountOrDash(int? value, string expected)
    {
        // Act
        var text = PageFormat.Count(value);

        // Assert
        Assert.AreEqual(expected, text, "A count should show its number, no value a dash");
    }

    /// <summary>
    /// A time is shown as <c>yyyy-MM-dd HH:mm</c> at the browser's offset, up to 14 hours either way.
    /// </summary>
    /// <param name="offsetMinutes">The offset in minutes</param>
    /// <param name="expected">The expected text</param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(0, "2026-11-13 12:34")]
    [DataRow(60, "2026-11-13 13:34")]
    [DataRow(-330, "2026-11-13 07:04")]
    [DataRow(840, "2026-11-14 02:34")]
    [DataRow(-840, "2026-11-12 22:34")]
    public void PageFormatTimeShowsInstantAtOffset(int offsetMinutes, string expected)
    {
        // Act
        var text = PageFormat.Time(_instant, offsetMinutes);

        // Assert
        Assert.AreEqual(expected, text, "The time should be shown at the browser's offset");
    }

    /// <summary>
    /// Without an offset, or with one beyond 14 hours either way, the time is shown in UTC with the suffix <c> UTC</c>.
    /// </summary>
    /// <param name="offsetMinutes">The offset in minutes, or <see langword="null" /></param>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(null)]
    [DataRow(841)]
    [DataRow(-841)]
    [DataRow(int.MaxValue)]
    [DataRow(int.MinValue)]
    public void PageFormatTimeWithoutUsableOffsetShowsUtc(int? offsetMinutes)
    {
        // Act
        var text = PageFormat.Time(_instant, offsetMinutes);

        // Assert
        Assert.AreEqual("2026-11-13 12:34 UTC", text, "Without a usable offset the time should be shown in UTC, marked as such");
    }

    #endregion // Methods
}