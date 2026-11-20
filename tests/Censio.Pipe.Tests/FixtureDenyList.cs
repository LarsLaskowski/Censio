using System.Globalization;
using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// The local deny list of the fixtures and the check of the fixtures against it: every non-empty name (figure, island,
/// route, folder) must be an invented name and equal no denied name ignoring case, and every non-zero finite economic
/// value and trade-route amount must differ from the value at the same place of the fixture's original recording.
/// </summary>
public sealed class FixtureDenyList
{
    #region Constants

    /// <summary>
    /// The environment variable that holds the path of the local deny list.
    /// </summary>
    public const string VariableName = "CENSIO_FIXTURE_DENY_LIST";

    /// <summary>
    /// The start of a line that denies a name.
    /// </summary>
    private const string DenyPrefix = "deny ";

    /// <summary>
    /// The start of a line that names a fixture's original recording.
    /// </summary>
    private const string OriginalPrefix = "original ";

    /// <summary>
    /// The start of an invented island name.
    /// </summary>
    private const string IslandPrefix = "Island ";

    /// <summary>
    /// The start of an invented figure name.
    /// </summary>
    private const string PlayerPrefix = "Player ";

    /// <summary>
    /// The start of an invented route name.
    /// </summary>
    private const string RoutePrefix = "Route ";

    /// <summary>
    /// The start of an invented folder name.
    /// </summary>
    private const string FolderPrefix = "Folder ";

    /// <summary>
    /// The character a reader puts in place of an invalid UTF-8 byte.
    /// </summary>
    private const char ReplacementCharacter = (char)0xFFFD;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The denied names.
    /// </summary>
    private readonly List<string> _denied = [];

    /// <summary>
    /// The full path of each fixture's original, by fixture file name.
    /// </summary>
    private readonly Dictionary<string, string> _originals = [with(StringComparer.Ordinal)];

    /// <summary>
    /// The line number of each original line, by fixture file name.
    /// </summary>
    private readonly Dictionary<string, int> _originalLines = [with(StringComparer.Ordinal)];

    /// <summary>
    /// The numbers of the lines that are not understood.
    /// </summary>
    private readonly List<int> _invalidLines = [];

    /// <summary>
    /// Whether every fixture needs an original line.
    /// </summary>
    private readonly bool _requiresOriginals;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FixtureDenyList" /> class.
    /// </summary>
    /// <param name="requiresOriginals">Whether every fixture needs an original line</param>
    private FixtureDenyList(bool requiresOriginals)
    {
        _requiresOriginals = requiresOriginals;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the folder the fixtures are copied to next to the tests.
    /// </summary>
    public static string FixturesFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    /// Gets a deny list without entries, which checks only that every name is an invented name.
    /// </summary>
    public static FixtureDenyList Empty => new(false);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Reads a deny list: lines <c>deny &lt;name&gt;</c> and <c>original &lt;fixture file name&gt; &lt;path&gt;</c>; empty
    /// lines and lines starting with <c>#</c> are skipped.
    /// </summary>
    /// <param name="text">The text of the deny list</param>
    /// <param name="baseFolder">The folder a relative original path is resolved against</param>
    /// <returns>The deny list</returns>
    public static FixtureDenyList Parse(string text, string baseFolder)
    {
        var list = new FixtureDenyList(true);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            list.ReadLine(lines[index], index + 1, baseFolder);
        }

        return list;
    }

    /// <summary>
    /// Reads a deny list file in UTF-8.
    /// </summary>
    /// <param name="path">The path of the file</param>
    /// <param name="cancellationToken">The token that cancels the read</param>
    /// <returns>The deny list</returns>
    public static async Task<FixtureDenyList> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        return Parse(text, Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty);
    }

    /// <summary>
    /// Checks every <c>*.jsonl</c> fixture of a folder.
    /// </summary>
    /// <param name="fixturesFolder">The folder of the fixtures</param>
    /// <param name="cancellationToken">The token that cancels the check</param>
    /// <returns>The report</returns>
    public async Task<FixtureDenyListReport> CheckAsync(string fixturesFolder, CancellationToken cancellationToken)
    {
        var report = new FixtureDenyListReport();
        var fixtures = Directory.GetFiles(fixturesFolder, "*.jsonl").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList();

        foreach (var line in _invalidLines)
        {
            report.AddFinding(string.Create(CultureInfo.InvariantCulture, $"deny list line {line}: not understood"));
        }

        foreach (var unknown in _originalLines.Where(pair => fixtures.Contains(pair.Key, StringComparer.Ordinal) == false).Select(pair => pair.Value).Order())
        {
            report.AddFinding(string.Create(CultureInfo.InvariantCulture, $"deny list line {unknown}: names no fixture"));
        }

        foreach (var fixture in fixtures)
        {
            report.AddFixture();

            await CheckFixtureAsync(fixturesFolder, fixture, report, cancellationToken).ConfigureAwait(false);
        }

        return report;
    }

    /// <summary>
    /// Checks every trade-route and item-offer frame of a fixture's original: it must decode, end exactly at the frame's
    /// end and hold no U+FFFD in a route or folder name. Findings name the line, route and field, never a value.
    /// </summary>
    /// <param name="fixture">The fixture file name whose original is checked</param>
    /// <param name="cancellationToken">The token that cancels the check</param>
    /// <returns>The report</returns>
    public async Task<FixtureDenyListReport> CheckOriginalTradeFramesAsync(string fixture, CancellationToken cancellationToken)
    {
        var report = new FixtureDenyListReport();

        if (_originals.TryGetValue(fixture, out var path) == false)
        {
            report.AddFinding($"{fixture}: no original in the deny list");

            return report;
        }

        await using var stream = TryOpen(path);

        if (stream is null)
        {
            report.AddFinding($"{fixture}: the original is missing or unreadable");

            return report;
        }

        var reader = new PipeRecordingReader(stream);
        var line = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        while (line.Kind == PipeRecordingLineKind.Frame)
        {
            CheckOriginalTradeFrame(report, string.Create(CultureInfo.InvariantCulture, $"{fixture}: original line {line.LineNumber}"), line.Frame);

            line = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        if (line.Kind == PipeRecordingLineKind.Problem)
        {
            report.AddFinding(string.Create(CultureInfo.InvariantCulture, $"{fixture}: the original is not readable ({line.Problem?.Kind} in line {line.Problem?.LineNumber})"));
        }

        return report;
    }

    /// <summary>
    /// Checks one frame of an original when it is a trade-route or item-offer frame.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture and the original's line</param>
    /// <param name="frame">The frame</param>
    private static void CheckOriginalTradeFrame(FixtureDenyListReport report, string place, byte[] frame)
    {
        if (frame.Length == 0 || frame[0] is not ((byte)PipeMessageType.TradeRoutes or (byte)PipeMessageType.ItemOffers))
        {
            return;
        }

        report.AddTradeFrame();

        var result = PipeMessageParser.Parse(frame, out var consumed);

        if (result.Message is null)
        {
            report.AddFinding($"{place}: frame does not decode");

            return;
        }

        if (consumed != frame.Length)
        {
            report.AddFinding($"{place}: frame has bytes after its last field");
        }

        if (result.Message is not TradeRoutesMessage routes)
        {
            return;
        }

        for (var index = 0; index < routes.Routes.Count; index++)
        {
            var routePlace = string.Create(CultureInfo.InvariantCulture, $"{place}: route {index + 1}");

            CheckReplacement(report, routePlace, routes.Routes[index].Name, "route");
            CheckReplacement(report, routePlace, routes.Routes[index].Folder, "folder");
        }
    }

    /// <summary>
    /// Reports a name of an original that holds U+FFFD, by place and kind only.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture, the original's line and the route</param>
    /// <param name="name">The name</param>
    /// <param name="kind">The kind of the name, for the finding</param>
    private static void CheckReplacement(FixtureDenyListReport report, string place, string name, string kind)
    {
        if (name.Contains(ReplacementCharacter, StringComparison.Ordinal))
        {
            report.AddFinding($"{place}: {kind} name holds U+FFFD");
        }
    }

    /// <summary>
    /// Compares the amounts of two trade-route messages of the same line: per route the problem, ship and lost-ship
    /// counts, per good its amount unless the original's is negative (kept by design).
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture and line</param>
    /// <param name="routes">The fixture's message</param>
    /// <param name="original">The original's message</param>
    private static void CompareTradeRoutes(FixtureDenyListReport report, string place, TradeRoutesMessage routes, TradeRoutesMessage original)
    {
        if (HasSameShape(routes, original) == false)
        {
            report.AddFinding($"{place}: frame differs in structure from the original");

            return;
        }

        for (var index = 0; index < routes.Routes.Count; index++)
        {
            CompareRoute(report, string.Create(CultureInfo.InvariantCulture, $"{place}: route {index + 1}"), routes.Routes[index], original.Routes[index]);
        }
    }

    /// <summary>
    /// Checks that two trade-route messages have the same number of routes, stations per route and goods per station.
    /// </summary>
    /// <param name="routes">The fixture's message</param>
    /// <param name="original">The original's message</param>
    /// <returns><see langword="true" /> when the shape is the same</returns>
    private static bool HasSameShape(TradeRoutesMessage routes, TradeRoutesMessage original)
    {
        return routes.Routes.Count == original.Routes.Count
               && routes.Routes.Zip(original.Routes)
                               .All(pair => pair.First.Stations.Count == pair.Second.Stations.Count
                                            && pair.First.Stations.Zip(pair.Second.Stations).All(stations => stations.First.Goods.Count == stations.Second.Goods.Count));
    }

    /// <summary>
    /// Compares the amounts of two routes.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture, line and route</param>
    /// <param name="route">The fixture's route</param>
    /// <param name="original">The original's route</param>
    private static void CompareRoute(FixtureDenyListReport report, string place, TradeRoute route, TradeRoute original)
    {
        Compare(report, place, nameof(TradeRoute.MajorProblems), route.MajorProblems, original.MajorProblems);
        Compare(report, place, nameof(TradeRoute.MinorProblems), route.MinorProblems, original.MinorProblems);
        Compare(report, place, nameof(TradeRoute.Ships), route.Ships, original.Ships);
        Compare(report, place, nameof(TradeRoute.LostShips), route.LostShips, original.LostShips);

        for (var station = 0; station < route.Stations.Count; station++)
        {
            var goods = route.Stations[station].Goods;
            var originalGoods = original.Stations[station].Goods;

            for (var good = 0; good < goods.Count; good++)
            {
                if (originalGoods[good].Amount >= 0)
                {
                    Compare(report, string.Create(CultureInfo.InvariantCulture, $"{place}: station {station + 1}: good {good + 1}"), nameof(TradeRouteGood.Amount), goods[good].Amount, originalGoods[good].Amount);
                }
            }
        }
    }

    /// <summary>
    /// Checks whether a name is an invented name: the prefix and a number from 1 without leading zeros.
    /// </summary>
    /// <param name="name">The name</param>
    /// <param name="prefix">The prefix of the invented names of its kind</param>
    /// <returns><see langword="true" /> when the name is invented</returns>
    private static bool IsInvented(string name, string prefix)
    {
        if (name.StartsWith(prefix, StringComparison.Ordinal) == false)
        {
            return false;
        }

        var number = name[prefix.Length..];

        return number.Length > 0 && number[0] != '0' && number.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Opens a recording for reading, or gives <see langword="null" /> when it cannot be opened.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>The stream, or <see langword="null" /></returns>
    private static FileStream? TryOpen(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Compares one value with the original's, bit for bit, and records it; zero and non-finite originals are skipped.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture, line and entry of the value</param>
    /// <param name="field">The field name</param>
    /// <param name="value">The fixture's value</param>
    /// <param name="original">The original's value</param>
    private static void Compare(FixtureDenyListReport report, string place, string field, double value, double original)
    {
        if (original == 0 || double.IsFinite(original) == false)
        {
            return;
        }

        report.AddComparedValue();

        if (BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(original))
        {
            report.AddFinding($"{place}: {field} equals the original");
        }
    }

    /// <summary>
    /// Compares the economic values of two entries.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture, line and entry</param>
    /// <param name="entry">The fixture's entry</param>
    /// <param name="original">The original's entry</param>
    private static void CompareEntry(FixtureDenyListReport report, string place, ProductionEntry entry, ProductionEntry original)
    {
        Compare(report, place, nameof(ProductionEntry.Generation), entry.Generation, original.Generation);
        Compare(report, place, nameof(ProductionEntry.Consumption), entry.Consumption, original.Consumption);
        Compare(report, place, nameof(ProductionEntry.Delta), entry.Delta, original.Delta);
        Compare(report, place, nameof(ProductionEntry.PerfectGeneration), entry.PerfectGeneration, original.PerfectGeneration);
        Compare(report, place, nameof(ProductionEntry.PerfectConsumption), entry.PerfectConsumption, original.PerfectConsumption);
        Compare(report, place, nameof(ProductionEntry.Maintenance), entry.Maintenance, original.Maintenance);
        Compare(report, place, nameof(ProductionEntry.Income), entry.Income, original.Income);
        Compare(report, place, nameof(ProductionEntry.Profit), entry.Profit, original.Profit);
        Compare(report, place, nameof(ProductionEntry.SummedProductivity), entry.SummedProductivity, original.SummedProductivity);
        Compare(report, place, nameof(ProductionEntry.AverageProductivity), entry.AverageProductivity, original.AverageProductivity);

        foreach (var pair in original.WorkforceAmounts)
        {
            Compare(report, place, nameof(ProductionEntry.WorkforceAmounts), entry.WorkforceAmounts.TryGetValue(pair.Key, out var amount) ? amount : 0, pair.Value);
        }
    }

    /// <summary>
    /// Compares the economic values of two frames of the same line.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture and line</param>
    /// <param name="frame">The fixture's frame</param>
    /// <param name="original">The original's frame</param>
    private static void CompareFrames(FixtureDenyListReport report, string place, byte[] frame, byte[] original)
    {
        var message = PipeMessageParser.Parse(frame).Message;
        var originalMessage = PipeMessageParser.Parse(original).Message;

        if (message?.MessageType != originalMessage?.MessageType)
        {
            report.AddFinding($"{place}: frame differs in structure from the original");

            return;
        }

        if (message is TradeRoutesMessage routes && originalMessage is TradeRoutesMessage originalRoutes)
        {
            CompareTradeRoutes(report, place, routes, originalRoutes);

            return;
        }

        if (message is not AreaProductionStatisticsMessage area || originalMessage is not AreaProductionStatisticsMessage originalArea)
        {
            return;
        }

        if (area.Entries.Count != originalArea.Entries.Count)
        {
            report.AddFinding($"{place}: frame differs in structure from the original");

            return;
        }

        for (var index = 0; index < area.Entries.Count; index++)
        {
            CompareEntry(report, string.Create(CultureInfo.InvariantCulture, $"{place}: entry {index + 1}"), area.Entries[index], originalArea.Entries[index]);
        }
    }

    /// <summary>
    /// Checks that the original's line is readable and of the same kind as the fixture's line.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="fixture">The fixture file name</param>
    /// <param name="line">The fixture's line</param>
    /// <param name="originalLine">The original's line</param>
    /// <returns><c>true</c> if the original can still be compared, <c>false</c> if it is dropped</returns>
    private static bool MatchesOriginal(FixtureDenyListReport report, string fixture, PipeRecordingLine line, PipeRecordingLine originalLine)
    {
        if (originalLine.Kind == PipeRecordingLineKind.Problem)
        {
            report.AddFinding($"{fixture}: the original is missing or unreadable");

            return false;
        }

        if (originalLine.Kind != line.Kind)
        {
            report.AddFinding(originalLine.Kind == PipeRecordingLineKind.Frame
                                  ? $"{fixture}: the original has more lines"
                                  : string.Create(CultureInfo.InvariantCulture, $"{fixture}: line {line.LineNumber}: the original has no matching frame"));

            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads one line of the deny list.
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="number">The one-based line number</param>
    /// <param name="baseFolder">The folder a relative original path is resolved against</param>
    private void ReadLine(string line, int number, string baseFolder)
    {
        if (line.Length == 0 || line.StartsWith('#'))
        {
            return;
        }

        if (line.StartsWith(DenyPrefix, StringComparison.Ordinal) && line.Length > DenyPrefix.Length)
        {
            _denied.Add(line[DenyPrefix.Length..]);

            return;
        }

        var parts = line.StartsWith(OriginalPrefix, StringComparison.Ordinal) ? line[OriginalPrefix.Length..].Split(' ', 2) : [];

        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 && _originals.ContainsKey(parts[0]) == false)
        {
            _originals[parts[0]] = Path.GetFullPath(parts[1], baseFolder);
            _originalLines[parts[0]] = number;

            return;
        }

        _invalidLines.Add(number);
    }

    /// <summary>
    /// Checks a name of a fixture.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture and line</param>
    /// <param name="name">The name</param>
    /// <param name="prefix">The prefix of the invented names of its kind</param>
    /// <param name="kind">The kind of the name, for the finding</param>
    private void CheckName(FixtureDenyListReport report, string place, string name, string prefix, string kind)
    {
        if (name.Length == 0)
        {
            return;
        }

        report.AddName();

        if (IsInvented(name, prefix) == false)
        {
            report.AddFinding($"{place}: {kind} name is not an invented name");
        }

        if (_denied.Any(denied => string.Equals(denied, name, StringComparison.OrdinalIgnoreCase)))
        {
            report.AddFinding($"{place}: {kind} name is denied");
        }
    }

    /// <summary>
    /// Checks the names of a frame.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture and line</param>
    /// <param name="frame">The frame</param>
    private void CheckNames(FixtureDenyListReport report, string place, byte[] frame)
    {
        switch (PipeMessageParser.Parse(frame).Message)
        {
            case SessionStartMessage start:
                CheckName(report, place, start.Headline, PlayerPrefix, "figure");
                break;

            case AreaProductionStatisticsMessage area:
                CheckName(report, place, area.AreaName, IslandPrefix, "island");
                break;

            case TradeRoutesMessage routes:
                CheckRouteNames(report, place, routes);
                break;
        }
    }

    /// <summary>
    /// Checks the route and folder names of a trade-route message.
    /// </summary>
    /// <param name="report">The report</param>
    /// <param name="place">The fixture and line</param>
    /// <param name="routes">The message</param>
    private void CheckRouteNames(FixtureDenyListReport report, string place, TradeRoutesMessage routes)
    {
        for (var index = 0; index < routes.Routes.Count; index++)
        {
            var routePlace = string.Create(CultureInfo.InvariantCulture, $"{place}: route {index + 1}");

            CheckName(report, routePlace, routes.Routes[index].Name, RoutePrefix, "route");
            CheckName(report, routePlace, routes.Routes[index].Folder, FolderPrefix, "folder");
        }
    }

    /// <summary>
    /// Checks one fixture: its names, and its values against its original when the deny list names one.
    /// </summary>
    /// <param name="fixturesFolder">The folder of the fixtures</param>
    /// <param name="fixture">The fixture file name</param>
    /// <param name="report">The report</param>
    /// <param name="cancellationToken">The token that cancels the check</param>
    /// <returns>The task</returns>
    private async Task CheckFixtureAsync(string fixturesFolder, string fixture, FixtureDenyListReport report, CancellationToken cancellationToken)
    {
        FileStream? originalStream = null;

        if (_originals.TryGetValue(fixture, out var originalPath))
        {
            originalStream = TryOpen(originalPath);

            if (originalStream is null)
            {
                report.AddFinding($"{fixture}: the original is missing or unreadable");
            }
        }
        else if (_requiresOriginals)
        {
            report.AddFinding($"{fixture}: no original in the deny list");
        }

        await using var fixtureStream = new FileStream(Path.Combine(fixturesFolder, fixture), FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var original = originalStream;

        await WalkAsync(fixture, new PipeRecordingReader(fixtureStream), original is null ? null : new PipeRecordingReader(original), report, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Walks a fixture and its original line by line.
    /// </summary>
    /// <param name="fixture">The fixture file name</param>
    /// <param name="reader">The fixture's reader</param>
    /// <param name="original">The original's reader, if any</param>
    /// <param name="report">The report</param>
    /// <param name="cancellationToken">The token that cancels the check</param>
    /// <returns>The task</returns>
    private async Task WalkAsync(string fixture, PipeRecordingReader reader, PipeRecordingReader? original, FixtureDenyListReport report, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var originalLine = original is null ? null : await original.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (line.Kind == PipeRecordingLineKind.Problem)
            {
                report.AddFinding(string.Create(CultureInfo.InvariantCulture, $"{fixture}: not readable ({line.Problem?.Kind} in line {line.Problem?.LineNumber})"));

                return;
            }

            if (originalLine is not null && MatchesOriginal(report, fixture, line, originalLine) == false)
            {
                original = null;
            }

            if (line.Kind == PipeRecordingLineKind.End)
            {
                return;
            }

            var place = string.Create(CultureInfo.InvariantCulture, $"{fixture}: line {line.LineNumber}");

            CheckNames(report, place, line.Frame);

            if (original is not null && originalLine is not null)
            {
                CompareFrames(report, place, line.Frame, originalLine.Frame);
            }
        }
    }

    #endregion // Methods
}