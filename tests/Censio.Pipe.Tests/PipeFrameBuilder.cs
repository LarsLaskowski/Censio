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
    public PipeFrameBuilder Int32(int value)
    {
        return Raw(RawLength(value));
    }

    /// <summary>
    /// Appends a little-endian 64-bit integer.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>This builder</returns>
    public PipeFrameBuilder Int64(long value)
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
    public PipeFrameBuilder Float32(float value)
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
    public PipeFrameBuilder String(string value)
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
        Int32(pairs.Count);

        foreach (var pair in pairs)
        {
            Int32(pair.Key).Int32(pair.Value);
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
        Int32(productGuid);

        for (var index = 1; index <= 5; index++)
        {
            Float32(productGuid + (index / 4f));
        }

        Int32(productGuid + 6).Int32(productGuid + 7).Float32(productGuid + 8.5f).Int32(productGuid + 9);

        return Float32(productGuid + 10.25f).Float32(productGuid + 11.75f);
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