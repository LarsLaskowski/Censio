using System.Collections.Concurrent;
using System.Globalization;

using Censio.Pipe;
using Censio.Pipe.Tests;

using Microsoft.Extensions.Time.Testing;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="DiagnosticsWindowSession" />.
/// </summary>
[TestClass]
public sealed class DiagnosticsWindowSessionTests
{
    #region Fields

    /// <summary>
    /// The current time of the clock of the session: 2026-11-02 18:00:00 in the local time zone of <see cref="CreateClock()" />.
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 17, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The receive time of the first frame of the live and replayed runs.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 17, 9, 0, TimeSpan.Zero);

    /// <summary>
    /// The real-time limit that keeps a broken implementation from hanging a test.
    /// </summary>
    private static readonly TimeSpan _guard = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The actions posted to the window's thread and not yet run.
    /// </summary>
    private readonly ConcurrentQueue<Action> _posted = [];

    /// <summary>
    /// The windows the session created, in order.
    /// </summary>
    private readonly List<FakeDiagnosticsWindow> _windows = [];

    /// <summary>
    /// The status text the session reads.
    /// </summary>
    private string _statusText = "Receiving";

    /// <summary>
    /// The tick the session reads.
    /// </summary>
    private PipeTick? _tick;

    /// <summary>
    /// Whether a posted action runs at once instead of waiting in <see cref="_posted" />.
    /// </summary>
    private bool _postAtOnce;

    /// <summary>
    /// The number of times the session read the status text.
    /// </summary>
    private int _statusReads;

    /// <summary>
    /// The number of times the session read the tick.
    /// </summary>
    private int _tickReads;

    /// <summary>
    /// The number of status reads right after the session opened its window.
    /// </summary>
    private int _statusReadsAfterOpen;

    /// <summary>
    /// The number of tick reads right after the session opened its window.
    /// </summary>
    private int _tickReadsAfterOpen;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsWindowSessionTests" /> class.
    /// </summary>
    /// <param name="testContext">The test context</param>
    public DiagnosticsWindowSessionTests(TestContext testContext)
    {
        TestContext = testContext;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the test context.
    /// </summary>
    public TestContext TestContext { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A notification shows the latest tick in the open window.
    /// </summary>
    [TestMethod]
    public void DiagnosticsWindowSessionNotifyShowsLatestTick()
    {
        // Arrange
        var session = CreateSession(CreateClock());
        var next = CreateTick(200, _now.AddMinutes(-1));

        _tick = CreateTick(100, _now.AddMinutes(-2));
        session.Open();
        _tick = next;

        // Act
        session.Notify();
        RunPosted();

        // Assert
        var views = _windows.Single().Views;

        Assert.HasCount(2, views, "The window should show the view of the open and the one of the notification");
        Assert.AreSame(next, views[1].Tick, "The notification should show the latest tick");
        Assert.AreEqual("Game time: 200 (no data yet)", views[1].HeaderLines[2], "The header should show the latest tick's game time");
    }

    /// <summary>
    /// A notification after a status change shows the new status text.
    /// </summary>
    [TestMethod]
    public void DiagnosticsWindowSessionNotifyShowsNewStatusText()
    {
        // Arrange
        var session = CreateSession(CreateClock());

        session.Open();
        _statusText = "Waiting for game";

        // Act
        session.Notify();
        RunPosted();

        // Assert
        var views = _windows.Single().Views;

        Assert.AreEqual("Status: Receiving", views[0].HeaderLines[0], "The open should show the status of then");
        Assert.AreEqual("Status: Waiting for game", views[^1].HeaderLines[0], "The notification should show the new status");
    }

    /// <summary>
    /// Notifications faster than the window updates leave at most one update waiting, and it shows the last tick.
    /// </summary>
    [TestMethod]
    public void DiagnosticsWindowSessionNotifyCoalescesToOnePendingUpdate()
    {
        // Arrange
        var session = CreateSession(CreateClock());
        var last = CreateTick(400, _now);

        session.Open();

        // Act
        _tick = CreateTick(200, _now);
        session.Notify();
        _tick = CreateTick(300, _now);
        session.Notify();
        _tick = last;
        session.Notify();

        var pending = _posted.Count;

        RunPosted();

        // Assert
        var views = _windows.Single().Views;

        Assert.AreEqual(1, pending, "Three notifications before the update runs should leave one update waiting");
        Assert.HasCount(2, views, "The one update should show one view");
        Assert.AreSame(last, views[1].Tick, "The update should show the last tick");
    }

    /// <summary>
    /// A notification after local midnight shows the last tick of the day before with its date.
    /// </summary>
    [TestMethod]
    public void DiagnosticsWindowSessionNotifyAfterMidnightShowsDatedLastTick()
    {
        // Arrange
        var clock = CreateClock(new DateTimeOffset(2026, 11, 2, 22, 59, 30, TimeSpan.Zero));
        var session = CreateSession(clock);

        _tick = CreateTick(100, new DateTimeOffset(2026, 11, 2, 22, 59, 0, TimeSpan.Zero));
        session.Open();
        clock.Advance(TimeSpan.FromMinutes(1));

        // Act
        session.Notify();
        RunPosted();

        // Assert
        var views = _windows.Single().Views;

        Assert.AreEqual("Last tick: 23:59:00, 1 island", views[0].HeaderLines[1], "Before midnight the last tick should show the time only");
        Assert.AreEqual("Last tick: 2026-11-02 23:59:00, 1 island", views[^1].HeaderLines[1], "After midnight the last tick should show its date");
    }

    /// <summary>
    /// With no window open a notification reads neither the status nor the tick and shows nothing.
    /// </summary>
    [TestMethod]
    public void DiagnosticsWindowSessionClosedShowsNothingOnNotify()
    {
        // Arrange
        var session = CreateSession(CreateClock());

        _tick = CreateTick(100, _now);

        // Act
        session.Notify();
        RunPosted();

        // Assert
        Assert.AreEqual(0, _statusReads, "No status should be read without a window");
        Assert.AreEqual(0, _tickReads, "No tick should be read without a window");
        Assert.IsEmpty(_windows, "No window should be created");
        Assert.IsFalse(session.IsOpen, "No window should be open");
    }

    /// <summary>
    /// Opening the window again shows at once the tick that closed while no window was open.
    /// </summary>
    [TestMethod]
    public void DiagnosticsWindowSessionReopenShowsTickClosedWhileClosed()
    {
        // Arrange
        var session = CreateSession(CreateClock());
        var closedWhileClosed = CreateTick(200, _now);

        _tick = CreateTick(100, _now.AddMinutes(-1));
        session.Open();
        _windows[0].Close();
        _tick = closedWhileClosed;
        session.Notify();
        RunPosted();

        // Act
        session.Open();

        // Assert
        Assert.HasCount(2, _windows, "Opening again should create a new window");
        Assert.AreSame(closedWhileClosed, _windows[1].Views.Single().Tick, "The new window should show the tick that closed while no window was open");
    }

    /// <summary>
    /// Opening while a window is open brings that window to the front instead of creating a second one.
    /// </summary>
    [TestMethod]
    public void DiagnosticsWindowSessionOpenTwiceBringsWindowToFront()
    {
        // Arrange
        var session = CreateSession(CreateClock());

        session.Open();

        // Act
        session.Open();

        // Assert
        Assert.HasCount(1, _windows, "No second window should be created");
        Assert.AreEqual(1, _windows[0].BringToFrontCount, "The open window should be brought to the front");
        Assert.IsTrue(session.IsOpen, "The window should stay open");
    }

    /// <summary>
    /// Opening after the window closed creates a new window.
    /// </summary>
    [TestMethod]
    public void DiagnosticsWindowSessionOpenAfterCloseCreatesNewWindow()
    {
        // Arrange
        var session = CreateSession(CreateClock());

        session.Open();
        _windows[0].Close();

        var openAfterClose = session.IsOpen;

        // Act
        session.Open();

        // Assert
        Assert.IsFalse(openAfterClose, "The closed window should no longer be open");
        Assert.HasCount(2, _windows, "Opening after the close should create a new window");
        Assert.HasCount(1, _windows[1].Views, "The new window should show a view at once");
        Assert.AreEqual(0, _windows[0].BringToFrontCount, "The closed window should not be brought to the front");
        Assert.IsTrue(session.IsOpen, "The new window should be open");
    }

    /// <summary>
    /// After the window closed, a new tick makes no view: the session reads neither the status nor the tick and shows
    /// nothing, neither in the closed window nor in a new one.
    /// </summary>
    [TestMethod]
    public void DiagnosticsWindowSessionClosedHoldsNoView()
    {
        // Arrange
        var session = CreateSession(CreateClock());

        _tick = CreateTick(100, _now.AddMinutes(-1));
        session.Open();
        _windows[0].Close();

        var statusReadsAfterClose = _statusReads;
        var tickReadsAfterClose = _tickReads;

        _tick = CreateTick(200, _now);

        // Act
        session.Notify();
        RunPosted();

        // Assert
        Assert.IsFalse(session.IsOpen, "The window should be closed");
        Assert.AreEqual(statusReadsAfterClose, _statusReads, "No status should be read for a view once the window closed");
        Assert.AreEqual(tickReadsAfterClose, _tickReads, "No tick should be read for a view once the window closed");
        Assert.HasCount(1, _windows[0].Views, "The closed window should be shown no further view");
        Assert.HasCount(1, _windows, "No window should be created for the new tick");
    }

    /// <summary>
    /// A stalled window neither slows nor changes a replay: the replay reads on, and its ticks and states equal those of a
    /// replay without a window.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task DiagnosticsWindowSessionStalledWindowLeavesReplayUnchanged()
    {
        // Arrange
        var expected = await ReplayAsync(_ =>
                                         {
                                         });

        // Act
        var actual = await ReplayAsync(runner =>
                                       {
                                           var session = OpenStalledSession(() => TrayMenu.FormatReplayStatusText(runner.Status), () => runner.LatestTick);

                                           runner.StatusChanged += (_, _) => session.Notify();
                                           runner.TickReceived += (_, _) => session.Notify();
                                       });

        // Assert
        Assert.HasCount(3, expected.Ticks, "The replay without a window should give three ticks");
        Assert.AreEqual(_statusReadsAfterOpen, _statusReads, "The stalled window should read no status while the replay runs");
        Assert.AreEqual(_tickReadsAfterOpen, _tickReads, "The stalled window should read no tick while the replay runs");
        Assert.HasCount(1, _posted, "Exactly one update should wait for the stalled window");
        Assert.AreSequenceEqual(expected.Ticks, actual.Ticks, "The ticks should equal those of a replay without a window");
        Assert.AreSequenceEqual(expected.Statuses, actual.Statuses, "The states should equal those of a replay without a window");
    }

    /// <summary>
    /// A stalled window neither slows nor changes a live connection with a running recording: the connection reads on,
    /// and its ticks, states and recorded frames equal those of a run without a window.
    /// </summary>
    /// <returns>The task</returns>
    [TestMethod]
    public async Task DiagnosticsWindowSessionStalledWindowLeavesLiveConnectionUnchanged()
    {
        // Arrange
        var expected = await RunLiveAsync((_, _) =>
                                          {
                                          });

        // Act
        var actual = await RunLiveAsync((connection, recording) =>
                                        {
                                            var session = OpenStalledSession(() => TrayMenu.FormatStatusText(connection.Status.State, recording.State), () => connection.LatestTick);

                                            connection.StatusChanged += (_, _) => session.Notify();
                                            connection.TickReceived += (_, _) => session.Notify();
                                            recording.StateChanged += (_, _) => session.Notify();
                                        });

        // Assert
        Assert.HasCount(3, expected.Ticks, "The run without a window should give three ticks");
        Assert.AreEqual(_statusReadsAfterOpen, _statusReads, "The stalled window should read no status while the frames are read");
        Assert.AreEqual(_tickReadsAfterOpen, _tickReads, "The stalled window should read no tick while the frames are read");
        Assert.HasCount(1, _posted, "Exactly one update should wait for the stalled window");
        Assert.AreSequenceEqual(expected.Ticks, actual.Ticks, "The ticks should equal those of a run without a window");
        Assert.AreSequenceEqual(expected.Statuses, actual.Statuses, "The states should equal those of a run without a window");
        Assert.AreEqual(expected.Recording, actual.Recording, "The recorded frames should equal those of a run without a window");
    }

    /// <summary>
    /// Creates a clock at <see cref="_now" /> in a local time zone one hour ahead of UTC.
    /// </summary>
    /// <returns>The clock</returns>
    private static FakeTimeProvider CreateClock()
    {
        return CreateClock(_now);
    }

    /// <summary>
    /// Creates a clock at a time in a local time zone one hour ahead of UTC.
    /// </summary>
    /// <param name="utcNow">The current time of the clock</param>
    /// <returns>The clock</returns>
    private static FakeTimeProvider CreateClock(DateTimeOffset utcNow)
    {
        var clock = new FakeTimeProvider(utcNow);

        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Censio Test", TimeSpan.FromHours(1), "Censio Test", "Censio Test"));

        return clock;
    }

    /// <summary>
    /// Creates a tick of one island without entries.
    /// </summary>
    /// <param name="gameTime">The game time</param>
    /// <param name="receiveTime">The receive time</param>
    /// <returns>The tick</returns>
    private static PipeTick CreateTick(long gameTime, DateTimeOffset receiveTime)
    {
        return new PipeTick
               {
                   GameTime = gameTime,
                   ReceiveTime = receiveTime,
                   Islands = [
                                 new PipeTickIsland
                                 {
                                     Key = new PipeIslandKey(1, 1, 0),
                                     SessionId = 4,
                                     Name = "Ostia",
                                     Entries = []
                                 }
                             ],
                   CloseReason = PipeTickCloseReason.GameTimeChanged
               };
    }

    /// <summary>
    /// Describes a tick by its game time, receive time and island count.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <returns>The description</returns>
    private static string Describe(PipeTick tick)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{tick.GameTime} {tick.ReceiveTime:O} {tick.Islands.Count}");
    }

    /// <summary>
    /// Gets the frames of the live and replayed runs: the version and island frames of three ticks.
    /// </summary>
    /// <returns>The frames without their length prefix</returns>
    private static byte[][] Frames()
    {
        return [
                   PipeTestFrames.Version(2),
                   PipeTestFrames.Island(1, 100),
                   PipeTestFrames.Island(2, 100),
                   PipeTestFrames.Island(1, 200),
                   PipeTestFrames.Island(1, 300),
                   PipeTestFrames.Island(2, 300)
               ];
    }

    /// <summary>
    /// Creates the session under test, whose windows are kept in <see cref="_windows" />.
    /// </summary>
    /// <param name="clock">The clock</param>
    /// <returns>The session</returns>
    private DiagnosticsWindowSession CreateSession(TimeProvider clock)
    {
        return new DiagnosticsWindowSession(ReadStatusText, ReadTick, () => null, clock, Post, CreateWindow);
    }

    /// <summary>
    /// Creates a window and keeps it in <see cref="_windows" />.
    /// </summary>
    /// <returns>The window</returns>
    private FakeDiagnosticsWindow CreateWindow()
    {
        var window = new FakeDiagnosticsWindow();

        _windows.Add(window);

        return window;
    }

    /// <summary>
    /// Reads the status text and counts the read.
    /// </summary>
    /// <returns>The status text</returns>
    private string ReadStatusText()
    {
        Interlocked.Increment(ref _statusReads);

        return _statusText;
    }

    /// <summary>
    /// Reads the tick and counts the read.
    /// </summary>
    /// <returns>The tick</returns>
    private PipeTick? ReadTick()
    {
        Interlocked.Increment(ref _tickReads);

        return _tick;
    }

    /// <summary>
    /// Posts an action to the window's thread: runs it at once while <see cref="_postAtOnce" /> is set, otherwise queues it.
    /// </summary>
    /// <param name="action">The action</param>
    private void Post(Action action)
    {
        if (_postAtOnce)
        {
            action();
        }
        else
        {
            _posted.Enqueue(action);
        }
    }

    /// <summary>
    /// Runs the posted actions in order, including those they post.
    /// </summary>
    private void RunPosted()
    {
        while (_posted.TryDequeue(out var action))
        {
            action();
        }
    }

    /// <summary>
    /// Creates a session over the given reads, opens its window with posted actions running at once, takes the read counts,
    /// then stalls the window: every later posted action waits in <see cref="_posted" /> and never runs.
    /// </summary>
    /// <param name="readStatusText">Reads the status text</param>
    /// <param name="readTick">Reads the latest tick</param>
    /// <returns>The session</returns>
    private DiagnosticsWindowSession OpenStalledSession(Func<string> readStatusText, Func<PipeTick?> readTick)
    {
        var session = new DiagnosticsWindowSession(() =>
                                                   {
                                                       Interlocked.Increment(ref _statusReads);

                                                       return readStatusText();
                                                   },
                                                   () =>
                                                   {
                                                       Interlocked.Increment(ref _tickReads);

                                                       return readTick();
                                                   },
                                                   () => null,
                                                   CreateClock(),
                                                   Post,
                                                   CreateWindow);

        _postAtOnce = true;
        session.Open();

        _statusReadsAfterOpen = Volatile.Read(ref _statusReads);
        _tickReadsAfterOpen = Volatile.Read(ref _tickReads);
        _postAtOnce = false;

        return session;
    }

    /// <summary>
    /// Replays a recording of <see cref="Frames" /> at maximum speed to its end.
    /// </summary>
    /// <param name="attach">Subscribes to the runner before it runs</param>
    /// <returns>The descriptions of the ticks and the states, in order</returns>
    private async Task<(string[] Ticks, PipeReplayStatus[] Statuses)> ReplayAsync(Action<PipeReplayRunner> attach)
    {
        var recording = PipeTestFrames.Recording(_start, [.. Frames().Select((frame, index) => (_start.AddSeconds(index), frame))]);
        var runner = new PipeReplayRunner(() => new MemoryStream(recording), PipeReplaySpeed.Maximum, TimeProvider.System);
        var ticks = new ConcurrentQueue<string>();
        var statuses = new ConcurrentQueue<PipeReplayStatus>();

        runner.TickReceived += (_, tick) => ticks.Enqueue(Describe(tick));
        runner.StatusChanged += (_, status) => statuses.Enqueue(status);
        attach(runner);

        await runner.RunAsync(TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

        return ([.. ticks], [.. statuses]);
    }

    /// <summary>
    /// Feeds <see cref="Frames" /> to a live connection with a running recording, one second apart, ends the stream so the
    /// last tick closes, waits until the connection waits for the game again and stops it and the recording.
    /// </summary>
    /// <param name="attach">Subscribes to the connection and the recording before the run starts</param>
    /// <returns>The descriptions of the ticks, the states, in order, and the text of the recording</returns>
    private async Task<(string[] Ticks, PipeConnectionStatus[] Statuses, string Recording)> RunLiveAsync(Action<PipeConnection, PipeRecordingSession> attach)
    {
        var clock = new RecordingTimeProvider(_start);
        var connector = new ScriptedPipeConnector(clock);
        var stream = new ControlledPipeStream();
        var sink = new ControlledWriteStream();
        var recording = new PipeRecordingSession(_ => sink);
        var connection = new PipeConnection(connector, clock, recording);
        var ticks = new ConcurrentQueue<string>();
        var statuses = new ConcurrentQueue<PipeConnectionStatus>();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = Frames();

        connector.Enqueue(PipeConnectResult.FromStream(stream));
        recording.Start();
        connection.TickReceived += (_, tick) => ticks.Enqueue(Describe(tick));
        connection.StatusChanged += (_, status) =>
                                    {
                                        statuses.Enqueue(status);

                                        if (status.State == PipeConnectionState.WaitingForGame)
                                        {
                                            waiting.TrySetResult();
                                        }
                                    };
        attach(connection, recording);

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var run = connection.RunAsync(stop.Token);

        try
        {
            await stream.WaitForPendingReadsAsync(1, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);

            for (var index = 0; index < frames.Length; index++)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                stream.Feed(PipeTestFrames.Wire(frames[index]));

                await stream.WaitForPendingReadsAsync(index + 2, TestContext.CancellationToken).WaitAsync(_guard, TestContext.CancellationToken);
            }

            stream.End();

            await waiting.Task.WaitAsync(_guard, TestContext.CancellationToken);
        }
        finally
        {
            await stop.CancelAsync();
            await run.WaitAsync(_guard, TestContext.CancellationToken);
        }

        await recording.StopAsync(_guard).WaitAsync(_guard, TestContext.CancellationToken);

        return ([.. ticks], [.. statuses], sink.Text);
    }

    #endregion // Methods
}