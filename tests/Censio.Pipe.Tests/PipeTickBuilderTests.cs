namespace Censio.Pipe.Tests;

/// <summary>
/// Tests for <see cref="PipeTickBuilder" />.
/// </summary>
[TestClass]
public sealed class PipeTickBuilderTests
{
    #region Fields

    /// <summary>
    /// The receive time of the first item of a test.
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The island key of most tests.
    /// </summary>
    private static readonly PipeIslandKey _ostia = new(1, 1, 0);

    /// <summary>
    /// The session GUIDs of the frames over the island limit.
    /// </summary>
    private static readonly int[] _overGuids = [901, 902, 903];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the session events with the result kind each one is delivered as after an island frame.
    /// </summary>
    public static IEnumerable<object[]> SessionEvents
    {
        get
        {
            yield return [PipeMessageType.SessionStart, PipeTickResultKind.SessionStart];
            yield return [PipeMessageType.SessionEnd, PipeTickResultKind.SessionEnd];
        }
    }

    /// <summary>
    /// Gets the items that are not island frames and skip nothing, with the result kind each one is delivered as.
    /// </summary>
    public static IEnumerable<object[]> NonIslandItems
    {
        get
        {
            yield return [
                             PipeReadResult.FromError(new PipeError
                                                      {
                                                          Kind = PipeErrorKind.MalformedMessage,
                                                          TypeValue = 3,
                                                          Offset = 5
                                                      }),
                             PipeTickResultKind.Error
                         ];
            yield return [PipeReadResult.FromMessage(new VersionMessage(2)), PipeTickResultKind.Version];
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// A builder without a tick has no quiet deadline.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderWithoutTickHasNoQuietDeadline()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        // Act
        var deadline = builder.QuietDeadline;

        // Assert
        Assert.IsNull(deadline, "A new builder should have no quiet deadline");
    }

    /// <summary>
    /// Frames with one timestamp form one tick that a frame of another timestamp closes.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderFramesWithOneTimestampFormOneTick()
    {
        // Arrange
        var builder = new PipeTickBuilder();
        var ostia = CreateEntries(2);
        var neapolis = CreateEntries(1);
        var capua = CreateEntries(3);

        var first = builder.Add(Frame(new PipeIslandKey(1, 1, 0), "Ostia", 100, ostia, 11), At(0));
        var second = builder.Add(Frame(new PipeIslandKey(1, 2, 0), "Neapolis", 100, neapolis, 12), At(1));
        var third = builder.Add(Frame(new PipeIslandKey(1, 3, 0), "Capua", 100, capua, 13), At(2));

        // Act
        var closing = builder.Add(Frame(new PipeIslandKey(1, 1, 0), "Ostia", 200, ostia), At(3));

        // Assert
        Assert.IsEmpty(first, "The first frame should deliver nothing");
        Assert.IsEmpty(second, "The second frame should deliver nothing");
        Assert.IsEmpty(third, "The third frame should deliver nothing");
        Assert.HasCount(1, closing, "The frame of the next timestamp should deliver one item");

        var tick = AssertTick(closing[0], PipeTickCloseReason.GameTimeChanged);

        Assert.AreEqual(100L, tick.GameTime, "The tick should carry the game time of its frames");
        Assert.HasCount(3, tick.Islands, "The tick should hold three islands");
        Assert.AreEqual(new PipeIslandKey(1, 1, 0), tick.Islands[0].Key, "The first island should be Ostia");
        Assert.AreEqual(new PipeIslandKey(1, 2, 0), tick.Islands[1].Key, "The second island should be Neapolis");
        Assert.AreEqual(new PipeIslandKey(1, 3, 0), tick.Islands[2].Key, "The third island should be Capua");
        Assert.AreEqual("Ostia", tick.Islands[0].Name, "The first island should carry its name");
        Assert.AreEqual((byte)11, tick.Islands[0].SessionId, "The first island should carry its session ID");
        Assert.AreEqual((byte)13, tick.Islands[2].SessionId, "The third island should carry its session ID");
        Assert.AreSequenceEqual(ostia, tick.Islands[0].Entries, "The first island should carry its entries");
        Assert.AreSequenceEqual(neapolis, tick.Islands[1].Entries, "The second island should carry its entries");
        Assert.AreSequenceEqual(capua, tick.Islands[2].Entries, "The third island should carry its entries");
        Assert.AreEqual(At(3), builder.QuietDeadline!.Value - PipeTickLimits.QuietPeriod, "The frame of the next timestamp should open a tick of its own");
    }

    /// <summary>
    /// A tick carries the receive time of its last island frame.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderTickReceiveTimeIsLastFrame()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(new PipeIslandKey(1, 1, 0), "Ostia", 100), At(0));
        builder.Add(Frame(new PipeIslandKey(1, 2, 0), "Neapolis", 100), At(1.5));
        builder.Add(Frame(new PipeIslandKey(1, 3, 0), "Capua", 100), At(2.5));

        // Act
        var results = builder.Add(Frame(new PipeIslandKey(1, 1, 0), "Ostia", 200), At(5));

        // Assert
        var tick = AssertTick(results[0], PipeTickCloseReason.GameTimeChanged);

        Assert.AreEqual(At(2.5), tick.ReceiveTime, "The tick should carry the receive time of its last island frame");
    }

    /// <summary>
    /// A higher or a lower timestamp closes the tick as game time changed.
    /// </summary>
    /// <param name="timestamp">The timestamp of the second frame</param>
    [TestMethod]
    [DataRow(101L)]
    [DataRow(99L)]
    public void PipeTickBuilderChangedTimestampClosesTickAsGameTimeChanged(long timestamp)
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        // Act
        var results = builder.Add(Frame(new PipeIslandKey(1, 2, 0), "Neapolis", timestamp), At(1));

        // Assert
        Assert.HasCount(1, results, "The changed timestamp should deliver the open tick");

        var tick = AssertTick(results[0], PipeTickCloseReason.GameTimeChanged);

        Assert.AreEqual(100L, tick.GameTime, "The closed tick should keep its game time");
        Assert.HasCount(1, tick.Islands, "The closed tick should not hold the island of the new timestamp");
    }

    /// <summary>
    /// A frame at the quiet deadline closes the open tick as quiet period before it is added.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderFrameAtQuietDeadlineClosesTickAsQuietPeriodFirst()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        // Act
        var results = builder.Add(Frame(new PipeIslandKey(1, 2, 0), "Neapolis", 100), At(30));

        // Assert
        Assert.HasCount(1, results, "The frame at the deadline should deliver the open tick");

        var tick = AssertTick(results[0], PipeTickCloseReason.QuietPeriod);

        Assert.HasCount(1, tick.Islands, "The quiet-period tick should not hold the frame that arrived at the deadline");
        Assert.AreEqual(At(0), tick.ReceiveTime, "The quiet-period tick should carry the receive time of its last frame");
        Assert.AreEqual(At(60), builder.QuietDeadline, "The frame should open the next tick with its own deadline");
    }

    /// <summary>
    /// A frame with the game time of a tick closed by the quiet period opens a new tick with that game time.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderFrameAfterQuietCloseOpensTickWithSameGameTime()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        var quiet = builder.CloseIfQuiet(At(30));

        var added = builder.Add(Frame(_ostia, "Ostia", 100), At(31));

        // Act
        var second = builder.CloseIfQuiet(At(61));

        // Assert
        Assert.IsNotNull(quiet, "The deadline should close the first tick");
        Assert.AreEqual(PipeTickCloseReason.QuietPeriod, quiet.Tick?.CloseReason, "The first tick should close as quiet period");
        Assert.IsEmpty(added, "The late frame should deliver nothing");
        Assert.IsNotNull(second, "The late frame should have opened a second tick");

        var tick = AssertTick(second, PipeTickCloseReason.QuietPeriod);

        Assert.AreEqual(100L, tick.GameTime, "The second tick should carry the same game time");
        Assert.HasCount(1, tick.Islands, "The second tick should hold the late frame only");
        Assert.AreEqual(At(31), tick.ReceiveTime, "The second tick should carry the receive time of the late frame");
    }

    /// <summary>
    /// Quiet checks before the deadline change nothing.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderQuietChecksBeforeDeadlineChangeNothing()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        var deadline = builder.QuietDeadline;

        // Act
        var joinTime = _start.AddTicks(299_995_000);
        var early = new[] { builder.CloseIfQuiet(At(0)), builder.CloseIfQuiet(At(10)), builder.CloseIfQuiet(_start.AddTicks(299_990_000)) };

        var joined = builder.Add(Frame(new PipeIslandKey(1, 2, 0), "Neapolis", 100), joinTime);
        var beforeClose = builder.CloseIfQuiet(joinTime + PipeTickLimits.QuietPeriod - TimeSpan.FromTicks(1));
        var closed = builder.CloseIfQuiet(joinTime + PipeTickLimits.QuietPeriod);

        // Assert
        Assert.AreEqual(At(30), deadline, "The deadline should be the last island receive time plus the quiet period");
        Assert.IsTrue(early.All(item => item is null), "No check before the deadline should deliver anything");
        Assert.IsEmpty(joined, "The frame should join the open tick without delivering anything");
        Assert.IsNull(beforeClose, "One tick before the deadline of the last frame should deliver nothing");
        Assert.IsNotNull(closed, "The deadline of the last frame should close the tick");
        Assert.HasCount(2, AssertTick(closed, PipeTickCloseReason.QuietPeriod).Islands, "The early checks should not have changed the open tick");
    }

    /// <summary>
    /// A key that is already in the open tick closes it as island repeated, also with an unchanged timestamp.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderRepeatedIslandKeyClosesTickAsIslandRepeated()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "First", 100), At(0));

        // Act
        var results = builder.Add(Frame(_ostia, "Second", 100), At(1));

        var next = builder.CloseIfQuiet(At(31));

        // Assert
        Assert.HasCount(1, results, "The repeated key should deliver the open tick");

        var tick = AssertTick(results[0], PipeTickCloseReason.IslandRepeated);

        Assert.HasCount(1, tick.Islands, "The closed tick should hold the first frame only");
        Assert.AreEqual("First", tick.Islands[0].Name, "The closed tick should hold the first frame");
        Assert.IsNotNull(next, "The repeated frame should have opened the next tick");
        Assert.AreEqual("Second", AssertTick(next, PipeTickCloseReason.QuietPeriod).Islands[0].Name, "The next tick should start with the repeated frame");
    }

    /// <summary>
    /// A frame that differs in the session GUID or in the area index only joins the open tick.
    /// </summary>
    /// <param name="sessionGuid">The session GUID of the second frame</param>
    /// <param name="areaIndex">The area index of the second frame</param>
    [TestMethod]
    [DataRow(2, (byte)0)]
    [DataRow(1, (byte)1)]
    public void PipeTickBuilderDifferentKeyPartJoinsSameTick(int sessionGuid, byte areaIndex)
    {
        // Arrange
        var builder = new PipeTickBuilder();
        var other = new PipeIslandKey(sessionGuid, 1, areaIndex);

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        // Act
        var results = builder.Add(Frame(other, "Londinium", 100), At(1));

        var closed = builder.CloseIfQuiet(At(31));

        // Assert
        Assert.IsEmpty(results, "The second frame should close nothing");
        Assert.IsNotNull(closed, "The tick should still be open");

        var tick = AssertTick(closed, PipeTickCloseReason.QuietPeriod);

        Assert.HasCount(2, tick.Islands, "The tick should hold both islands");
        Assert.AreEqual(_ostia, tick.Islands[0].Key, "The first island should keep its key");
        Assert.AreEqual(other, tick.Islands[1].Key, "The second island should carry its own key");
    }

    /// <summary>
    /// A tick whose islands all have no entries is marked no data yet.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderAllIslandsWithoutEntriesMarkTickNoDataYet()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(new PipeIslandKey(1, 1, 0), "Ostia", 100), At(0));
        builder.Add(Frame(new PipeIslandKey(1, 2, 0), "Neapolis", 100), At(1));

        // Act
        var closed = builder.CloseIfQuiet(At(31));

        // Assert
        Assert.IsNotNull(closed, "The tick should close at the deadline");

        var tick = AssertTick(closed, PipeTickCloseReason.QuietPeriod);

        Assert.IsTrue(tick.IsNoDataYet, "A tick of islands without entries should be marked no data yet");
        Assert.IsTrue(tick.Islands.All(island => island.HasNoEntries), "Every island should be marked no entries");
        Assert.IsTrue(tick.Islands.All(island => island.Entries.Count == 0), "No island should carry goods");
    }

    /// <summary>
    /// Only the islands without entries are marked in a mixed tick, which is not no data yet.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderMixedIslandsMarkOnlyEmptyIslands()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(new PipeIslandKey(1, 1, 0), "Ostia", 100, CreateEntries(2)), At(0));
        builder.Add(Frame(new PipeIslandKey(1, 2, 0), "Neapolis", 100), At(1));

        // Act
        var closed = builder.CloseIfQuiet(At(31));

        // Assert
        Assert.IsNotNull(closed, "The tick should close at the deadline");

        var tick = AssertTick(closed, PipeTickCloseReason.QuietPeriod);

        Assert.IsFalse(tick.IsNoDataYet, "A tick with an island with entries should not be marked no data yet");
        Assert.IsFalse(tick.Islands[0].HasNoEntries, "The island with entries should not be marked no entries");
        Assert.IsTrue(tick.Islands[1].HasNoEntries, "The island without entries should be marked no entries");
    }

    /// <summary>
    /// A skipped frame is delivered as its error and leaves its island out of the open tick.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderSkippedFrameLeavesIslandAbsent()
    {
        // Arrange
        var builder = new PipeTickBuilder();
        var error = new PipeError
                    {
                        Kind = PipeErrorKind.MalformedMessage,
                        TypeValue = 3,
                        Offset = 9
                    };

        builder.Add(Frame(new PipeIslandKey(1, 1, 0), "Ostia", 100, CreateEntries(1)), At(0));

        // Act
        var skipped = builder.Add(PipeReadResult.FromError(error), At(1));

        builder.Add(Frame(new PipeIslandKey(1, 3, 0), "Capua", 100, CreateEntries(1)), At(2));

        var closed = builder.CloseIfQuiet(At(32));

        // Assert
        Assert.HasCount(1, skipped, "The skipped frame should deliver its error");
        Assert.AreEqual(PipeTickResultKind.Error, skipped[0].Kind, "The item should be an error");
        Assert.AreSame(error, skipped[0].Error, "The error should be delivered unchanged");
        Assert.IsNotNull(closed, "The tick should have stayed open");
        Assert.AreSequenceEqual(new byte[] { 1, 3 }, AssertTick(closed, PipeTickCloseReason.QuietPeriod).Islands.Select(island => island.Key.IslandId).ToList(), "The tick should lack the skipped island");
    }

    /// <summary>
    /// A session start is delivered with its headline.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderSessionStartDeliversHeadline()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        // Act
        var results = builder.Add(PipeReadResult.FromMessage(new SessionStartMessage("Mare Nostrum")), At(0));

        // Assert
        Assert.HasCount(1, results, "The session start should deliver one item");
        Assert.AreEqual(PipeTickResultKind.SessionStart, results[0].Kind, "The item should be a session start");
        Assert.AreEqual("Mare Nostrum", results[0].Headline, "The headline should be delivered");
    }

    /// <summary>
    /// A session end before anything else on the stream is a repeated session end.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderSessionEndAtStreamStartIsRepeated()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        // Act
        var results = builder.Add(PipeReadResult.FromMessage(new SessionEndMessage()), At(0));

        // Assert
        Assert.HasCount(1, results, "The session end should deliver one item");
        Assert.AreEqual(PipeTickResultKind.RepeatedSessionEnd, results[0].Kind, "The item should be a repeated session end");
    }

    /// <summary>
    /// A second session end without a frame or a start in between is a repeated session end.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderSecondSessionEndIsRepeated()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));
        builder.Add(PipeReadResult.FromMessage(new SessionEndMessage()), At(1));

        // Act
        var results = builder.Add(PipeReadResult.FromMessage(new SessionEndMessage()), At(2));

        // Assert
        Assert.HasCount(1, results, "The second session end should deliver one item");
        Assert.AreEqual(PipeTickResultKind.RepeatedSessionEnd, results[0].Kind, "The second session end should be repeated");
    }

    /// <summary>
    /// A session end after an island frame is a session end that closes the tick first.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderSessionEndAfterIslandFrameIsSessionEnd()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        // Act
        var results = builder.Add(PipeReadResult.FromMessage(new SessionEndMessage()), At(1));

        // Assert
        Assert.HasCount(2, results, "The session end should deliver the tick and itself");
        Assert.AreEqual(PipeTickResultKind.SessionEnd, results[1].Kind, "The session end after a frame should be a session end");
    }

    /// <summary>
    /// A session end after a session start is a session end.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderSessionEndAfterStartIsSessionEnd()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(PipeReadResult.FromMessage(new SessionStartMessage("Mare Nostrum")), At(0));

        // Act
        var results = builder.Add(PipeReadResult.FromMessage(new SessionEndMessage()), At(1));

        // Assert
        Assert.HasCount(1, results, "The session end should deliver one item");
        Assert.AreEqual(PipeTickResultKind.SessionEnd, results[0].Kind, "The session end after a start should be a session end");
    }

    /// <summary>
    /// Two session starts are delivered as two starts with their own headlines.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderTwoStartsDeliverTwoStarts()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        // Act
        var results = Feed(builder,
                           (PipeReadResult.FromMessage(new SessionStartMessage("First")), At(0)),
                           (PipeReadResult.FromMessage(new SessionStartMessage(string.Empty)), At(1)));

        // Assert
        Assert.HasCount(2, results, "Both starts should be delivered");
        Assert.AreEqual(PipeTickResultKind.SessionStart, results[0].Kind, "The first item should be a session start");
        Assert.AreEqual("First", results[0].Headline, "The first headline should be delivered");
        Assert.AreEqual(PipeTickResultKind.SessionStart, results[1].Kind, "The second item should be a session start");
        Assert.AreEqual(string.Empty, results[1].Headline, "An empty headline should be delivered as an empty string");
    }

    /// <summary>
    /// A session event delivers the open tick first.
    /// </summary>
    /// <param name="messageType">The type of the session event</param>
    /// <param name="expected">The result kind of the event</param>
    [TestMethod]
    [DynamicData(nameof(SessionEvents))]
    public void PipeTickBuilderSessionEventClosesTickFirst(PipeMessageType messageType, PipeTickResultKind expected)
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        // Act
        var results = builder.Add(PipeReadResult.FromMessage(CreateSessionEvent(messageType)), At(1));

        // Assert
        Assert.HasCount(2, results, "The event should deliver the tick and itself");
        AssertTick(results[0], PipeTickCloseReason.SessionEvent);
        Assert.AreEqual(expected, results[1].Kind, "The event should follow the tick");
        Assert.IsNull(builder.QuietDeadline, "No tick should be open after the event");
    }

    /// <summary>
    /// Names and headlines are delivered as the reader decoded them.
    /// </summary>
    /// <param name="headline">The headline</param>
    [TestMethod]
    [DataRow("Mare_Nostrum ")]
    [DataRow("")]
    public void PipeTickBuilderNamesAndHeadlineDeliveredUnchanged(string headline)
    {
        // Arrange
        var builder = new PipeTickBuilder();

        // Act
        var results = Feed(builder,
                           (PipeReadResult.FromMessage(new SessionStartMessage(headline)), At(0)),
                           (Frame(new PipeIslandKey(1, 1, 0), "R_mische K_ste", 100), At(1)),
                           (Frame(new PipeIslandKey(1, 2, 0), "Zycada ", 100), At(2)),
                           (PipeReadResult.FromMessage(new SessionEndMessage()), At(3)));

        // Assert
        Assert.HasCount(3, results, "The start, the tick and the end should be delivered");
        Assert.AreEqual(headline, results[0].Headline, "The headline should be unchanged");

        var tick = AssertTick(results[1], PipeTickCloseReason.SessionEvent);

        Assert.AreEqual("R_mische K_ste", tick.Islands[0].Name, "The underscore should be kept");
        Assert.AreEqual("Zycada ", tick.Islands[1].Name, "The trailing space should be kept");
    }

    /// <summary>
    /// A renamed island keeps its key and each tick carries the name of its frame.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderRenamedIslandKeepsKey()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        // Act
        var results = Feed(builder,
                           (Frame(_ostia, "Old", 100), At(0)),
                           (Frame(_ostia, "New", 200), At(1)),
                           (Frame(_ostia, "New", 300), At(2)));

        // Assert
        Assert.HasCount(2, results, "Two ticks should be closed");

        var first = AssertTick(results[0], PipeTickCloseReason.GameTimeChanged);
        var second = AssertTick(results[1], PipeTickCloseReason.GameTimeChanged);

        Assert.AreEqual("Old", first.Islands[0].Name, "The first tick should carry the old name");
        Assert.AreEqual("New", second.Islands[0].Name, "The second tick should carry the new name");
        Assert.AreEqual(first.Islands[0].Key, second.Islands[0].Key, "The island should keep its key");
    }

    /// <summary>
    /// A version and a skipping error are delivered unchanged and leave the tick open.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderErrorAndVersionLeaveTickOpen()
    {
        // Arrange
        var builder = new PipeTickBuilder();
        var error = new PipeError
                    {
                        Kind = PipeErrorKind.UnknownType,
                        TypeValue = 9
                    };

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        // Act
        var results = Feed(builder,
                           (PipeReadResult.FromMessage(new VersionMessage(2)), At(1)),
                           (PipeReadResult.FromError(error), At(2)));

        // Assert
        Assert.HasCount(2, results, "Both items should be delivered");
        Assert.AreEqual(PipeTickResultKind.Version, results[0].Kind, "The first item should be the version");
        Assert.AreEqual(2, results[0].Version, "The version should be delivered");
        Assert.AreEqual(PipeTickResultKind.Error, results[1].Kind, "The second item should be the error");
        Assert.AreSame(error, results[1].Error, "The error should be delivered unchanged");
        Assert.AreEqual(At(30), builder.QuietDeadline, "The tick should stay open with its deadline");
    }

    /// <summary>
    /// The end of the stream closes the open tick as stream ended and is delivered after it.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderEndOfStreamClosesTickAsStreamEnded()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        // Act
        var results = builder.Add(PipeReadResult.End, At(1));

        // Assert
        Assert.HasCount(2, results, "The end should deliver the tick and itself");
        AssertTick(results[0], PipeTickCloseReason.StreamEnded);
        Assert.AreEqual(PipeTickResultKind.EndOfStream, results[1].Kind, "The end should follow the tick");
    }

    /// <summary>
    /// The end of the stream without an open tick delivers only the end.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderEndOfStreamWithoutTickDeliversOnlyEnd()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        // Act
        var results = builder.Add(PipeReadResult.End, At(0));

        // Assert
        Assert.HasCount(1, results, "The end should deliver one item");
        Assert.AreEqual(PipeTickResultKind.EndOfStream, results[0].Kind, "The item should be the end");
    }

    /// <summary>
    /// Anything added after the end of the stream delivers nothing.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderAddAfterEndDeliversNothing()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(PipeReadResult.End, At(0));

        // Act
        var results = builder.Add(Frame(_ostia, "Ostia", 100), At(1));

        // Assert
        Assert.IsEmpty(results, "A frame after the end of the stream should deliver nothing");
        Assert.IsNull(builder.QuietDeadline, "A frame after the end of the stream should not open a tick");
    }

    /// <summary>
    /// A stream-ending error closes the open tick as stream ended before it is delivered.
    /// </summary>
    /// <param name="kind">The error kind</param>
    [TestMethod]
    [DataRow(PipeErrorKind.TruncatedFrame)]
    [DataRow(PipeErrorKind.OversizedFrame)]
    [DataRow(PipeErrorKind.ReadFailed)]
    public void PipeTickBuilderStreamEndingErrorClosesTickBeforeError(PipeErrorKind kind)
    {
        // Arrange
        var builder = new PipeTickBuilder();
        var error = new PipeError
                    {
                        Kind = kind
                    };

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        // Act
        var results = builder.Add(PipeReadResult.FromError(error), At(1));

        // Assert
        Assert.HasCount(3, results, "The error should deliver the tick, itself and the end");
        AssertTick(results[0], PipeTickCloseReason.StreamEnded);
        Assert.AreEqual(PipeTickResultKind.Error, results[1].Kind, "The error should follow the tick");
        Assert.AreSame(error, results[1].Error, "The error should be delivered unchanged");
        Assert.AreEqual(PipeTickResultKind.EndOfStream, results[2].Kind, "The end should follow the error");
    }

    /// <summary>
    /// A tick of exactly 256 islands keeps all of them.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderTickAtLimitsKeepsAllIslands()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        // Act
        var results = AddIslands(builder, PipeTickLimits.MaxIslands);

        var closed = builder.CloseIfQuiet(At(31));

        // Assert
        Assert.IsEmpty(results, "No island up to the limit should deliver anything");
        Assert.IsNotNull(closed, "The tick should be open");
        Assert.HasCount(PipeTickLimits.MaxIslands, AssertTick(closed, PipeTickCloseReason.QuietPeriod).Islands, "The tick should keep all islands at the limit");
    }

    /// <summary>
    /// A tick of exactly 524,288 entries and pairs keeps its frame.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderTickAtItemLimitKeepsFrame()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        // Act
        var results = builder.Add(Frame(_ostia, "Ostia", 100, CreateItemsAtLimit()), At(0));

        var closed = builder.CloseIfQuiet(At(30));

        // Assert
        Assert.IsEmpty(results, "A frame exactly at the item limit should deliver nothing");
        Assert.IsNotNull(closed, "The tick should be open");
        Assert.HasCount(1, AssertTick(closed, PipeTickCloseReason.QuietPeriod).Islands, "The frame at the item limit should be kept");
    }

    /// <summary>
    /// The 257th island is dropped and delivered as a tick limit with its key.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderIslandOverIslandLimitGivesTickLimit()
    {
        // Arrange
        var builder = new PipeTickBuilder();
        var over = new PipeIslandKey(999, 1, 0);

        AddIslands(builder, PipeTickLimits.MaxIslands);

        // Act
        var results = builder.Add(Frame(over, "Over", 100, CreateEntries(1)), At(1));

        var closed = builder.CloseIfQuiet(At(30));

        // Assert
        Assert.HasCount(1, results, "The island over the limit should deliver one item");
        Assert.AreEqual(PipeTickResultKind.TickLimit, results[0].Kind, "The item should be a tick limit");
        Assert.AreEqual(over, results[0].IslandKey, "The item should carry the key of the dropped island");
        Assert.IsNull(results[0].Tick, "The tick limit should carry no tick");
        Assert.IsNotNull(closed, "The tick should have stayed open");

        var tick = AssertTick(closed, PipeTickCloseReason.QuietPeriod);

        Assert.HasCount(PipeTickLimits.MaxIslands, tick.Islands, "The tick should keep its earlier islands");
        Assert.DoesNotContain(over, tick.Islands.Select(island => island.Key).ToList(), "The dropped island should not be in the tick");
    }

    /// <summary>
    /// A frame that would pass 524,288 entries and pairs is dropped and delivered as a tick limit.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderIslandOverItemLimitGivesTickLimit()
    {
        // Arrange
        var builder = new PipeTickBuilder();
        var over = new PipeIslandKey(1, 2, 0);

        builder.Add(Frame(_ostia, "Ostia", 100, CreateItemsAtLimit()), At(0));

        // Act
        var results = builder.Add(Frame(over, "Over", 100, CreateEntries(1)), At(1));

        var closed = builder.CloseIfQuiet(At(30));

        // Assert
        Assert.HasCount(1, results, "The frame over the limit should deliver one item");
        Assert.AreEqual(PipeTickResultKind.TickLimit, results[0].Kind, "The item should be a tick limit");
        Assert.AreEqual(over, results[0].IslandKey, "The item should carry the key of the dropped island");
        Assert.IsNotNull(closed, "The tick should have stayed open");
        Assert.AreSequenceEqual(new[] { _ostia }, AssertTick(closed, PipeTickCloseReason.QuietPeriod).Islands.Select(island => island.Key).ToList(), "The tick should keep its earlier island only");
    }

    /// <summary>
    /// A frame that alone passes the item limit on an empty tick opens no tick.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderFrameOverItemLimitOnEmptyTickOpensNoTick()
    {
        // Arrange
        var builder = new PipeTickBuilder();
        var entries = CreateItemsAtLimit().Append(CreateEntries(1)[0]).ToList();

        // Act
        var results = builder.Add(Frame(_ostia, "Ostia", 100, entries), At(0));

        // Assert
        Assert.HasCount(1, results, "The frame should deliver one item");
        Assert.AreEqual(PipeTickResultKind.TickLimit, results[0].Kind, "The item should be a tick limit");
        Assert.AreEqual(_ostia, results[0].IslandKey, "The item should carry the key of the dropped island");
        Assert.IsNull(builder.QuietDeadline, "The dropped frame should open no tick");
    }

    /// <summary>
    /// A dropped key does not count as a repeated key.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderDroppedKeyDoesNotCountAsRepeated()
    {
        // Arrange
        var builder = new PipeTickBuilder();
        var over = new PipeIslandKey(999, 1, 0);

        AddIslands(builder, PipeTickLimits.MaxIslands);

        builder.Add(Frame(over, "Over", 100), At(1));

        // Act
        var results = builder.Add(Frame(over, "Over", 100), At(2));

        // Assert
        Assert.HasCount(1, results, "The second frame of the dropped key should deliver one item");
        Assert.AreEqual(PipeTickResultKind.TickLimit, results[0].Kind, "The item should be a tick limit again and not a closed tick");
        Assert.AreEqual(over, results[0].IslandKey, "The item should carry the key");
        Assert.IsNotNull(builder.QuietDeadline, "The tick should still be open");
    }

    /// <summary>
    /// Each frame over the limit gives its own tick limit.
    /// </summary>
    [TestMethod]
    public void PipeTickBuilderEachFrameOverLimitGivesOwnTickLimit()
    {
        // Arrange
        var builder = new PipeTickBuilder();

        AddIslands(builder, PipeTickLimits.MaxIslands);

        // Act
        var results = Feed(builder,
                           (Frame(new PipeIslandKey(_overGuids[0], 1, 0), "A", 100), At(1)),
                           (Frame(new PipeIslandKey(_overGuids[1], 1, 0), "B", 100), At(2)),
                           (Frame(new PipeIslandKey(_overGuids[2], 1, 0), "C", 100), At(3)));

        // Assert
        Assert.HasCount(3, results, "Each frame should deliver one item");
        Assert.IsTrue(results.All(result => result.Kind == PipeTickResultKind.TickLimit), "Every item should be a tick limit");
        Assert.AreSequenceEqual(_overGuids, results.Select(result => result.IslandKey!.Value.SessionGuid).ToList(), "Each item should carry its own key");
    }

    /// <summary>
    /// A non-island item does not restart the quiet period.
    /// </summary>
    /// <param name="item">The non-island item</param>
    /// <param name="expected">The result kind of the item</param>
    [TestMethod]
    [DynamicData(nameof(NonIslandItems))]
    public void PipeTickBuilderNonIslandItemsDoNotRestartQuietPeriod(PipeReadResult item, PipeTickResultKind expected)
    {
        // Arrange
        var builder = new PipeTickBuilder();

        builder.Add(Frame(_ostia, "Ostia", 100), At(0));

        // Act
        var results = Feed(builder, [.. Enumerable.Range(1, 31).Select(second => (item, At(second)))]);

        // Assert
        Assert.HasCount(32, results, "The tick and the 31 items should be delivered");
        Assert.IsTrue(results.Take(29).All(result => result.Kind == expected), "The items of the first 29 seconds should be delivered as they are");

        var tick = AssertTick(results[29], PipeTickCloseReason.QuietPeriod);

        Assert.AreEqual(At(0), tick.ReceiveTime, "The tick should close 30 seconds after its last island frame");
        Assert.AreEqual(expected, results[30].Kind, "The item of the 30th second should follow the tick");
        Assert.AreEqual(expected, results[31].Kind, "The item of the 31st second should be delivered");
    }

    /// <summary>
    /// Gets the receive time the given number of seconds after the start.
    /// </summary>
    /// <param name="seconds">The seconds after the start</param>
    /// <returns>The receive time</returns>
    private static DateTimeOffset At(double seconds)
    {
        return _start.AddSeconds(seconds);
    }

    /// <summary>
    /// Creates an island frame result.
    /// </summary>
    /// <param name="key">The island key</param>
    /// <param name="name">The area name</param>
    /// <param name="timestamp">The timestamp</param>
    /// <param name="entries">The entries; none when omitted</param>
    /// <param name="sessionId">The session ID</param>
    /// <returns>The read result</returns>
    private static PipeReadResult Frame(PipeIslandKey key, string name, long timestamp, IReadOnlyList<ProductionEntry>? entries = null, byte sessionId = 7)
    {
        return PipeReadResult.FromMessage(new AreaProductionStatisticsMessage
                                          {
                                              SessionId = sessionId,
                                              IslandId = key.IslandId,
                                              AreaIndex = key.AreaIndex,
                                              SessionGuid = key.SessionGuid,
                                              AreaName = name,
                                              Timestamp = timestamp,
                                              Entries = entries ?? []
                                          });
    }

    /// <summary>
    /// Creates entries without map pairs.
    /// </summary>
    /// <param name="count">The number of entries</param>
    /// <returns>The entries</returns>
    private static List<ProductionEntry> CreateEntries(int count)
    {
        return [.. Enumerable.Range(1, count).Select(guid => CreateEntry(guid, new Dictionary<int, int>()))];
    }

    /// <summary>
    /// Creates the 512 entries of 1,023 workforce pairs each that make exactly 524,288 entries and pairs.
    /// </summary>
    /// <returns>The entries</returns>
    private static List<ProductionEntry> CreateItemsAtLimit()
    {
        var pairs = Enumerable.Range(1, 1023).ToDictionary(guid => guid, _ => 1);

        return [.. Enumerable.Range(1, 512).Select(guid => CreateEntry(guid, pairs))];
    }

    /// <summary>
    /// Creates an entry with the given workforce map.
    /// </summary>
    /// <param name="guid">The product GUID</param>
    /// <param name="workforce">The workforce map</param>
    /// <returns>The entry</returns>
    private static ProductionEntry CreateEntry(int guid, IReadOnlyDictionary<int, int> workforce)
    {
        return new ProductionEntry
               {
                   ProductGuid = guid,
                   Generation = 1f,
                   Consumption = 2f,
                   Delta = 3f,
                   PerfectGeneration = 4f,
                   PerfectConsumption = 5f,
                   BuildingCount = 6,
                   Maintenance = 7,
                   Income = 8f,
                   Profit = 9,
                   SummedProductivity = 10f,
                   AverageProductivity = 11f,
                   WorkforceAmounts = workforce,
                   BuildingAmounts = new Dictionary<int, int>()
               };
    }

    /// <summary>
    /// Creates a session event message.
    /// </summary>
    /// <param name="messageType">The session event type</param>
    /// <returns>The message</returns>
    private static PipeMessage CreateSessionEvent(PipeMessageType messageType)
    {
        return messageType == PipeMessageType.SessionStart ? new SessionStartMessage("Mare Nostrum") : new SessionEndMessage();
    }

    /// <summary>
    /// Adds the given number of islands with one entry each, a second apart, all with one timestamp.
    /// </summary>
    /// <param name="builder">The builder</param>
    /// <param name="count">The number of islands</param>
    /// <returns>The items the frames delivered</returns>
    private static List<PipeTickResult> AddIslands(PipeTickBuilder builder, int count)
    {
        var frames = Enumerable.Range(0, count)
                               .Select(index => (Frame(new PipeIslandKey(index, 1, 0), "Island", 100, CreateEntries(1)), At(0)))
                               .ToArray();

        return Feed(builder, frames);
    }

    /// <summary>
    /// Adds the results in order and collects what they deliver.
    /// </summary>
    /// <param name="builder">The builder</param>
    /// <param name="items">The results with their receive times</param>
    /// <returns>The delivered items in order</returns>
    private static List<PipeTickResult> Feed(PipeTickBuilder builder, params (PipeReadResult Result, DateTimeOffset Time)[] items)
    {
        var delivered = new List<PipeTickResult>();

        foreach (var (result, time) in items)
        {
            delivered.AddRange(builder.Add(result, time));
        }

        return delivered;
    }

    /// <summary>
    /// Asserts that an item is a tick with the given close reason.
    /// </summary>
    /// <param name="result">The item</param>
    /// <param name="reason">The expected close reason</param>
    /// <returns>The tick</returns>
    private static PipeTick AssertTick(PipeTickResult result, PipeTickCloseReason reason)
    {
        Assert.AreEqual(PipeTickResultKind.Tick, result.Kind, "The item should be a tick");
        Assert.IsNotNull(result.Tick, "The item should carry the tick");
        Assert.AreEqual(reason, result.Tick.CloseReason, "The tick should carry the expected close reason");

        return result.Tick;
    }

    #endregion // Methods
}