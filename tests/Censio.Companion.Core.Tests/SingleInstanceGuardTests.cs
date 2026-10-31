namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="SingleInstanceGuard" />.
/// </summary>
[TestClass]
public sealed class SingleInstanceGuardTests
{
    #region Methods

    /// <summary>
    /// The first guard on a name acquires it.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardFirstTryAcquireReturnsTrue()
    {
        // Arrange
        using var guard = new SingleInstanceGuard(CreateName());

        // Act
        var acquired = guard.TryAcquire();

        // Assert
        Assert.IsTrue(acquired, "The first guard on a name should acquire it");
    }

    /// <summary>
    /// A second guard on the same name does not acquire it.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardSecondGuardWithSameNameReturnsFalse()
    {
        // Arrange
        var name = CreateName();
        using var first = new SingleInstanceGuard(name);
        using var second = new SingleInstanceGuard(name);
        var firstAcquired = first.TryAcquire();

        // Act
        var secondAcquired = second.TryAcquire();

        // Assert
        Assert.IsTrue(firstAcquired, "The first guard should acquire the name");
        Assert.IsFalse(secondAcquired, "The second guard on the same name should not acquire it");
    }

    /// <summary>
    /// After every guard on a name is disposed, the name can be acquired again.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardAfterDisposeNameCanBeAcquiredAgain()
    {
        // Arrange
        var name = CreateName();
        var first = new SingleInstanceGuard(name);
        var second = new SingleInstanceGuard(name);
        var firstAcquired = first.TryAcquire();
        var secondAcquired = second.TryAcquire();

        second.Dispose();
        first.Dispose();

        using var third = new SingleInstanceGuard(name);

        // Act
        var thirdAcquired = third.TryAcquire();

        // Assert
        Assert.IsTrue(firstAcquired, "The first guard should acquire the name");
        Assert.IsFalse(secondAcquired, "The second guard should not acquire the name while the first holds it");
        Assert.IsTrue(thirdAcquired, "The name should be free again once every earlier guard is disposed");
    }

    /// <summary>
    /// Disposing a guard that did not acquire the name does not release the owner.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardDisposeOfNonOwnerKeepsNameHeld()
    {
        // Arrange
        var name = CreateName();
        using var first = new SingleInstanceGuard(name);
        var firstAcquired = first.TryAcquire();
        var second = new SingleInstanceGuard(name);
        var secondAcquired = second.TryAcquire();

        // Act
        second.Dispose();

        using var third = new SingleInstanceGuard(name);
        var thirdAcquired = third.TryAcquire();

        // Assert
        Assert.IsTrue(firstAcquired, "The first guard should acquire the name");
        Assert.IsFalse(secondAcquired, "The second guard should not acquire the name");
        Assert.IsFalse(thirdAcquired, "The name should stay held by the first guard after the second one is disposed");
    }

    /// <summary>
    /// The default name is session-local.
    /// </summary>
    [TestMethod]
    public void SingleInstanceGuardDefaultNameIsSessionLocal()
    {
        // Arrange
        const string expectedPrefix = "Local\\";

        // Act
        var name = SingleInstanceGuard.DefaultName;

        // Assert
        Assert.Contains("\\", name, "The default name should contain the backslash character");
        Assert.StartsWith(expectedPrefix, name, StringComparison.Ordinal, "The default name should start with Local followed by a backslash");
    }

    /// <summary>
    /// Creates a mutex name that no other test or run uses.
    /// </summary>
    /// <returns>A unique session-local mutex name</returns>
    private static string CreateName()
    {
        return "Local\\Censio.Tests." + Guid.NewGuid().ToString("N");
    }

    #endregion // Methods
}