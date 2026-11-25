using System.Globalization;

namespace Censio.Companion.Core;

/// <summary>
/// Every text the Companion shows, in one language.
/// </summary>
public sealed class CompanionTexts
{
    #region Properties

    /// <summary>
    /// Gets the English texts.
    /// </summary>
    public static CompanionTexts English { get; } = new()
                                                    {
                                                        Language = CompanionLanguage.English,
                                                        ToolTip = TrayMenu.ToolTip,
                                                        ExitText = TrayMenu.ExitText,
                                                        InvalidCommandLineToolTip = TrayMenu.InvalidCommandLineToolTip,
                                                        InvalidCommandLineStatusText = TrayMenu.InvalidCommandLineStatusText,
                                                        StartRecordingText = TrayMenu.StartRecordingText,
                                                        StopRecordingText = TrayMenu.StopRecordingText,
                                                        OpenRecordingsFolderText = TrayMenu.OpenRecordingsFolderText,
                                                        OpenLogFolderText = TrayMenu.OpenLogFolderText,
                                                        OpenDiagnosticsText = TrayMenu.OpenDiagnosticsText,
                                                        StartWithWindowsText = TrayMenu.StartWithWindowsText,
                                                        OpenCensioText = TrayMenu.OpenCensioText,
                                                        OpenSettingsText = "Settings…"
                                                    };

    /// <summary>
    /// Gets the German texts.
    /// </summary>
    public static CompanionTexts German { get; } = new()
                                                   {
                                                       Language = CompanionLanguage.German,
                                                       ToolTip = TrayMenu.ToolTip,
                                                       ExitText = TrayMenu.ExitText,
                                                       InvalidCommandLineToolTip = TrayMenu.InvalidCommandLineToolTip,
                                                       InvalidCommandLineStatusText = TrayMenu.InvalidCommandLineStatusText,
                                                       StartRecordingText = TrayMenu.StartRecordingText,
                                                       StopRecordingText = TrayMenu.StopRecordingText,
                                                       OpenRecordingsFolderText = TrayMenu.OpenRecordingsFolderText,
                                                       OpenLogFolderText = TrayMenu.OpenLogFolderText,
                                                       OpenDiagnosticsText = TrayMenu.OpenDiagnosticsText,
                                                       StartWithWindowsText = TrayMenu.StartWithWindowsText,
                                                       OpenCensioText = TrayMenu.OpenCensioText,
                                                       OpenSettingsText = "Settings…"
                                                   };

    /// <summary>
    /// Gets the language of the texts.
    /// </summary>
    public required CompanionLanguage Language { get; init; }

    /// <summary>
    /// Gets the tooltip of the tray icon.
    /// </summary>
    public required string ToolTip { get; init; }

    /// <summary>
    /// Gets the text of the exit entry.
    /// </summary>
    public required string ExitText { get; init; }

    /// <summary>
    /// Gets the tooltip of the tray icon when the command line is invalid.
    /// </summary>
    public required string InvalidCommandLineToolTip { get; init; }

    /// <summary>
    /// Gets the status text when the command line is invalid.
    /// </summary>
    public required string InvalidCommandLineStatusText { get; init; }

    /// <summary>
    /// Gets the text of the entry that starts the recording.
    /// </summary>
    public required string StartRecordingText { get; init; }

    /// <summary>
    /// Gets the text of the entry that stops the recording.
    /// </summary>
    public required string StopRecordingText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens the recordings folder.
    /// </summary>
    public required string OpenRecordingsFolderText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens the log folder.
    /// </summary>
    public required string OpenLogFolderText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens the diagnostics window.
    /// </summary>
    public required string OpenDiagnosticsText { get; init; }

    /// <summary>
    /// Gets the text of the entry that turns the start with Windows on or off.
    /// </summary>
    public required string StartWithWindowsText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens Censio in the browser.
    /// </summary>
    public required string OpenCensioText { get; init; }

    /// <summary>
    /// Gets the text of the entry that opens the settings window.
    /// </summary>
    public required string OpenSettingsText { get; init; }

    #endregion // Properties

    #region Methods

#pragma warning disable RH2003 // Skeleton: removed with the implementation
    /// <summary>
    /// Gets the texts of a language.
    /// </summary>
    /// <param name="language">The language</param>
    /// <returns>The texts</returns>
    public static CompanionTexts For(CompanionLanguage language)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the language a culture asks for.
    /// </summary>
    /// <param name="culture">The culture</param>
    /// <returns>German for a German culture, otherwise English</returns>
    public static CompanionLanguage LanguageOf(CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats the text of a problem with a value of the settings window.
    /// </summary>
    /// <param name="kind">The problem kind</param>
    /// <returns>The text</returns>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public string FormatProblem(SettingsProblemKind kind)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Formats the text of a notice about the active profile.
    /// </summary>
    /// <param name="notice">The notice</param>
    /// <returns>The text</returns>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public string FormatNotice(ProfileNotice notice)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325 // Skeleton: removed with the implementation

    /// <summary>
    /// Formats the text of the outcome of saving the settings.
    /// </summary>
    /// <param name="outcome">The outcome</param>
    /// <returns>The text</returns>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public string FormatOutcome(SettingsSaveOutcome outcome)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325 // Skeleton: removed with the implementation

#pragma warning restore RH2003 // Skeleton: removed with the implementation

    #endregion // Methods
}