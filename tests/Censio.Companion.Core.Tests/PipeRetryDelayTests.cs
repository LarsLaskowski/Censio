namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeRetryDelay" />.
/// </summary>
[TestClass]
public sealed class PipeRetryDelayTests
{
    #region Methods

    /// <summary>
    /// The delay doubles from one second and stops at ten seconds.
    /// </summary>
    /// <param name="failures">The number of consecutive failures</param>
    /// <param name="expectedSeconds">The expected delay in seconds</param>
    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(2, 2)]
    [DataRow(3, 4)]
    [DataRow(4, 8)]
    [DataRow(5, 10)]
    [DataRow(50, 10)]
    [DataRow(int.MaxValue, 10)]
    public void PipeRetryDelayForReturnsDoublingUpToTenSeconds(int failures, int expectedSeconds)
    {
        // Arrange
        var expected = TimeSpan.FromSeconds(expectedSeconds);

        // Act
        var delay = PipeRetryDelay.For(failures);

        // Assert
        Assert.AreEqual(expected, delay, "The delay should double up to ten seconds");
    }

    /// <summary>
    /// Fewer than one failure is not a valid input.
    /// </summary>
    /// <param name="failures">The number of consecutive failures</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void PipeRetryDelayForBelowOneThrows(int failures)
    {
        // Arrange
        Action call = () => PipeRetryDelay.For(failures);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(call, "A count below one should be rejected");

        // Assert
        Assert.AreEqual("consecutiveFailures", exception.ParamName, "The exception should name the parameter");
    }

    /// <summary>
    /// The maximum delay and the time of a successful connection are fixed.
    /// </summary>
    [TestMethod]
    public void PipeRetryDelayLimitsAreTenAndThirtySeconds()
    {
        // Arrange
        var expectedMax = TimeSpan.FromSeconds(10);
        var expectedSuccess = TimeSpan.FromSeconds(30);

        // Act
        var max = PipeRetryDelay.Max;
        var success = PipeRetryDelay.SuccessfulConnectionTime;

        // Assert
        Assert.AreEqual(expectedMax, max, "The maximum delay should be ten seconds");
        Assert.AreEqual(expectedSuccess, success, "A connection should count as successful after thirty seconds");
    }

    #endregion // Methods
}