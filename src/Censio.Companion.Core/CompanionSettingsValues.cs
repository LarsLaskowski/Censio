namespace Censio.Companion.Core;

/// <summary>
/// The values of the fields of the settings window.
/// </summary>
public sealed class CompanionSettingsValues
{
    #region Properties

    /// <summary>
    /// Gets the mode.
    /// </summary>
    public required CompanionSettingsMode Mode { get; init; }

    /// <summary>
    /// Gets the text of the local port.
    /// </summary>
    public required string LocalPort { get; init; }

    /// <summary>
    /// Gets the text of the server address.
    /// </summary>
    public required string ServerAddress { get; init; }

    /// <summary>
    /// Gets the text of the server token.
    /// </summary>
    public required string ServerToken { get; init; }

    /// <summary>
    /// Gets the text of the buffer hours.
    /// </summary>
    public required string BufferHours { get; init; }

    /// <summary>
    /// Gets the text of the buffer megabytes.
    /// </summary>
    public required string BufferMegabytes { get; init; }

    /// <summary>
    /// Gets the text of the game folder.
    /// </summary>
    public required string GameFolder { get; init; }

    /// <summary>
    /// Gets the language, or <see langword="null" /> when none is set.
    /// </summary>
    public CompanionLanguage? Language { get; init; }

    #endregion // Properties
}