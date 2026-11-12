using System.Buffers;

namespace Censio.Contracts;

/// <summary>
/// A growing byte buffer that refuses to hold more than a limit, so a message above its bound is never built whole.
/// </summary>
internal sealed class ContractBufferWriter : IBufferWriter<byte>
{
    #region Constants

    /// <summary>
    /// The capacity the buffer starts with.
    /// </summary>
    private const int InitialCapacity = 1024;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The most bytes the buffer accepts.
    /// </summary>
    private readonly int _limit;

    /// <summary>
    /// The buffer.
    /// </summary>
    private byte[] _buffer = new byte[InitialCapacity];

    /// <summary>
    /// The number of bytes written.
    /// </summary>
    private int _length;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractBufferWriter" /> class.
    /// </summary>
    /// <param name="limit">The most bytes the buffer accepts</param>
    internal ContractBufferWriter(int limit)
    {
        _limit = limit;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets a value indicating whether more bytes were written than the limit allows.
    /// </summary>
    internal bool Exceeded { get; private set; }

    /// <summary>
    /// Gets the bytes written.
    /// </summary>
    internal ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _length);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Makes room for a number of bytes after the bytes written.
    /// </summary>
    /// <param name="sizeHint">The number of bytes, at least one is made room for</param>
    private void EnsureRoom(int sizeHint)
    {
        var needed = (long)_length + Math.Max(sizeHint, 1);

        if (needed <= _buffer.Length)
        {
            return;
        }

        if (needed > Array.MaxLength)
        {
            Exceeded = true;

            throw new InvalidOperationException("The buffer cannot hold the bytes.");
        }

        Array.Resize(ref _buffer, (int)Math.Max(needed, Math.Min((long)_buffer.Length * 2, Array.MaxLength)));
    }

    #endregion // Methods

    #region IBufferWriter

    /// <inheritdoc />
    public void Advance(int count)
    {
        if (Exceeded)
        {
            return;
        }

        if (count < 0 || count > _limit - _length)
        {
            Exceeded = true;

            throw new InvalidOperationException("The bytes exceed the limit.");
        }

        _length += count;
    }

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureRoom(sizeHint);

        return _buffer.AsMemory(_length);
    }

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureRoom(sizeHint);

        return _buffer.AsSpan(_length);
    }

    #endregion // IBufferWriter
}