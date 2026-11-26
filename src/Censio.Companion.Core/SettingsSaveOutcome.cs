namespace Censio.Companion.Core;

/// <summary>
/// The outcome of saving the settings.
/// </summary>
public sealed record SettingsSaveOutcome
{
    #region Properties

    /// <summary>
    /// Gets the kind of the outcome.
    /// </summary>
    public required SettingsSaveKind Kind { get; init; }

    /// <summary>
    /// Gets the problems of the values.
    /// </summary>
    public IReadOnlyList<SettingsProblem> Problems { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the start with Windows could not be changed.
    /// </summary>
    public bool IsAutostartFailed { get; init; }

    #endregion // Properties
}