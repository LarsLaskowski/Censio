namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeAnonymiserRandom" />.
/// </summary>
[TestClass]
public sealed class PipeAnonymiserRandomTests
{
    #region Constants

    /// <summary>
    /// The number of draws of the test.
    /// </summary>
    private const int DrawCount = 10000;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Every draw lies in <c>[0, 1)</c>, and the draws differ.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void PipeAnonymiserRandomNextDoubleStaysInUnitRange()
    {
        // Arrange
        var random = new PipeAnonymiserRandom();

        // Act
        var draws = Enumerable.Range(0, DrawCount).Select(_ => random.NextDouble()).ToList();

        // Assert
        Assert.IsGreaterThanOrEqualTo(0.0, draws.Min(), "No draw should be below 0");
        Assert.IsLessThan(1.0, draws.Max(), "No draw should reach 1");
        Assert.IsGreaterThan(DrawCount / 2, draws.Distinct().Count(), "The draws should differ");
        Assert.IsLessThan(0.1, draws.Min(), "The draws should reach the low end of the range");
        Assert.IsGreaterThan(0.9, draws.Max(), "The draws should reach the high end of the range");
    }

    #endregion // Methods
}