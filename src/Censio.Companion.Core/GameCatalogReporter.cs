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
    private readonly Func<GameCatalog?> _readCatalog;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Guards the reported set and the cap flag.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The roles and GUIDs reported so far; it stops growing at <see cref="MaxReports" />.
    /// </summary>
    private readonly HashSet<(GameCatalogKind Role, int AssetGuid)> _reported = [];

    /// <summary>
    /// Whether the line about the cap was logged.
    /// </summary>
    private bool _isCapped;

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
    public void Report(PipeTick tick)
    {
        if (_readCatalog() is not { } catalog)
        {
            return;
        }

        foreach (var island in tick.Islands)
        {
            ReportGuid(catalog, GameCatalogKind.Region, island.Key.SessionGuid);

            foreach (var entry in island.Entries)
            {
                ReportGuid(catalog, GameCatalogKind.Good, entry.ProductGuid);

                foreach (var guid in entry.WorkforceAmounts.Keys)
                {
                    ReportGuid(catalog, GameCatalogKind.Workforce, guid);
                }

                foreach (var guid in entry.BuildingAmounts.Keys)
                {
                    ReportGuid(catalog, GameCatalogKind.Building, guid);
                }
            }
        }
    }

    /// <summary>
    /// Logs a GUID once if the catalog cannot name it.
    /// </summary>
    /// <param name="catalog">The catalog</param>
    /// <param name="role">The role</param>
    /// <param name="guid">The GUID</param>
    private void ReportGuid(GameCatalog catalog, GameCatalogKind role, int guid)
    {
        if (guid == 0)
        {
            return;
        }

        var problem = catalog.Check(guid);

        if (problem == GameCatalogProblem.None)
        {
            return;
        }

        lock (_lock)
        {
            if (_isCapped || _reported.Contains((role, guid)))
            {
                return;
            }

            if (_reported.Count >= MaxReports)
            {
                _isCapped = true;
                CompanionLog.CatalogReportsCapped(_logger, MaxReports);

                return;
            }

            _reported.Add((role, guid));
            CompanionLog.CatalogCannotName(_logger, role, guid, problem);
        }
    }

    #endregion // Methods
}