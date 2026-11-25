namespace Censio.Companion.Core;

/// <summary>
/// What the settings window shows.
/// </summary>
public sealed class SettingsView
{
    #region Properties

    /// <summary>
    /// Gets the texts.
    /// </summary>
    public required CompanionTexts Texts { get; init; }

    /// <summary>
    /// Gets the values of the fields.
    /// </summary>
    public required CompanionSettingsValues Values { get; init; }

    /// <summary>
    /// Gets the problem of the settings file as a whole, <c>null</c> if none.
    /// </summary>
    public string? FileProblem { get; init; }

    /// <summary>
    /// Gets the problems of the fields.
    /// </summary>
    public required IReadOnlyList<SettingsProblem> Problems { get; init; }

    /// <summary>
    /// Gets the profiles.
    /// </summary>
    public required IReadOnlyList<CompanionProfile> ProfileList { get; init; }

    /// <summary>
    /// Gets the id of the active profile.
    /// </summary>
    public required Guid ActiveProfile { get; init; }

    /// <summary>
    /// Gets a value indicating whether the profiles file is invalid.
    /// </summary>
    public bool IsProfilesFileInvalid { get; init; }

    /// <summary>
    /// Gets a value indicating whether the Companion starts with Windows.
    /// </summary>
    public bool IsAutostartEnabled { get; init; }

    /// <summary>
    /// Gets the warnings.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    #endregion // Properties

#pragma warning disable S2325, RH2003 // Skeleton: removed with the implementation

    #region Methods

    /// <summary>
    /// Gets a value indicating whether a field is enabled in a mode.
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="mode">The mode</param>
    /// <returns><c>true</c> if the field is enabled</returns>
    public bool IsFieldEnabled(SettingsField field, CompanionSettingsMode mode)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods

#pragma warning restore S2325, RH2003
}