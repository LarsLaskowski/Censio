namespace Censio.Pipe;

/// <summary>
/// One item the tick assembler delivers.
/// </summary>
public sealed class PipeTickResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeTickResult" /> class.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <param name="tick">The tick</param>
    /// <param name="headline">The headline</param>
    /// <param name="version">The version</param>
    /// <param name="error">The error</param>
    /// <param name="islandKey">The island key</param>
    private PipeTickResult(PipeTickResultKind kind, PipeTick? tick, string? headline, int? version, PipeError? error, PipeIslandKey? islandKey)
    {
        Kind = kind;
        Tick = tick;
        Headline = headline;
        Version = version;
        Error = error;
        IslandKey = islandKey;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the end-of-stream result.
    /// </summary>
    public static PipeTickResult End { get; } = new(PipeTickResultKind.EndOfStream, null, null, null, null, null);

    /// <summary>
    /// Gets the session-end result.
    /// </summary>
    public static PipeTickResult SessionEnd { get; } = new(PipeTickResultKind.SessionEnd, null, null, null, null, null);

    /// <summary>
    /// Gets the repeated-session-end result.
    /// </summary>
    public static PipeTickResult RepeatedSessionEnd { get; } = new(PipeTickResultKind.RepeatedSessionEnd, null, null, null, null, null);

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public PipeTickResultKind Kind { get; }

    /// <summary>
    /// Gets the tick of a <see cref="PipeTickResultKind.Tick" /> result.
    /// </summary>
    public PipeTick? Tick { get; }

    /// <summary>
    /// Gets the headline of a <see cref="PipeTickResultKind.SessionStart" /> result.
    /// </summary>
    public string? Headline { get; }

    /// <summary>
    /// Gets the version of a <see cref="PipeTickResultKind.Version" /> result.
    /// </summary>
    public int? Version { get; }

    /// <summary>
    /// Gets the error of a <see cref="PipeTickResultKind.Error" /> result.
    /// </summary>
    public PipeError? Error { get; }

    /// <summary>
    /// Gets the island key of a <see cref="PipeTickResultKind.TickLimit" /> result.
    /// </summary>
    public PipeIslandKey? IslandKey { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates a result that holds a closed tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <returns>The result</returns>
    public static PipeTickResult FromTick(PipeTick tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        return new PipeTickResult(PipeTickResultKind.Tick, tick, null, null, null, null);
    }

    /// <summary>
    /// Creates a session-start result.
    /// </summary>
    /// <param name="headline">The headline, unchanged</param>
    /// <returns>The result</returns>
    public static PipeTickResult FromSessionStart(string headline)
    {
        ArgumentNullException.ThrowIfNull(headline);

        return new PipeTickResult(PipeTickResultKind.SessionStart, null, headline, null, null, null);
    }

    /// <summary>
    /// Creates a version result.
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns>The result</returns>
    public static PipeTickResult FromVersion(int version)
    {
        return new PipeTickResult(PipeTickResultKind.Version, null, null, version, null, null);
    }

    /// <summary>
    /// Creates an error result.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    public static PipeTickResult FromError(PipeError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new PipeTickResult(PipeTickResultKind.Error, null, null, null, error, null);
    }

    /// <summary>
    /// Creates a tick-limit result.
    /// </summary>
    /// <param name="key">The key of the island frame that exceeded a limit</param>
    /// <returns>The result</returns>
    public static PipeTickResult FromTickLimit(PipeIslandKey key)
    {
        return new PipeTickResult(PipeTickResultKind.TickLimit, null, null, null, null, key);
    }

    #endregion // Methods
}