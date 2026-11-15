using System.Net;

namespace Censio.Server;

/// <summary>
/// Holds the limits of the ingest endpoint per host: the pending hellos, the failed hellos and the block.
/// </summary>
public sealed partial class IngestHosts : IDisposable
{
    #region Constants

    /// <summary>
    /// The maximum number of connections of one host that wait for their hello at the same time.
    /// </summary>
    public const int MaxPendingHellosPerHost = 4;

    /// <summary>
    /// The number of failed hellos of one host within <see cref="FailureWindow"/> that blocks the host.
    /// </summary>
    public const int MaxFailedHellos = 10;

    /// <summary>
    /// The maximum number of hosts that have an entry of their own.
    /// </summary>
    public const int MaxTrackedHosts = 1024;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The lock of all state.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The entries of the tracked hosts by the key of their address.
    /// </summary>
    private readonly Dictionary<string, IngestHost> _table = [];

    /// <summary>
    /// The entry shared by the hosts beyond the tracked ones.
    /// </summary>
    private readonly IngestHost _shared = new(null);

    /// <summary>
    /// The time source of the window and the block.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger of the block lines.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The entry of the host of the last accepted hello, kept outside the table.
    /// </summary>
    private IngestHost? _protected;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestHosts"/> class.
    /// </summary>
    /// <param name="timeProvider">The time source of the window and the block</param>
    /// <param name="logger">The logger of the block lines</param>
    public IngestHosts(TimeProvider timeProvider, ILogger logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the window in which failed hellos of one host are counted.
    /// </summary>
    public static TimeSpan FailureWindow { get; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the time a host stays blocked.
    /// </summary>
    public static TimeSpan BlockDuration { get; } = TimeSpan.FromSeconds(60);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Tries to count a connection of a host that waits for its hello.
    /// </summary>
    /// <param name="address">The address of the TCP peer, or <c>null</c> when it is unknown</param>
    /// <returns>The entry of the host when the connection was counted; otherwise <c>null</c></returns>
    public IngestHost? TryEnterPendingHello(IPAddress? address)
    {
        var key = CreateKey(address);

        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();
            var host = FindOrCreate(key, now);

            if (host.BlockEnd > now)
            {
                host.Refused++;

                return null;
            }

            if (host.Waiting >= MaxPendingHellosPerHost)
            {
                return null;
            }

            host.Waiting++;

            return host;
        }
    }

    /// <summary>
    /// Releases a connection of a host that waited for its hello.
    /// </summary>
    /// <param name="host">The entry the connection was counted on</param>
    public void LeavePendingHello(IngestHost host)
    {
        lock (_lock)
        {
            Leave(host);
        }
    }

    /// <summary>
    /// Counts a failed hello of a host and blocks the host at <see cref="MaxFailedHellos"/>.
    /// </summary>
    /// <param name="host">The entry the connection was counted on</param>
    public void RecordFailedHello(IngestHost host)
    {
        var endedBlock = false;
        var refused = 0;
        var blockStarted = false;

        lock (_lock)
        {
            Leave(host);

            var now = _timeProvider.GetUtcNow();

            if (host.BlockEnd > now)
            {
                return;
            }

            if (host.BlockTimer is not null)
            {
                // The block is over, but its timer has not run yet: end it here, so its line is not lost.
                endedBlock = true;
                refused = host.Refused;
                host.BlockTimer.Dispose();
                host.BlockTimer = null;
            }

            host.FailedHellos.RemoveAll(time => now - time >= FailureWindow);
            host.FailedHellos.Add(now);
            host.LastFailedHello = now;

            if (host.FailedHellos.Count >= MaxFailedHellos)
            {
                StartBlock(host, now);
                blockStarted = true;
            }
        }

        if (endedBlock)
        {
            LogBlockEnded(_logger, refused);
        }

        if (blockStarted)
        {
            LogBlockStarted(_logger);
        }
    }

    /// <summary>
    /// Records an accepted hello of a host and gives the host an entry of its own.
    /// </summary>
    /// <param name="host">The entry the connection was counted on</param>
    /// <param name="address">The address of the TCP peer, or <c>null</c> when it is unknown</param>
    public void RecordAcceptedHello(IngestHost host, IPAddress? address)
    {
        var key = CreateKey(address);

        lock (_lock)
        {
            Leave(host);

            var target = IsHeld(host) ? host : FindExisting(key) ?? new IngestHost(key);

            target.FailedHellos.Clear();
            Protect(target);
        }
    }

    /// <summary>
    /// Creates the key of an address: its bytes, an IPv4-mapped address as IPv4, the scope id dropped.
    /// </summary>
    /// <param name="address">The address, or <c>null</c> when it is unknown</param>
    /// <returns>The key; empty when the address is unknown</returns>
    private static string CreateKey(IPAddress? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        return Convert.ToHexString(normalized.GetAddressBytes());
    }

    /// <summary>
    /// Releases a waiting place of an entry.
    /// </summary>
    /// <param name="host">The entry</param>
    private static void Leave(IngestHost host)
    {
        host.Waiting = Math.Max(0, host.Waiting - 1);
    }

    /// <summary>
    /// Determines whether an entry can be forgotten.
    /// </summary>
    /// <param name="host">The entry</param>
    /// <param name="now">The current time</param>
    /// <returns><c>true</c> when nothing waits and the entry is quiet for the window</returns>
    private static bool IsForgettable(IngestHost host, DateTimeOffset now)
    {
        return host.Waiting == 0
               && (host.LastFailedHello is null || now - host.LastFailedHello >= FailureWindow)
               && (host.BlockEnd is null || now - host.BlockEnd >= FailureWindow);
    }

    /// <summary>
    /// Determines whether an entry is one of the own entries of a host, not the shared one and not a dropped one.
    /// </summary>
    /// <param name="host">The entry</param>
    /// <returns><c>true</c> when the entry is the protected one or in the table</returns>
    private bool IsHeld(IngestHost host)
    {
        return ReferenceEquals(_protected, host)
               || (host.Key is not null && _table.TryGetValue(host.Key, out var tracked) && ReferenceEquals(tracked, host));
    }

    /// <summary>
    /// Finds the existing entry of a host.
    /// </summary>
    /// <param name="key">The key of the host's address</param>
    /// <returns>The entry, or <c>null</c> when the host has none</returns>
    private IngestHost? FindExisting(string key)
    {
        if (_protected is not null && _protected.Key == key)
        {
            return _protected;
        }

        return _table.GetValueOrDefault(key);
    }

    /// <summary>
    /// Finds the entry of a host or creates it; when the table is full, the shared entry.
    /// </summary>
    /// <param name="key">The key of the host's address</param>
    /// <param name="now">The current time</param>
    /// <returns>The entry</returns>
    private IngestHost FindOrCreate(string key, DateTimeOffset now)
    {
        var host = FindExisting(key);

        if (host is not null)
        {
            return host;
        }

        if (_table.Count >= MaxTrackedHosts)
        {
            RemoveForgettable(now);
        }

        if (_table.Count >= MaxTrackedHosts)
        {
            return _shared;
        }

        host = new IngestHost(key);
        _table.Add(key, host);

        return host;
    }

    /// <summary>
    /// Removes the entries that nothing waits on and whose last failed hello and block ended long enough ago.
    /// </summary>
    /// <param name="now">The current time</param>
    private void RemoveForgettable(DateTimeOffset now)
    {
        var forgettable = _table.Where(pair => IsForgettable(pair.Value, now))
                                .Select(pair => pair.Key)
                                .ToList();

        foreach (var key in forgettable)
        {
            _table.Remove(key);
        }
    }

    /// <summary>
    /// Makes an entry the protected one; the earlier protected entry goes back to the table when there is room.
    /// </summary>
    /// <param name="target">The entry</param>
    private void Protect(IngestHost target)
    {
        if (ReferenceEquals(_protected, target))
        {
            return;
        }

        if (target.Key is not null && _table.TryGetValue(target.Key, out var tracked) && ReferenceEquals(tracked, target))
        {
            _table.Remove(target.Key);
        }

        if (_protected?.Key is { } previousKey && _table.Count < MaxTrackedHosts)
        {
            _table[previousKey] = _protected;
        }

        _protected = target;
    }

    /// <summary>
    /// Starts the block of a host and the timer that ends it.
    /// </summary>
    /// <param name="host">The entry</param>
    /// <param name="now">The current time</param>
    private void StartBlock(IngestHost host, DateTimeOffset now)
    {
        host.FailedHellos.Clear();
        host.BlockEnd = now + BlockDuration;
        host.Refused = 0;
        host.BlockNumber++;

        var number = host.BlockNumber;

        host.BlockTimer = _timeProvider.CreateTimer(_ => EndBlock(host, number), null, BlockDuration, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Ends the block of a host when its timer runs and logs the end.
    /// </summary>
    /// <param name="host">The entry</param>
    /// <param name="number">The number of the block the timer belongs to</param>
    private void EndBlock(IngestHost host, int number)
    {
        int refused;

        lock (_lock)
        {
            if (host.BlockNumber != number || host.BlockTimer is null)
            {
                return;
            }

            refused = host.Refused;
            host.BlockTimer.Dispose();
            host.BlockTimer = null;
        }

        LogBlockEnded(_logger, refused);
    }

    #endregion // Methods

    #region Logging

    /// <summary>
    /// Logs the start of a block.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "A host was refused 10 times within 60 seconds; its connections are refused for 60 seconds.")]
    private static partial void LogBlockStarted(ILogger logger);

    /// <summary>
    /// Logs the end of a block.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="refused">The number of connections refused during the block.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "A host's block ended; {Refused} connections were refused while it lasted.")]
    private static partial void LogBlockEnded(ILogger logger, int refused);

    #endregion // Logging

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var host in _table.Values.Append(_shared).Concat(_protected is null ? [] : [_protected]))
            {
                host.BlockTimer?.Dispose();
                host.BlockTimer = null;
            }
        }
    }

    #endregion // IDisposable
}