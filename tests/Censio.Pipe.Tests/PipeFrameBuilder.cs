using System.Buffers.Binary;
using System.Text;

namespace Censio.Pipe.Tests;

/// <summary>
/// Builds synthetic pipe frames: a type byte and a little-endian payload, optionally with the 4-byte length prefix.
/// </summary>
public sealed class PipeFrameBuilder
{
    #region Fields

    /// <summary>
    /// The bytes written so far.
    /// </summary>
    private readonly List<byte> _bytes = [];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Creates a 4-byte little-endian length prefix with a raw value, for the corrupt cases.
    /// </summary>
    /// <param name="length">The raw length value</param>
    /// <returns>The prefix bytes</returns>
    public static byte[] RawLength(int length)
    {
        var prefix = new byte[sizeof(int)];

        BinaryPrimitives.WriteInt32LittleEndian(prefix, length);

        return prefix;
    }

    /// <summary>
    /// Creates a complete frame, the length prefix included.
    /// </summary>
    /// <param name="frame">The frame bytes (type byte and payload)</param>
    /// <returns>The prefix followed by the frame</returns>
    public static byte[] WithPrefix(byte[] frame)
    {
        return [.. RawLength(frame.Length), .. frame];
    }

    /// <summary>
    /// Appends an unsigned byte.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Byte(byte value)
    {
        _bytes.Add(value);

        return this;
    }

    /// <summary>
    /// Appends raw bytes.
    /// </summary>
    /// <param name="values">The bytes</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Raw(params byte[] values)
    {
        _bytes.AddRange(values);

        return this;
    }

    /// <summary>
    /// Appends a little-endian 32-bit integer.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Int32Value(int value)
    {
        return Raw(RawLength(value));
    }

    /// <summary>
    /// Appends a little-endian 64-bit integer.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Int64Value(long value)
    {
        var buffer = new byte[sizeof(long)];

        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);

        return Raw(buffer);
    }

    /// <summary>
    /// Appends a little-endian 32-bit float.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Float32Value(float value)
    {
        var buffer = new byte[sizeof(float)];

        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);

        return Raw(buffer);
    }

    /// <summary>
    /// Appends a string as one length byte and its UTF-8 bytes.
    /// </summary>
    /// <param name="value">The text, at most 255 UTF-8 bytes</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder StringValue(string value)
    {
        var encoded = Encoding.UTF8.GetBytes(value);

        return Byte((byte)encoded.Length).Raw(encoded);
    }

    /// <summary>
    /// Appends a map as its pair count and the (GUID, amount) pairs.
    /// </summary>
    /// <param name="pairs">The pairs, duplicates allowed</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Map(IReadOnlyCollection<KeyValuePair<int, int>> pairs)
    {
        Int32Value(pairs.Count);

        foreach (var pair in pairs)
        {
            Int32Value(pair.Key).Int32Value(pair.Value);
        }

        return this;
    }

    /// <summary>
    /// Appends a production entry with deterministic values derived from its GUID.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <param name="workforce">The workforce map pairs</param>
    /// <param name="buildings">The building map pairs</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Entry(int productGuid,
                                  IReadOnlyCollection<KeyValuePair<int, int>> workforce,
                                  IReadOnlyCollection<KeyValuePair<int, int>> buildings)
    {
        EntryFields(productGuid);

        return Map(workforce).Map(buildings);
    }

    /// <summary>
    /// Appends the twelve scalar values of a production entry, without its maps.
    /// </summary>
    /// <param name="productGuid">The product GUID</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder EntryFields(int productGuid)
    {
        Int32Value(productGuid);

        for (var index = 1; index <= 5; index++)
        {
            Float32Value(productGuid + (index / 4f));
        }

        Int32Value(productGuid + 6).Int32Value(productGuid + 7).Float32Value(productGuid + 8.5f).Int32Value(productGuid + 9);

        return Float32Value(productGuid + 10.25f).Float32Value(productGuid + 11.75f);
    }

    /// <summary>
    /// Appends a trade-route (type 4) payload in the layout of the reference reader (commit <c>4cc8ad7</c>,
    /// <c>src/pipe.cpp</c>): the route count and every route.
    /// </summary>
    /// <param name="routes">The routes, written in their order</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder TradeRoutes(IReadOnlyCollection<TradeRoute> routes)
    {
        Int32Value(routes.Count);

        return routes.Aggregate(this, (builder, route) => builder.Route(route));
    }

    /// <summary>
    /// Appends one trade route: its fields, its station count and every station.
    /// </summary>
    /// <param name="route">The route</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Route(TradeRoute route)
    {
        RouteFields(route).Int32Value(route.Stations.Count);

        return route.Stations.Aggregate(this, (builder, station) => builder.Station(station));
    }

    /// <summary>
    /// Appends the fields of a trade route before its station count: the ID, the name, the folder, the major and minor
    /// problem counts, the ship count and the lost-ship count.
    /// </summary>
    /// <param name="route">The route; its stations are not written</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder RouteFields(TradeRoute route)
    {
        return Int32Value(route.Id).StringValue(route.Name)
                                   .StringValue(route.Folder)
                                   .Int32Value(route.MajorProblems)
                                   .Int32Value(route.MinorProblems)
                                   .Int32Value(route.Ships)
                                   .Int32Value(route.LostShips);
    }

    /// <summary>
    /// Appends one station: its fields with the flags byte of its named flags, its goods count and every good.
    /// </summary>
    /// <param name="station">The station</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Station(TradeRouteStation station)
    {
        var flags = (station.ThrowOverboard ? 1 : 0) | (station.WaitUntilFull ? 2 : 0) | (station.WaitUntilEmpty ? 4 : 0);

        StationFields(station, (byte)flags).Int32Value(station.Goods.Count);

        return station.Goods.Aggregate(this, (builder, good) => builder.Good(good));
    }

    /// <summary>
    /// Appends the fields of a station before its goods count: the ID, the session ID, the island ID, the area index, the
    /// session GUID, the owner and a raw flags byte (bit 0 throw overboard, bit 1 wait until full, bit 2 wait until empty).
    /// </summary>
    /// <param name="station">The station; its flags and goods are not written</param>
    /// <param name="flags">The raw flags byte</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder StationFields(TradeRouteStation station, byte flags)
    {
        return Int32Value(station.Id).Byte(station.SessionId)
                                     .Byte(station.IslandId)
                                     .Byte(station.AreaIndex)
                                     .Int32Value(station.SessionGuid)
                                     .Int32Value(station.Owner)
                                     .Byte(flags);
    }

    /// <summary>
    /// Appends one good with the flags byte of its loading flag.
    /// </summary>
    /// <param name="good">The good</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Good(TradeRouteGood good)
    {
        return Good(good, good.Loading ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Appends one good: its product GUID, its amount and a raw flags byte (bit 0 loading).
    /// </summary>
    /// <param name="good">The good; its loading flag is not written</param>
    /// <param name="flags">The raw flags byte</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Good(TradeRouteGood good, byte flags)
    {
        return Int32Value(good.ProductGuid).Int32Value(good.Amount).Byte(flags);
    }

    /// <summary>
    /// Appends an item-offer (type 5) payload in the layout of the reference reader (commit <c>4cc8ad7</c>,
    /// <c>src/pipe.cpp</c>): the offer count and every offer.
    /// </summary>
    /// <param name="offers">The offers, written in their order</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder ItemOffers(IReadOnlyCollection<ItemOffer> offers)
    {
        Int32Value(offers.Count);

        return offers.Aggregate(this, (builder, offer) => builder.Offer(offer));
    }

    /// <summary>
    /// Appends one item offer: the trader ID, the item count and every item GUID.
    /// </summary>
    /// <param name="offer">The offer</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Offer(ItemOffer offer)
    {
        Int32Value(offer.TraderId).Int32Value(offer.ItemGuids.Count);

        return offer.ItemGuids.Aggregate(this, (builder, guid) => builder.Int32Value(guid));
    }

    /// <summary>
    /// Gets the bytes written so far: the frame without a length prefix.
    /// </summary>
    /// <returns>The frame bytes</returns>
    public byte[] Build()
    {
        return [.. _bytes];
    }

    /// <summary>
    /// Gets the bytes written so far with the correct length prefix.
    /// </summary>
    /// <returns>The prefix followed by the frame bytes</returns>
    public byte[] BuildWithPrefix()
    {
        return WithPrefix(Build());
    }

    #endregion // Methods
}