using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// What the settings window session needs from the running Companion.
/// </summary>
public sealed class SettingsWindowContext
{
    #region Properties

    /// <summary>
    /// Gets the path of the settings file.
    /// </summary>
    public required string SettingsPath { get; init; }

    /// <summary>
    /// Gets the values the Companion started with.
    /// </summary>
    public required CompanionSettingsValues StartupValues { get; init; }

    /// <summary>
    /// Gets the profiles.
    /// </summary>
    public required CompanionProfiles Profiles { get; init; }

    /// <summary>
    /// Gets the start with Windows.
    /// </summary>
    public required CompanionAutostart Autostart { get; init; }

    /// <summary>
    /// Gets the restart.
    /// </summary>
    public required CompanionRestart Restart { get; init; }

    /// <summary>
    /// Gets the reader of the texts in the current language.
    /// </summary>
    public required Func<CompanionTexts> Texts { get; init; }

    /// <summary>
    /// Gets the action that applies a language.
    /// </summary>
    public required Action<CompanionLanguage> ApplyLanguage { get; init; }

    /// <summary>
    /// Gets the reader of the run state.
    /// </summary>
    public required Func<SettingsRunState> RunState { get; init; }

    /// <summary>
    /// Gets the action that exits the Companion for a restart.
    /// </summary>
    public required Action Exit { get; init; }

    /// <summary>
    /// Gets the check whether a folder exists.
    /// </summary>
    public required Func<string, bool> DirectoryExists { get; init; }

    /// <summary>
    /// Gets the check whether a folder is a game installation.
    /// </summary>
    public required Func<string, bool> IsInstallation { get; init; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    public required ILogger Logger { get; init; }

    #endregion // Properties
}