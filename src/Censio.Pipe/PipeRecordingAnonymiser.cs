namespace Censio.Pipe;

/// <summary>
/// Rewrites a pipe recording so that it holds no names of the player and no exact amounts.
/// </summary>
public sealed class PipeRecordingAnonymiser
{
    #region Constants

    /// <summary>
    /// The smallest shift of the receive times in days.
    /// </summary>
    public const int MinShiftDays = 30;

    /// <summary>
    /// The largest additional shift of the receive times in days.
    /// </summary>
    public const int MaxShiftDays = 365;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The random source.
    /// </summary>
    private readonly IPipeAnonymiserRandom _random;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeRecordingAnonymiser"/> class.
    /// </summary>
    /// <param name="random">The random source</param>
    public PipeRecordingAnonymiser(IPipeAnonymiserRandom random)
    {
        _random = random;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Anonymises a recording.
    /// </summary>
    /// <param name="recording">The recording to read; it must support seeking</param>
    /// <param name="output">The stream the anonymised recording is written to</param>
    /// <param name="cancellationToken">The token that cancels the work</param>
    /// <returns>The result</returns>
    // Skeleton: removed with the implementation
#pragma warning disable RH2003
    public Task<PipeAnonymiseResult> AnonymiseAsync(Stream recording, Stream output, CancellationToken cancellationToken)
    {
        _ = _random;

        throw new NotImplementedException();
    }

#pragma warning restore RH2003

    #endregion // Methods
}