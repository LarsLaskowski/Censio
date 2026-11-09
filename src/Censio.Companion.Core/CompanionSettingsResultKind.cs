namespace Censio.Companion.Core;

/// <summary>
/// The outcome kind of reading the settings file of the Companion.
/// </summary>
public enum CompanionSettingsResultKind
{
    /// <summary>
    /// The settings name no game folder.
    /// </summary>
    NoGameFolder,

    /// <summary>
    /// The settings name a game folder.
    /// </summary>
    GameFolder,

    /// <summary>
    /// The settings file cannot be used.
    /// </summary>
    Invalid
}