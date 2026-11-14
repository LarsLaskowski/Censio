using System.Diagnostics.CodeAnalysis;

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

    /// <summary>
    /// Gets the address of the server to send the replay to, if any.
    /// </summary>
    public Uri? ServerAddress { get; init; }

    /// <summary>
    /// Gets the path of the file that holds the token, if any.
    /// </summary>
    public string? TokenFilePath { get; init; }

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
        string? speedText = null;
        string? serverText = null;
        string? tokenFilePath = null;
        var index = 0;

        while (index < args.Count)
        {
            if (string.Equals(args[index], "--record", StringComparison.OrdinalIgnoreCase) && record == false)
            {
                record = true;
                index++;
            }
            else if (TryTakeValue(args, ref index, "--replay", ref replayPath, IsPath)
                     || TryTakeValue(args, ref index, "--speed", ref speedText, value => PipeReplaySpeed.TryParse(value, out _))
                     || TryTakeValue(args, ref index, "--server", ref serverText, value => TryParseServerAddress(value, out _))
                     || TryTakeValue(args, ref index, "--token-file", ref tokenFilePath, IsPath))
            {
                // The switch and its value are taken.
            }
            else
            {
                return CreateInvalid();
            }
        }

        Uri? serverAddress = null;
        PipeReplaySpeed? speed = null;

        if (serverText is not null && TryParseServerAddress(serverText, out var address))
        {
            serverAddress = address;
        }

        if (speedText is not null && PipeReplaySpeed.TryParse(speedText, out var parsed))
        {
            speed = parsed;
        }

        if (replayPath is not null)
        {
            return CreateReplay(record, replayPath, speed, serverAddress, tokenFilePath);
        }

        if (speed is not null || serverAddress is not null || tokenFilePath is not null)
        {
            return CreateInvalid();
        }

        return new CompanionCommandLine
               {
                   Mode = record ? CompanionMode.Record : CompanionMode.Normal
               };
    }

    /// <summary>
    /// Takes a switch and its value when the switch is at the index, was not given before and the value is accepted.
    /// </summary>
    /// <param name="args">The arguments</param>
    /// <param name="index">The index of the switch; advanced by two when taken</param>
    /// <param name="name">The name of the switch</param>
    /// <param name="slot">The value of the switch; <c>null</c> until it is given</param>
    /// <param name="accept">Decides whether a value is valid</param>
    /// <returns><c>true</c> if the switch and its value were taken</returns>
    private static bool TryTakeValue(IReadOnlyList<string> args, ref int index, string name, ref string? slot, Func<string, bool> accept)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase) == false
            || slot is not null
            || index + 1 >= args.Count
            || accept(args[index + 1]) == false)
        {
            return false;
        }

        slot = args[index + 1];
        index += 2;

        return true;
    }

    /// <summary>
    /// Creates the command line of a replay.
    /// </summary>
    /// <param name="record">Whether <c>--record</c> was given</param>
    /// <param name="replayPath">The path of the recording</param>
    /// <param name="speed">The speed, if given</param>
    /// <param name="serverAddress">The address of the server, if given</param>
    /// <param name="tokenFilePath">The path of the token file, if given</param>
    /// <returns>The replay command line, or the invalid one if the switches do not fit</returns>
    private static CompanionCommandLine CreateReplay(bool record, string replayPath, PipeReplaySpeed? speed, Uri? serverAddress, string? tokenFilePath)
    {
        if (record || (tokenFilePath is not null && serverAddress is null))
        {
            return CreateInvalid();
        }

        return new CompanionCommandLine
               {
                   Mode = CompanionMode.Replay,
                   ReplayPath = replayPath,
                   Speed = speed ?? PipeReplaySpeed.RealTime,
                   ServerAddress = serverAddress,
                   TokenFilePath = tokenFilePath
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
    /// Parses the address of the server.
    /// </summary>
    /// <param name="argument">The raw argument</param>
    /// <param name="address">The address if the argument is valid</param>
    /// <returns>
    /// <c>true</c> if the argument is an absolute <c>ws</c> or <c>wss</c> address with a host, without user
    /// information, fragment, white space or control characters
    /// </returns>
    private static bool TryParseServerAddress(string argument, [NotNullWhen(true)] out Uri? address)
    {
        address = null;

        if (argument.Length == 0
            || argument.Contains('#')
            || argument.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        if (Uri.TryCreate(argument, UriKind.Absolute, out var parsed) == false
            || (parsed.Scheme != Uri.UriSchemeWs && parsed.Scheme != Uri.UriSchemeWss)
            || parsed.Host.Length == 0
            || parsed.UserInfo.Length > 0)
        {
            return false;
        }

        address = parsed;

        return true;
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