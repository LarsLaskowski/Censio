namespace Censio.Pipe;

/// <summary>
/// Collects the names of the frames of a recording and rewrites the frames.
/// </summary>
internal sealed class PipeFrameAnonymiser
{
    #region Fields

    /// <summary>
    /// The invented names.
    /// </summary>
    private readonly PipeAnonymousNames _names;

    /// <summary>
    /// The random source.
    /// </summary>
    private readonly IPipeAnonymiserRandom _random;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeFrameAnonymiser"/> class.
    /// </summary>
    /// <param name="names">The invented names</param>
    /// <param name="random">The random source</param>
    public PipeFrameAnonymiser(PipeAnonymousNames names, IPipeAnonymiserRandom random)
    {
        _names = names;
        _random = random;
    }

    #endregion // Constructors

    #region Methods

    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S2325
    /// <summary>
    /// Collects the names of a frame.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns><see langword="true" /> when the frame can be anonymised</returns>
    public bool TryCollect(ReadOnlySpan<byte> frame)
    {
        _ = _names;

        throw new NotImplementedException();
    }

    /// <summary>
    /// Rewrites a frame.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns>The rewritten frame, or <see langword="null" /> when it cannot be anonymised</returns>
    public byte[]? Anonymise(ReadOnlySpan<byte> frame)
    {
        _ = _random;

        throw new NotImplementedException();
    }

    /// <summary>
    /// Resets the version gate for a new pass.
    /// </summary>
    public void Reset()
    {
        throw new NotImplementedException();
    }

#pragma warning restore RH2003, S2325

    #endregion // Methods
}