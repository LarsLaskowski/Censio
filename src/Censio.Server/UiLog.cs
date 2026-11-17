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
    /// <param name="preference">The kind of the preference, <c>theme</c>, <c>language</c>, <c>chart range</c> or <c>time axis</c>, never its value.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "The browser did not store the {Preference} choice.")]
    public static partial void PreferenceNotStored(ILogger logger, string preference);

    /// <summary>
    /// Logs that a chart could not be read from the database.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception of the read.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "A chart could not be read from the database.")]
    public static partial void ChartReadFailed(ILogger logger, Exception exception);

    #endregion // Methods
}