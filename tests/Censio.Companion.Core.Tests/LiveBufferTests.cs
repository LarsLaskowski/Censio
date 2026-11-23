using Censio.Server.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="LiveBuffer" />.
/// </summary>
[TestClass]
public sealed class LiveBufferTests
{
    #region Constants

    /// <summary>
    /// The log line of one unreadable entry.
    /// </summary>
    private const string OneUnreadableLine = "Live buffer entries unreadable: 1; discarded";

    /// <summary>
    /// The start of every log line of unreadable entries.
    /// </summary>
    private const string UnreadablePrefix = "Live buffer entries unreadable:";

    /// <summary>
    /// The log line of a buffer that cannot be written.
    /// </summary>
    private const string NotWritableLine = "Live buffer cannot be written; waiting messages are kept in memory until Exit";

    /// <summary>
    /// The length of a small message's JSON.
    /// </summary>
    private const int SmallJson = 32;

    /// <summary>
    /// The number of polls of a file system condition, ten milliseconds apart.
    /// </summary>
    private const int MaxPolls = 500;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The longest wait for a result of the buffer.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The names in the buffer folder the buffer never writes.
    /// </summary>
    private static readonly string[] _foreignNames = ["notes.txt", "0000000000000000002.MSG", "1000000000000000000.msg", "0000000000000000003.msg.bak"];

    /// <summary>
    /// The time of the clock when a test starts.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The clock of the buffers.
    /// </summary>
    private readonly FakeTimeProvider _clock = new(_start);

    /// <summary>
    /// The logger of the buffers.
    /// </summary>
    private readonly ConcurrentRecordingLogger _logger = new();

    /// <summary>
    /// The temporary folder of the test, which holds the buffer folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "censio-buffer-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The runs started by the test.
    /// </summary>
    private readonly List<Task> _runs = [];

    /// <summary>
    /// The buffers of the test, disposed afterwards.
    /// </summary>
    private readonly List<LiveBuffer> _buffers = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveBufferTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public LiveBufferTests(TestContext testContext)
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
    /// Gets the buffer folder of the test.
    /// </summary>
    private string Folder => Path.Combine(_root, "Buffer");

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Disposes the buffers, awaits their runs and deletes the temporary folder.
    /// </summary>
    /// <returns>A task that completes when the cleanup is done</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (var buffer in _buffers)
        {
            await buffer.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);
        }

        await Task.WhenAny(Task.WhenAll(_runs)).WaitAsync(_guard, TestContext.CancellationToken);

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// Messages are returned in the order taken, each with its identity and JSON (S1).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task LiveBufferNextAsyncReturnsInOrderTaken()
    {
        // Arrange
        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);
        buffer.Add(Id(1), Json(SmallJson, 1));
        buffer.Add(Id(2), Json(SmallJson, 2));
        buffer.Add(Id(3), Json(SmallJson, 3));

        // Act
        var entries = await TakeAsync(buffer, 3);

        // Assert
        Assert.AreSequenceEqual(new[] { Id(1), Id(2), Id(3) }, entries.Select(entry => entry.Id).ToArray(), "The messages should be returned in the order taken");
        Assert.AreSequenceEqual(Json(SmallJson, 3), entries[2].Utf8Json.ToArray(), "A message should be returned with its JSON");
    }

    /// <summary>
    /// A message added before the stored messages are loaded comes after them (S1, S2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC1")]
    public async Task LiveBufferAddDuringLoadComesAfterStoredMessages()
    {
        // Arrange
        WriteStored(4, 1, _start.AddHours(-1));
        WriteStored(5, 2, _start.AddMinutes(-30));

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        buffer.Add(Id(3), Json(SmallJson, 3));
        Start(buffer);

        // Act
        var entries = await TakeAsync(buffer, 3);

        // Assert
        Assert.AreSequenceEqual(new[] { Id(1), Id(2), Id(3) }, entries.Select(entry => entry.Id).ToArray(), "The stored messages should come before the message added during the load");
    }

    /// <summary>
    /// The messages of an earlier run are read in the order of their sequence, with their time taken, and their number is
    /// logged (S2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task LiveBufferRunAsyncReadsEarlierRunInOrderAndLogs()
    {
        // Arrange
        WriteStored(7, 3, _start.AddHours(-1));
        WriteStored(5, 1, _start.AddHours(-3));
        WriteStored(6, 2, _start.AddHours(-2));

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        // Act
        var entries = await TakeAsync(buffer, 3);

        // Assert
        Assert.AreSequenceEqual(new[] { Id(1), Id(2), Id(3) }, entries.Select(entry => entry.Id).ToArray(), "The stored messages should be returned in the order of their sequence");
        Assert.AreEqual(_start.AddHours(-3), entries[0].TakenAt, "A stored message should keep its time taken");
        Assert.AreSequenceEqual(Json(SmallJson, 1), entries[0].Utf8Json.ToArray(), "A stored message should keep its JSON");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Information, Message: "Live buffer holds 3 messages from an earlier run" }, _logger.Entries.ToList(), "The number of stored messages should be logged once");
    }

    /// <summary>
    /// An acknowledged message's file is deleted and the message no longer waits (E8).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task LiveBufferAcknowledgeDeletesFile()
    {
        // Arrange
        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);
        buffer.Add(Id(1), Json(SmallJson, 1));

        var entry = await NextAsync(buffer);
        var written = MessageFiles().Count;

        // Act
        buffer.Acknowledge(entry.Id);

        // Assert
        Assert.AreEqual(1, written, "The message in flight should be in its file");
        Assert.IsTrue(await WaitForAsync(() => MessageFiles().Count == 0), "The file of an acknowledged message should be deleted");
        Assert.AreEqual(0, buffer.Status.Waiting, "An acknowledged message should no longer wait");
    }

    /// <summary>
    /// A temporary file left by an earlier run is deleted, never sent, and counted as dropped (N5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task LiveBufferRunAsyncDeletesTemporaryFileAndCountsIt()
    {
        // Arrange
        WriteStored(4, 1, _start.AddHours(-1));

        var temporary = Path.Combine(Folder, LiveBufferFile.TemporaryName(5));

        await File.WriteAllBytesAsync(temporary, Format(2, _start), TestContext.CancellationToken);

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        // Act
        var entry = await NextAsync(buffer);

        // Assert
        Assert.AreEqual(Id(1), entry.Id, "The stored message should be returned, not the temporary file");
        Assert.IsTrue(await WaitForAsync(() => File.Exists(temporary) == false), "The temporary file should be deleted");
        Assert.AreEqual(1, buffer.Status.Dropped, "The temporary file should be counted as dropped");
        Assert.AreEqual(1, buffer.Status.Waiting, "Only the stored message should wait");
    }

    /// <summary>
    /// After a failed connection the message in flight is returned again with the same identity (E8).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task LiveBufferReleaseInFlightReturnsSameMessageAgain()
    {
        // Arrange
        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);
        buffer.Add(Id(1), Json(SmallJson, 1));
        buffer.Add(Id(2), Json(SmallJson, 2));

        await NextAsync(buffer);
        buffer.ReleaseInFlight();

        // Act
        var again = await NextAsync(buffer);

        // Assert
        Assert.AreEqual(Id(1), again.Id, "The released message should come next with the same identity");
        Assert.AreSequenceEqual(Json(SmallJson, 1), again.Utf8Json.ToArray(), "The released message should come next with its JSON");
        Assert.AreEqual(2, buffer.Status.Waiting, "Both messages should still wait");
    }

    /// <summary>
    /// A message older than the age limit is never returned: the expired head is dropped and counted, the next one is
    /// returned (S3, E7).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveBufferNextAsyncDropsMessagesOlderThanLimit()
    {
        // Arrange
        var buffer = CreateBuffer(Limits(1, LiveBufferLimits.DefaultMegabytes));

        buffer.Add(Id(1), Json(SmallJson, 1));
        _clock.Advance(TimeSpan.FromMinutes(30));
        buffer.Add(Id(2), Json(SmallJson, 2));
        _clock.Advance(TimeSpan.FromMinutes(31));
        Start(buffer);

        // Act
        var entry = await NextAsync(buffer);

        // Assert
        Assert.AreEqual(Id(2), entry.Id, "The message within the age limit should be returned");
        Assert.AreEqual(_start.AddMinutes(30), entry.TakenAt, "The returned message should carry its time taken");
        Assert.AreEqual(1, buffer.Status.Dropped, "The expired message should be counted as dropped");
        Assert.AreEqual(1, buffer.Status.Waiting, "Only the returned message should wait");
    }

    /// <summary>
    /// A stored message older than the age limit is dropped at load, counted and its file deleted (S3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveBufferRunAsyncDropsExpiredStoredMessages()
    {
        // Arrange
        WriteStored(1, 1, _start.AddHours(-25));
        WriteStored(2, 2, _start.AddHours(-1));

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        // Act
        var entry = await NextAsync(buffer);

        // Assert
        Assert.AreEqual(Id(2), entry.Id, "The stored message within the age limit should be returned");
        Assert.AreEqual(1, buffer.Status.Dropped, "The expired stored message should be counted as dropped");
        Assert.IsTrue(await WaitForAsync(() => File.Exists(StoredPath(1)) == false), "The file of the expired message should be deleted");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Information, Message: "Live buffer holds 1 messages from an earlier run" }, _logger.Entries.ToList(), "The number of stored messages kept should be logged");
    }

    /// <summary>
    /// A message taken after the clock's time, as after the clock was set back, is kept and returned (E5).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC3")]
    public async Task LiveBufferNextAsyncKeepsMessageTakenInFuture()
    {
        // Arrange
        WriteStored(1, 1, _start.AddHours(48));

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        // Act
        var entry = await NextAsync(buffer);

        // Assert
        Assert.AreEqual(Id(1), entry.Id, "A message taken in the future should be returned");
        Assert.AreEqual(_start.AddHours(48), entry.TakenAt, "The message should keep its time taken");
        Assert.AreEqual(0, buffer.Status.Dropped, "A message taken in the future should not be dropped");
    }

    /// <summary>
    /// Beyond the size limit the oldest message is dropped until the rest fits (S4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveBufferAddBeyondSizeDropsOldestKeepsNewest()
    {
        // Arrange
        var buffer = CreateBuffer(Limits(LiveBufferLimits.DefaultHours, 1));

        buffer.Add(Id(1), Json(400000, 1));
        buffer.Add(Id(2), Json(400000, 2));
        buffer.Add(Id(3), Json(400000, 3));
        Start(buffer);

        // Act
        var entries = await TakeAsync(buffer, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Id(2), Id(3) }, entries.Select(entry => entry.Id).ToArray(), "The oldest message should be dropped so that 800,000 bytes remain");
        Assert.AreEqual(1, buffer.Status.Dropped, "The dropped message should be counted");
    }

    /// <summary>
    /// A single message larger than the size limit stays as the newest; the older one is dropped (E4).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveBufferAddLargerThanLimitKeepsOnlyNewest()
    {
        // Arrange
        var buffer = CreateBuffer(Limits(LiveBufferLimits.DefaultHours, 1));

        buffer.Add(Id(1), Json(100, 1));
        buffer.Add(Id(2), Json(LiveBufferLimits.BytesPerMegabyte + 1, 2));
        Start(buffer);

        // Act
        var entry = await NextAsync(buffer);

        // Assert
        Assert.AreEqual(Id(2), entry.Id, "The newest message should stay although it exceeds the limit");
        Assert.HasCount(LiveBufferLimits.BytesPerMegabyte + 1, entry.Utf8Json.ToArray(), "The newest message should be returned whole");
        Assert.AreEqual(1, buffer.Status.Dropped, "The older message should be dropped and counted");
        Assert.AreEqual(1, buffer.Status.Waiting, "Only the newest message should wait");
    }

    /// <summary>
    /// The waiting JSON exceeds the size limit only by the newest message and the one in flight, and the files hold that
    /// JSON plus 68 bytes each, with no temporary file left (N2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC4")]
    public async Task LiveBufferFilesStayWithinLimitPlusHeaders()
    {
        // Arrange
        var buffer = CreateBuffer(Limits(LiveBufferLimits.DefaultHours, 1));

        Start(buffer);
        buffer.Add(Id(1), Json(600000, 1));

        var inFlight = await NextAsync(buffer);

        buffer.Add(Id(2), Json(600000, 2));

        // Act
        buffer.Add(Id(3), Json(600000, 3));

        // Assert
        var settled = await WaitForAsync(() => StoredIds().SequenceEqual([Id(1), Id(3)]) && TemporaryFiles().Count == 0);
        var bytes = MessageFiles().Sum(path => new FileInfo(path).Length);

        Assert.AreEqual(Id(1), inFlight.Id, "The first message should be in flight");
        Assert.IsTrue(settled, "The files should hold the message in flight and the newest, with no temporary file");
        Assert.AreEqual(1, buffer.Status.Dropped, "The oldest message not in flight should be dropped");
        Assert.AreEqual(2, buffer.Status.Waiting, "The message in flight and the newest should wait");
        Assert.AreEqual(2 * (600000L + LiveBufferFile.HeaderLength), bytes, "The files should hold the two messages plus 68 bytes each");
    }

    /// <summary>
    /// The status counts the waiting messages and the messages dropped in this run (S1, S3).
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void LiveBufferStatusCountsWaitingAndDropped()
    {
        // Arrange
        var buffer = CreateBuffer(Limits(LiveBufferLimits.DefaultHours, 1));

        buffer.Add(Id(1), Json(400000, 1));
        buffer.Add(Id(2), Json(400000, 2));
        buffer.Add(Id(3), Json(400000, 3));

        // Act
        var status = buffer.Status;

        // Assert
        Assert.AreEqual(2, status.Waiting, "Two messages should wait");
        Assert.AreEqual(1, status.Dropped, "One message should be counted as dropped");
        Assert.IsFalse(status.IsMemoryOnly, "A buffer without a write failure should not be memory only");
    }

    /// <summary>
    /// The unlogged dropped count is taken once, while the run's dropped count stays (S3).
    /// </summary>
    [TestMethod]
    [TestCategory("AC5")]
    public void LiveBufferTakeUnloggedDroppedReturnsCountOnce()
    {
        // Arrange
        var buffer = CreateBuffer(Limits(LiveBufferLimits.DefaultHours, 1));

        buffer.Add(Id(1), Json(400000, 1));
        buffer.Add(Id(2), Json(400000, 2));
        buffer.Add(Id(3), Json(400000, 3));

        var first = buffer.TakeUnloggedDropped();

        // Act
        var second = buffer.TakeUnloggedDropped();

        // Assert
        Assert.AreEqual(1, first, "The first take should return the dropped message");
        Assert.AreEqual(0, second, "The second take should return nothing");
        Assert.AreEqual(1, buffer.Status.Dropped, "The run's dropped count should stay");
    }

    /// <summary>
    /// A dropped message is never returned, before or after later messages (S3, E6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC7")]
    public async Task LiveBufferNextAsyncNeverReturnsDroppedMessage()
    {
        // Arrange
        var buffer = CreateBuffer(Limits(LiveBufferLimits.DefaultHours, 1));

        buffer.Add(Id(1), Json(600000, 1));
        buffer.Add(Id(2), Json(600000, 2));
        Start(buffer);

        var first = await NextAsync(buffer);

        buffer.Acknowledge(first.Id);
        buffer.Add(Id(3), Json(SmallJson, 3));

        // Act
        var second = await NextAsync(buffer);

        // Assert
        Assert.AreEqual(Id(2), first.Id, "The message after the dropped one should come first");
        Assert.AreEqual(Id(3), second.Id, "The later message should come next, never the dropped one");
        Assert.AreEqual(1, buffer.Status.Dropped, "The dropped message should be counted once");
    }

    /// <summary>
    /// A stored file refused at load (header, length) or at send (hash) is discarded, counted and logged, and the others
    /// are returned in order (E3).
    /// </summary>
    /// <param name="damage">The damage of the file</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow("magic", DisplayName = "Other magic")]
    [DataRow("cut", DisplayName = "Last byte missing")]
    [DataRow("json", DisplayName = "A JSON byte changed")]
    public async Task LiveBufferRunAsyncDiscardsUnreadableFileCountsAndLogs(string damage)
    {
        // Arrange
        WriteStored(1, 1, _start.AddHours(-3));
        WriteStored(2, 2, _start.AddHours(-2));
        WriteStored(3, 3, _start.AddHours(-1));
        await File.WriteAllBytesAsync(StoredPath(2), Damaged(await File.ReadAllBytesAsync(StoredPath(2), TestContext.CancellationToken), damage), TestContext.CancellationToken);

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        // Act
        var entries = await TakeAsync(buffer, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Id(1), Id(3) }, entries.Select(entry => entry.Id).ToArray(), "The readable messages should be returned in order");
        Assert.AreEqual(1, buffer.Status.Dropped, "The unreadable file should be counted as dropped");
        Assert.IsTrue(await WaitForAsync(() => File.Exists(StoredPath(2)) == false), "The unreadable file should be deleted");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: OneUnreadableLine }, _logger.Entries.ToList(), "The unreadable file should be logged once");
    }

    /// <summary>
    /// Files and folders of names the buffer never writes are left alone: not read, sent, changed, deleted or counted
    /// (E3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LiveBufferRunAsyncLeavesForeignFiles()
    {
        // Arrange
        WriteStored(1, 1, _start.AddHours(-1));

        var foreign = _foreignNames.Select(name => Path.Combine(Folder, name)).ToArray();
        var content = Format(9, _start);
        var folder = Path.Combine(Folder, LiveBufferFile.MessageName(4));

        WriteFiles(foreign, content);
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, LiveBufferFile.MessageName(5)), content, TestContext.CancellationToken);

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        // Act
        var entries = await TakeAsync(buffer, 1);

        // Assert
        Assert.AreEqual(Id(1), entries[0].Id, "Only the stored message should be returned");
        Assert.IsTrue(await WaitForAsync(() => File.Exists(StoredPath(1)) == false), "The acknowledged file should be deleted");
        Assert.AreEqual(0, buffer.Status.Waiting, "No foreign file should wait");
        Assert.AreEqual(0, buffer.Status.Dropped, "No foreign file should be counted");
        Assert.IsTrue(foreign.All(path => File.ReadAllBytes(path).SequenceEqual(content)), "Every foreign file should be left unchanged");
        Assert.IsTrue(File.Exists(Path.Combine(folder, LiveBufferFile.MessageName(5))), "A folder with a message name should be left alone");
        Assert.DoesNotContain(entry => entry.Message.StartsWith(UnreadablePrefix, StringComparison.Ordinal), _logger.Entries.ToList(), "No foreign file should be logged as unreadable");
    }

    /// <summary>
    /// A stored file damaged after the load — changed, grown, shrunk, deleted or holding another identity than the
    /// index — is found unreadable when it is sent, discarded, counted and logged, and the next one is returned (E3, N3).
    /// </summary>
    /// <param name="damage">The damage of the file</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [TestCategory("AC10")]
    [DataRow("json", DisplayName = "Content changed")]
    [DataRow("grown", DisplayName = "File grown")]
    [DataRow("cut", DisplayName = "File shrunk")]
    [DataRow("deleted", DisplayName = "File deleted")]
    [DataRow("identity", DisplayName = "Identity differs from the index")]
    public async Task LiveBufferNextAsyncDiscardsFileDamagedAfterLoad(string damage)
    {
        // Arrange
        WriteStored(1, 1, _start.AddHours(-3));
        WriteStored(2, 2, _start.AddHours(-2));
        WriteStored(3, 3, _start.AddHours(-1));

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        var first = await NextAsync(buffer);

        DamageAfterLoad(StoredPath(2), damage);
        buffer.Acknowledge(first.Id);

        // Act
        var second = await NextAsync(buffer);

        // Assert
        Assert.AreEqual(Id(1), first.Id, "The first stored message should come first");
        Assert.AreEqual(Id(3), second.Id, "The message after the damaged one should come next");
        Assert.AreEqual(1, buffer.Status.Dropped, "The damaged file should be counted as dropped");
        Assert.IsTrue(await WaitForAsync(() => File.Exists(StoredPath(2)) == false), "The damaged file should be deleted");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: OneUnreadableLine }, _logger.Entries.ToList(), "The damaged file should be logged once");
    }

    /// <summary>
    /// A file link and a folder link with message names in the buffer folder are deleted as links, never opened, and
    /// counted unreadable; their targets stay unchanged (N6).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LiveBufferRunAsyncDeletesLinkLeavesTarget()
    {
        // Arrange
        var targets = Path.Combine(_root, "Targets");
        var targetFile = Path.Combine(targets, "target.msg");
        var targetFolder = Path.Combine(targets, "folder");
        var content = Format(9, _start.AddHours(-2));

        Directory.CreateDirectory(targetFolder);
        await File.WriteAllBytesAsync(targetFile, content, TestContext.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(targetFolder, "inside.txt"), content, TestContext.CancellationToken);
        WriteStored(3, 3, _start.AddHours(-1));
        File.CreateSymbolicLink(StoredPath(1), targetFile);
        Directory.CreateSymbolicLink(StoredPath(2), targetFolder);

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        // Act
        var entry = await NextAsync(buffer);

        // Assert
        var linksGone = await WaitForAsync(() => Directory.EnumerateFileSystemEntries(Folder).Select(Path.GetFileName).SequenceEqual([LiveBufferFile.MessageName(3)]));

        Assert.AreEqual(Id(3), entry.Id, "Only the stored file should be returned, never a link's target");
        Assert.IsTrue(linksGone, "Both links should be deleted");
        Assert.AreSequenceEqual(content, await File.ReadAllBytesAsync(targetFile, TestContext.CancellationToken), "The target file should stay unchanged");
        Assert.IsTrue(File.Exists(Path.Combine(targetFolder, "inside.txt")), "The target folder should keep its file");
        Assert.AreEqual(1, buffer.Status.Waiting, "No link should wait");
        Assert.AreEqual(2, buffer.Status.Dropped, "Both links should be counted as unreadable");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: "Live buffer entries unreadable: 2; discarded" }, _logger.Entries.ToList(), "Both links should be logged as unreadable");
    }

    /// <summary>
    /// A rename that fails (its target name taken by a folder) switches to memory only: no temporary file stays, the
    /// stored message is sent first, the failed one from memory, and the failure is logged once (E2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LiveBufferWriteMoveFailureKeepsMemoryOnly()
    {
        // Arrange
        WriteStored(4, 1, _start.AddHours(-1));
        Directory.CreateDirectory(StoredPath(5));

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);
        buffer.Add(Id(2), Json(SmallJson, 2));

        // Act
        var entries = await TakeAsync(buffer, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Id(1), Id(2) }, entries.Select(entry => entry.Id).ToArray(), "The stored message should come first, then the one kept in memory");
        Assert.AreSequenceEqual(Json(SmallJson, 2), entries[1].Utf8Json.ToArray(), "The message kept in memory should keep its JSON");
        Assert.IsTrue(buffer.Status.IsMemoryOnly, "A failed rename should switch the buffer to memory only");
        Assert.IsTrue(await WaitForAsync(() => TemporaryFiles().Count == 0), "No temporary file should stay after the failed rename");
        Assert.IsTrue(Directory.Exists(StoredPath(5)), "The folder in the way should be left alone");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: NotWritableLine }, _logger.Entries.ToList(), "The write failure should be logged once");
    }

    /// <summary>
    /// A file in place of the buffer folder switches to memory only: the messages are still returned in order, the
    /// failure is logged once and the file is left unchanged (E2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LiveBufferRunAsyncFolderBlockedKeepsMemoryOnly()
    {
        // Arrange
        BlockFolder();

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);
        buffer.Add(Id(1), Json(SmallJson, 1));
        buffer.Add(Id(2), Json(SmallJson, 2));

        // Act
        var entries = await TakeAsync(buffer, 2);

        // Assert
        Assert.AreSequenceEqual(new[] { Id(1), Id(2) }, entries.Select(entry => entry.Id).ToArray(), "The messages should be returned in order from memory");
        Assert.IsTrue(buffer.Status.IsMemoryOnly, "A blocked folder should switch the buffer to memory only");
        Assert.AreEqual("not a folder", await File.ReadAllTextAsync(Folder, TestContext.CancellationToken), "The file in place of the folder should be left unchanged");
        Assert.ContainsSingle(entry => entry is { Level: LogLevel.Warning, Message: NotWritableLine }, _logger.Entries.ToList(), "The write failure should be logged once");
    }

    /// <summary>
    /// After a write failure the memory keeps at most 64 messages or 16 MiB, dropping the oldest and keeping the order
    /// (E2).
    /// </summary>
    /// <param name="count">The number of messages added</param>
    /// <param name="length">The JSON length of each message</param>
    /// <param name="expectedWaiting">The expected number of waiting messages</param>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    [DataRow(70, SmallJson, 64, DisplayName = "Beyond 64 messages")]
    [DataRow(17, LiveBufferLimits.BytesPerMegabyte, 16, DisplayName = "Beyond 16 MiB")]
    public async Task LiveBufferAddAfterWriteFailureKeepsMemoryBound(int count, int length, int expectedWaiting)
    {
        // Arrange
        BlockFolder();

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        var memoryOnly = await WaitForAsync(() => buffer.Status.IsMemoryOnly);

        AddMany(buffer, count, length);

        // Act
        var entry = await NextAsync(buffer);

        // Assert
        Assert.IsTrue(memoryOnly, "A blocked folder should switch the buffer to memory only");
        Assert.AreEqual(Id(count - expectedWaiting + 1), entry.Id, "The oldest message kept should come first");
        Assert.AreEqual(expectedWaiting, buffer.Status.Waiting, "The memory should keep its bound");
        Assert.AreEqual(count - expectedWaiting, buffer.Status.Dropped, "The messages beyond the bound should be counted as dropped");
    }

    /// <summary>
    /// Adding messages and reading the status before the run touch no file system (N4).
    /// </summary>
    [TestMethod]
    [TestCategory("AC10")]
    public void LiveBufferAddAndStatusTouchNoFileSystem()
    {
        // Arrange
        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        buffer.Add(Id(1), Json(SmallJson, 1));
        buffer.Add(Id(2), Json(SmallJson, 2));
        buffer.Add(Id(3), Json(SmallJson, 3));

        // Act
        var status = buffer.Status;

        // Assert
        Assert.AreEqual(3, status.Waiting, "The added messages should wait");
        Assert.IsFalse(Directory.Exists(_root), "Neither the buffer folder nor its parent should be created before the run");
    }

    /// <summary>
    /// Messages waiting for their write keep at most 16 MiB; beyond it the oldest is dropped (N3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task LiveBufferAddPendingBeyondMemoryBoundDropsOldestPending()
    {
        // Arrange
        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        AddMany(buffer, 17, LiveBufferLimits.BytesPerMegabyte);

        var waiting = buffer.Status.Waiting;

        Start(buffer);

        // Act
        var entry = await NextAsync(buffer);

        // Assert
        Assert.AreEqual(16, waiting, "The messages waiting for their write should keep 16 MiB");
        Assert.AreEqual(Id(2), entry.Id, "The oldest message waiting for its write should be dropped");
        Assert.AreEqual(1, buffer.Status.Dropped, "The dropped message should be counted");
    }

    /// <summary>
    /// Messages written in this run are read from their files when they are sent, not held in memory: after every file
    /// is changed, each one is unreadable (N3).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC10")]
    public async Task LiveBufferNextAsyncReadsWrittenMessagesFromDisk()
    {
        // Arrange
        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        var written = await AddWrittenAsync(buffer, 20, LiveBufferLimits.BytesPerMegabyte);

        DamageEveryFile();
        buffer.Add(Id(21), Json(SmallJson, 21));

        // Act
        var entry = await NextAsync(buffer);

        // Assert
        Assert.IsTrue(written, "The 20 messages should be written to their files");
        Assert.AreEqual(Id(21), entry.Id, "Every changed message should be unreadable, so the fresh one comes first");
        Assert.AreEqual(20, buffer.Status.Dropped, "Every changed message should be counted as dropped");
        Assert.HasCount(20, _logger.Entries.Where(line => line.Message == OneUnreadableLine).ToList(), "Every changed message should be logged as unreadable");
    }

    /// <summary>
    /// Disposing the buffer ends its run and keeps the written files, the message in flight included (N5, E8).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    [TestCategory("AC10")]
    public async Task LiveBufferDisposeAsyncEndsRunAndKeepsWrittenFiles()
    {
        // Arrange
        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);

        var run = _runs[^1];

        buffer.Add(Id(1), Json(SmallJson, 1));
        buffer.Add(Id(2), Json(SmallJson, 2));

        var written = await WaitForAsync(() => MessageFiles().Count == 2);
        var inFlight = await NextAsync(buffer);

        // Act
        await buffer.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);

        // Assert
        var ended = await Task.WhenAny(run, Task.Delay(_guard, TestContext.CancellationToken)) == run;

        Assert.IsTrue(written, "Both messages should be written");
        Assert.AreEqual(Id(1), inFlight.Id, "The first message should be in flight");
        Assert.IsTrue(ended, "Disposing should end the run");
        Assert.AreSequenceEqual(new[] { Id(1), Id(2) }, StoredIds().ToArray(), "The written files, the one in flight included, should be kept");
    }

    /// <summary>
    /// After the buffer is disposed, an added message is ignored and a run neither starts nor creates the folder.
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    public async Task LiveBufferAddAndRunAsyncAfterDisposeAsyncAreIgnored()
    {
        // Arrange
        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        await buffer.DisposeAsync().AsTask().WaitAsync(_guard, TestContext.CancellationToken);

        // Act
        buffer.Add(Id(1), Json(SmallJson, 1));

        var run = buffer.RunAsync(TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(run.IsCompletedSuccessfully, "A run after disposing should end at once");
        Assert.AreEqual(0, buffer.Status.Waiting, "A message added after disposing should be ignored");
        Assert.IsFalse(Directory.Exists(_root), "A run after disposing should create no folder");
    }

    /// <summary>
    /// Acknowledging a message that is not in flight changes nothing: the message in flight stays, keeps its file and is
    /// returned again after a failed connection (E8).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC2")]
    public async Task LiveBufferAcknowledgeOtherIdKeepsInFlight()
    {
        // Arrange
        var buffer = CreateBuffer(LiveBufferLimits.Defaults);

        Start(buffer);
        buffer.Add(Id(1), Json(SmallJson, 1));
        buffer.Add(Id(2), Json(SmallJson, 2));

        var written = await WaitForAsync(() => MessageFiles().Count == 2);

        await NextAsync(buffer);

        // Act
        buffer.Acknowledge(Id(2));
        buffer.ReleaseInFlight();

        var again = await NextAsync(buffer);

        // Assert
        Assert.IsTrue(written, "Both messages should be written");
        Assert.AreEqual(Id(1), again.Id, "The message in flight should come again after acknowledging another identity");
        Assert.AreEqual(2, buffer.Status.Waiting, "Both messages should still wait");
        Assert.AreSequenceEqual(new[] { Id(1), Id(2) }, StoredIds().ToArray(), "Both files should be kept");
    }

    /// <summary>
    /// In memory only, the message in flight and the newest message are kept even when together they exceed 16 MiB: the
    /// bound drops neither (E2).
    /// </summary>
    /// <returns>A task that completes when the test is done</returns>
    [TestMethod]
    [TestCategory("AC8")]
    public async Task LiveBufferAddAfterWriteFailureKeepsInFlightAndNewest()
    {
        // Arrange
        BlockFolder();

        var buffer = CreateBuffer(LiveBufferLimits.Defaults);
        var length = 9 * LiveBufferLimits.BytesPerMegabyte;

        Start(buffer);

        var memoryOnly = await WaitForAsync(() => buffer.Status.IsMemoryOnly);

        buffer.Add(Id(1), Json(length, 1));

        var inFlight = await NextAsync(buffer);

        // Act
        buffer.Add(Id(2), Json(length, 2));
        buffer.Acknowledge(inFlight.Id);

        var newest = await NextAsync(buffer);

        // Assert
        Assert.IsTrue(memoryOnly, "A blocked folder should switch the buffer to memory only");
        Assert.AreEqual(Id(1), inFlight.Id, "The first message should be in flight");
        Assert.AreEqual(Id(2), newest.Id, "The newest message should be kept beside the one in flight");
        Assert.AreEqual(0, buffer.Status.Dropped, "Neither the message in flight nor the newest should be dropped");
    }

    /// <summary>
    /// Gets the identity of a message by its number.
    /// </summary>
    /// <param name="number">The number</param>
    /// <returns>The identity</returns>
    private static Guid Id(int number)
    {
        return new Guid(number, 0x4b3d, 0x4e8f, 0x9a, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd);
    }

    /// <summary>
    /// Creates the JSON of a message by its number.
    /// </summary>
    /// <param name="length">The length in bytes</param>
    /// <param name="number">The number of the message</param>
    /// <returns>The JSON</returns>
    private static byte[] Json(int length, int number)
    {
        var json = new byte[length];

        Array.Fill(json, (byte)('a' + (number % 26)));

        return json;
    }

    /// <summary>
    /// Creates limits.
    /// </summary>
    /// <param name="hours">The age limit in hours</param>
    /// <param name="megabytes">The size limit in megabytes</param>
    /// <returns>The limits</returns>
    private static LiveBufferLimits Limits(int hours, int megabytes)
    {
        return new LiveBufferLimits
               {
                   Hours = hours,
                   Megabytes = megabytes
               };
    }

    /// <summary>
    /// Formats the file of a small message by its number.
    /// </summary>
    /// <param name="number">The number of the message</param>
    /// <param name="takenAt">The time taken</param>
    /// <returns>The bytes of the file</returns>
    private static byte[] Format(int number, DateTimeOffset takenAt)
    {
        return LiveBufferFile.Format(new LiveBufferEntry
                                     {
                                         Id = Id(number),
                                         TakenAt = takenAt,
                                         Utf8Json = Json(SmallJson, number)
                                     });
    }

    /// <summary>
    /// Damages the bytes of a file.
    /// </summary>
    /// <param name="file">The bytes</param>
    /// <param name="damage">The damage: magic, cut, grown or json</param>
    /// <returns>The damaged bytes</returns>
    private static byte[] Damaged(byte[] file, string damage)
    {
        var copy = file.ToArray();

        switch (damage)
        {
            case "magic":
                {
                    copy[0] = (byte)'X';

                    return copy;
                }

            case "cut":
                {
                    return copy[..^1];
                }

            case "grown":
                {
                    return [.. copy, (byte)' '];
                }

            default:
                {
                    copy[LiveBufferFile.HeaderLength + 1] ^= 0x01;

                    return copy;
                }
        }
    }

    /// <summary>
    /// Damages a stored file after the load.
    /// </summary>
    /// <param name="path">The path of the file</param>
    /// <param name="damage">The damage: json, grown, cut, deleted or identity</param>
    private static void DamageAfterLoad(string path, string damage)
    {
        switch (damage)
        {
            case "deleted":
                File.Delete(path);

                break;

            case "identity":
                File.WriteAllBytes(path, Format(9, new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero)));

                break;

            default:
                File.WriteAllBytes(path, Damaged(File.ReadAllBytes(path), damage));

                break;
        }
    }

    /// <summary>
    /// Writes the same content into several files.
    /// </summary>
    /// <param name="paths">The paths</param>
    /// <param name="content">The content</param>
    private static void WriteFiles(IEnumerable<string> paths, byte[] content)
    {
        foreach (var path in paths)
        {
            File.WriteAllBytes(path, content);
        }
    }

    /// <summary>
    /// Adds messages numbered from 1.
    /// </summary>
    /// <param name="buffer">The buffer</param>
    /// <param name="count">The number of messages</param>
    /// <param name="length">The JSON length of each message</param>
    private static void AddMany(LiveBuffer buffer, int count, int length)
    {
        foreach (var number in Enumerable.Range(1, count))
        {
            buffer.Add(Id(number), Json(length, number));
        }
    }

    /// <summary>
    /// Reads the identity of a buffer file, or none when it cannot be read.
    /// </summary>
    /// <param name="path">The path of the file</param>
    /// <returns>The identity, or <see langword="null" /></returns>
    private static Guid? TryReadId(string path)
    {
        try
        {
            return LiveBufferFile.Read(File.ReadAllBytes(path)).Entry?.Id;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a buffer in the test's folder, disposed after the test.
    /// </summary>
    /// <param name="limits">The limits</param>
    /// <returns>The buffer</returns>
    private LiveBuffer CreateBuffer(LiveBufferLimits limits)
    {
        var buffer = new LiveBuffer(Folder, limits, _clock, _logger);

        _buffers.Add(buffer);

        return buffer;
    }

    /// <summary>
    /// Starts the run of a buffer, stopped after the test.
    /// </summary>
    /// <param name="buffer">The buffer</param>
    private void Start(LiveBuffer buffer)
    {
        _runs.Add(buffer.RunAsync(TestContext.CancellationToken));
    }

    /// <summary>
    /// Waits for the next message of a buffer, at most the guard time.
    /// </summary>
    /// <param name="buffer">The buffer</param>
    /// <returns>The message</returns>
    private Task<LiveBufferEntry> NextAsync(LiveBuffer buffer)
    {
        return buffer.NextAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
    }

    /// <summary>
    /// Takes and acknowledges messages of a buffer in turn.
    /// </summary>
    /// <param name="buffer">The buffer</param>
    /// <param name="count">The number of messages</param>
    /// <returns>The messages</returns>
    private async Task<List<LiveBufferEntry>> TakeAsync(LiveBuffer buffer, int count)
    {
        var entries = new List<LiveBufferEntry>();

        for (var index = 0; index < count; index++)
        {
            var entry = await NextAsync(buffer);

            buffer.Acknowledge(entry.Id);
            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// Adds messages numbered from 1 one at a time, each after the one before is written to its file.
    /// </summary>
    /// <param name="buffer">The buffer</param>
    /// <param name="count">The number of messages</param>
    /// <param name="length">The JSON length of each message</param>
    /// <returns><see langword="true" /> when every message was written</returns>
    private async Task<bool> AddWrittenAsync(LiveBuffer buffer, int count, int length)
    {
        var written = true;

        foreach (var number in Enumerable.Range(1, count))
        {
            buffer.Add(Id(number), Json(length, number));
            written &= await WaitForAsync(() => MessageFiles().Count == number);
        }

        return written;
    }

    /// <summary>
    /// Polls a file system condition, ten milliseconds apart, until it holds or the polls run out.
    /// </summary>
    /// <param name="condition">The condition</param>
    /// <returns>Whether the condition holds</returns>
    private async Task<bool> WaitForAsync(Func<bool> condition)
    {
        var polls = 0;

        while (condition() == false && polls < MaxPolls)
        {
            await Task.Delay(10, TestContext.CancellationToken);

            polls++;
        }

        return condition();
    }

    /// <summary>
    /// Writes a stored message of an earlier run.
    /// </summary>
    /// <param name="sequence">The sequence of the file</param>
    /// <param name="number">The number of the message</param>
    /// <param name="takenAt">The time taken</param>
    private void WriteStored(long sequence, int number, DateTimeOffset takenAt)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(StoredPath(sequence), Format(number, takenAt));
    }

    /// <summary>
    /// Gets the path of a message file.
    /// </summary>
    /// <param name="sequence">The sequence</param>
    /// <returns>The path</returns>
    private string StoredPath(long sequence)
    {
        return Path.Combine(Folder, LiveBufferFile.MessageName(sequence));
    }

    /// <summary>
    /// Puts a file in place of the buffer folder.
    /// </summary>
    private void BlockFolder()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Folder, "not a folder");
    }

    /// <summary>
    /// Gets the message files of the buffer folder, ordered by name.
    /// </summary>
    /// <returns>The paths</returns>
    private List<string> MessageFiles()
    {
        return Directory.Exists(Folder)
                   ? [.. Directory.EnumerateFiles(Folder, "*.msg").Order(StringComparer.Ordinal)]
                   : [];
    }

    /// <summary>
    /// Gets the temporary files of the buffer folder.
    /// </summary>
    /// <returns>The paths</returns>
    private List<string> TemporaryFiles()
    {
        return Directory.Exists(Folder)
                   ? [.. Directory.EnumerateFiles(Folder, "*.tmp")]
                   : [];
    }

    /// <summary>
    /// Gets the identities of the readable message files, ordered by name.
    /// </summary>
    /// <returns>The identities</returns>
    private List<Guid> StoredIds()
    {
        return [.. MessageFiles().Select(TryReadId).OfType<Guid>()];
    }

    /// <summary>
    /// Changes one JSON byte of every message file, keeping its length.
    /// </summary>
    private void DamageEveryFile()
    {
        foreach (var path in MessageFiles())
        {
            File.WriteAllBytes(path, Damaged(File.ReadAllBytes(path), "json"));
        }
    }

    #endregion // Methods
}