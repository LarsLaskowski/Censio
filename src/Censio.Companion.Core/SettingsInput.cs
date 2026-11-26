namespace Censio.Companion.Core;

/// <summary>
/// What the user entered in the settings window.
/// </summary>
public sealed class SettingsInput
{
    #region Properties

    /// <summary>
    /// Gets the values of the fields.
    /// </summary>
    public required CompanionSettingsValues Values { get; init; }

    /// <summary>
    /// Gets the id of the selected profile.
    /// </summary>
    public required Guid SelectedProfile { get; init; }

    /// <summary>
    /// Gets the name of a new profile, <c>null</c> if none is created.
    /// </summary>
    public string? NewProfileName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the Companion starts with Windows.
    /// </summary>
    public required bool IsAutostartEnabled { get; init; }

    /// <summary>
    /// Gets the fields the user edited.
    /// </summary>
    public required IReadOnlyCollection<SettingsField> EditedFields { get; init; }

    #endregion // Properties
}