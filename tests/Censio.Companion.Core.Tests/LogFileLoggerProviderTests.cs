using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="LogFileLoggerProvider" />.
/// </summary>
[TestClass]
public sealed class LogFileLoggerProviderTests
{
    #region Constants

    /// <summary>
    /// The line <see cref="CompanionLog.Exiting" /> writes at the start time.
    /// </summary>
    private const string ExitLine = "2026-11-02T18:26:03.045+01:00 INF Exit\n";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The UTC time the tests start at: 18:26:03.045 local time on 2026-11-02.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 17, 26, 3, 45, TimeSpan.Zero);

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The temporary folder of the test.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-log-provider-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// A relative folder name no other test uses.
    /// </summary>
    private readonly string _relative = "censio-log-provider-relative-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// The clock of the provider, in a local time zone one hour ahead of UTC.
    /// </summary>
    private readonly FakeTimeProvider _clock = CreateClock();

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LogFileLoggerProviderTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public LogFileLoggerProviderTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    /// <summary>
    /// Gets the log folder of the test.
    /// </summary>
    private string Folder => Path.Combine(_root, "Censio", "Logs");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Deletes the temporary folders.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }

        if (Directory.Exists(Path.GetFullPath(_relative)))
        {
            Directory.Delete(Path.GetFullPath(_relative), true);
        }
    }

    /// <summary>
    /// A line is written to the file of the local date in a folder that is created, with its control characters
    /// replaced.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderWritesLineToDailyFile()
    {
        // Arrange
        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");
        var fileName = $"a{char.ConvertFromUtf32(0x1B)}b.jsonl";

        // Act
        CompanionLog.RecordingFileCreated(logger, fileName);

        await FlushAsync(provider);

        // Assert
        string[] expected = ["companion-20261102.log"];

        Assert.AreSequenceEqual(expected, FileNames(Folder), "The folder should hold the file of the local date only");
        Assert.AreEqual("2026-11-02T18:26:03.045+01:00 INF Recording file a?b.jsonl created\n", ReadShared(Path.Combine(Folder, "companion-20261102.log")), "The file should hold the sanitized line");
    }

    /// <summary>
    /// A line is appended to today's file while the file stays within its size with the line, and goes to the
    /// next file when it would not.
    /// </summary>
    /// <param name="freeBytes">The bytes left in today's file before the line</param>
    /// <param name="expectedFile">The file the line is expected in</param>
    /// <returns>The task</returns>
    [TestMethod]
    [DataRow(100, "companion-20261102.log")]
    [DataRow(10, "companion-20261102-2.log")]
    public async Task LogFileLoggerProviderRollsPastMaxBytes(int freeBytes, string expectedFile)
    {
        // Arrange
        CreateFile("companion-20261102.log", LogFileLoggerProvider.MaxFileBytes - freeBytes);

        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        CompanionLog.Exiting(logger);

        await FlushAsync(provider);

        // Assert
        Assert.AreEqual(ExitLine, ReadTail(Path.Combine(Folder, expectedFile), ExitLine.Length), $"The line should be written to {expectedFile}");
        Assert.AreEqual(LogFileLoggerProvider.MaxFileBytes - freeBytes + ExitLine.Length, TotalBytes(Folder), "The line should be written once and nothing else");
    }

    /// <summary>
    /// When today's file with the highest number allowed is full, the line is dropped.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderLastNumberFullDropsLine()
    {
        // Arrange
        CreateFile("companion-20261102-9999.log", LogFileLoggerProvider.MaxFileBytes - 10);

        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        CompanionLog.Exiting(logger);

        await FlushAsync(provider);

        // Assert
        string[] expected = ["companion-20261102-9999.log"];

        Assert.AreSequenceEqual(expected, FileNames(Folder), "No file beyond number 9999 should be created");
        Assert.AreEqual(LogFileLoggerProvider.MaxFileBytes - 10, TotalBytes(Folder), "The line should be dropped");
    }

    /// <summary>
    /// A line of a new local day goes to a new file.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderNewDayStartsNewFile()
    {
        // Arrange
        _clock.SetUtcNow(new DateTimeOffset(2026, 11, 2, 22, 59, 0, TimeSpan.Zero));

        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        CompanionLog.RecordingStarted(logger);
        await FlushAsync(provider);
        _clock.Advance(TimeSpan.FromMinutes(2));

        // Act
        CompanionLog.RecordingStopped(logger);

        await FlushAsync(provider);

        // Assert
        string[] expected = ["companion-20261102.log", "companion-20261103.log"];

        Assert.AreSequenceEqual(expected, FileNames(Folder), "The new local day should start a new file");
        Assert.AreEqual("2026-11-02T23:59:00.000+01:00 INF Recording started\n", ReadShared(Path.Combine(Folder, "companion-20261102.log")), "The first day's file should hold the first line");
        Assert.AreEqual("2026-11-03T00:01:00.000+01:00 INF Recording stopped\n", ReadShared(Path.Combine(Folder, "companion-20261103.log")), "The new day's file should hold the second line");
    }

    /// <summary>
    /// A new provider continues today's file with the highest number.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderContinuesTodaysFileAfterRestart()
    {
        // Arrange
        Directory.CreateDirectory(Folder);
        await File.WriteAllTextAsync(Path.Combine(Folder, "companion-20261101-5.log"), "day before\n", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(Folder, "companion-20261102.log"), "first\n", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(Folder, "companion-20261102-3.log"), "third\n", TestContext.CancellationToken);

        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        CompanionLog.Exiting(logger);

        await FlushAsync(provider);

        // Assert
        Assert.AreEqual("third\n" + ExitLine, ReadShared(Path.Combine(Folder, "companion-20261102-3.log")), "The line should be appended to today's file with the highest number");
        Assert.AreEqual("first\n", ReadShared(Path.Combine(Folder, "companion-20261102.log")), "Today's earlier file should stay as it was");
        Assert.AreEqual("day before\n", ReadShared(Path.Combine(Folder, "companion-20261101-5.log")), "The day before's file should stay as it was");
    }

    /// <summary>
    /// Opening a new file deletes all but the newest log files by date and number, and no other file or folder.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderPrunesToMaxFilesAndKeepsOtherFiles()
    {
        // Arrange
        string[] old = [
                           "COMPANION-20261001.LOG",
                           "companion-20261020.log",
                           "companion-20261021.log",
                           "companion-20261022.log",
                           "companion-20261023.log",
                           "companion-20261024.log",
                           "companion-20261025.log",
                           "companion-20261026.log",
                           "companion-20261027.log",
                           "companion-20261028.log",
                           "companion-20261029.log",
                           "companion-20261030.log",
                           "companion-20261031.log",
                           "companion-20261031-2.log",
                       ];
        string[] others = ["companion-20261019.log.bak", "notes.txt", "recording-20261102-182603Z.jsonl"];

        CreateFiles(old);
        CreateFiles(others);
        Directory.CreateDirectory(Path.Combine(Folder, "companion-20261002.log"));

        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        CompanionLog.Exiting(logger);

        await FlushAsync(provider);

        // Assert
        string[] expected = [
                                "companion-20261019.log.bak",
                                "companion-20261024.log",
                                "companion-20261025.log",
                                "companion-20261026.log",
                                "companion-20261027.log",
                                "companion-20261028.log",
                                "companion-20261029.log",
                                "companion-20261030.log",
                                "companion-20261031-2.log",
                                "companion-20261031.log",
                                "companion-20261102.log",
                                "notes.txt",
                                "recording-20261102-182603Z.jsonl",
                            ];

        Assert.AreSequenceEqual(expected, FileNames(Folder), "The 10 newest log files and every other file should be kept");
        Assert.IsTrue(Directory.Exists(Path.Combine(Folder, "companion-20261002.log")), "A folder with a log file name should be kept");
    }

    /// <summary>
    /// The file just opened is never deleted, also when the folder holds 10 log files of later dates.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderPruneKeepsOpenFile()
    {
        // Arrange
        string[] later = [
                             "companion-20261103.log",
                             "companion-20261104.log",
                             "companion-20261105.log",
                             "companion-20261106.log",
                             "companion-20261107.log",
                             "companion-20261108.log",
                             "companion-20261109.log",
                             "companion-20261110.log",
                             "companion-20261111.log",
                             "companion-20261112.log",
                         ];

        CreateFiles(later);

        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        CompanionLog.Exiting(logger);

        await FlushAsync(provider);

        // Assert
        Assert.AreEqual(ExitLine, ReadShared(Path.Combine(Folder, "companion-20261102.log")), "The open file should be kept with its line");
    }

    /// <summary>
    /// A line that cannot be written is dropped without an error, and the next line is tried again.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderUnwritableFolderDropsLines()
    {
        // Arrange
        var blocking = Path.Combine(_root, "Censio");

        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(blocking, "a file where the folder should be", TestContext.CancellationToken);

        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        CompanionLog.RecordingStarted(logger);
        await FlushAsync(provider);
        File.Delete(blocking);

        // Act
        CompanionLog.RecordingStopped(logger);

        await FlushAsync(provider);

        // Assert
        Assert.AreEqual("2026-11-02T18:26:03.045+01:00 INF Recording stopped\n", ReadShared(Path.Combine(Folder, "companion-20261102.log")), "Only the line written after the folder became writable should be in the file");
    }

    /// <summary>
    /// A folder that is not a full path gets nothing written or created.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderRelativeFolderWritesNothing()
    {
        // Arrange
        using var provider = new LogFileLoggerProvider(_relative, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        CompanionLog.Exiting(logger);

        await FlushAsync(provider);

        // Assert
        Assert.IsFalse(Directory.Exists(Path.GetFullPath(_relative)), "Nothing should be created below the working directory");
    }

    /// <summary>
    /// While the writer is stalled, lines are queued up to the character limit and the rest is dropped. The stall
    /// is today's log file made a named pipe: opening it for writing waits until the test opens it for reading.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task LogFileLoggerProviderFullQueueDropsLines()
    {
        // Arrange
        var path = Path.Combine(Folder, "companion-20261102.log");

        Directory.CreateDirectory(Folder);
        await CreateNamedPipeAsync(path);

        var message = new string('x', 1000);
        var lineLength = "2026-11-02T18:26:03.045+01:00 INF ".Length + message.Length + 1;
        var fitting = LogFileLoggerProvider.MaxQueuedChars / lineLength;
        var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        WriteMany(logger, 3 * fitting, message);

        var reading = Task.Run(() => ReadShared(path), TestContext.CancellationToken);

        await FlushAsync(provider);
        provider.Dispose();

        var text = await reading.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        var written = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.IsGreaterThanOrEqualTo(fitting, written, "The lines that fit the queue should be written");
        Assert.IsLessThanOrEqualTo(fitting + 1, written, "Beyond the queue and the line being written, lines should be dropped");
    }

    /// <summary>
    /// Only Information and above is enabled.
    /// </summary>
    /// <param name="level">The level</param>
    /// <param name="expected">Whether the level is enabled</param>
    [TestMethod]
    [DataRow(LogLevel.Trace, false)]
    [DataRow(LogLevel.Debug, false)]
    [DataRow(LogLevel.Information, true)]
    [DataRow(LogLevel.Warning, true)]
    [DataRow(LogLevel.Error, true)]
    [DataRow(LogLevel.Critical, true)]
    [DataRow(LogLevel.None, false)]
    public void LogFileLoggerProviderIsEnabledFromInformation(LogLevel level, bool expected)
    {
        // Arrange
        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        var enabled = logger.IsEnabled(level);

        // Assert
        Assert.AreEqual(expected, enabled, $"The level {level} should be enabled only from Information");
    }

    /// <summary>
    /// The logger keeps no scopes.
    /// </summary>
    [TestMethod]
    public void LogFileLoggerProviderBeginScopeReturnsNull()
    {
        // Arrange
        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        var scope = logger.BeginScope("scope");

        // Assert
        Assert.IsNull(scope, "The logger should return no scope");
    }

    /// <summary>
    /// Lines below Information are not written.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderBelowInformationNotWritten()
    {
        // Arrange
        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        // Act
        Write(logger, LogLevel.Trace, "trace");
        Write(logger, LogLevel.Debug, "debug");
        Write(logger, LogLevel.Information, "information");

        await FlushAsync(provider);

        // Assert
        Assert.AreEqual("2026-11-02T18:26:03.045+01:00 INF information\n", ReadShared(Path.Combine(Folder, "companion-20261102.log")), "Only the Information line should be written");
    }

    /// <summary>
    /// The flush returns once every queued line is written, in order.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderFlushAsyncWritesQueue()
    {
        // Arrange
        using var provider = new LogFileLoggerProvider(Folder, _clock);
        var logger = provider.CreateLogger("Censio");

        CompanionLog.RecordingStarted(logger);
        CompanionLog.RecordingFailed(logger);
        CompanionLog.Exiting(logger);

        // Act
        await FlushAsync(provider);

        // Assert
        const string expected = "2026-11-02T18:26:03.045+01:00 INF Recording started\n"
                                + "2026-11-02T18:26:03.045+01:00 WRN Recording failed\n"
                                + ExitLine;

        Assert.AreEqual(expected, ReadShared(Path.Combine(Folder, "companion-20261102.log")), "Every queued line should be written in order once the flush returns");
    }

    /// <summary>
    /// A flush after the provider was disposed returns at once, without waiting for its timeout.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task LogFileLoggerProviderFlushAfterDisposeReturnsAtOnce()
    {
        // Arrange
        var provider = new LogFileLoggerProvider(Folder, _clock);

        provider.Dispose();

        // Act
        var flush = provider.FlushAsync(TimeSpan.FromDays(1));

        await flush.WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(flush.IsCompletedSuccessfully, "The flush should return without the fake clock reaching its timeout");
    }

    /// <summary>
    /// The dispose returns long before a flush gives up while the writer is stalled. The stall is today's log file
    /// made a named pipe; the provider runs on the real clock, so the flush timeout and the dispose are measured as
    /// a ratio of two durations in the same test.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task LogFileLoggerProviderDisposeReturnsPromptlyOnStalledWriter()
    {
        // Arrange
        var path = await CreateStalledFileAsync();
        var provider = new LogFileLoggerProvider(Folder, TimeProvider.System);
        var logger = provider.CreateLogger("Censio");

        Write(logger, LogLevel.Information, "first");

        var flushWatch = Stopwatch.StartNew();

        await provider.FlushAsync(TimeSpan.FromMilliseconds(500)).WaitAsync(_guard, TestContext.CancellationToken);

        var flushTime = flushWatch.Elapsed;

        // Act
        var disposeWatch = Stopwatch.StartNew();

        provider.Dispose();

        var disposeTime = disposeWatch.Elapsed;

        await Task.Run(() => ReadShared(path), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        var ratio = disposeTime / flushTime;

        Assert.IsLessThan(0.65, ratio, $"The dispose should not wait for the stalled writer: it took {disposeTime.TotalMilliseconds} ms against a flush timeout of {flushTime.TotalMilliseconds} ms");
    }

    /// <summary>
    /// Lines queued behind a stalled write are not written once the provider was disposed, even when the stall ends
    /// afterwards; at most the line being written when the writer stalled is.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task LogFileLoggerProviderDisposeDropsLinesQueuedBehindStalledWrite()
    {
        // Arrange
        var path = await CreateStalledFileAsync();
        var provider = new LogFileLoggerProvider(Folder, TimeProvider.System);
        var logger = provider.CreateLogger("Censio");

        Write(logger, LogLevel.Information, "first");
        Write(logger, LogLevel.Information, "second");
        Write(logger, LogLevel.Information, "third");

        await provider.FlushAsync(TimeSpan.FromMilliseconds(200)).WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        provider.Dispose();

        var text = await Task.Run(() => ReadShared(path), TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        var messages = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                           .Select(line => line[(line.IndexOf(" INF ", StringComparison.Ordinal) + " INF ".Length)..])
                           .ToArray();

        Assert.IsLessThanOrEqualTo(1, messages.Length, "Only the line being written when the writer stalled may be written after the dispose");
        Assert.IsTrue(messages.All(message => message == "first"), $"The lines queued behind the stalled write should not be written, but the file holds: {string.Join(", ", messages)}");
    }

    /// <summary>
    /// A second dispose of the provider does nothing.
    /// </summary>
    [TestMethod]
    public void LogFileLoggerProviderDisposeTwiceDoesNothing()
    {
        // Arrange
        var provider = new LogFileLoggerProvider(Folder, _clock);

        provider.Dispose();

        // Act
        var exception = Record(provider.Dispose);

        // Assert
        Assert.IsNull(exception, "A second dispose should not throw");
    }

    /// <summary>
    /// Runs an action and returns the exception it threw.
    /// </summary>
    /// <param name="action">The action</param>
    /// <returns>The exception, or <see langword="null" /> if the action did not throw</returns>
    private static Exception? Record(Action action)
    {
        try
        {
            action();

            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// Creates the clock of the tests.
    /// </summary>
    /// <returns>The clock</returns>
    private static FakeTimeProvider CreateClock()
    {
        var clock = new FakeTimeProvider(_start);

        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Censio Test", TimeSpan.FromHours(1), "Censio Test", "Censio Test"));

        return clock;
    }

    /// <summary>
    /// Writes a message at a level, without a message template.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="level">The level</param>
    /// <param name="message">The message</param>
    private static void Write(ILogger logger, LogLevel level, string message)
    {
        logger.Log(level, new EventId(0), message, null, (state, _) => state);
    }

    /// <summary>
    /// Writes the same Information message a number of times.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="count">The number of messages</param>
    /// <param name="message">The message</param>
    private static void WriteMany(ILogger logger, int count, string message)
    {
        for (var index = 0; index < count; index++)
        {
            Write(logger, LogLevel.Information, message);
        }
    }

    /// <summary>
    /// Reads a whole file, allowing the provider to keep it open for writing.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>The text</returns>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads the last bytes of a file as UTF-8.
    /// </summary>
    /// <param name="path">The path</param>
    /// <param name="count">The number of bytes</param>
    /// <returns>The text</returns>
    private static string ReadTail(string path, int count)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[count];

        stream.Seek(-count, SeekOrigin.End);
        stream.ReadExactly(buffer);

        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>
    /// Gets the names of the files directly in a folder, ordered by ordinal comparison.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <returns>The names</returns>
    private static string[] FileNames(string folder)
    {
        return Directory.GetFiles(folder).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Gets the total size of the files directly in a folder.
    /// </summary>
    /// <param name="folder">The folder</param>
    /// <returns>The size in bytes</returns>
    private static long TotalBytes(string folder)
    {
        return Directory.GetFiles(folder).Sum(path => new FileInfo(path).Length);
    }

    /// <summary>
    /// Waits for the provider to write its queue, within the guard.
    /// </summary>
    /// <param name="provider">The provider</param>
    /// <returns>The task</returns>
    private Task FlushAsync(LogFileLoggerProvider provider)
    {
        return provider.FlushAsync(_guard).WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Creates a file of a size in the log folder, without writing its bytes.
    /// </summary>
    /// <param name="name">The file name</param>
    /// <param name="length">The size in bytes</param>
    private void CreateFile(string name, long length)
    {
        Directory.CreateDirectory(Folder);

        using var stream = new FileStream(Path.Combine(Folder, name), FileMode.CreateNew, FileAccess.Write);

        stream.SetLength(length);
    }

    /// <summary>
    /// Creates small files in the log folder.
    /// </summary>
    /// <param name="names">The file names</param>
    private void CreateFiles(string[] names)
    {
        Directory.CreateDirectory(Folder);

        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(Folder, name), name + "\n");
        }
    }

    /// <summary>
    /// Makes the log file of today's local date on the real clock a named pipe, so that opening it for writing waits
    /// until the test opens it for reading.
    /// </summary>
    /// <returns>The path of the named pipe</returns>
    private async Task<string> CreateStalledFileAsync()
    {
        var path = Path.Combine(Folder, LogFileNames.Format(DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime), 1));

        Directory.CreateDirectory(Folder);
        await CreateNamedPipeAsync(path);

        return path;
    }

    /// <summary>
    /// Creates a named pipe with <c>mkfifo</c>.
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>The task</returns>
    private async Task CreateNamedPipeAsync(string path)
    {
        var start = new ProcessStartInfo("mkfifo")
                    {
                        UseShellExecute = false
                    };

        start.ArgumentList.Add(path);

        using var process = Process.Start(start)!;

        await process.WaitForExitAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        Assert.AreEqual(0, process.ExitCode, "mkfifo should create the named pipe");
    }

    #endregion // Methods
}