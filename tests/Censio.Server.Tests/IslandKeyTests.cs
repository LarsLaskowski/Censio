using System.Globalization;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="IslandKey" />.
/// </summary>
[TestClass]
public sealed class IslandKeyTests
{
    #region Fields

    /// <summary>
    /// Keys in no order.
    /// </summary>
    private static readonly string[] _unordered = ["2/1/0", "1/2/1", "1/2/0", "1/1/9"];

    /// <summary>
    /// The same keys in the order of session, island and area.
    /// </summary>
    private static readonly string[] _ordered = ["1/1/9", "1/2/0", "1/2/1", "2/1/0"];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Keys are ordered by session first, then island, then area, and each comparison operator follows that order.
    /// </summary>
    /// <param name="left">The session, island and area of the first key, joined by <c>/</c></param>
    /// <param name="right">The session, island and area of the second key, joined by <c>/</c></param>
    /// <param name="expected">The results of <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> and <c>&gt;=</c> and the sign of the comparison</param>
    [TestMethod]
    [DataRow("1/5/5", "2/0/0", "True,True,False,False,-1")]
    [DataRow("2/0/0", "1/5/5", "False,False,True,True,1")]
    [DataRow("3/1/9", "3/2/0", "True,True,False,False,-1")]
    [DataRow("3/2/0", "3/1/9", "False,False,True,True,1")]
    [DataRow("3/1/0", "3/1/1", "True,True,False,False,-1")]
    [DataRow("3/1/1", "3/1/0", "False,False,True,True,1")]
    [DataRow("3/1/1", "3/1/1", "False,True,False,True,0")]
    [DataRow("-5/1/1", "3/1/1", "True,True,False,False,-1")]
    public void IslandKeyOperatorsFollowSessionIslandAreaOrder(string left, string right, string expected)
    {
        // Arrange
        var first = Parse(left);
        var second = Parse(right);

        // Act
        var results = $"{first < second},{first <= second},{first > second},{first >= second},{Math.Sign(first.CompareTo(second))}";

        // Assert
        Assert.AreEqual(expected, results, $"The comparisons of {left} with {right} should follow session, island and area");
    }

    /// <summary>
    /// Sorting keys puts them in the order of session, island and area.
    /// </summary>
    [TestMethod]
    public void IslandKeyOrderSortsBySessionIslandArea()
    {
        // Arrange
        var keys = _unordered.Select(Parse).ToList();

        // Act
        var ordered = keys.Order().Select(key => $"{key.SessionGuid}/{key.IslandId}/{key.AreaIndex}").ToList();

        // Assert
        Assert.AreSequenceEqual(_ordered, ordered, "The keys should be sorted by session, island and area");
    }

    /// <summary>
    /// Parses a key written as session, island and area joined by <c>/</c>.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns>The key</returns>
    private static IslandKey Parse(string text)
    {
        var parts = text.Split('/');

        return new IslandKey(int.Parse(parts[0], CultureInfo.InvariantCulture), byte.Parse(parts[1], CultureInfo.InvariantCulture), byte.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    #endregion // Methods
}