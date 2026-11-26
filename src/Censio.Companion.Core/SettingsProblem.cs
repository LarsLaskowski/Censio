namespace Censio.Companion.Core;

/// <summary>
/// A problem with a field of the settings window.
/// </summary>
public readonly record struct SettingsProblem
{
    #region Properties

    /// <summary>
    /// Gets the field.
    /// </summary>
    public required SettingsField Field { get; init; }

    /// <summary>
    /// Gets the kind of the problem.
    /// </summary>
    public required SettingsProblemKind Kind { get; init; }

    #endregion // Properties
}