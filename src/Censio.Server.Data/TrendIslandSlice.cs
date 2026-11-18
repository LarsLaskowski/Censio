namespace Censio.Server.Data;

/// <summary>
/// The block of points and islands that one part of an islands trend read covers.
/// </summary>
/// <param name="PointStart">The index of the first point of the part.</param>
/// <param name="PointCount">The number of points of the part.</param>
/// <param name="IslandStart">The index of the first island of the part.</param>
/// <param name="IslandCount">The number of islands of the part.</param>
internal readonly record struct TrendIslandSlice(int PointStart, int PointCount, int IslandStart, int IslandCount);