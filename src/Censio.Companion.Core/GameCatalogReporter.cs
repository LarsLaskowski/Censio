using Censio.GameData;
using Censio.Pipe;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Logs the GUIDs of a tick that the catalog cannot name.
/// </summary>
public sealed class GameCatalogReporter
{
    #region Constants

    /// <summary>
    /// The most GUIDs logged in one run.
    /// </summary>
    public const int MaxReports = 1000;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// Reads the catalog.
    /// </summary>
#pragma warning disable S1144, S4487 // Skeleton: removed with the implementation
    private readonly Func<GameCatalog?> _readCatalog;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;
#pragma warning restore S1144, S4487

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCatalogReporter" /> class.
    /// </summary>
    /// <param name="readCatalog">Reads the catalog</param>
    /// <param name="logger">The logger</param>
    public GameCatalogReporter(Func<GameCatalog?> readCatalog, ILogger logger)
    {
        _readCatalog = readCatalog;
        _logger = logger;
    }

    #endregion // Constructor

    #region Methods

    /// <summary>
    /// Logs the GUIDs of a tick that the catalog cannot name.
    /// </summary>
    /// <param name="tick">The tick</param>
#pragma warning disable S2325 // Skeleton: removed with the implementation
    public void Report(PipeTick tick)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S2325

    #endregion // Methods
}