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
    // Skeleton: removed with the implementation
#pragma warning disable RH2003, S1172
    public Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        _ = _output;
        _ = _error;
        _ = _random;

        throw new NotImplementedException();
    }

#pragma warning restore RH2003, S1172

    #endregion // Methods
}