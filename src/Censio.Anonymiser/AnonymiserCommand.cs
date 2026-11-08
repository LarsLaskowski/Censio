using System.Globalization;

using Censio.Pipe;

namespace Censio.Anonymiser;

/// <summary>
/// The anonymise command: reads a recording and writes the anonymised copy.
/// </summary>
public sealed class AnonymiserCommand
{
    #region Constants

    /// <summary>
    /// The exit code of a successful run.
    /// </summary>
    public const int SuccessCode = 0;

    /// <summary>
    /// The exit code of a failed run.
    /// </summary>
    public const int ErrorCode = 1;

    /// <summary>
    /// The exit code of a wrong command line.
    /// </summary>
    public const int UsageCode = 2;

    /// <summary>
    /// The usage line.
    /// </summary>
    private const string Usage = "Usage: Censio.Anonymiser <recording> <output>";

    /// <summary>
    /// The reason of an output that exists.
    /// </summary>
    private const string OutputExists = "output file exists";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The writer of the success line.
    /// </summary>
    private readonly TextWriter _output;

    /// <summary>
    /// The writer of the errors and the usage.
    /// </summary>
    private readonly TextWriter _error;

    /// <summary>
    /// The random source.
    /// </summary>
    private readonly IPipeAnonymiserRandom _random;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymiserCommand"/> class.
    /// </summary>
    /// <param name="output">The writer of the success line</param>
    /// <param name="error">The writer of the errors and the usage</param>
    /// <param name="random">The random source</param>
    public AnonymiserCommand(TextWriter output, TextWriter error, IPipeAnonymiserRandom random)
    {
        _output = output;
        _error = error;
        _random = random;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">The command line arguments: the recording and the output file</param>
    /// <param name="cancellationToken">The token that cancels the run</param>
    /// <returns>The exit code</returns>
    public async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count != 2)
        {
            await _error.WriteLineAsync(Usage).ConfigureAwait(false);

            return UsageCode;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await RunCoreAsync(arguments[0], arguments[1], cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return await FailAsync("cancelled").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens a recording for reading while others may only read it.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>The stream, or <see langword="null" /> when it cannot be opened</returns>
    private static FileStream? TryOpen(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks whether two paths name the same file.
    /// </summary>
    /// <param name="first">The first path</param>
    /// <param name="second">The second path</param>
    /// <returns><see langword="true" /> when the full paths are equal</returns>
    private static bool IsSamePath(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }

    /// <summary>
    /// Gives the reason of a result that is not an anonymised recording.
    /// </summary>
    /// <param name="result">The result</param>
    /// <returns>The reason</returns>
    private static string Describe(PipeAnonymiseResult result)
    {
        if (result.Kind == PipeAnonymiseResultKind.FrameNotAnonymisable)
        {
            return string.Create(CultureInfo.InvariantCulture, $"frame cannot be anonymised in line {result.LineNumber}");
        }

        return result.Problem?.Kind switch
               {
                   PipeReplayProblemKind.NotARecording => "not a recording",
                   PipeReplayProblemKind.UnsupportedVersion => "unsupported version",
                   PipeReplayProblemKind.InvalidLine => string.Create(CultureInfo.InvariantCulture, $"invalid line {result.Problem.LineNumber}"),
                   _ => "read failed"
               };
    }

    /// <summary>
    /// Deletes the temporary file if it exists; a failure to delete is ignored.
    /// </summary>
    /// <param name="path">The path, or <see langword="null" /> when none was chosen</param>
    private static void DeleteQuietly(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file stays behind; the error of the run is the one to report.
        }
    }

    /// <summary>
    /// Opens the recording and runs the command on it.
    /// </summary>
    /// <param name="inputPath">The path of the recording</param>
    /// <param name="outputPath">The path of the output</param>
    /// <param name="cancellationToken">The token that cancels the run</param>
    /// <returns>The exit code</returns>
    private async Task<int> RunCoreAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        var input = TryOpen(inputPath);

        if (input is null)
        {
            return await FailAsync("file not found").ConfigureAwait(false);
        }

        await using (input.ConfigureAwait(false))
        {
            return input.CanSeek
                       ? await WriteOutputAsync(input, inputPath, outputPath, cancellationToken).ConfigureAwait(false)
                       : await FailAsync("read failed").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Anonymises the recording into a temporary file beside the output and moves it to the output.
    /// </summary>
    /// <param name="input">The recording</param>
    /// <param name="inputPath">The path of the recording</param>
    /// <param name="outputPath">The path of the output</param>
    /// <param name="cancellationToken">The token that cancels the run</param>
    /// <returns>The exit code</returns>
    private async Task<int> WriteOutputAsync(Stream input, string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        string? temporaryPath = null;

        try
        {
            if (IsSamePath(inputPath, outputPath))
            {
                return await FailAsync("output is the input").ConfigureAwait(false);
            }

            if (File.Exists(outputPath) || Directory.Exists(outputPath))
            {
                return await FailAsync(OutputExists).ConfigureAwait(false);
            }

            temporaryPath = string.Concat(outputPath, ".", Path.GetRandomFileName(), ".tmp");

            var result = await AnonymiseAsync(input, temporaryPath, cancellationToken).ConfigureAwait(false);

            if (result.Kind != PipeAnonymiseResultKind.Anonymised)
            {
                return await FailAsync(Describe(result)).ConfigureAwait(false);
            }

            File.Move(temporaryPath, outputPath);

            await ReportSuccessAsync(result.FrameCount, outputPath).ConfigureAwait(false);

            return SuccessCode;
        }
        catch (IOException) when (File.Exists(outputPath) || Directory.Exists(outputPath))
        {
            return await FailAsync(OutputExists).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return await FailAsync("write failed").ConfigureAwait(false);
        }
        finally
        {
            DeleteQuietly(temporaryPath);
        }
    }

    /// <summary>
    /// Anonymises the recording into a new file.
    /// </summary>
    /// <param name="input">The recording</param>
    /// <param name="path">The path of the new file</param>
    /// <param name="cancellationToken">The token that cancels the work</param>
    /// <returns>The result</returns>
    private async Task<PipeAnonymiseResult> AnonymiseAsync(Stream input, string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        await using (stream.ConfigureAwait(false))
        {
            var result = await new PipeRecordingAnonymiser(_random).AnonymiseAsync(input, stream, cancellationToken).ConfigureAwait(false);

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            return result;
        }
    }

    /// <summary>
    /// Writes the success line; a failing standard output is ignored, as the output file is complete.
    /// </summary>
    /// <param name="frameCount">The number of frames written</param>
    /// <param name="outputPath">The output as typed</param>
    /// <returns>The task</returns>
    private async Task ReportSuccessAsync(long frameCount, string outputPath)
    {
        try
        {
            await _output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Anonymised {frameCount} frames to {outputPath}")).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The output file is complete; a standard output that cannot be written does not fail the run.
        }
    }

    /// <summary>
    /// Writes an error line.
    /// </summary>
    /// <param name="reason">The reason</param>
    /// <returns>The error code</returns>
    private async Task<int> FailAsync(string reason)
    {
        await _error.WriteLineAsync($"Error: {reason}").ConfigureAwait(false);

        return ErrorCode;
    }

    #endregion // Methods
}