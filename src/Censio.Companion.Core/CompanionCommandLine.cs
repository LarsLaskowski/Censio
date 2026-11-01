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
        var record = false;
        string? replayPath = null;
        PipeReplaySpeed? speed = null;
        var index = 0;

        while (index < args.Count)
        {
            var argument = args[index];

            if (string.Equals(argument, "--record", StringComparison.OrdinalIgnoreCase) && record == false)
            {
                record = true;
                index++;
            }
            else if (string.Equals(argument, "--replay", StringComparison.OrdinalIgnoreCase) && replayPath is null
                     && index + 1 < args.Count && IsPath(args[index + 1]))
            {
                replayPath = args[index + 1];
                index += 2;
            }
            else if (string.Equals(argument, "--speed", StringComparison.OrdinalIgnoreCase) && speed is null
                     && index + 1 < args.Count && PipeReplaySpeed.TryParse(args[index + 1], out var parsed))
            {
                speed = parsed;
                index += 2;
            }
            else
            {
                return CreateInvalid();
            }
        }

        if (replayPath is not null)
        {
            return record
                       ? CreateInvalid()
                       : new CompanionCommandLine
                         {
                             Mode = CompanionMode.Replay,
                             ReplayPath = replayPath,
                             Speed = speed ?? PipeReplaySpeed.RealTime
                         };
        }

        if (speed is not null)
        {
            return CreateInvalid();
        }

        return new CompanionCommandLine
               {
                   Mode = record ? CompanionMode.Record : CompanionMode.Normal
               };
    }

    /// <summary>
    /// Creates the command line that is invalid.
    /// </summary>
    /// <returns>The invalid command line</returns>
    private static CompanionCommandLine CreateInvalid()
    {
        return new CompanionCommandLine
               {
                   Mode = CompanionMode.Invalid
               };
    }

    /// <summary>
    /// Checks whether an argument can be the path of a recording.
    /// </summary>
    /// <param name="argument">The argument</param>
    /// <returns><c>true</c> if the argument is not empty and does not look like an option</returns>
    private static bool IsPath(string argument)
    {
        return argument.Length > 0 && argument.StartsWith("--", StringComparison.Ordinal) == false;
    }

    #endregion // Methods
}