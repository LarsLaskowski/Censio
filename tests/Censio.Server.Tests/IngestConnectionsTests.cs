namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IngestConnections" />.
/// </summary>
[TestClass]
public sealed class IngestConnectionsTests
{
    #region Methods

    /// <summary>
    /// Sixteen connections may wait for their hello at once; the seventeenth is refused.
    /// </summary>
    [TestMethod]
    [TestCategory("AC11")]
    public void IngestConnectionsTryEnterPendingHelloSeventeenthReturnsFalse()
    {
        // Arrange
        var connections = new IngestConnections();
        var entered = EnterPendingHellos(connections, IngestConnections.MaxPendingHellos);

        // Act
        var seventeenth = connections.TryEnterPendingHello();

        // Assert
        Assert.AreEqual(16, IngestConnections.MaxPendingHellos, "The bound should be 16 pending hellos");
        Assert.AreEqual(16, entered, "Sixteen pending hellos should be counted");
        Assert.IsFalse(seventeenth, "The seventeenth pending hello should be refused");
    }

    /// <summary>
    /// A connection that leaves its pending place frees it for the next one.
    /// </summary>
    [TestMethod]
    [TestCategory("AC11")]
    public void IngestConnectionsLeavePendingHelloFreesPlace()
    {
        // Arrange
        var connections = new IngestConnections();

        EnterPendingHellos(connections, IngestConnections.MaxPendingHellos);
        connections.LeavePendingHello();

        // Act
        var entered = connections.TryEnterPendingHello();

        // Assert
        Assert.IsTrue(entered, "The freed place should be taken by the next connection");
        Assert.IsFalse(connections.TryEnterPendingHello(), "No further place should be free");
    }

    /// <summary>
    /// While the one Companion's place is taken, a second Companion cannot take it.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void IngestConnectionsTryTakeCompanionWhenTakenReturnsFalse()
    {
        // Arrange
        var connections = new IngestConnections();
        var first = connections.TryTakeCompanion();

        // Act
        var second = connections.TryTakeCompanion();

        // Assert
        Assert.IsTrue(first, "The first Companion should take the place");
        Assert.IsFalse(second, "A second Companion should be refused while the place is taken");
    }

    /// <summary>
    /// A released Companion place is taken by the next Companion.
    /// </summary>
    [TestMethod]
    [TestCategory("AC6")]
    public void IngestConnectionsReleaseCompanionFreesPlace()
    {
        // Arrange
        var connections = new IngestConnections();

        connections.TryTakeCompanion();
        connections.ReleaseCompanion();

        // Act
        var next = connections.TryTakeCompanion();

        // Assert
        Assert.IsTrue(next, "The next Companion should take the released place");
    }

    /// <summary>
    /// Enters pending hellos until one is refused or the count is reached.
    /// </summary>
    /// <param name="connections">The connections</param>
    /// <param name="count">The number of pending hellos to enter</param>
    /// <returns>The number entered</returns>
    private static int EnterPendingHellos(IngestConnections connections, int count)
    {
        var entered = 0;

        while (entered < count && connections.TryEnterPendingHello())
        {
            entered++;
        }

        return entered;
    }

    #endregion // Methods
}