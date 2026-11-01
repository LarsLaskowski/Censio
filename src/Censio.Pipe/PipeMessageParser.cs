using System.Buffers.Binary;
using System.Text;

namespace Censio.Pipe;

/// <summary>
/// Decodes one frame (type byte and payload) into a typed pipe message.
/// </summary>
public static class PipeMessageParser
{
    #region Constants

    /// <summary>
    /// The smallest size of a production entry in bytes, the two map counts included
    /// </summary>
    private const int MinEntrySize = 56;

    /// <summary>
    /// The size of one map pair in bytes
    /// </summary>
    private const int PairSize = 8;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Decodes a frame without applying the version gate.
    /// </summary>
    /// <param name="frame">The frame bytes (type byte and payload)</param>
    /// <returns>The decoded message, or the error that describes why the frame is rejected</returns>
    public static PipeReadResult Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.IsEmpty)
        {
            return PipeReadResult.FromError(new PipeError
                                            {
                                                Kind = PipeErrorKind.EmptyFrame
                                            });
        }

        var typeValue = frame[0];

        if (typeValue > (byte)PipeMessageType.AreaProductionStatistics)
        {
            return PipeReadResult.FromError(new PipeError
                                            {
                                                Kind = PipeErrorKind.UnknownType,
                                                TypeValue = typeValue
                                            });
        }

        var position = 1;
        PipeMessage? message = (PipeMessageType)typeValue switch
                               {
                                   PipeMessageType.Version => ParseVersion(frame, ref position),
                                   PipeMessageType.SessionStart => ParseSessionStart(frame, ref position),
                                   PipeMessageType.SessionEnd => new SessionEndMessage(),
                                   _ => ParseStatistics(frame, ref position)
                               };

        return message is null
                   ? PipeReadResult.FromError(new PipeError
                                              {
                                                  Kind = PipeErrorKind.MalformedMessage,
                                                  TypeValue = typeValue,
                                                  Offset = position
                                              })
                   : PipeReadResult.FromMessage(message);
    }

    /// <summary>
    /// Parses a version payload.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position; on failure the offset of the field that did not fit</param>
    /// <returns>The message, or <c>null</c> when the payload is malformed</returns>
    private static VersionMessage? ParseVersion(ReadOnlySpan<byte> frame, ref int position)
    {
        return TryReadInt(frame, ref position, out var version) ? new VersionMessage(version) : null;
    }

    /// <summary>
    /// Parses a session-start payload.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position; on failure the offset of the field that did not fit</param>
    /// <returns>The message, or <c>null</c> when the payload is malformed</returns>
    private static SessionStartMessage? ParseSessionStart(ReadOnlySpan<byte> frame, ref int position)
    {
        return TryReadString(frame, ref position, out var headline) ? new SessionStartMessage(headline) : null;
    }

    /// <summary>
    /// Parses an area production statistics payload.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position; on failure the offset of the field that did not fit</param>
    /// <returns>The message, or <c>null</c> when the payload is malformed</returns>
    private static AreaProductionStatisticsMessage? ParseStatistics(ReadOnlySpan<byte> frame, ref int position)
    {
        if (TryReadByte(frame, ref position, out var sessionId) == false
            || TryReadByte(frame, ref position, out var islandId) == false
            || TryReadByte(frame, ref position, out var areaIndex) == false
            || TryReadInt(frame, ref position, out var sessionGuid) == false
            || TryReadString(frame, ref position, out var areaName) == false
            || TryReadLong(frame, ref position, out var timestamp) == false)
        {
            return null;
        }

        var countPosition = position;

        if (TryReadInt(frame, ref position, out var entryCount) == false)
        {
            return null;
        }

        if (entryCount < 0 || entryCount > (frame.Length - position) / MinEntrySize)
        {
            position = countPosition;

            return null;
        }

        var entries = new List<ProductionEntry>(entryCount);

        for (var index = 0; index < entryCount; index++)
        {
            var entry = ParseEntry(frame, ref position);

            if (entry is null)
            {
                return null;
            }

            entries.Add(entry);
        }

        return new AreaProductionStatisticsMessage
               {
                   SessionId = sessionId,
                   IslandId = islandId,
                   AreaIndex = areaIndex,
                   SessionGuid = sessionGuid,
                   AreaName = areaName,
                   Timestamp = timestamp,
                   Entries = entries
               };
    }

    /// <summary>
    /// Parses one production entry.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position; on failure the offset of the field that did not fit</param>
    /// <returns>The entry, or <c>null</c> when it is malformed</returns>
    private static ProductionEntry? ParseEntry(ReadOnlySpan<byte> frame, ref int position)
    {
        if (TryReadInt(frame, ref position, out var productGuid) == false
            || TryReadFloat(frame, ref position, out var generation) == false
            || TryReadFloat(frame, ref position, out var consumption) == false
            || TryReadFloat(frame, ref position, out var delta) == false
            || TryReadFloat(frame, ref position, out var perfectGeneration) == false
            || TryReadFloat(frame, ref position, out var perfectConsumption) == false
            || TryReadInt(frame, ref position, out var buildingCount) == false
            || TryReadInt(frame, ref position, out var maintenance) == false
            || TryReadFloat(frame, ref position, out var income) == false
            || TryReadInt(frame, ref position, out var profit) == false
            || TryReadFloat(frame, ref position, out var summedProductivity) == false
            || TryReadFloat(frame, ref position, out var averageProductivity) == false
            || TryReadMap(frame, ref position, out var workforceAmounts) == false
            || TryReadMap(frame, ref position, out var buildingAmounts) == false)
        {
            return null;
        }

        return new ProductionEntry
               {
                   ProductGuid = productGuid,
                   Generation = generation,
                   Consumption = consumption,
                   Delta = delta,
                   PerfectGeneration = perfectGeneration,
                   PerfectConsumption = perfectConsumption,
                   BuildingCount = buildingCount,
                   Maintenance = maintenance,
                   Income = income,
                   Profit = profit,
                   SummedProductivity = summedProductivity,
                   AverageProductivity = averageProductivity,
                   WorkforceAmounts = workforceAmounts,
                   BuildingAmounts = buildingAmounts
               };
    }

    /// <summary>
    /// Reads a map of GUID to amount pairs, summing the amounts of a repeated GUID.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position; on failure the offset of the field that did not fit</param>
    /// <param name="map">The map</param>
    /// <returns><c>true</c> when the map was read</returns>
    private static bool TryReadMap(ReadOnlySpan<byte> frame, ref int position, out Dictionary<int, int> map)
    {
        map = [];

        var countPosition = position;

        if (TryReadInt(frame, ref position, out var pairCount) == false)
        {
            return false;
        }

        if (pairCount < 0 || pairCount > PipeProtocol.MaxMapPairs || pairCount > (frame.Length - position) / PairSize)
        {
            position = countPosition;

            return false;
        }

        map = new Dictionary<int, int>(pairCount);

        for (var index = 0; index < pairCount; index++)
        {
            var pairPosition = position;

            if (TryReadInt(frame, ref position, out var guid) == false || TryReadInt(frame, ref position, out var amount) == false)
            {
                return false;
            }

            map.TryGetValue(guid, out var current);

            var sum = (long)current + amount;

            if (sum is < int.MinValue or > int.MaxValue)
            {
                position = pairPosition;

                return false;
            }

            map[guid] = (int)sum;
        }

        if (map.Count < pairCount)
        {
            map.TrimExcess();
        }

        return true;
    }

    /// <summary>
    /// Reads a string made of one unsigned length byte and that many UTF-8 bytes.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position; on failure the offset of the field that did not fit</param>
    /// <param name="value">The string</param>
    /// <returns><c>true</c> when the string was read</returns>
    private static bool TryReadString(ReadOnlySpan<byte> frame, ref int position, out string value)
    {
        value = string.Empty;

        var start = position;

        if (TryReadByte(frame, ref position, out var length) == false)
        {
            return false;
        }

        if (length > frame.Length - position)
        {
            position = start;

            return false;
        }

        value = Encoding.UTF8.GetString(frame.Slice(position, length));
        position += length;

        return true;
    }

    /// <summary>
    /// Reads an unsigned byte.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position</param>
    /// <param name="value">The value</param>
    /// <returns><c>true</c> when the byte was read</returns>
    private static bool TryReadByte(ReadOnlySpan<byte> frame, ref int position, out byte value)
    {
        if (position >= frame.Length)
        {
            value = 0;

            return false;
        }

        value = frame[position];
        position++;

        return true;
    }

    /// <summary>
    /// Reads a little-endian 32-bit integer.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position</param>
    /// <param name="value">The value</param>
    /// <returns><c>true</c> when the integer was read</returns>
    private static bool TryReadInt(ReadOnlySpan<byte> frame, ref int position, out int value)
    {
        if (frame.Length - position < sizeof(int))
        {
            value = 0;

            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(frame.Slice(position));
        position += sizeof(int);

        return true;
    }

    /// <summary>
    /// Reads a little-endian 64-bit integer.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position</param>
    /// <param name="value">The value</param>
    /// <returns><c>true</c> when the integer was read</returns>
    private static bool TryReadLong(ReadOnlySpan<byte> frame, ref int position, out long value)
    {
        if (frame.Length - position < sizeof(long))
        {
            value = 0;

            return false;
        }

        value = BinaryPrimitives.ReadInt64LittleEndian(frame.Slice(position));
        position += sizeof(long);

        return true;
    }

    /// <summary>
    /// Reads a little-endian 32-bit float.
    /// </summary>
    /// <param name="frame">The frame</param>
    /// <param name="position">The read position</param>
    /// <param name="value">The value</param>
    /// <returns><c>true</c> when the float was read</returns>
    private static bool TryReadFloat(ReadOnlySpan<byte> frame, ref int position, out float value)
    {
        if (frame.Length - position < sizeof(float))
        {
            value = 0;

            return false;
        }

        value = BinaryPrimitives.ReadSingleLittleEndian(frame.Slice(position));
        position += sizeof(float);

        return true;
    }

    #endregion // Methods
}