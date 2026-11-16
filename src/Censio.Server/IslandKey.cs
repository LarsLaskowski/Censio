namespace Censio.Server;

/// <summary>
/// The key of an island: its session, island and area.
/// </summary>
/// <param name="SessionGuid">The session GUID</param>
/// <param name="IslandId">The island identity</param>
/// <param name="AreaIndex">The area index</param>
public readonly record struct IslandKey(int SessionGuid, byte IslandId, byte AreaIndex) : IComparable<IslandKey>
{
    #region Operators

#pragma warning disable IDE0024 // The formatter writes block bodies, which this rule reports for operators
    /// <summary>
    /// Tells whether a key is ordered before another.
    /// </summary>
    /// <param name="left">The first key</param>
    /// <param name="right">The second key</param>
    /// <returns><see langword="true"/> when the first key is ordered before the second</returns>
    public static bool operator <(IslandKey left, IslandKey right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Tells whether a key is ordered before another or equal to it.
    /// </summary>
    /// <param name="left">The first key</param>
    /// <param name="right">The second key</param>
    /// <returns><see langword="true"/> when the first key is not ordered after the second</returns>
    public static bool operator <=(IslandKey left, IslandKey right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Tells whether a key is ordered after another.
    /// </summary>
    /// <param name="left">The first key</param>
    /// <param name="right">The second key</param>
    /// <returns><see langword="true"/> when the first key is ordered after the second</returns>
    public static bool operator >(IslandKey left, IslandKey right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Tells whether a key is ordered after another or equal to it.
    /// </summary>
    /// <param name="left">The first key</param>
    /// <param name="right">The second key</param>
    /// <returns><see langword="true"/> when the first key is not ordered before the second</returns>
    public static bool operator >=(IslandKey left, IslandKey right)
    {
        return left.CompareTo(right) >= 0;
    }
#pragma warning restore IDE0024

    #endregion // Operators

    #region IComparable

    /// <inheritdoc/>
    public int CompareTo(IslandKey other)
    {
        var bySession = SessionGuid.CompareTo(other.SessionGuid);

        if (bySession != 0)
        {
            return bySession;
        }

        var byIsland = IslandId.CompareTo(other.IslandId);

        return byIsland != 0 ? byIsland : AreaIndex.CompareTo(other.AreaIndex);
    }

    #endregion // IComparable
}