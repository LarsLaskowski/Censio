using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// The rules of the optional start of the Companion with Windows.
/// </summary>
public sealed class CompanionAutostart
{
    #region Constants

    /// <summary>
    /// The name of the autostart entry.
    /// </summary>
    public const string EntryName = "Censio Companion";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The store of the autostart entry.
    /// </summary>
    private readonly IStartupEntryStore _store;

    /// <summary>
    /// The full path of the executable, or <see langword="null" /> if unknown.
    /// </summary>
    private readonly string? _exePath;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionAutostart" /> class.
    /// </summary>
    /// <param name="store">The store of the autostart entry</param>
    /// <param name="exePath">The full path of the executable, or <see langword="null" /> if unknown</param>
    /// <param name="logger">The logger</param>
    public CompanionAutostart(IStartupEntryStore store, string? exePath, ILogger logger)
    {
        _store = store;
        _exePath = exePath;
        _logger = logger;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the entry of the last refresh is the Companion's own.
    /// </summary>
    public bool IsEnabled { get; private set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Formats the command written for an executable.
    /// </summary>
    /// <param name="exePath">The full path of the executable</param>
    /// <returns>The quoted path, or <see langword="null" /> if the path cannot be used</returns>
    public static string? FormatCommand(string? exePath)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Determines whether a stored command is exactly the one the Companion writes for an executable.
    /// </summary>
    /// <param name="command">The stored command, or <see langword="null" /> if absent</param>
    /// <param name="exePath">The full path of the executable</param>
    /// <returns><see langword="true" /> if the command is the Companion's own</returns>
    public static bool Matches(string? command, string? exePath)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads the entry and updates <see cref="IsEnabled" />.
    /// </summary>
    /// <returns><see langword="true" /> if the entry is the Companion's own</returns>
    public bool Refresh()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Turns the autostart on when the last refresh found it off, otherwise off.
    /// </summary>
    public void Toggle()
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}