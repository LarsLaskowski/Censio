namespace Censio.Server;

/// <summary>
/// The log messages of the web UI.
/// </summary>
public static partial class UiLog
{
    #region Methods

    /// <summary>
    /// Logs that the browser did not store a choice.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="preference">The kind of the preference, <c>theme</c> or <c>language</c>, never its value.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "The browser did not store the {Preference} choice.")]
    public static partial void PreferenceNotStored(ILogger logger, string preference);

    #endregion // Methods
}