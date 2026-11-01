using System.Globalization;

namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeReplaySpeed" />.
/// </summary>
[TestClass]
public sealed class PipeReplaySpeedTests
{
    #region Methods

    /// <summary>
    /// The named speeds are real speed (factor 1) and maximum speed (no factor).
    /// </summary>
    [TestMethod]
    public void PipeReplaySpeedNamedSpeedsHaveTheirFactors()
    {
        // Arrange

        // Act
        var realTime = PipeReplaySpeed.RealTime;
        var maximum = PipeReplaySpeed.Maximum;

        // Assert
        Assert.AreEqual(1d, realTime.Factor, "Real speed should have the factor 1");
        Assert.IsNull(maximum.Factor, "Maximum speed should have no factor");
    }

    /// <summary>
    /// A valid text gives the speed it names.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="expected">The expected factor, or <see langword="null" /> for maximum speed</param>
    [TestMethod]
    [DataRow("max", null)]
    [DataRow("MAX", null)]
    [DataRow("Max", null)]
    [DataRow("1", 1d)]
    [DataRow("0.1", 0.1d)]
    [DataRow("1000", 1000d)]
    [DataRow("10", 10d)]
    [DataRow("1.5", 1.5d)]
    [DataRow("0.50", 0.5d)]
    [DataRow("1000.0", 1000d)]
    public void PipeReplaySpeedTryParseAcceptsValidValues(string text, double? expected)
    {
        // Arrange

        // Act
        var parsed = PipeReplaySpeed.TryParse(text, out var speed);

        // Assert
        Assert.IsTrue(parsed, "The text should be a valid speed");
        Assert.AreEqual(expected, speed.Factor, "The speed should have the factor the text names");
    }

    /// <summary>
    /// An invalid text is refused.
    /// </summary>
    /// <param name="text">The text</param>
    [TestMethod]
    [DataRow("0.09")]
    [DataRow("0.0999999")]
    [DataRow("1000.1")]
    [DataRow("1000.0001")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("+1")]
    [DataRow("1,5")]
    [DataRow("1.000,5")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("1e2")]
    [DataRow("1.0.0")]
    [DataRow("fast")]
    [DataRow("maximum")]
    [DataRow("")]
    [DataRow(null)]
    public void PipeReplaySpeedTryParseRejectsInvalidValues(string? text)
    {
        // Arrange

        // Act
        var parsed = PipeReplaySpeed.TryParse(text, out _);

        // Assert
        Assert.IsFalse(parsed, "The text should not be a valid speed");
    }

    /// <summary>
    /// A number written with a point is read the same under every current culture.
    /// </summary>
    /// <param name="cultureName">The name of the current culture</param>
    [TestMethod]
    [DataRow("de-DE")]
    [DataRow("fr-FR")]
    [DataRow("th-TH")]
    public void PipeReplaySpeedTryParseAcceptsPointUnderOtherCulture(string cultureName)
    {
        // Arrange

        // Act
        var (parsed, factor) = ParseUnderCulture(cultureName, "1.5");

        // Assert
        Assert.IsTrue(parsed, "A number written with a point should be valid under every culture");
        Assert.AreEqual(1.5d, factor, "The number should be read with the point as the decimal separator");
    }

    /// <summary>
    /// A number written with a comma is refused under every current culture.
    /// </summary>
    /// <param name="cultureName">The name of the current culture</param>
    [TestMethod]
    [DataRow("de-DE")]
    [DataRow("fr-FR")]
    [DataRow("th-TH")]
    public void PipeReplaySpeedTryParseRejectsCommaUnderOtherCulture(string cultureName)
    {
        // Arrange

        // Act
        var (parsed, _) = ParseUnderCulture(cultureName, "1,5");

        // Assert
        Assert.IsFalse(parsed, "A number written with a comma should be refused under every culture");
    }

    /// <summary>
    /// A pause is divided by the factor; maximum speed has no pause.
    /// </summary>
    /// <param name="text">The speed</param>
    /// <param name="pauseSeconds">The recorded pause in seconds</param>
    /// <param name="expectedMilliseconds">The expected time to wait in milliseconds</param>
    [TestMethod]
    [DataRow("1", 10, 10000)]
    [DataRow("10", 10, 1000)]
    [DataRow("1000", 10, 10)]
    [DataRow("0.5", 10, 20000)]
    [DataRow("2", 0, 0)]
    [DataRow("max", 10, 0)]
    [DataRow("max", 0, 0)]
    public void PipeReplaySpeedScaleDividesPause(string text, int pauseSeconds, long expectedMilliseconds)
    {
        // Arrange
        Assert.IsTrue(PipeReplaySpeed.TryParse(text, out var speed), "The speed should be valid");

        // Act
        var scaled = speed.Scale(TimeSpan.FromSeconds(pauseSeconds));

        // Assert
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), scaled, "The pause should be divided by the factor");
    }

    /// <summary>
    /// The slowest speed multiplies the pause by ten, within one tick of the exact value.
    /// </summary>
    [TestMethod]
    public void PipeReplaySpeedScaleAtSlowestSpeedMultipliesByTen()
    {
        // Arrange
        Assert.IsTrue(PipeReplaySpeed.TryParse("0.1", out var speed), "The speed should be valid");

        // Act
        var scaled = speed.Scale(TimeSpan.FromSeconds(30));

        // Assert
        Assert.IsLessThanOrEqualTo(1L, Math.Abs(scaled.Ticks - TimeSpan.FromSeconds(300).Ticks), "The pause should be ten times as long, apart from rounding");
    }

    /// <summary>
    /// A pause too long to scale gives the largest time span instead of an exception.
    /// </summary>
    /// <param name="pauseTicks">The recorded pause in ticks</param>
    [TestMethod]
    [DataRow(long.MaxValue)]
    [DataRow(long.MaxValue / 5)]
    [DataRow(long.MaxValue / 10 + 1)]
    public void PipeReplaySpeedScaleSaturatesAtMaximum(long pauseTicks)
    {
        // Arrange
        Assert.IsTrue(PipeReplaySpeed.TryParse("0.1", out var speed), "The speed should be valid");

        // Act
        var scaled = speed.Scale(TimeSpan.FromTicks(pauseTicks));

        // Assert
        Assert.AreEqual(TimeSpan.MaxValue, scaled, "A pause over the largest time span should saturate");
    }

    /// <summary>
    /// A full-range pause at real speed stays the full range.
    /// </summary>
    [TestMethod]
    public void PipeReplaySpeedScaleAtRealSpeedKeepsFullRangePause()
    {
        // Arrange

        // Act
        var scaled = PipeReplaySpeed.RealTime.Scale(TimeSpan.MaxValue);

        // Assert
        Assert.AreEqual(TimeSpan.MaxValue, scaled, "A pause at real speed should not change");
    }

    /// <summary>
    /// Parses a text with the current culture set to the given one, and restores the culture.
    /// </summary>
    /// <param name="cultureName">The name of the culture</param>
    /// <param name="text">The text</param>
    /// <returns>Whether the text is valid, and the factor of the speed</returns>
    private static (bool Parsed, double? Factor) ParseUnderCulture(string cultureName, string text)
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            var parsed = PipeReplaySpeed.TryParse(text, out var speed);

            return (parsed, speed.Factor);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    #endregion // Methods
}