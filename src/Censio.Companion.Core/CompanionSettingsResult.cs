namespace Censio.Companion.Core;

// Skeleton: removed with the implementation
#pragma warning disable S3453
/// <summary>
/// The result of reading the settings file of the Companion.
/// </summary>
public sealed class CompanionSettingsResult
{
    #region Constructor

    // Skeleton: removed with the implementation
#pragma warning disable S1144
    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionSettingsResult" /> class.
    /// </summary>
    /// <param name="kind">The outcome kind</param>
    /// <param name="gameFolder">The game folder, if any</param>
    /// <param name="problem">The problem, if any</param>
    /// <param name="exception">The exception behind the problem, if any</param>
    private CompanionSettingsResult(CompanionSettingsResultKind kind, string? gameFolder, string? problem, Exception? exception)
    {
        Kind = kind;
        GameFolder = gameFolder;
        Problem = problem;
        Exception = exception;
    }
#pragma warning restore S1144

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the outcome kind.
    /// </summary>
    public CompanionSettingsResultKind Kind { get; }

    /// <summary>
    /// Gets the game folder; <c>null</c> unless the kind is <see cref="CompanionSettingsResultKind.GameFolder" />.
    /// </summary>
    public string? GameFolder { get; }

    /// <summary>
    /// Gets the problem; <c>null</c> unless the kind is <see cref="CompanionSettingsResultKind.Invalid" />.
    /// </summary>
    public string? Problem { get; }

    /// <summary>
    /// Gets the exception behind the problem, if any.
    /// </summary>
    public Exception? Exception { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates the result of settings that name no game folder.
    /// </summary>
    /// <returns>The result</returns>
    public static CompanionSettingsResult NoGameFolder()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the result of settings that name a game folder.
    /// </summary>
    /// <param name="folder">The game folder</param>
    /// <returns>The result</returns>
    public static CompanionSettingsResult FromGameFolder(string folder)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the result of a settings file that cannot be used.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <param name="exception">The exception behind the problem, if any</param>
    /// <returns>The result</returns>
    public static CompanionSettingsResult FromProblem(string problem, Exception? exception)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}
#pragma warning restore S3453