using System.Collections;
using System.Reflection;

using Censio.GameData;
using Censio.Pipe;
using Censio.Server.Tests;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="GameCatalogReporter" />.
/// </summary>
[TestClass]
public sealed class GameCatalogReporterTests
{
    #region Constants

    /// <summary>
    /// A name that must never reach the log.
    /// </summary>
    private const string SecretName = "INVENTED-NAME-7QX";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The lines of one GUID in the four roles, in the order of the ordinal comparison.
    /// </summary>
    private static readonly string[] _oncePerRole = ["Catalog cannot name Building 5: Unknown", "Catalog cannot name Good 5: Unknown", "Catalog cannot name Region 5: Unknown", "Catalog cannot name Workforce 5: Unknown"];

    /// <summary>
    /// The logger of the reporter.
    /// </summary>
    private readonly RecordingLogger _logger = new();

    /// <summary>
    /// The catalog the reporter reads.
    /// </summary>
    private GameCatalog? _catalog;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// An unknown building GUID and an unknown workforce GUID are each reported with role and reason.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReporterReportLogsUnknownBuildingAndWorkforce()
    {
        // Arrange
        _catalog = Catalog();

        var reporter = CreateReporter();

        // Act
        reporter.Report(Tick(1, [Entry(2, workforce: [7], buildings: [8])]));

        // Assert
        Assert.Contains("Catalog cannot name Building 8: Unknown", Messages(), "The unknown building should be reported");
        Assert.Contains("Catalog cannot name Workforce 7: Unknown", Messages(), "The unknown workforce should be reported");
    }

    /// <summary>
    /// An unknown region and an unknown good are each reported with role and reason.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReporterReportLogsUnknownRegionAndGood()
    {
        // Arrange
        _catalog = Catalog();

        var reporter = CreateReporter();

        // Act
        reporter.Report(Tick(5, [Entry(6)]));

        // Assert
        Assert.Contains("Catalog cannot name Region 5: Unknown", Messages(), "The unknown region should be reported");
        Assert.Contains("Catalog cannot name Good 6: Unknown", Messages(), "The unknown good should be reported");
        Assert.IsTrue(_logger.Entries.All(entry => entry.Level == LogLevel.Information), "The reports should be information lines");
    }

    /// <summary>
    /// An incomplete, a nameless and an ambiguous GUID are reported with their reason; a fully named one is not.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReporterReportLogsIncompleteNamelessAndAmbiguous()
    {
        // Arrange
        _catalog = new GameCatalog([Named(11, SecretName, null), Named(12, null, null), Named(14, "Good 14", "Ware 14")], [13], 0, 0);

        var reporter = CreateReporter();

        // Act
        reporter.Report(Tick(1, [Entry(11), Entry(12), Entry(13), Entry(14)]));

        // Assert
        Assert.Contains("Catalog cannot name Good 11: Incomplete", Messages(), "The incomplete good should be reported");
        Assert.Contains("Catalog cannot name Good 12: Nameless", Messages(), "The nameless good should be reported");
        Assert.Contains("Catalog cannot name Good 13: Ambiguous", Messages(), "The ambiguous good should be reported");
        Assert.DoesNotContain(message => message.Contains("Good 14", StringComparison.Ordinal), Messages(), "A named good should not be reported at all");
    }

    /// <summary>
    /// A GUID is reported once per role and run, however often it comes; the same GUID in another role is reported
    /// separately.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReporterReportLogsOncePerGuidAndRole()
    {
        // Arrange
        _catalog = Catalog();

        var reporter = CreateReporter();
        var tick = Tick(5, [Entry(5, buildings: [5], workforce: [5]), Entry(5)]);

        // Act
        reporter.Report(tick);
        reporter.Report(tick);

        // Assert
        Assert.AreSequenceEqual(_oncePerRole,
                                Messages().Order(StringComparer.Ordinal).ToArray(),
                                "Each role of the GUID should be reported once");
    }

    /// <summary>
    /// The lines hold the GUID, role and reason and no name, also for an incomplete entry.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReporterReportLogsNoName()
    {
        // Arrange
        _catalog = new GameCatalog([Named(11, SecretName, null), Named(12, null, SecretName), Named(5, SecretName, SecretName)], [], 0, 0);

        var reporter = CreateReporter();

        // Act
        reporter.Report(Tick(5, [Entry(11), Entry(12)]));

        // Assert
        Assert.IsNotEmpty(_logger.Entries, "The incomplete GUIDs should be reported");
        Assert.DoesNotContain(entry => entry.Message.Contains(SecretName, StringComparison.Ordinal), _logger.Entries, "No line should hold a name");
    }

    /// <summary>
    /// GUID 0 means none: it is never looked up or reported, in any role.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReporterReportSkipsZero()
    {
        // Arrange
        _catalog = Catalog();

        var reporter = CreateReporter();

        // Act
        reporter.Report(Tick(0, [Entry(0, workforce: [0], buildings: [0])]));

        // Assert
        Assert.IsEmpty(_logger.Entries, "GUID 0 should never be reported");
    }

    /// <summary>
    /// Without a catalog nothing is reported and nothing is remembered: the same tick is reported once a catalog is
    /// there.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReporterReportSkipsTickWithoutCatalog()
    {
        // Arrange
        var reporter = CreateReporter();
        var tick = Tick(5, [Entry(6)]);

        // Act
        reporter.Report(tick);

        var before = _logger.Entries.Count;

        _catalog = Catalog();
        reporter.Report(tick);

        // Assert
        Assert.AreEqual(0, before, "Without a catalog nothing should be reported");
        Assert.Contains("Catalog cannot name Good 6: Unknown", Messages(), "The tick should be reported once the catalog is there");
    }

    /// <summary>
    /// After <see cref="GameCatalogReporter.MaxReports" /> reports one line says that further ones are not logged, no
    /// more GUID is logged, and the set of remembered GUIDs stops growing.
    /// </summary>
    [TestMethod]
    [TestCategory("AC3")]
    public void GameCatalogReporterReportStopsAtMaxReports()
    {
        // Arrange
        _catalog = Catalog();

        var reporter = CreateReporter();
        var first = Enumerable.Range(1, GameCatalogReporter.MaxReports + 50).ToArray();
        var second = Enumerable.Range(10_000, 500).ToArray();

        // Act
        reporter.Report(Tick(1, [Entry(1, buildings: first)]));

        var linesAfterFirst = _logger.Entries.Count;
        var rememberedAfterFirst = Remembered(reporter);

        reporter.Report(Tick(1, [Entry(1, buildings: second)]));
        reporter.Report(Tick(1, [Entry(1, buildings: first)]));

        // Assert
        var capLines = Messages().Where(message => message == "Catalog: more than 1000 GUIDs cannot be named; further ones are not logged in this run").ToList();
        var nameLines = Messages().Where(message => message.StartsWith("Catalog cannot name", StringComparison.Ordinal)).ToList();

        Assert.HasCount(1, capLines, "One line should say that further GUIDs are not logged");
        Assert.HasCount(GameCatalogReporter.MaxReports, nameLines, "Exactly the maximum number of GUIDs should be logged");
        Assert.AreEqual(GameCatalogReporter.MaxReports + 1, linesAfterFirst, "The first tick should log the maximum and the cap line");
        Assert.HasCount(linesAfterFirst, _logger.Entries, "No further line should be logged after the cap");
        Assert.IsGreaterThan(0, rememberedAfterFirst, "The reporter should remember the GUIDs it reported");
        Assert.AreEqual(rememberedAfterFirst, Remembered(reporter), "The remembered GUIDs should stop growing at the cap");
    }

    #endregion // Methods

    #region Helpers

    /// <summary>
    /// Creates an empty catalog.
    /// </summary>
    /// <returns>The catalog</returns>
    private static GameCatalog Catalog()
    {
        return new GameCatalog([], [], 0, 0);
    }

    /// <summary>
    /// Creates a good entry with names.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="english">The English name</param>
    /// <param name="german">The German name</param>
    /// <returns>The catalog entry</returns>
    private static GameCatalogEntry Named(int guid, string? english, string? german)
    {
        return new GameCatalogEntry
               {
                   AssetGuid = guid,
                   Kind = GameCatalogKind.Good,
                   EnglishName = english,
                   GermanName = german
               };
    }

    /// <summary>
    /// Creates a production entry.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="workforce">The workforce GUIDs</param>
    /// <param name="buildings">The building GUIDs</param>
    /// <returns>The entry</returns>
    private static ProductionEntry Entry(int productGuid, int[]? workforce = null, int[]? buildings = null)
    {
        return new ProductionEntry
               {
                   ProductGuid = productGuid,
                   Generation = 1,
                   Consumption = 1,
                   Delta = 0,
                   PerfectGeneration = 1,
                   PerfectConsumption = 1,
                   BuildingCount = 1,
                   Maintenance = 0,
                   Income = 0,
                   Profit = 0,
                   SummedProductivity = 1,
                   AverageProductivity = 1,
                   WorkforceAmounts = (workforce ?? []).ToDictionary(guid => guid, _ => 1),
                   BuildingAmounts = (buildings ?? []).ToDictionary(guid => guid, _ => 1)
               };
    }

    /// <summary>
    /// Creates a tick with one island.
    /// </summary>
    /// <param name="sessionGuid">The session GUID of the island</param>
    /// <param name="entries">The entries</param>
    /// <returns>The tick</returns>
    private static PipeTick Tick(int sessionGuid, ProductionEntry[] entries)
    {
        return new PipeTick
               {
                   GameTime = 100,
                   ReceiveTime = new DateTimeOffset(2026, 11, 1, 17, 9, 4, TimeSpan.Zero),
                   Islands = [
                                 new PipeTickIsland
                                 {
                                     Key = new PipeIslandKey(sessionGuid, 1, 0),
                                     SessionId = 4,
                                     Name = "Ostia",
                                     Entries = entries
                                 }
                             ],
                   CloseReason = PipeTickCloseReason.GameTimeChanged
               };
    }

    /// <summary>
    /// Counts the items of every collection the reporter keeps in a field.
    /// </summary>
    /// <param name="reporter">The reporter</param>
    /// <returns>The number of items</returns>
    private static int Remembered(GameCatalogReporter reporter)
    {
        var count = 0;

        foreach (var field in typeof(GameCatalogReporter).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (field.GetValue(reporter) is IEnumerable items and not string)
            {
                count += items.Cast<object?>().Count();
            }
        }

        return count;
    }

    /// <summary>
    /// Creates the reporter of a test.
    /// </summary>
    /// <returns>The reporter</returns>
    private GameCatalogReporter CreateReporter()
    {
        return new GameCatalogReporter(() => _catalog, _logger);
    }

    /// <summary>
    /// Gets the logged messages.
    /// </summary>
    /// <returns>The messages</returns>
    private List<string> Messages()
    {
        return [.. _logger.Entries.Select(entry => entry.Message)];
    }

    #endregion // Helpers
}