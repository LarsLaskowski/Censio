using System.Buffers.Binary;
using System.Text;

namespace Censio.Pipe;

/// <summary>
/// Collects the names of the frames of a recording and rewrites the frames.
/// </summary>
internal sealed class PipeFrameAnonymiser
{
    #region Constants

    /// <summary>
    /// The length in bytes of the scalar fields of an entry, before its maps.
    /// </summary>
    private const int EntryScalarsLength = 48;

    /// <summary>
    /// The length in bytes of the fields of an island frame before its name: the type, three identifiers and the session GUID.
    /// </summary>
    private const int IslandHeadLength = 8;

    /// <summary>
    /// The bits of the standard positive NaN.
    /// </summary>
    private const uint PositiveNaNBits = 0x7FC00000;

    /// <summary>
    /// A side draw from this value on goes upwards.
    /// </summary>
    private const double UpwardsFrom = 0.5;

    /// <summary>
    /// The smallest integer magnitude that changes by a percentage.
    /// </summary>
    private const long PercentFrom = 5;

    /// <summary>
    /// The largest magnitude of a negative 32-bit integer.
    /// </summary>
    private const long MaxNegativeMagnitude = 2147483648;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The invented names.
    /// </summary>
    private readonly PipeAnonymousNames _names;

    /// <summary>
    /// The random source.
    /// </summary>
    private readonly IPipeAnonymiserRandom _random;

    /// <summary>
    /// Indicates whether a supported version frame has been seen.
    /// </summary>
    private bool _supported;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeFrameAnonymiser"/> class.
    /// </summary>
    /// <param name="names">The invented names</param>
    /// <param name="random">The random source</param>
    public PipeFrameAnonymiser(PipeAnonymousNames names, IPipeAnonymiserRandom random)
    {
        _names = names;
        _random = random;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Collects the names of a frame.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns><see langword="true" /> when the frame can be anonymised</returns>
    public bool TryCollect(ReadOnlySpan<byte> frame)
    {
        if (TryAccept(frame, out var message) == false)
        {
            return false;
        }

        switch (message)
        {
            case SessionStartMessage start:
                _names.AddFigure(start.Headline);

                break;

            case AreaProductionStatisticsMessage area:
                _names.AddIsland(area.AreaName);

                break;
        }

        return true;
    }

    /// <summary>
    /// Rewrites a frame.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <returns>The rewritten frame, or <see langword="null" /> when it cannot be anonymised</returns>
    public byte[]? Anonymise(ReadOnlySpan<byte> frame)
    {
        if (TryAccept(frame, out var message) == false)
        {
            return null;
        }

        var written = message switch
                      {
                          null => [],
                          VersionMessage version => WriteVersion(version),
                          SessionStartMessage start => WriteSessionStart(start),
                          AreaProductionStatisticsMessage area => WriteIsland(frame, area),
                          _ => [(byte)PipeMessageType.SessionEnd]
                      };

        return written is not null && (message is null || IsReadBack(written, message)) ? written : null;
    }

    /// <summary>
    /// Resets the version gate for a new pass.
    /// </summary>
    public void Reset()
    {
        _supported = false;
    }

    /// <summary>
    /// Writes a version frame.
    /// </summary>
    /// <param name="version">The message</param>
    /// <returns>The frame</returns>
    private static byte[] WriteVersion(VersionMessage version)
    {
        var frame = new byte[1 + sizeof(int)];

        frame[0] = (byte)PipeMessageType.Version;
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1), version.Version);

        return frame;
    }

    /// <summary>
    /// Checks whether a finite value multiplied by the factor would become infinite.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <param name="factor">The factor</param>
    /// <returns><see langword="true" /> when a multiplied value would overflow</returns>
    private static bool Overflows(ProductionEntry entry, double factor)
    {
        float[] values = [entry.Generation, entry.Consumption, entry.PerfectGeneration, entry.PerfectConsumption, entry.Income, entry.SummedProductivity];

        return values.Any(value => float.IsFinite(value) && float.IsInfinity((float)(value * factor)));
    }

    /// <summary>
    /// Multiplies a float by the factor; an infinity stays and a NaN becomes the standard NaN of its sign.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="factor">The factor</param>
    /// <returns>The scaled value</returns>
    private static float Scale(float value, double factor)
    {
        return float.IsFinite(value) ? (float)(value * factor) : KeepNonFinite(value);
    }

    /// <summary>
    /// Gives a non-finite float without payload: an infinity stays, a NaN becomes the standard NaN with its sign.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The value</returns>
    private static float KeepNonFinite(float value)
    {
        if (float.IsNaN(value) == false)
        {
            return value;
        }

        return float.IsNegative(value) ? float.NaN : BitConverter.UInt32BitsToSingle(PositiveNaNBits);
    }

    /// <summary>
    /// Writes a float by its bits.
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="value">The value</param>
    private static void WriteFloat(BinaryWriter writer, float value)
    {
        writer.Write(BitConverter.SingleToUInt32Bits(value));
    }

    /// <summary>
    /// Derives the delta of an entry from the perturbed generation and consumption.
    /// </summary>
    /// <param name="entry">The original entry</param>
    /// <param name="generation">The perturbed generation</param>
    /// <param name="consumption">The perturbed consumption</param>
    /// <param name="factor">The factor of the entry</param>
    /// <returns>The delta</returns>
    private static float DeriveDelta(ProductionEntry entry, float generation, float consumption, double factor)
    {
        if (float.IsFinite(entry.Delta) == false)
        {
            return KeepNonFinite(entry.Delta);
        }

        return float.IsFinite(entry.Generation) && float.IsFinite(entry.Consumption) ? generation - consumption : (float)(entry.Delta * factor);
    }

    /// <summary>
    /// Derives the average productivity of an entry from the perturbed summed productivity.
    /// </summary>
    /// <param name="entry">The original entry</param>
    /// <param name="summed">The perturbed summed productivity</param>
    /// <param name="factor">The factor of the entry</param>
    /// <returns>The average productivity</returns>
    private static float DeriveAverage(ProductionEntry entry, float summed, double factor)
    {
        if (float.IsFinite(entry.AverageProductivity) == false)
        {
            return KeepNonFinite(entry.AverageProductivity);
        }

        if (entry.BuildingCount == 0)
        {
            return 0f;
        }

        return float.IsFinite(summed) ? (float)(100.0 * summed / entry.BuildingCount) : (float)(entry.AverageProductivity * factor);
    }

    /// <summary>
    /// Copies a map unchanged.
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="frame">The original frame</param>
    /// <param name="position">The position of the map; behind it afterwards</param>
    private static void CopyMap(BinaryWriter writer, ReadOnlySpan<byte> frame, ref int position)
    {
        var length = sizeof(int) + (BinaryPrimitives.ReadInt32LittleEndian(frame.Slice(position)) * 2 * sizeof(int));

        writer.Write(frame.Slice(position, length));
        position += length;
    }

    /// <summary>
    /// Perturbs an integer: 0 stays, 1 becomes 2, 2 to 4 change by 1, larger magnitudes by 1 % to 20 %, at least 1; the
    /// sign is kept and an overflow goes downwards.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="upwards">Whether the magnitude grows</param>
    /// <param name="magnitude">The draw of the size of the change</param>
    /// <returns>The perturbed value</returns>
    private static int Perturb(int value, bool upwards, double magnitude)
    {
        if (value == 0)
        {
            return 0;
        }

        var negative = value < 0;
        var original = Math.Abs((long)value);
        var limit = negative ? MaxNegativeMagnitude : int.MaxValue;
        var changed = ChangeMagnitude(original, upwards, magnitude);

        if (changed > limit)
        {
            changed = ChangeMagnitude(original, false, magnitude);
        }

        return (int)(negative ? -changed : changed);
    }

    /// <summary>
    /// Changes a magnitude.
    /// </summary>
    /// <param name="original">The magnitude, at least 1</param>
    /// <param name="upwards">Whether the magnitude grows</param>
    /// <param name="magnitude">The draw of the size of the change</param>
    /// <returns>The changed magnitude</returns>
    private static long ChangeMagnitude(long original, bool upwards, double magnitude)
    {
        if (original == 1)
        {
            return 2;
        }

        var step = upwards ? 1L : -1L;

        if (original < PercentFrom)
        {
            return original + step;
        }

        var percent = 0.01 + (0.19 * magnitude);
        var changed = (long)Math.Round(original * (1 + (step * percent)), MidpointRounding.AwayFromZero);

        return changed == original ? original + step : changed;
    }

    /// <summary>
    /// Creates the length byte and the UTF-8 bytes of a name.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns>The bytes</returns>
    private static byte[] NameBytes(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);

        return [(byte)bytes.Length, .. bytes];
    }

    /// <summary>
    /// Parses a written frame back and checks that it ends where the decoding ends and that it keeps the structure of the
    /// original, so that a mismatch of the walker never lets bytes of the original pass.
    /// </summary>
    /// <param name="written">The written frame</param>
    /// <param name="original">The message the original frame decodes to</param>
    /// <returns><see langword="true" /> when the written frame keeps the structure</returns>
    private static bool IsReadBack(byte[] written, PipeMessage original)
    {
        var result = PipeMessageParser.Parse(written, out var consumed);

        if (consumed != written.Length || result.Message is null || result.Message.MessageType != original.MessageType)
        {
            return false;
        }

        return original is not AreaProductionStatisticsMessage area
               || (result.Message is AreaProductionStatisticsMessage back && KeepsStructure(area, back));
    }

    /// <summary>
    /// Compares the structure of two island messages: the entries, their product GUIDs and the GUIDs of their maps.
    /// </summary>
    /// <param name="original">The original message</param>
    /// <param name="written">The message of the written frame</param>
    /// <returns><see langword="true" /> when the structure is the same</returns>
    private static bool KeepsStructure(AreaProductionStatisticsMessage original, AreaProductionStatisticsMessage written)
    {
        return original.Entries.Count == written.Entries.Count
               && original.Entries.Zip(written.Entries)
                                  .All(pair => pair.First.ProductGuid == pair.Second.ProductGuid
                                               && pair.First.WorkforceAmounts.Keys.ToHashSet().SetEquals(pair.Second.WorkforceAmounts.Keys)
                                               && pair.First.BuildingAmounts.Keys.ToHashSet().SetEquals(pair.Second.BuildingAmounts.Keys));
    }

    /// <summary>
    /// Picks the profit among the integers next to the new income minus maintenance.
    /// </summary>
    /// <param name="profit">The original profit</param>
    /// <param name="lower">The new income minus maintenance rounded down</param>
    /// <param name="upper">The new income minus maintenance rounded up</param>
    /// <param name="below">Whether the original profit was at or below its income minus maintenance</param>
    /// <returns>The profit</returns>
    private static int PickProfit(int profit, long lower, long upper, bool below)
    {
        var candidate = PreferredProfit(profit, lower, upper, below);

        if (candidate == profit && profit != 0)
        {
            candidate = AlternativeProfit(lower, upper, below);
        }

        if (candidate is < int.MinValue or > int.MaxValue)
        {
            candidate = candidate == lower ? upper : lower;
        }

        if (candidate is >= int.MinValue and <= int.MaxValue && (candidate != profit || profit == 0))
        {
            return (int)candidate;
        }

        var edge = (int)Math.Clamp(candidate, int.MinValue, int.MaxValue);

        if (edge != profit)
        {
            return edge;
        }

        return edge > 0 ? edge - 1 : edge + 1;
    }

    /// <summary>
    /// Gives the profit to try first: 0 for a zero profit while 0 is within 1 of the new difference, else the neighbour
    /// on the original's side.
    /// </summary>
    /// <param name="profit">The original profit</param>
    /// <param name="lower">The new income minus maintenance rounded down</param>
    /// <param name="upper">The new income minus maintenance rounded up</param>
    /// <param name="below">Whether the original profit was at or below its income minus maintenance</param>
    /// <returns>The profit to try first</returns>
    private static long PreferredProfit(int profit, long lower, long upper, bool below)
    {
        if (profit == 0 && lower >= -1 && upper <= 1)
        {
            return 0;
        }

        return below ? lower : upper;
    }

    /// <summary>
    /// Gives the other profit next to the new difference than the preferred one.
    /// </summary>
    /// <param name="lower">The new income minus maintenance rounded down</param>
    /// <param name="upper">The new income minus maintenance rounded up</param>
    /// <param name="below">Whether the original profit was at or below its income minus maintenance</param>
    /// <returns>The profit</returns>
    private static long AlternativeProfit(long lower, long upper, bool below)
    {
        if (lower != upper)
        {
            return below ? upper : lower;
        }

        return below ? lower - 1 : upper + 1;
    }

    /// <summary>
    /// Converts an integral double to a long, limited to a range that cannot overflow.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The long</returns>
    private static long ToLong(double value)
    {
        return (long)Math.Clamp(value, -4e18, 4e18);
    }

    /// <summary>
    /// Decodes a frame and applies the version gate as the message reader does.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="message">The message, or <see langword="null" /> for an empty frame</param>
    /// <returns><see langword="true" /> when the frame is empty or a message the gate lets pass</returns>
    private bool TryAccept(ReadOnlySpan<byte> frame, out PipeMessage? message)
    {
        message = null;

        if (frame.IsEmpty)
        {
            return true;
        }

        message = PipeMessageParser.Parse(frame).Message;

        if (message is VersionMessage version)
        {
            _supported = version.Version == PipeProtocol.SupportedVersion;

            return true;
        }

        return message is not null && _supported;
    }

    /// <summary>
    /// Writes a session-start frame with the invented name.
    /// </summary>
    /// <param name="start">The message</param>
    /// <returns>The frame, or <see langword="null" /> when the name was not collected</returns>
    private byte[]? WriteSessionStart(SessionStartMessage start)
    {
        if (_names.TryGetFigure(start.Headline, out var invented) == false)
        {
            return null;
        }

        return [(byte)PipeMessageType.SessionStart, .. NameBytes(invented)];
    }

    /// <summary>
    /// Writes an island frame field by field with the amounts perturbed.
    /// </summary>
    /// <param name="frame">The original frame</param>
    /// <param name="area">The message the original frame decodes to</param>
    /// <returns>The frame, or <see langword="null" /> when the name was not collected</returns>
    private byte[]? WriteIsland(ReadOnlySpan<byte> frame, AreaProductionStatisticsMessage area)
    {
        if (_names.TryGetIsland(area.AreaName, out var invented) == false)
        {
            return null;
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(frame[..IslandHeadLength]);
        writer.Write(NameBytes(invented));

        var position = IslandHeadLength + 1 + frame[IslandHeadLength];

        writer.Write(frame.Slice(position, sizeof(long) + sizeof(int)));
        position += sizeof(long) + sizeof(int);

        foreach (var entry in area.Entries)
        {
            position += EntryScalarsLength;
            WriteEntryScalars(writer, entry);
            WriteWorkforce(writer, frame, ref position);
            CopyMap(writer, frame, ref position);
        }

        writer.Flush();

        return stream.ToArray();
    }

    /// <summary>
    /// Writes the scalar fields of an entry with the amounts perturbed.
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="entry">The original entry</param>
    private void WriteEntryScalars(BinaryWriter writer, ProductionEntry entry)
    {
        var factor = DrawFactor(entry);
        var generation = Scale(entry.Generation, factor);
        var consumption = Scale(entry.Consumption, factor);
        var income = Scale(entry.Income, factor);
        var summed = Scale(entry.SummedProductivity, factor);
        var maintenance = PerturbInteger(entry.Maintenance);

        writer.Write(entry.ProductGuid);
        WriteFloat(writer, generation);
        WriteFloat(writer, consumption);
        WriteFloat(writer, DeriveDelta(entry, generation, consumption, factor));
        WriteFloat(writer, Scale(entry.PerfectGeneration, factor));
        WriteFloat(writer, Scale(entry.PerfectConsumption, factor));
        writer.Write(entry.BuildingCount);
        writer.Write(maintenance);
        WriteFloat(writer, income);
        writer.Write(DeriveProfit(entry, income, maintenance));
        WriteFloat(writer, summed);
        WriteFloat(writer, DeriveAverage(entry, summed, factor));
    }

    /// <summary>
    /// Draws the factor of an entry: 0.80 to 0.99 or 1.01 to 1.20, the lower range when a multiplied value would overflow.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The factor</returns>
    private double DrawFactor(ProductionEntry entry)
    {
        var upwards = _random.NextDouble() >= UpwardsFrom;
        var magnitude = 0.19 * _random.NextDouble();

        if (upwards)
        {
            var factor = 1.01 + magnitude;

            if (Overflows(entry, factor) == false)
            {
                return factor;
            }
        }

        return 0.80 + magnitude;
    }

    /// <summary>
    /// Derives the profit of an entry: an integer within 1 of the new income minus maintenance, on the original's side of it,
    /// that differs from a non-zero original; at the edge of the 32-bit range it moves 1 inwards.
    /// </summary>
    /// <param name="entry">The original entry</param>
    /// <param name="income">The perturbed income</param>
    /// <param name="maintenance">The perturbed maintenance</param>
    /// <returns>The profit</returns>
    private int DeriveProfit(ProductionEntry entry, float income, int maintenance)
    {
        if (float.IsFinite(income) == false)
        {
            return PerturbInteger(entry.Profit);
        }

        var difference = (double)income - maintenance;
        var below = (double)entry.Income - entry.Maintenance - entry.Profit >= 0;

        return PickProfit(entry.Profit, ToLong(Math.Floor(difference)), ToLong(Math.Ceiling(difference)), below);
    }

    /// <summary>
    /// Writes a workforce map with every amount perturbed, keeping the sum of a repeated GUID within the 32-bit range.
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="frame">The original frame</param>
    /// <param name="position">The position of the map; behind it afterwards</param>
    private void WriteWorkforce(BinaryWriter writer, ReadOnlySpan<byte> frame, ref int position)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(frame.Slice(position));
        var sums = new Dictionary<int, long>();

        writer.Write(count);
        position += sizeof(int);

        for (var index = 0; index < count; index++)
        {
            var guid = BinaryPrimitives.ReadInt32LittleEndian(frame.Slice(position));
            var amount = BinaryPrimitives.ReadInt32LittleEndian(frame.Slice(position + sizeof(int)));
            var upwards = _random.NextDouble() >= UpwardsFrom;
            var magnitude = _random.NextDouble();
            var changed = Perturb(amount, upwards, magnitude);

            sums.TryGetValue(guid, out var sum);

            if (sum + changed is < int.MinValue or > int.MaxValue)
            {
                changed = Perturb(amount, upwards == false, magnitude);
            }

            sums[guid] = sum + changed;
            writer.Write(guid);
            writer.Write(changed);
            position += 2 * sizeof(int);
        }
    }

    /// <summary>
    /// Draws a side and a magnitude and perturbs an integer.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The perturbed value</returns>
    private int PerturbInteger(int value)
    {
        var upwards = _random.NextDouble() >= UpwardsFrom;

        return Perturb(value, upwards, _random.NextDouble());
    }

    #endregion // Methods
}