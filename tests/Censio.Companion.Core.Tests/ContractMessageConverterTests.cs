using System.Globalization;

using Censio.Contracts;
using Censio.Pipe;

namespace Censio.Companion.Core.Tests;

/// <summary>
/// Tests for <see cref="ContractMessageConverter" />.
/// </summary>
[TestClass]
public sealed class ContractMessageConverterTests
{
    #region Fields

    /// <summary>
    /// A profile identity.
    /// </summary>
    private static readonly Guid _profile = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");

    /// <summary>
    /// A receive time with seven fractional digits.
    /// </summary>
    private static readonly DateTimeOffset _time = new DateTimeOffset(2026, 11, 12, 10, 20, 30, TimeSpan.Zero).AddTicks(1234567);

    /// <summary>
    /// The finite values of the eight floats of an entry, in field order, distinct and not exactly representable in a short form.
    /// </summary>
    private static readonly float[] _floats = [1f / 3f, 0.1f, -2.5f, 123456.79f, -0f, float.Epsilon, float.MaxValue, float.MinValue];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the non-finite cases: the index of the float field (generation, consumption, delta, perfect generation, perfect
    /// consumption, income, summed productivity, average productivity) and the value the game sent.
    /// </summary>
    private static IEnumerable<object[]> NonFiniteRows => Enumerable.Range(0, 8).SelectMany(index => new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity }.Select(value => new object[] { index, value }));

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Each of the six pipe states maps to the contract pipe state of the same name, and the status is written and read back.
    /// </summary>
    /// <param name="state">The pipe state</param>
    /// <param name="expected">The contract pipe state</param>
    [TestMethod]
    [TestCategory("AC1")]
    [DataRow(PipeConnectionState.WaitingForGame, ContractPipeState.WaitingForGame)]
    [DataRow(PipeConnectionState.Connected, ContractPipeState.Connected)]
    [DataRow(PipeConnectionState.Receiving, ContractPipeState.Receiving)]
    [DataRow(PipeConnectionState.PipeBusy, ContractPipeState.PipeBusy)]
    [DataRow(PipeConnectionState.ProtocolMismatch, ContractPipeState.ProtocolMismatch)]
    [DataRow(PipeConnectionState.Error, ContractPipeState.Error)]
    public void ContractMessageConverterStatusMapsEveryPipeState(PipeConnectionState state, ContractPipeState expected)
    {
        // Arrange
        // Act
        var status = ContractMessageConverter.ToStatus(state);

        // Assert
        Assert.AreEqual(expected, status.PipeState, $"The state {state} should map to {expected}");
        Assert.AreEqual(expected, RoundTrip<ContractStatus>(status, state.ToString()).PipeState, $"The status {expected} should read back");
    }

    /// <summary>
    /// A pipe tick converts to a contract tick that carries every island with key, session ID and name, every entry with
    /// its eleven values and both maps, the receive time and the game time, exact, also after a write and a read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC2")]
    public void ContractMessageConverterTickCarriesEveryValueOfThePipeTick()
    {
        // Arrange
        var tick = CreateFullTick();

        // Act
        var converted = ContractMessageConverter.ToTick(tick, _profile);

        // Assert
        Assert.AreNotEqual(Guid.Empty, converted.Id, "The tick should get an identity");
        Assert.AreEqual(_profile, converted.ProfileId, "The tick should carry the profile it was converted for");
        Assert.AreEqual(DescribePipe(tick), DescribeContract(converted), "The converted tick should carry every value of the pipe tick");

        var read = RoundTrip<ContractTick>(converted, "full tick");

        Assert.AreEqual(converted.Id, read.Id, "The identity should read back");
        Assert.AreEqual(_profile, read.ProfileId, "The profile should read back");
        Assert.AreEqual(DescribePipe(tick), DescribeContract(read), "The tick read back should carry every value of the pipe tick");
    }

    /// <summary>
    /// A non-finite float in any of the eight float fields crosses as no value, never 0, through write and read; every other
    /// value stays unchanged.
    /// </summary>
    /// <param name="field">The index of the float field</param>
    /// <param name="value">The non-finite value the game sent</param>
    [TestMethod]
    [TestCategory("AC3")]
    [DynamicData(nameof(NonFiniteRows))]
    public void ContractMessageConverterNonFiniteFloatCrossesAsNoValue(int field, float value)
    {
        // Arrange
        var tick = CreateTick(CreateIsland(new PipeIslandKey(180023, 1, 0), 2, "Isle", CreateEntry(1010017, WithValue(field, value))));

        // Act
        var converted = ContractMessageConverter.ToTick(tick, _profile);

        // Assert
        var entry = RoundTrip<ContractTick>(converted, $"field {field} {value}").Islands[0].Entries[0];

        Assert.AreSequenceEqual(ExpectedBits(field), Floats(entry).Select(Bits).ToArray(), $"Field {field} should read back as no value and every other float unchanged: {value}");
        Assert.AreEqual(17, entry.BuildingCount, "The building count should stay unchanged");
        Assert.AreEqual(-250, entry.Maintenance, "The maintenance should stay unchanged");
        Assert.AreEqual(int.MaxValue, entry.Profit, "The profit should stay unchanged");
    }

    /// <summary>
    /// Session events and ticks converted separately from equal input get different identities, none of them all zero.
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void ContractMessageConverterSessionEventsTakenSeparatelyGetDifferentIdentities()
    {
        // Arrange
        var tick = CreateFullTick();

        // Act
        var identities = new[]
                         {
                             ContractMessageConverter.ToSessionEnd(_profile, _time).Id,
                             ContractMessageConverter.ToSessionEnd(_profile, _time).Id,
                             ContractMessageConverter.ToSessionStart(_profile, _time).Id,
                             ContractMessageConverter.ToSessionStart(_profile, _time).Id,
                             ContractMessageConverter.ToTick(tick, _profile).Id,
                             ContractMessageConverter.ToTick(tick, _profile).Id
                         };

        // Assert
        Assert.DoesNotContain(Guid.Empty, identities, "No message should get the all-zero identity");
        Assert.HasCount(identities.Length, identities.Distinct().ToArray(), "Every message taken separately should get its own identity");
    }

    /// <summary>
    /// A session start and a session end carry the receive time and the profile the caller gives, also after a write and a read.
    /// </summary>
    [TestMethod]
    [TestCategory("AC9")]
    public void ContractMessageConverterSessionEventCarriesItsReceiveTime()
    {
        // Arrange
        var startTime = _time.AddTicks(3);
        var endTime = _time.AddSeconds(7).AddTicks(9);

        // Act
        var events = new ContractMessage[] { ContractMessageConverter.ToSessionStart(_profile, startTime), ContractMessageConverter.ToSessionEnd(_profile, endTime) };

        // Assert
        Assert.AreEqual($"{_profile}|{startTime.UtcTicks}|{TimeSpan.Zero}", DescribeEvent(events[0]), "The session start should carry the profile and receive time it was given");
        Assert.AreEqual($"{_profile}|{endTime.UtcTicks}|{TimeSpan.Zero}", DescribeEvent(events[1]), "The session end should carry the profile and receive time it was given");
        Assert.AreEqual(DescribeEvent(events[0]), DescribeEvent(RoundTrip<ContractSessionStart>(events[0], "session start")), "The session start should read back with its profile and receive time");
        Assert.AreEqual(DescribeEvent(events[1]), DescribeEvent(RoundTrip<ContractSessionEnd>(events[1], "session end")), "The session end should read back with its profile and receive time");
    }

    /// <summary>
    /// Writes a message and reads it back, asserting neither step refuses it.
    /// </summary>
    /// <typeparam name="T">The expected message type</typeparam>
    /// <param name="message">The message</param>
    /// <param name="context">What the case was, for the messages</param>
    /// <returns>The message read back</returns>
    private static T RoundTrip<T>(ContractMessage message, string context)
        where T : ContractMessage
    {
        var written = ContractWriter.Write(message);

        Assert.IsNull(written.Error, "The message should be written: " + context);

        var read = ContractReader.Read(written.Utf8Json.Span);

        Assert.IsNull(read.Error, "The message should be read back: " + context);

        return Assert.IsInstanceOfType<T>(read.Message, "The message should read back with its type: " + context);
    }

    /// <summary>
    /// Returns the bits of the finite float values with one field as no value.
    /// </summary>
    /// <param name="field">The index of the field without a value</param>
    /// <returns>The eight descriptions</returns>
    private static string[] ExpectedBits(int field)
    {
        var bits = _floats.Select(value => Bits(value)).ToArray();

        bits[field] = Bits(null);

        return bits;
    }

    /// <summary>
    /// Returns the finite float values with one field replaced.
    /// </summary>
    /// <param name="field">The index of the field</param>
    /// <param name="value">The value of the field</param>
    /// <returns>The eight values</returns>
    private static float[] WithValue(int field, float value)
    {
        var values = _floats.ToArray();

        values[field] = value;

        return values;
    }

    /// <summary>
    /// Creates a pipe tick of the receive time and game time of these tests.
    /// </summary>
    /// <param name="islands">The islands</param>
    /// <returns>The tick</returns>
    private static PipeTick CreateTick(params PipeTickIsland[] islands)
    {
        return new PipeTick
               {
                   GameTime = 987654321012,
                   ReceiveTime = _time,
                   Islands = islands,
                   CloseReason = PipeTickCloseReason.GameTimeChanged
               };
    }

    /// <summary>
    /// Creates a pipe tick of every value: islands of distinct keys, an island without entries, a repeated product GUID,
    /// negative GUIDs, a name of replacement characters and maps given in descending order.
    /// </summary>
    /// <returns>The tick</returns>
    private static PipeTick CreateFullTick()
    {
        return CreateTick(CreateIsland(new PipeIslandKey(180023, 1, 0), 2, "Isle Alpha", CreateEntry(1010017, _floats), CreateEntry(1010017, [.. _floats.Reverse()])),
                          CreateIsland(new PipeIslandKey(-180045, 255, 255), 255, new string((char)0xFFFD, 255), CreateEntry(-5, _floats)),
                          CreateIsland(new PipeIslandKey(180023, 2, 0), 0, string.Empty));
    }

    /// <summary>
    /// Creates a pipe island.
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="name">The name</param>
    /// <param name="entries">The entries</param>
    /// <returns>The island</returns>
    private static PipeTickIsland CreateIsland(PipeIslandKey key, byte sessionId, string name, params ProductionEntry[] entries)
    {
        return new PipeTickIsland
               {
                   Key = key,
                   SessionId = sessionId,
                   Name = name,
                   Entries = entries
               };
    }

    /// <summary>
    /// Creates a pipe entry of eight float values, fixed integers and maps in descending GUID order.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="floats">The eight float values in field order</param>
    /// <returns>The entry</returns>
    private static ProductionEntry CreateEntry(int productGuid, float[] floats)
    {
        return new ProductionEntry
               {
                   ProductGuid = productGuid,
                   Generation = floats[0],
                   Consumption = floats[1],
                   Delta = floats[2],
                   PerfectGeneration = floats[3],
                   PerfectConsumption = floats[4],
                   BuildingCount = 17,
                   Maintenance = -250,
                   Income = floats[5],
                   Profit = int.MaxValue,
                   SummedProductivity = floats[6],
                   AverageProductivity = floats[7],
                   WorkforceAmounts = new Dictionary<int, int>
                                      {
                                          [1010053] = 0,
                                          [1010052] = 40,
                                          [-9] = int.MinValue
                                      },
                   BuildingAmounts = new Dictionary<int, int>
                                     {
                                         [1010343] = 3
                                     }
               };
    }

    /// <summary>
    /// Returns the eight floats of a contract entry in field order.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The values</returns>
    private static float?[] Floats(ContractTickEntry entry)
    {
        return [entry.Generation, entry.Consumption, entry.Delta, entry.PerfectGeneration, entry.PerfectConsumption, entry.Income, entry.SummedProductivity, entry.AverageProductivity];
    }

    /// <summary>
    /// Describes a float by its bits, or a dash for no value.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The description</returns>
    private static string Bits(float? value)
    {
        return value is null ? "-" : BitConverter.SingleToInt32Bits(value.Value).ToString("x8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Describes a map in GUID order.
    /// </summary>
    /// <param name="map">The map</param>
    /// <returns>The description</returns>
    private static string DescribeMap(IReadOnlyDictionary<int, int> map)
    {
        return string.Join(",", map.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
    }

    /// <summary>
    /// Describes every value of a pipe tick that crosses the contract.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <returns>The description</returns>
    private static string DescribePipe(PipeTick tick)
    {
        var islands = tick.Islands.Select(island => $"{island.Key.SessionGuid}/{island.Key.IslandId}/{island.Key.AreaIndex}/{island.SessionId}/{island.Name.Length}:{island.Name}[{string.Join(" ", island.Entries.Select(entry => $"{entry.ProductGuid}:{Bits(entry.Generation)},{Bits(entry.Consumption)},{Bits(entry.Delta)},{Bits(entry.PerfectGeneration)},{Bits(entry.PerfectConsumption)},{entry.BuildingCount},{entry.Maintenance},{Bits(entry.Income)},{entry.Profit},{Bits(entry.SummedProductivity)},{Bits(entry.AverageProductivity)},w{DescribeMap(entry.WorkforceAmounts)},b{DescribeMap(entry.BuildingAmounts)}"))}]");

        return $"{tick.ReceiveTime.UtcTicks}|{tick.ReceiveTime.Offset}|{tick.GameTime}|{string.Join(";", islands)}";
    }

    /// <summary>
    /// Describes every value of a contract tick in the form of <see cref="DescribePipe" />.
    /// </summary>
    /// <param name="tick">The tick</param>
    /// <returns>The description</returns>
    private static string DescribeContract(ContractTick tick)
    {
        var islands = tick.Islands.Select(island => $"{island.SessionGuid}/{island.IslandId}/{island.AreaIndex}/{island.SessionId}/{island.Name.Length}:{island.Name}[{string.Join(" ", island.Entries.Select(entry => $"{entry.ProductGuid}:{Bits(entry.Generation)},{Bits(entry.Consumption)},{Bits(entry.Delta)},{Bits(entry.PerfectGeneration)},{Bits(entry.PerfectConsumption)},{entry.BuildingCount},{entry.Maintenance},{Bits(entry.Income)},{entry.Profit},{Bits(entry.SummedProductivity)},{Bits(entry.AverageProductivity)},w{DescribeMap(entry.WorkforceAmounts)},b{DescribeMap(entry.BuildingAmounts)}"))}]");

        return $"{tick.ReceiveTime.UtcTicks}|{tick.ReceiveTime.Offset}|{tick.GameTime}|{string.Join(";", islands)}";
    }

    /// <summary>
    /// Describes the profile and receive time of a session event.
    /// </summary>
    /// <param name="message">The session start or session end</param>
    /// <returns>The description</returns>
    private static string DescribeEvent(ContractMessage message)
    {
        return message switch
               {
                   ContractSessionStart start => $"{start.ProfileId}|{start.ReceiveTime.UtcTicks}|{start.ReceiveTime.Offset}",
                   ContractSessionEnd end => $"{end.ProfileId}|{end.ReceiveTime.UtcTicks}|{end.ReceiveTime.Offset}",
                   _ => message.GetType().Name
               };
    }

    #endregion // Methods
}