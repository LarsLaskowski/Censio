namespace Censio.Pipe;

/// <summary>
/// The message types of the statistics pipe protocol, identified by the first byte of a frame.
/// </summary>
public enum PipeMessageType : byte
{
    /// <summary>
    /// The protocol version announcement.
    /// </summary>
    Version = 0,

    /// <summary>
    /// The start of a game session.
    /// </summary>
    SessionStart = 1,

    /// <summary>
    /// The end of a game session.
    /// </summary>
    SessionEnd = 2,

    /// <summary>
    /// The production statistics of one area.
    /// </summary>
    AreaProductionStatistics = 3,

    /// <summary>
    /// The trade routes of a session, announced from protocol version 3.
    /// </summary>
    TradeRoutes = 4,

    /// <summary>
    /// The item offers of a session, announced from protocol version 3.
    /// </summary>
    ItemOffers = 5
}