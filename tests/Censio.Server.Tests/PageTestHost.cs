using System.Globalization;
using System.Text;

using AngleSharp.Dom;

using Bunit;

using Censio.Contracts;
using Censio.Server.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using MudBlazor.Interop;
using MudBlazor.Services;

namespace Censio.Server.Tests;

/// <summary>
/// A bUnit context for the data pages: MudBlazor's services, loose JavaScript interop, a restored <see cref="ServerState" />
/// over an in-memory database (or a given file), a <see cref="PageReader" /> over that state or over a fake source, a
/// <see cref="HistoryRetention" /> over that state registered by a late-bound factory as the server registers it, the chart
/// reads (<see cref="IHistoryTrends" /> over the database or a fake, and a <see cref="TrendReader" />), an interactive
/// renderer, a measured chart SVG and a configuration; pages get the language, its texts, the theme and whether they may
/// delete cascaded, as the frame cascades them.
/// </summary>
public sealed class PageTestHost : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The receive time of a tick at second 0.
    /// </summary>
    public const string BaseReceiveTimeText = "2026-11-13 12:00";

    /// <summary>
    /// The name under which the frame cascades whether a page may delete a profile's history.
    /// </summary>
    public const string DeletionAllowedName = "DeletionAllowed";

    /// <summary>
    /// The name under which the frame cascades the callback that takes a new chart choice.
    /// </summary>
    public const string ChooseChartName = "ChooseChart";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The longest a test waits for a page to show something.
    /// </summary>
    public static readonly TimeSpan WaitBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The receive time of a tick at second 0.
    /// </summary>
    private static readonly DateTimeOffset _baseReceiveTime = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The database of the state.
    /// </summary>
    private readonly ServerDatabase _database;

    /// <summary>
    /// The cancellation token of the test.
    /// </summary>
    private readonly CancellationToken _cancellationToken;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PageTestHost" /> class.
    /// </summary>
    /// <param name="database">The database of the state</param>
    /// <param name="state">The restored state</param>
    /// <param name="context">The bUnit context</param>
    /// <param name="configuration">The configuration the pages read</param>
    /// <param name="cancellationToken">The cancellation token of the test</param>
    private PageTestHost(ServerDatabase database, ServerState state, BunitContext context, ConfigurationManager configuration, CancellationToken cancellationToken)
    {
        _database = database;
        _cancellationToken = cancellationToken;
        State = state;
        Context = context;
        Configuration = configuration;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the bUnit context.
    /// </summary>
    public BunitContext Context { get; }

    /// <summary>
    /// Gets the server state the pages follow.
    /// </summary>
    public ServerState State { get; }

    /// <summary>
    /// Gets the configuration the pages read, registered as <see cref="IConfiguration" />; without a setting of its own.
    /// </summary>
    public ConfigurationManager Configuration { get; }

    /// <summary>
    /// Gets or sets a value indicating whether a page rendered by <see cref="Render{TPage}" /> may delete a profile's
    /// history, as the frame cascades it for a page opened by a trusted host; <see langword="true" /> unless a test sets it.
    /// </summary>
    public bool DeletionAllowed { get; set; } = true;

    /// <summary>
    /// Gets or sets the theme a page rendered by <see cref="Render{TPage}" /> gets cascaded, as the frame cascades it;
    /// <see cref="UiTheme.Light" /> unless a test sets it.
    /// </summary>
    public UiTheme Theme { get; set; } = UiTheme.Light;

    /// <summary>
    /// Gets or sets the chart choice a page rendered by <see cref="Render{TPage}" /> gets cascaded, as the frame cascades
    /// it; <see cref="ChartChoice.Default" /> unless a test sets it.
    /// </summary>
    public ChartChoice ChartChoice { get; set; } = ChartChoice.Default;

    /// <summary>
    /// Gets the choices the page rendered by <see cref="Render{TPage}" /> handed to the cascaded callback, in order.
    /// </summary>
    public List<ChartChoice> Chosen { get; } = [];

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Creates a host whose page reader reads the state, or the fake source when one is given.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token of the test</param>
    /// <param name="source">The fake source the page reader reads, or <see langword="null" /> for the state</param>
    /// <param name="databaseFile">The database file, or <see langword="null" /> for an in-memory database</param>
    /// <param name="trends">The fake chart reads, or <see langword="null" /> for the history store over the database</param>
    /// <param name="trendLogger">The logger of the chart reader, or <see langword="null" /> for none</param>
    /// <param name="configureServices">Registers further services before the renderer is set up, or <see langword="null" /> for none</param>
    /// <returns>The host</returns>
    public static async Task<PageTestHost> CreateAsync(CancellationToken cancellationToken, FakeProfileStateSource? source = null, string? databaseFile = null, FakeHistoryTrends? trends = null, ILogger<TrendReader>? trendLogger = null, Action<IServiceCollection>? configureServices = null)
    {
        var opened = await ServerDatabase.OpenAsync(databaseFile, NullLoggerFactory.Instance, cancellationToken).ConfigureAwait(false);
        var database = opened.Database ?? throw new InvalidOperationException($"The database could not be opened: {opened.Kind}", opened.Failure);
        var state = new ServerState(new ServerStore(database), NullLogger.Instance);

        await state.RestoreAsync(cancellationToken).ConfigureAwait(false);

        var context = new BunitContext();
        var configuration = new ConfigurationManager();

        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.Setup<ElementSize>("mudGetSvgBBox", _ => true).SetResult(new ElementSize
                                                                                   {
                                                                                       Width = 40,
                                                                                       Height = 12
                                                                                   });
        context.Services.AddMudServices();
        context.Services.AddSingleton(state);
        context.Services.AddSingleton<IProfileStateSource>(state);
        context.Services.AddSingleton(new PageReader(source ?? (IProfileStateSource)state));
        context.Services.AddSingleton<IConfiguration>(configuration);
        context.Services.AddSingleton(_ => new HistoryRetention(state, new HistoryStore(database), new FakeTimeProvider(_baseReceiveTime), NullLogger.Instance));

        IHistoryTrends historyTrends = trends is null ? new HistoryStore(database) : trends;

        context.Services.AddSingleton(historyTrends);
        context.Services.AddSingleton(provider => new TrendReader(provider.GetRequiredService<IHistoryTrends>(), trendLogger ?? NullLogger<TrendReader>.Instance));

        configureServices?.Invoke(context.Services);
        context.SetRendererInfo(new RendererInfo("Server", true));

        return new PageTestHost(database, state, context, configuration, cancellationToken);
    }

    /// <summary>
    /// Gives the JSON of a catalog good.
    /// </summary>
    /// <param name="assetGuid">The asset GUID</param>
    /// <param name="english">The English name, or <see langword="null" /></param>
    /// <param name="german">The German name, or <see langword="null" /></param>
    /// <param name="icon">The icon reference, or <see langword="null" /></param>
    /// <returns>The JSON</returns>
    public static string GoodEntry(int assetGuid, string? english, string? german, string? icon)
    {
        return $"{{\"guid\":{Number(assetGuid)},\"kind\":\"good\",\"names\":{Names(english, german)},\"regionGuid\":null,\"regionTypes\":null,\"icon\":{Json(icon)}}}";
    }

    /// <summary>
    /// Gives the JSON of a catalog region entry.
    /// </summary>
    /// <param name="sessionGuid">The session GUID of the entry</param>
    /// <param name="regionGuid">The region GUID</param>
    /// <param name="english">The English name, or <see langword="null" /></param>
    /// <param name="german">The German name, or <see langword="null" /></param>
    /// <returns>The JSON</returns>
    public static string RegionEntry(int sessionGuid, int regionGuid, string? english, string? german)
    {
        return $"{{\"guid\":{Number(sessionGuid)},\"kind\":\"region\",\"names\":{Names(english, german)},\"regionGuid\":{Number(regionGuid)},\"regionTypes\":[\"Sample\"],\"icon\":null}}";
    }

    /// <summary>
    /// Creates a tick.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveSeconds">The receive time in seconds after <see cref="BaseReceiveTimeText" /></param>
    /// <param name="islands">The islands</param>
    /// <returns>The tick</returns>
    public static ContractTick Tick(Guid profileId, long receiveSeconds, params ContractTickIsland[] islands)
    {
        return new ContractTick
               {
                   Id = Guid.NewGuid(),
                   ProfileId = profileId,
                   ReceiveTime = _baseReceiveTime.AddSeconds(receiveSeconds),
                   GameTime = 3_600_000 + receiveSeconds,
                   Islands = islands
               };
    }

    /// <summary>
    /// Creates an island in area 0.
    /// </summary>
    /// <param name="sessionGuid">The session GUID</param>
    /// <param name="islandId">The island identifier</param>
    /// <param name="name">The island name</param>
    /// <param name="entries">The entries</param>
    /// <returns>The island</returns>
    public static ContractTickIsland Island(int sessionGuid, byte islandId, string name, params ContractTickEntry[] entries)
    {
        return new ContractTickIsland
               {
                   SessionGuid = sessionGuid,
                   IslandId = islandId,
                   AreaIndex = 0,
                   SessionId = 1,
                   Name = name,
                   Entries = entries
               };
    }

    /// <summary>
    /// Creates an entry with the values a page shows.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="generation">The production</param>
    /// <param name="consumption">The consumption</param>
    /// <param name="delta">The delta</param>
    /// <param name="buildings">The building count</param>
    /// <param name="productivity">The average productivity in percent</param>
    /// <returns>The entry</returns>
    public static ContractTickEntry Entry(int productGuid, float? generation, float? consumption, float? delta, int buildings, float? productivity)
    {
        return new ContractTickEntry
               {
                   ProductGuid = productGuid,
                   Generation = generation,
                   Consumption = consumption,
                   Delta = delta,
                   PerfectGeneration = null,
                   PerfectConsumption = null,
                   BuildingCount = buildings,
                   Maintenance = 0,
                   Income = null,
                   Profit = 0,
                   SummedProductivity = null,
                   AverageProductivity = productivity,
                   WorkforceAmounts = new Dictionary<int, int>(),
                   BuildingAmounts = new Dictionary<int, int>()
               };
    }

    /// <summary>
    /// Gives the trimmed texts of the cells of each body row of the page's tables.
    /// </summary>
    /// <typeparam name="TPage">The type of the page</typeparam>
    /// <param name="page">The rendered page</param>
    /// <returns>The rows</returns>
    public static List<List<string>> Rows<TPage>(IRenderedComponent<TPage> page)
        where TPage : IComponent
    {
        return [.. page.FindAll("tbody tr").Select(row => row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToList())];
    }

    /// <summary>
    /// Gives the trimmed texts of the header cells of the page's tables.
    /// </summary>
    /// <typeparam name="TPage">The type of the page</typeparam>
    /// <param name="page">The rendered page</param>
    /// <returns>The header texts</returns>
    public static List<string> Headers<TPage>(IRenderedComponent<TPage> page)
        where TPage : IComponent
    {
        return [.. page.FindAll("thead th").Select(cell => cell.TextContent.Trim())];
    }

    /// <summary>
    /// Gives the addresses of the links in the body rows of the page's tables.
    /// </summary>
    /// <typeparam name="TPage">The type of the page</typeparam>
    /// <param name="page">The rendered page</param>
    /// <returns>The addresses</returns>
    public static List<string?> RowLinks<TPage>(IRenderedComponent<TPage> page)
        where TPage : IComponent
    {
        return [.. page.FindAll("tbody tr a").Select(link => link.GetAttribute("href"))];
    }

    /// <summary>
    /// Describes each body row of the page's tables as its cell texts joined by <c>|</c>.
    /// </summary>
    /// <typeparam name="TPage">The type of the page</typeparam>
    /// <param name="page">The rendered page</param>
    /// <returns>The rows</returns>
    public static List<string> Describe<TPage>(IRenderedComponent<TPage> page)
        where TPage : IComponent
    {
        return [.. Rows(page).Select(cells => string.Join('|', cells))];
    }

    /// <summary>
    /// Gives the text of the rendered page.
    /// </summary>
    /// <typeparam name="TPage">The type of the page</typeparam>
    /// <param name="page">The rendered page</param>
    /// <returns>The text</returns>
    public static string Text<TPage>(IRenderedComponent<TPage> page)
        where TPage : IComponent
    {
        return string.Concat(page.Nodes.Select(node => node.TextContent));
    }

    /// <summary>
    /// Gives the colour a cell shows its value in: <c>error</c>, <c>success</c> or <c>none</c>, by MudBlazor's text colour
    /// classes on the cell or an element inside it.
    /// </summary>
    /// <param name="cell">The cell</param>
    /// <returns>The colour</returns>
    public static string Colour(IElement cell)
    {
        var elements = cell.QuerySelectorAll("*").Prepend(cell).ToList();
        var error = elements.Any(element => element.ClassList.Contains("mud-error-text"));
        var success = elements.Any(element => element.ClassList.Contains("mud-success-text"));

        return (error, success) switch
               {
                   (true, false) => "error",
                   (false, true) => "success",
                   (false, false) => "none",
                   _ => "both"
               };
    }

    /// <summary>
    /// Gives a list of expected lines, so that a test passes no constant array.
    /// </summary>
    /// <param name="lines">The lines</param>
    /// <returns>The list</returns>
    public static List<string> Expect(params string[] lines)
    {
        return [.. lines];
    }

    /// <summary>
    /// Clicks an item of a toggle group of the chart controls.
    /// </summary>
    /// <typeparam name="TComponent">The type of the rendered component</typeparam>
    /// <param name="component">The rendered component</param>
    /// <param name="group">The accessible name of the group</param>
    /// <param name="item">The text of the item</param>
    /// <returns>A task that completes when the click was handled</returns>
    public static async Task ChooseAsync<TComponent>(IRenderedComponent<TComponent> component, string group, string item)
        where TComponent : IComponent
    {
        var button = component.FindAll($"[role='group'][aria-label='{group}'] button").Single(candidate => candidate.TextContent.Trim() == item);

        await button.ClickAsync(new MouseEventArgs()).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the texts of the items of a toggle group of the chart controls.
    /// </summary>
    /// <typeparam name="TComponent">The type of the rendered component</typeparam>
    /// <param name="component">The rendered component</param>
    /// <param name="group">The accessible name of the group</param>
    /// <returns>The texts, in order</returns>
    public static List<string> Items<TComponent>(IRenderedComponent<TComponent> component, string group)
        where TComponent : IComponent
    {
        return [.. component.FindAll($"[role='group'][aria-label='{group}'] button").Select(button => button.TextContent.Trim())];
    }

    /// <summary>
    /// Gives the texts of the selected items of a toggle group of the chart controls.
    /// </summary>
    /// <typeparam name="TComponent">The type of the rendered component</typeparam>
    /// <param name="component">The rendered component</param>
    /// <param name="group">The accessible name of the group</param>
    /// <returns>The texts of the selected items</returns>
    public static List<string> Selected<TComponent>(IRenderedComponent<TComponent> component, string group)
        where TComponent : IComponent
    {
        return [.. component.FindAll($"[role='group'][aria-label='{group}'] button[aria-checked='true']").Select(button => button.TextContent.Trim())];
    }

    /// <summary>
    /// Renders a page with the language, its texts, <see cref="Theme" />, <see cref="DeletionAllowed" />, <see cref="ChartChoice" /> and a callback that records into <see cref="Chosen" /> cascaded, as the frame cascades them.
    /// </summary>
    /// <typeparam name="TPage">The type of the page</typeparam>
    /// <param name="language">The language shown</param>
    /// <param name="parameters">The page's parameters</param>
    /// <returns>The rendered page</returns>
    public IRenderedComponent<TPage> Render<TPage>(UiLanguage language, Action<ComponentParameterCollectionBuilder<TPage>> parameters)
        where TPage : IComponent
    {
        return Context.Render<TPage>(builder =>
                                     {
                                         builder.AddCascadingValue(language)
                                                .AddCascadingValue(Theme)
                                                .AddCascadingValue(UiTexts.For(language))
                                                .AddCascadingValue(DeletionAllowedName, DeletionAllowed)
                                                .AddCascadingValue(ChartChoice)
                                                .AddCascadingValue(ChooseChartName, EventCallback.Factory.Create<ChartChoice>(this, Chosen.Add));
                                         parameters(builder);
                                     });
    }

    /// <summary>
    /// Stores a profile with its name.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="name">The profile name</param>
    /// <returns>A task that completes when the profile is stored</returns>
    public async Task StoreProfileAsync(Guid profileId, string name)
    {
        var result = await State.StoreProfileAsync(profileId, name, _cancellationToken).ConfigureAwait(false);

        EnsureStored(result.Outcome, "profile");
    }

    /// <summary>
    /// Stores ticks in order.
    /// </summary>
    /// <param name="ticks">The ticks</param>
    /// <returns>A task that completes when the ticks are stored</returns>
    public async Task StoreTicksAsync(params ContractTick[] ticks)
    {
        foreach (var tick in ticks)
        {
            var result = await State.StoreTickAsync(tick, _cancellationToken).ConfigureAwait(false);

            EnsureStored(result.Outcome, "tick");
        }
    }

    /// <summary>
    /// Stores a catalog built from entries in the JSON of the catalog document.
    /// </summary>
    /// <param name="entries">The JSON of the entries</param>
    /// <returns>A task that completes when the catalog is stored</returns>
    public async Task StoreCatalogAsync(params string[] entries)
    {
        var bytes = Encoding.UTF8.GetBytes($"{{\"schemaVersion\":1,\"gameVersion\":null,\"entries\":[{string.Join(',', entries)}]}}");
        var document = CatalogDocumentReader.Read(bytes).Document ?? throw new InvalidOperationException("The test catalog should be read");
        var upload = new ContractCatalogUpload
                     {
                         Id = Guid.NewGuid(),
                         Document = bytes
                     };
        var result = await State.StoreCatalogAsync(upload, document, _cancellationToken).ConfigureAwait(false);

        EnsureStored(result.Result.Outcome, "catalog");
    }

    /// <summary>
    /// Stores an icon under a reference.
    /// </summary>
    /// <param name="reference">The icon reference</param>
    /// <returns>A task that completes when the icon is stored</returns>
    public async Task StoreIconAsync(string reference)
    {
        var icon = new ContractIconImage
                   {
                       Id = Guid.NewGuid(),
                       Reference = reference,
                       Png = Encoding.UTF8.GetBytes("png " + reference)
                   };
        var result = await State.StoreIconAsync(icon, _cancellationToken).ConfigureAwait(false);

        EnsureStored(result.Outcome, "icon");
    }

    /// <summary>
    /// Fails when a store did not store.
    /// </summary>
    /// <param name="outcome">The outcome of the store</param>
    /// <param name="what">What was stored</param>
    private static void EnsureStored(ServerStoreOutcome outcome, string what)
    {
        if (outcome != ServerStoreOutcome.Stored)
        {
            throw new InvalidOperationException($"The test {what} was not stored: {outcome}");
        }
    }

    /// <summary>
    /// Gives the JSON of the names of an entry.
    /// </summary>
    /// <param name="english">The English name, or <see langword="null" /></param>
    /// <param name="german">The German name, or <see langword="null" /></param>
    /// <returns>The JSON</returns>
    private static string Names(string? english, string? german)
    {
        var names = new List<string>();

        if (english is not null)
        {
            names.Add($"\"en\":{Json(english)}");
        }

        if (german is not null)
        {
            names.Add($"\"de\":{Json(german)}");
        }

        return $"{{{string.Join(',', names)}}}";
    }

    /// <summary>
    /// Gives the JSON of a string, or <c>null</c>.
    /// </summary>
    /// <param name="value">The string, or <see langword="null" /></param>
    /// <returns>The JSON</returns>
    private static string Json(string? value)
    {
        return value is null
                   ? "null"
                   : System.Text.Json.JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// Gives a number in the invariant culture.
    /// </summary>
    /// <param name="value">The number</param>
    /// <returns>The text</returns>
    private static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync().ConfigureAwait(false);
        Configuration.Dispose();
        _database.Dispose();
        SqliteConnection.ClearAllPools();
    }

    #endregion // IAsyncDisposable
}