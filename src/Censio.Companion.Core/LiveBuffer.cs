using System.Security;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// The bounded buffer of live messages that wait for the server, kept in files.
/// </summary>
public sealed class LiveBuffer : IAsyncDisposable
{
    #region Constants

    /// <summary>
    /// The number of messages the memory-only list keeps.
    /// </summary>
    public const int MaxMemoryMessages = 64;

    /// <summary>
    /// The number of bytes the memory-only list and the pending list keep.
    /// </summary>
    public const int MaxMemoryBytes = 16777216;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The buffer folder.
    /// </summary>
    private readonly string _folder;

    /// <summary>
    /// The limits.
    /// </summary>
    private readonly LiveBufferLimits _limits;

    /// <summary>
    /// The time provider.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The lock of the state below.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// The token source that ends the run when the buffer is disposed.
    /// </summary>
    private readonly CancellationTokenSource _disposeSource = new();

    /// <summary>
    /// The waiting messages in the order taken: stored ones, then those waiting for their write, then those in memory.
    /// </summary>
    private readonly List<LiveBufferItem> _items = [];

    /// <summary>
    /// The paths of the files to delete.
    /// </summary>
    private readonly List<string> _deletes = [];

    /// <summary>
    /// The signal that the state changed.
    /// </summary>
    private TaskCompletionSource _signal = NewSignal();

    /// <summary>
    /// The task of the run.
    /// </summary>
    private Task? _run;

    /// <summary>
    /// The message in flight.
    /// </summary>
    private LiveBufferItem? _inFlight;

    /// <summary>
    /// The sequence of the next file.
    /// </summary>
    private long _nextSequence;

    /// <summary>
    /// The number of messages dropped in this run.
    /// </summary>
    private int _dropped;

    /// <summary>
    /// The number of dropped messages not taken yet.
    /// </summary>
    private int _unloggedDropped;

    /// <summary>
    /// Whether the stored messages are loaded.
    /// </summary>
    private bool _isLoaded;

    /// <summary>
    /// Whether the buffer keeps messages in memory only.
    /// </summary>
    private bool _isMemoryOnly;

    /// <summary>
    /// Whether the buffer is disposed.
    /// </summary>
    private bool _isDisposed;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveBuffer" /> class.
    /// </summary>
    /// <param name="folder">The buffer folder</param>
    /// <param name="limits">The limits</param>
    /// <param name="timeProvider">The time provider</param>
    /// <param name="logger">The logger</param>
    public LiveBuffer(string folder, LiveBufferLimits limits, TimeProvider timeProvider, ILogger logger)
    {
        _folder = folder;
        _limits = limits;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the status.
    /// </summary>
    public LiveBufferStatus Status
    {
        get
        {
            lock (_gate)
            {
                return new LiveBufferStatus
                       {
                           Waiting = _items.Count,
                           Dropped = _dropped,
                           IsMemoryOnly = _isMemoryOnly
                       };
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Adds a message.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    /// <param name="utf8Json">The UTF-8 JSON of the message</param>
    public void Add(Guid id, ReadOnlyMemory<byte> utf8Json)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            var json = utf8Json.ToArray();

            _items.Add(new LiveBufferItem
                       {
                           Id = id,
                           TakenAt = _timeProvider.GetUtcNow(),
                           Length = json.Length,
                           Json = json,
                           IsMemory = _isMemoryOnly
                       });

            ApplyBounds();
            Notify();
        }
    }

    /// <summary>
    /// Loads the stored messages and writes the pending ones until cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the run</param>
    /// <returns>The task of the run</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return Task.CompletedTask;
            }

            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeSource.Token);

            _run = Task.Run(() => RunCoreAsync(linked), CancellationToken.None);

            return _run;
        }
    }

    /// <summary>
    /// Waits for the next message and marks it in flight.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the wait</param>
    /// <returns>The next message</returns>
    public async Task<LiveBufferEntry> NextAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (head, signal, path) = TakeHead();

            if (head is null)
            {
                await signal.WaitAsync(cancellationToken).ConfigureAwait(false);

                continue;
            }

            if (head.Json is { } json)
            {
                return new LiveBufferEntry
                       {
                           Id = head.Id,
                           TakenAt = head.TakenAt,
                           Utf8Json = json
                       };
            }

            var entry = await ReadStoredAsync(path, head, cancellationToken).ConfigureAwait(false);

            if (entry is not null)
            {
                return entry;
            }

            Discard(head);
            CompanionLog.LiveBufferUnreadable(_logger, 1);
        }
    }

    /// <summary>
    /// Removes the message in flight after the server acknowledged it.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    public void Acknowledge(Guid id)
    {
        lock (_gate)
        {
            if (_inFlight is not { } item || item.Id != id)
            {
                return;
            }

            _items.Remove(item);
            _inFlight = null;

            if (item.Json is null)
            {
                _deletes.Add(PathOf(item.Sequence));
            }

            Notify();
        }
    }

    /// <summary>
    /// Clears the in-flight mark after a failed connection.
    /// </summary>
    public void ReleaseInFlight()
    {
        lock (_gate)
        {
            _inFlight = null;
        }
    }

    /// <summary>
    /// Removes the message in flight, without an acknowledgement, when it is the one with the identity.
    /// </summary>
    /// <param name="id">The identity of the message</param>
    public void DropInFlight(Guid id)
    {
        lock (_gate)
        {
            if (_inFlight is not { } item || item.Id != id)
            {
                return;
            }

            _items.Remove(item);
            _inFlight = null;

            if (item.Json is null)
            {
                _deletes.Add(PathOf(item.Sequence));
            }

            CountDropped(1);
            Notify();
        }
    }

    /// <summary>
    /// Takes the number of dropped messages that were not logged yet.
    /// </summary>
    /// <returns>The number of dropped messages</returns>
    public int TakeUnloggedDropped()
    {
        lock (_gate)
        {
            var count = _unloggedDropped;

            _unloggedDropped = 0;

            return count;
        }
    }

    /// <summary>
    /// Creates a signal.
    /// </summary>
    /// <returns>The signal</returns>
    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Determines whether an exception is one a file system operation of the buffer ends with.
    /// </summary>
    /// <param name="exception">The exception</param>
    /// <returns><see langword="true" /> when the exception is a file system failure</returns>
    private static bool IsFileSystemFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;
    }

    /// <summary>
    /// Deletes a file system entry, ignoring a failure: the file is read again at the next start.
    /// </summary>
    /// <param name="info">The entry</param>
    private static void DeleteQuietly(FileSystemInfo info)
    {
        try
        {
            info.Delete();
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            // The entry stays and is found again at the next start
        }
    }

    /// <summary>
    /// Deletes a file, ignoring a failure: the file is read again at the next start.
    /// </summary>
    /// <param name="path">The path</param>
    private static void DeleteQuietly(string path)
    {
        DeleteQuietly(new FileInfo(path));
    }

    /// <summary>
    /// Reads the header of a stored file into an index entry.
    /// </summary>
    /// <param name="path">The path of the file</param>
    /// <param name="sequence">The sequence of the file</param>
    /// <returns>The entry, or <see langword="null" /> when the file is unreadable</returns>
    private static LiveBufferItem? ReadIndexed(string path, long sequence)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, false);

            var header = new byte[LiveBufferFile.HeaderLength];
            var read = stream.ReadAtLeast(header, header.Length, false);
            var result = LiveBufferFile.ReadHeader(header.AsSpan(0, read), stream.Length);

            return result is { Kind: LiveBufferFileResultKind.Read, Entry: { } entry }
                       ? new LiveBufferItem
                         {
                             Id = entry.Id,
                             TakenAt = entry.TakenAt,
                             Length = result.Length,
                             Sequence = sequence
                         }
                       : null;
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a stored file when its message is sent.
    /// </summary>
    /// <param name="path">The path of the file</param>
    /// <param name="item">The indexed message</param>
    /// <param name="cancellationToken">The token that ends the read</param>
    /// <returns>The entry, or <see langword="null" /> when the file is unreadable or differs from the index</returns>
    private static async Task<LiveBufferEntry?> ReadStoredAsync(string path, LiveBufferItem item, CancellationToken cancellationToken)
    {
        try
        {
            var options = new FileStreamOptions
                          {
                              Mode = FileMode.Open,
                              Access = FileAccess.Read,
                              Share = FileShare.Read,
                              Options = FileOptions.Asynchronous
                          };

            var stream = new FileStream(path, options);

            await using (stream.ConfigureAwait(false))
            {
                if (stream.Length != LiveBufferFile.HeaderLength + (long)item.Length)
                {
                    return null;
                }

                var bytes = new byte[LiveBufferFile.HeaderLength + item.Length];

                await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);

                var result = LiveBufferFile.Read(bytes);

                return result is { Kind: LiveBufferFileResultKind.Read, Entry: { } entry } && entry.Id == item.Id
                           ? entry
                           : null;
            }
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// Handles one entry of the folder: a link is deleted, a folder or a foreign name is left alone, a temporary file
    /// is deleted and a message file is read at its header.
    /// </summary>
    /// <param name="info">The entry</param>
    /// <param name="load">What the folder holds so far</param>
    private static void ScanEntry(FileSystemInfo info, LiveBufferLoad load)
    {
        var isMessage = LiveBufferFile.TryParseMessageName(info.Name, out var sequence);

        if (isMessage == false && LiveBufferFile.TryParseTemporaryName(info.Name, out sequence) == false)
        {
            return;
        }

        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            DeleteQuietly(info);

            load.Unreadable++;

            return;
        }

        if (info is DirectoryInfo)
        {
            return;
        }

        load.NextSequence = Math.Max(load.NextSequence, sequence + 1);

        if (isMessage == false)
        {
            DeleteQuietly(info);

            load.Temporary++;

            return;
        }

        var item = ReadIndexed(info.FullName, sequence);

        if (item is null)
        {
            DeleteQuietly(info);

            load.Unreadable++;
        }
        else
        {
            load.Items.Add(item);
        }
    }

    /// <summary>
    /// Gets the path of a message file.
    /// </summary>
    /// <param name="sequence">The sequence</param>
    /// <returns>The path</returns>
    private string PathOf(long sequence)
    {
        return Path.Combine(_folder, LiveBufferFile.MessageName(sequence));
    }

    /// <summary>
    /// Signals that the state changed. The caller holds the lock.
    /// </summary>
    private void Notify()
    {
        var signal = _signal;

        _signal = NewSignal();
        signal.TrySetResult();
    }

    /// <summary>
    /// Applies the bounds to the waiting messages. The caller holds the lock.
    /// </summary>
    private void ApplyBounds()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var item in _items.Where(item => item != _inFlight && IsExpired(item, now)).ToList())
        {
            Drop(item);
        }

        var total = _items.Sum(item => (long)item.Length);

        while (total > _limits.MaxBytes && _items.Count(item => item != _inFlight) >= 2)
        {
            var oldest = _items.First(item => item != _inFlight);

            total -= oldest.Length;

            Drop(oldest);
        }

        LimitJson(false, int.MaxValue);
        LimitJson(true, MaxMemoryMessages);
    }

    /// <summary>
    /// Determines whether a message is older than the age limit.
    /// </summary>
    /// <param name="item">The message</param>
    /// <param name="now">The time now</param>
    /// <returns><see langword="true" /> when the message is expired</returns>
    private bool IsExpired(LiveBufferItem item, DateTimeOffset now)
    {
        var age = now - item.TakenAt;

        return age > TimeSpan.Zero && age > _limits.MaxAge;
    }

    /// <summary>
    /// Drops the oldest messages of the pending list or the memory list beyond its bound, keeping the newest. The caller
    /// holds the lock.
    /// </summary>
    /// <param name="isMemory"><see langword="true" /> for the memory list, <see langword="false" /> for the pending list</param>
    /// <param name="maxCount">The number of messages the list keeps</param>
    private void LimitJson(bool isMemory, int maxCount)
    {
        while (true)
        {
            var group = _items.Where(item => item.Json is not null && item.IsMemory == isMemory).ToList();

            if (group.Count <= maxCount && group.Sum(item => (long)item.Length) <= MaxMemoryBytes)
            {
                return;
            }

            var oldest = group.FirstOrDefault(item => item != _inFlight);

            if (oldest is null || oldest == group[^1])
            {
                return;
            }

            Drop(oldest);
        }
    }

    /// <summary>
    /// Drops a message and counts it. The caller holds the lock.
    /// </summary>
    /// <param name="item">The message</param>
    private void Drop(LiveBufferItem item)
    {
        if (_items.Remove(item) == false)
        {
            return;
        }

        if (item.Json is null)
        {
            _deletes.Add(PathOf(item.Sequence));
        }

        CountDropped(1);
    }

    /// <summary>
    /// Counts dropped messages. The caller holds the lock.
    /// </summary>
    /// <param name="count">The number of messages</param>
    private void CountDropped(int count)
    {
        _dropped += count;
        _unloggedDropped += count;
    }

    /// <summary>
    /// Switches to memory only. The caller holds the lock.
    /// </summary>
    /// <returns><see langword="true" /> when the buffer was not memory only before</returns>
    private bool EnterMemoryOnly()
    {
        var isNew = _isMemoryOnly == false;

        _isMemoryOnly = true;

        foreach (var item in _items.Where(item => item.IsPending))
        {
            item.IsMemory = true;
        }

        return isNew;
    }

    /// <summary>
    /// Applies the bounds and takes the first message that can be sent and marks it in flight.
    /// </summary>
    /// <returns>The message, or <see langword="null" /> with the signal to wait for; the path of a stored message's file</returns>
    private (LiveBufferItem? Head, Task Signal, string Path) TakeHead()
    {
        lock (_gate)
        {
            if (_isLoaded)
            {
                ApplyBounds();
            }

            var head = _isLoaded ? _items.FirstOrDefault() : null;

            if (head is null || head.IsPending)
            {
                return (null, _signal.Task, string.Empty);
            }

            _inFlight = head;

            return (head, _signal.Task, head.Json is null ? PathOf(head.Sequence) : string.Empty);
        }
    }

    /// <summary>
    /// Discards a stored message whose file is unreadable.
    /// </summary>
    /// <param name="item">The message</param>
    private void Discard(LiveBufferItem item)
    {
        lock (_gate)
        {
            if (_inFlight == item)
            {
                _inFlight = null;
            }

            Drop(item);
            Notify();
        }
    }

    /// <summary>
    /// Loads the stored messages, then writes the pending ones until cancelled.
    /// </summary>
    /// <param name="linked">The token source of the run</param>
    /// <returns>The task of the run</returns>
    private async Task RunCoreAsync(CancellationTokenSource linked)
    {
        using (linked)
        {
            try
            {
                Load();

                await WriteLoopAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The run ends when it is cancelled
            }
        }
    }

    /// <summary>
    /// Scans the folder and brings its messages into the index.
    /// </summary>
    private void Load()
    {
        var load = Scan();
        int stored;

        lock (_gate)
        {
            _nextSequence = load.NextSequence;
            _items.InsertRange(0, load.Items.OrderBy(item => item.Sequence));
            CountDropped(load.Unreadable + load.Temporary);

            if (load.Failed && EnterMemoryOnly())
            {
                CompanionLog.LiveBufferNotWritable(_logger);
            }

            ApplyBounds();

            stored = _items.Count(item => item.Json is null);
            _isLoaded = true;

            Notify();
        }

        if (stored > 0)
        {
            CompanionLog.LiveBufferRestored(_logger, stored);
        }

        if (load.Unreadable > 0)
        {
            CompanionLog.LiveBufferUnreadable(_logger, load.Unreadable);
        }
    }

    /// <summary>
    /// Creates the folder if needed and reads the headers of its message files.
    /// </summary>
    /// <returns>What the folder holds</returns>
    private LiveBufferLoad Scan()
    {
        var load = new LiveBufferLoad();

        try
        {
            Directory.CreateDirectory(_folder);

            foreach (var info in new DirectoryInfo(_folder).EnumerateFileSystemInfos().ToList())
            {
                ScanEntry(info, load);
            }
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            load.Failed = true;
        }

        return load;
    }

    /// <summary>
    /// Deletes files and writes pending messages one at a time until cancelled.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the loop</param>
    /// <returns>The task of the loop</returns>
    private async Task WriteLoopAsync(CancellationToken cancellationToken)
    {
        while (cancellationToken.IsCancellationRequested == false)
        {
            string? deletePath = null;
            LiveBufferItem? pending = null;
            var sequence = 0L;
            Task signal;

            lock (_gate)
            {
                signal = _signal.Task;

                if (_deletes.Count > 0)
                {
                    deletePath = _deletes[0];

                    _deletes.RemoveAt(0);
                }
                else if (_isMemoryOnly == false)
                {
                    pending = _items.FirstOrDefault(item => item.IsPending);
                    sequence = _nextSequence;

                    if (pending is not null)
                    {
                        _nextSequence++;
                    }
                }
            }

            if (deletePath is not null)
            {
                DeleteQuietly(deletePath);
            }
            else if (pending is not null)
            {
                WriteMessage(pending, sequence);
            }
            else
            {
                await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Writes a pending message to its file, or switches to memory only when that fails.
    /// </summary>
    /// <param name="item">The message</param>
    /// <param name="sequence">The sequence of its file</param>
    private void WriteMessage(LiveBufferItem item, long sequence)
    {
        var isWritten = TryWriteFile(item, sequence);

        lock (_gate)
        {
            if (isWritten == false)
            {
                if (EnterMemoryOnly())
                {
                    CompanionLog.LiveBufferNotWritable(_logger);
                }

                ApplyBounds();
            }
            else if (_items.Contains(item))
            {
                item.Sequence = sequence;
                item.Json = null;
            }
            else
            {
                _deletes.Add(PathOf(sequence));
            }

            Notify();
        }
    }

    /// <summary>
    /// Writes a message to a temporary file, flushes it and renames it to its message name.
    /// </summary>
    /// <param name="item">The message</param>
    /// <param name="sequence">The sequence of its file</param>
    /// <returns><see langword="true" /> when the file was written</returns>
    private bool TryWriteFile(LiveBufferItem item, long sequence)
    {
        var temporary = Path.Combine(_folder, LiveBufferFile.TemporaryName(sequence));

        try
        {
            var bytes = LiveBufferFile.Format(new LiveBufferEntry
                                              {
                                                  Id = item.Id,
                                                  TakenAt = item.TakenAt,
                                                  Utf8Json = item.Json ?? []
                                              });

            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, false))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }

            File.Move(temporary, PathOf(sequence), false);

            return true;
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            DeleteQuietly(temporary);

            return false;
        }
    }

    #endregion // Methods

    #region IAsyncDisposable

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Task? run;

        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            run = _run;
        }

        await _disposeSource.CancelAsync().ConfigureAwait(false);

        if (run is not null)
        {
            await run.ConfigureAwait(false);
        }

        _disposeSource.Dispose();
    }

    #endregion // IAsyncDisposable
}