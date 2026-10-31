namespace Censio.Contracts.Tests;

/// <summary>
/// Temporary probe that must turn the build red.
/// </summary>
[TestClass]
public sealed class ProbeFailingTests
{
    #region Methods

    /// <summary>
    /// Always fails.
    /// </summary>
    [TestMethod]
    public void ProbeFailsOnPurpose()
    {
        Assert.Fail("Deliberate failure to prove that a failing test turns CI red.");
    }

    #endregion // Methods
}