using Censio.Pipe;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="PipeTickSummary" />.
/// </summary>
[TestClass]
public sealed class PipeTickSummaryTests
{
    #region Methods

    /// <summary>
    /// The summary of a tick holds its receive time and the number of its islands.
    /// </summary>
    /// <param name="islandCount">The number of islands of the tick</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    public void PipeTickSummaryFromTickCountsIslands(int islandCount)
    {
        // Arrange
        var receiveTime = new DateTimeOffset(2026, 11, 2, 18, 26, 3, 45, TimeSpan.Zero);
        var tick = new PipeTick
                   {
                       GameTime = 123456,
                       ReceiveTime = receiveTime,
                       Islands = [.. Enumerable.Range(1, islandCount).Select(Island)],
                       CloseReason = PipeTickCloseReason.GameTimeChanged
                   };

        // Act
        var summary = PipeTickSummary.From(tick);

        // Assert
        Assert.AreEqual(receiveTime, summary.ReceiveTime, "The summary should hold the tick's receive time");
        Assert.AreEqual(islandCount, summary.IslandCount, "The summary should count the tick's islands");
    }

    /// <summary>
    /// Creates an island without entries.
    /// </summary>
    /// <param name="sessionGuid">The session GUID of the island</param>
    /// <returns>The island</returns>
    private static PipeTickIsland Island(int sessionGuid)
    {
        return new PipeTickIsland
               {
                   Key = new PipeIslandKey(sessionGuid, 1, 0),
                   SessionId = 7,
                   Name = "Ostia",
                   Entries = []
               };
    }

    #endregion // Methods
}