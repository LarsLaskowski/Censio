namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="LiveBufferLimits" />.
/// </summary>
[TestClass]
public sealed class LiveBufferLimitsTests
{
    #region Methods

    /// <summary>
    /// Without settings the limits are 24 hours and 100 megabytes of 1,048,576 bytes, within ranges of 1 to 168 hours and
    /// 1 to 10,240 megabytes (S5, N1).
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void LiveBufferLimitsDefaultsAndConversion()
    {
        // Act
        var limits = LiveBufferLimits.Defaults;

        // Assert
        Assert.AreEqual(24, LiveBufferLimits.DefaultHours, "The default age limit should be 24 hours");
        Assert.AreEqual(168, LiveBufferLimits.MaxHours, "The largest age limit should be 168 hours");
        Assert.AreEqual(100, LiveBufferLimits.DefaultMegabytes, "The default size limit should be 100 megabytes");
        Assert.AreEqual(10240, LiveBufferLimits.MaxMegabytes, "The largest size limit should be 10,240 megabytes");
        Assert.AreEqual(1048576, LiveBufferLimits.BytesPerMegabyte, "A megabyte should be 1,048,576 bytes");
        Assert.AreEqual(24, limits.Hours, "The default limits should hold 24 hours");
        Assert.AreEqual(100, limits.Megabytes, "The default limits should hold 100 megabytes");
        Assert.AreEqual(TimeSpan.FromHours(24), limits.MaxAge, "The default age limit should be 24 hours");
        Assert.AreEqual(104857600L, limits.MaxBytes, "The default size limit should be 100 times 1,048,576 bytes");
    }

    /// <summary>
    /// The age limit is the hours as a time span and the size limit the megabytes times 1,048,576 bytes, beyond the
    /// range of a 32-bit integer at the largest limit (S5, N1).
    /// </summary>
    /// <param name="hours">The age limit in hours</param>
    /// <param name="megabytes">The size limit in megabytes</param>
    /// <param name="expectedBytes">The expected size limit in bytes</param>
    [TestMethod]
    [TestCategory("AC6")]
    [DataRow(1, 1, 1048576L, DisplayName = "Smallest limits")]
    [DataRow(48, 500, 524288000L, DisplayName = "48 hours and 500 megabytes")]
    [DataRow(168, 10240, 10737418240L, DisplayName = "Largest limits")]
    public void LiveBufferLimitsMaxAgeAndMaxBytesFollowHoursAndMegabytes(int hours, int megabytes, long expectedBytes)
    {
        // Arrange
        var limits = new LiveBufferLimits
                     {
                         Hours = hours,
                         Megabytes = megabytes
                     };

        // Act
        var maxAge = limits.MaxAge;
        var maxBytes = limits.MaxBytes;

        // Assert
        Assert.AreEqual(TimeSpan.FromHours(hours), maxAge, "The age limit should be the hours as a time span");
        Assert.AreEqual(expectedBytes, maxBytes, "The size limit should be the megabytes times 1,048,576 bytes");
    }

    #endregion // Methods
}