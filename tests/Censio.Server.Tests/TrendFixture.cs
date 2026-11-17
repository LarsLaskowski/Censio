using Censio.Companion.Core;
using Censio.Contracts;
using Censio.Pipe;
using Censio.Server.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Censio.Server.Tests;

/// <summary>
/// The pipe fixture <c>long-session.jsonl</c> replayed into the history of an in-memory database, with the chart reader over
/// it: the ticks as the Companion sends them, stored as the server stores them, before any retention run.
/// </summary>
public sealed class TrendFixture : IDisposable
{
    #region Fields

    /// <summary>
    /// The profile the fixture is stored under.
    /// </summary>
    public static readonly Guid ProfileId = new("7e57f1c7-0000-4000-8000-000000000002");

    /// <summary>
    /// The database of the history.
    /// </summary>
    private readonly ServerDatabase _database;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendFixture" /> class.
    /// </summary>
    /// <param name="database">The database of the history</param>
    /// <param name="ticks">The ticks in replay order</param>
    private TrendFixture(ServerDatabase database, IReadOnlyList<ContractTick> ticks)
    {
        _database = database;
        Ticks = ticks;
        History = new HistoryStore(database);
        Reader = new TrendReader(History, NullLogger<TrendReader>.Instance);
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the ticks of the fixture in replay order.
    /// </summary>
    public IReadOnlyList<ContractTick> Ticks { get; }

    /// <summary>
    /// Gets the history store over the database.
    /// </summary>
    public HistoryStore History { get; }

    /// <summary>
    /// Gets the chart reader over the history store.
    /// </summary>
    public TrendReader Reader { get; }

    /// <summary>
    /// Gets the receive time of the latest tick.
    /// </summary>
    public DateTimeOffset LatestTick => Ticks[^1].ReceiveTime;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Replays the fixture and stores its ticks.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The fixture</returns>
    public static async Task<TrendFixture> CreateAsync(CancellationToken cancellationToken)
    {
        var ticks = await ReplayAsync(cancellationToken).ConfigureAwait(false);
        var opened = await ServerDatabase.OpenAsync(null, NullLoggerFactory.Instance, cancellationToken).ConfigureAwait(false);
        var database = opened.Database ?? throw new InvalidOperationException($"The database could not be opened: {opened.Kind}", opened.Failure);
        var store = new ServerStore(database);

        foreach (var tick in ticks)
        {
            var stored = await store.StoreTickAsync(tick, cancellationToken).ConfigureAwait(false);

            if (stored.Outcome != ServerStoreOutcome.Stored)
            {
                throw new InvalidOperationException($"A fixture tick was not stored: {stored.Outcome}");
            }
        }

        return new TrendFixture(database, ticks);
    }

    /// <summary>
    /// Gives the key of an island of a tick.
    /// </summary>
    /// <param name="island">The island</param>
    /// <returns>The key</returns>
    public static IslandKey KeyOf(ContractTickIsland island)
    {
        return new IslandKey(island.SessionGuid, island.IslandId, island.AreaIndex);
    }

    /// <summary>
    /// Finds the first entry of a good on an island of a tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="island">The island</param>
    /// <param name="productGuid">The product GUID of the good</param>
    /// <returns>The entry, or <see langword="null" /> when the tick lacks the island or the good</returns>
    public static ContractTickEntry? FirstEntry(ContractTick tick, IslandKey island, int productGuid)
    {
        return FindIsland(tick, island)?.Entries.FirstOrDefault(entry => entry.ProductGuid == productGuid);
    }

    /// <summary>
    /// Finds an island of a tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <param name="island">The island</param>
    /// <returns>The island, or <see langword="null" /> when the tick lacks it</returns>
    public static ContractTickIsland? FindIsland(ContractTick tick, IslandKey island)
    {
        return tick.Islands.FirstOrDefault(candidate => KeyOf(candidate) == island);
    }

    /// <summary>
    /// Replays the fixture through the message reader and the tick assembler and converts each tick as the Companion does.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The contract ticks in replay order</returns>
    private static async Task<List<ContractTick>> ReplayAsync(CancellationToken cancellationToken)
    {
        var clock = new PipeReplayClock(DateTimeOffset.UnixEpoch);

        using var replay = new PipeReplayStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pipe", "long-session.jsonl")), PipeReplaySpeed.Maximum, clock, TimeProvider.System);
        using var assembler = new PipeTickAssembler(new PipeMessageReader(replay), clock);

        var ticks = new List<ContractTick>();
        var result = await assembler.ReadAsync(cancellationToken).ConfigureAwait(false);

        while (result.Kind != PipeTickResultKind.EndOfStream)
        {
            if (result.Tick is { } tick)
            {
                ticks.Add(ContractMessageConverter.ToTick(tick, ProfileId));
            }

            result = await assembler.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        return ticks;
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _database.Dispose();
        SqliteConnection.ClearAllPools();
    }

    #endregion // IDisposable
}