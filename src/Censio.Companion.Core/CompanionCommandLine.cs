using Censio.Pipe;

namespace Censio.Companion.Core;

/// <summary>
/// The parsed command line of the Companion.
/// </summary>
public sealed class CompanionCommandLine
{
    #region Properties

    /// <summary>
    /// Gets the mode.
    /// </summary>
    public required CompanionMode Mode { get; init; }

    /// <summary>
    /// Gets the path of the recording to replay, if any.
    /// </summary>
    public string? ReplayPath { get; init; }

    /// <summary>
    /// Gets the replay speed.
    /// </summary>
    public PipeReplaySpeed Speed { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Parses the arguments of the command line.
    /// </summary>
    /// <param name="args">The arguments</param>
    /// <returns>The parsed command line</returns>
    public static CompanionCommandLine Parse(IReadOnlyList<string> args)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}