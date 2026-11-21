namespace Censio.Companion.Core;

/// <summary>
/// The local server setting of the settings file.
/// </summary>
public sealed record LocalServerSetting
{
    #region Properties

    /// <summary>
    /// Gets the kind of the setting.
    /// </summary>
    public required LocalServerSettingKind Kind { get; init; }

    /// <summary>
    /// Gets the port of the local server.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Gets the problem of an invalid setting, or <see langword="null" /> when there is none.
    /// </summary>
    public string? Problem { get; init; }

    #endregion // Properties
}