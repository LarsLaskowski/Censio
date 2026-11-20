namespace Censio.Pipe;

/// <summary>
/// Holds the constants of the statistics pipe protocol.
/// </summary>
public static class PipeProtocol
{
    #region Constants

    /// <summary>
    /// The oldest protocol version this reader decodes.
    /// </summary>
    public const int OldestSupportedVersion = 2;

    /// <summary>
    /// The newest protocol version this reader decodes.
    /// </summary>
    public const int NewestSupportedVersion = 3;

    /// <summary>
    /// The largest accepted frame length in bytes (1 MiB), counting the type byte and the payload.
    /// </summary>
    public const int MaxFrameLength = 1048576;

    /// <summary>
    /// The largest accepted number of pairs in one workforce or building map.
    /// </summary>
    public const int MaxMapPairs = 1024;

    /// <summary>
    /// The largest accepted number of routes in one trade-routes frame.
    /// </summary>
    public const int MaxTradeRoutes = 1024;

    /// <summary>
    /// The largest accepted number of stations in one trade route.
    /// </summary>
    public const int MaxRouteStations = 256;

    /// <summary>
    /// The largest accepted number of goods in one trade route station.
    /// </summary>
    public const int MaxStationGoods = 256;

    /// <summary>
    /// The largest accepted number of offers in one item-offers frame.
    /// </summary>
    public const int MaxItemOffers = 1024;

    /// <summary>
    /// The largest accepted number of items in one item offer.
    /// </summary>
    public const int MaxOfferItems = 1024;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Determines whether this reader decodes the given protocol version.
    /// </summary>
    /// <param name="version">The announced protocol version</param>
    /// <returns><see langword="true"/> when the version lies within the supported range</returns>
    public static bool IsSupported(int version)
    {
        return version is >= OldestSupportedVersion and <= NewestSupportedVersion;
    }

    #endregion // Methods
}