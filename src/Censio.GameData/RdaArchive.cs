using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Censio.GameData;

/// <summary>
/// Reads the entries of an RDA archive of format version 2.2 within fixed limits.
/// </summary>
public sealed class RdaArchive : IDisposable
{
    #region Constants

    /// <summary>
    /// The size of the header region in bytes.
    /// </summary>
    private const int HeaderSize = 792;

    /// <summary>
    /// The position of the first block offset in the header region.
    /// </summary>
    private const int FirstBlockOffsetPosition = 784;

    /// <summary>
    /// The size of a block header in bytes.
    /// </summary>
    private const int BlockHeaderSize = 32;

    /// <summary>
    /// The size of a directory entry in bytes.
    /// </summary>
    private const int EntrySize = 560;

    /// <summary>
    /// The size of the path field of a directory entry in bytes.
    /// </summary>
    private const int PathFieldSize = 520;

    /// <summary>
    /// The size of the two data sizes in front of the directory of a memory-resident block.
    /// </summary>
    private const int MemoryResidentSizesSize = 16;

    /// <summary>
    /// The magic of the supported version.
    /// </summary>
    private const string Magic = "Resource File V2.2";

    /// <summary>
    /// The flags the reader knows.
    /// </summary>
    private const RdaBlockAttributes KnownFlags = RdaBlockAttributes.Compressed | RdaBlockAttributes.Encrypted | RdaBlockAttributes.MemoryResident | RdaBlockAttributes.Deleted;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The stream the archive is read from.
    /// </summary>
    private readonly Stream _stream;

    /// <summary>
    /// The limits the archive is read within.
    /// </summary>
    private readonly RdaLimits _limits;

    /// <summary>
    /// The position of the directory of each block in chain order; -1 for a block whose directory was not read.
    /// </summary>
    private readonly IReadOnlyList<long> _directoryOffsets;

    /// <summary>
    /// Whether the archive has been disposed.
    /// </summary>
    private bool _disposed;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RdaArchive" /> class.
    /// </summary>
    /// <param name="stream">The stream the archive is read from</param>
    /// <param name="limits">The limits the archive is read within</param>
    /// <param name="entries">The listed entries</param>
    /// <param name="encryptedBlockCount">The number of encrypted blocks</param>
    /// <param name="directoryOffsets">The position of the directory of each block in chain order</param>
    internal RdaArchive(Stream stream, RdaLimits limits, IReadOnlyList<RdaEntry> entries, int encryptedBlockCount, IReadOnlyList<long> directoryOffsets)
    {
        _stream = stream;
        _limits = limits;
        _directoryOffsets = directoryOffsets;
        Entries = entries;
        EncryptedBlockCount = encryptedBlockCount;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the entries in archive order, deleted and encrypted blocks excluded.
    /// </summary>
    public IReadOnlyList<RdaEntry> Entries { get; }

    /// <summary>
    /// Gets the number of encrypted blocks that were skipped.
    /// </summary>
    public int EncryptedBlockCount { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Opens the archive file for reading, shared with other readers and writers.
    /// </summary>
    /// <param name="path">The path of the archive file</param>
    /// <param name="limits">The limits to read within</param>
    /// <returns>The archive or the reason it could not be opened</returns>
    public static RdaOpenResult Open(string path, RdaLimits limits)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(limits);

        FileStream stream;

        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception exception) when (IsStreamFailure(exception))
        {
            return RdaOpenResult.FromError(Unreadable(exception));
        }

        return Open(stream, limits);
    }

    /// <summary>
    /// Opens an archive from a stream, which the archive takes ownership of.
    /// </summary>
    /// <param name="stream">The readable, seekable stream</param>
    /// <param name="limits">The limits to read within</param>
    /// <returns>The archive or the reason it could not be opened</returns>
    public static RdaOpenResult Open(Stream stream, RdaLimits limits)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(limits);

        RdaError? error;
        RdaArchive? archive = null;

        try
        {
            error = stream is { CanRead: true, CanSeek: true }
                        ? ReadChain(stream, limits, out archive)
                        : new RdaError(RdaErrorKind.Unreadable, "The archive stream cannot be read and sought.");
        }
        catch (Exception exception) when (IsStreamFailure(exception))
        {
            error = Unreadable(exception);
        }

        if (archive is not null && error is null)
        {
            return RdaOpenResult.FromArchive(archive);
        }

        stream.Dispose();

        return RdaOpenResult.FromError(error ?? new RdaError(RdaErrorKind.CorruptArchive, "The archive could not be read."));
    }

    /// <summary>
    /// Reads the decompressed content of the entry of a path.
    /// </summary>
    /// <param name="path">The path inside the archive</param>
    /// <returns>The content or the reason it could not be read</returns>
    public RdaReadResult Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ObjectDisposedException.ThrowIf(_disposed, this);

        RdaEntry? found = null;

        foreach (var candidate in Entries)
        {
            if (string.Equals(candidate.Path, path, StringComparison.Ordinal) == false)
            {
                continue;
            }

            if (found is not null)
            {
                return RdaReadResult.FromError(new RdaError(RdaErrorKind.DuplicateEntry, "More than one entry has the requested path."));
            }

            found = candidate;
        }

        if (found is null)
        {
            return RdaReadResult.FromError(new RdaError(RdaErrorKind.NotFound, "The archive has no entry with the requested path."));
        }

        try
        {
            var error = ReadContent(found, out var content);

            return error is null ? RdaReadResult.FromContent(content) : RdaReadResult.FromError(error);
        }
        catch (Exception exception) when (IsStreamFailure(exception))
        {
            return RdaReadResult.FromError(Unreadable(exception));
        }
    }

    /// <summary>
    /// Creates the error of a stream that failed.
    /// </summary>
    /// <param name="exception">The exception, of which only the type name is kept</param>
    /// <returns>The error</returns>
    private static RdaError Unreadable(Exception exception)
    {
        return new RdaError(RdaErrorKind.Unreadable, $"The archive could not be read ({exception.GetType().Name}).");
    }

    /// <summary>
    /// Determines whether an exception is a failure of the underlying file or stream.
    /// </summary>
    /// <param name="exception">The exception</param>
    /// <returns>True for the failures that become an unreadable archive</returns>
    private static bool IsStreamFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or ObjectDisposedException;
    }

    /// <summary>
    /// Creates a corrupt-archive error.
    /// </summary>
    /// <param name="message">The fixed message</param>
    /// <returns>The error</returns>
    private static RdaError CorruptArchive(string message)
    {
        return new RdaError(RdaErrorKind.CorruptArchive, message);
    }

    /// <summary>
    /// Creates a corrupt-entry error.
    /// </summary>
    /// <param name="message">The fixed message</param>
    /// <returns>The error</returns>
    private static RdaError CorruptEntry(string message)
    {
        return new RdaError(RdaErrorKind.CorruptEntry, message);
    }

    /// <summary>
    /// Reads the header region and walks the block chain.
    /// </summary>
    /// <param name="stream">The stream of the archive</param>
    /// <param name="limits">The limits to read within</param>
    /// <param name="archive">Receives the archive when the chain was read</param>
    /// <returns>The error, or null</returns>
    private static RdaError? ReadChain(Stream stream, RdaLimits limits, out RdaArchive? archive)
    {
        archive = null;

        var length = stream.Length;
        var magic = Encoding.ASCII.GetBytes(Magic);
        var header = new byte[HeaderSize];
        var prefix = (int)Math.Min(length, magic.Length);

        stream.Position = 0;
        stream.ReadExactly(header, 0, prefix);

        if (prefix < magic.Length || header.AsSpan(0, magic.Length).SequenceEqual(magic) == false)
        {
            return new RdaError(RdaErrorKind.UnsupportedArchive, "The file is not an RDA archive of version 2.2.");
        }

        if (length < HeaderSize)
        {
            return CorruptArchive("The archive is shorter than its header region.");
        }

        stream.ReadExactly(header, prefix, HeaderSize - prefix);

        var first = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(FirstBlockOffsetPosition));

        if (first > long.MaxValue)
        {
            return CorruptArchive("The first block offset is above the supported range.");
        }

        var state = new RdaChainState(length);
        var error = WalkChain(stream, limits, state, (long)first);

        if (error is not null)
        {
            return error;
        }

        archive = new RdaArchive(stream, limits, state.Entries, state.EncryptedBlocks, state.DirectoryOffsets);

        return null;
    }

    /// <summary>
    /// Walks the blocks from the first block offset to the end of the file.
    /// </summary>
    /// <param name="stream">The stream of the archive</param>
    /// <param name="limits">The limits to read within</param>
    /// <param name="state">The state of the walk</param>
    /// <param name="firstOffset">The offset of the first block header</param>
    /// <returns>The error, or null</returns>
    private static RdaError? WalkChain(Stream stream, RdaLimits limits, RdaChainState state, long firstOffset)
    {
        var visited = new HashSet<long>();
        var offset = firstOffset;

        while (true)
        {
            if (offset < HeaderSize || offset > state.Length || state.Length - offset < BlockHeaderSize)
            {
                return CorruptArchive($"Block {state.DirectoryOffsets.Count} lies outside the archive.");
            }

            if (visited.Add(offset) == false)
            {
                return CorruptArchive($"Block {state.DirectoryOffsets.Count} was reached twice.");
            }

            if (state.DirectoryOffsets.Count >= limits.MaxBlocks)
            {
                return CorruptArchive($"The archive has more than {limits.MaxBlocks} blocks.");
            }

            var error = ReadBlock(stream, limits, state, offset, out var next);

            if (error is not null)
            {
                return error;
            }

            if (next == state.Length)
            {
                return null;
            }

            offset = next;
        }
    }

    /// <summary>
    /// Reads one block header and, where the block counts, its directory.
    /// </summary>
    /// <param name="stream">The stream of the archive</param>
    /// <param name="limits">The limits to read within</param>
    /// <param name="state">The state of the walk</param>
    /// <param name="headerOffset">The offset of the block header</param>
    /// <param name="next">Receives the offset of the next block header</param>
    /// <returns>The error, or null</returns>
    private static RdaError? ReadBlock(Stream stream, RdaLimits limits, RdaChainState state, long headerOffset, out long next)
    {
        var index = state.DirectoryOffsets.Count;
        var header = new byte[BlockHeaderSize];

        next = 0;
        state.DirectoryOffsets.Add(-1);
        stream.Position = headerOffset;
        stream.ReadExactly(header);

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(header);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
        var stored = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(8));
        var declared = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(16));
        var nextOffset = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(24));

        if (stored > long.MaxValue || declared > long.MaxValue || nextOffset > long.MaxValue)
        {
            return CorruptArchive($"Block {index} has a value above the supported range.");
        }

        next = (long)nextOffset;

        if ((flags & (uint)RdaBlockAttributes.Encrypted) != 0)
        {
            state.EncryptedBlocks++;

            return null;
        }

        if ((flags & (uint)RdaBlockAttributes.Deleted) != 0)
        {
            return null;
        }

        var block = new RdaBlockHeader(index, headerOffset, flags, count, (long)stored, (long)declared);
        var error = CheckDirectory(limits, state, block);

        if (error is not null || count == 0)
        {
            return error;
        }

        state.DirectoryOffsets[index] = block.DirectoryOffset;

        return ReadDirectory(stream, state, block);
    }

    /// <summary>
    /// Checks the sizes a block declares for its directory before the directory is read.
    /// </summary>
    /// <param name="limits">The limits to read within</param>
    /// <param name="state">The state of the walk</param>
    /// <param name="block">The header of the block</param>
    /// <returns>The error, or null</returns>
    private static RdaError? CheckDirectory(RdaLimits limits, RdaChainState state, RdaBlockHeader block)
    {
        var index = block.Index;
        var count = block.Count;
        var stored = block.Stored;
        var declared = block.Declared;

        if (state.Entries.Count + (long)count > limits.MaxEntries)
        {
            return CorruptArchive($"The archive has more than {limits.MaxEntries} entries.");
        }

        if (declared != (long)count * EntrySize || declared > limits.MaxDirectoryBytes)
        {
            return CorruptArchive($"The directory of block {index} has a size that does not fit its file count or exceeds {limits.MaxDirectoryBytes} bytes.");
        }

        if (stored > limits.MaxDirectoryBytes || stored > block.HeaderOffset - HeaderSize)
        {
            return CorruptArchive($"The stored directory of block {index} exceeds {limits.MaxDirectoryBytes} bytes or lies outside the archive.");
        }

        if ((block.Flags & (uint)RdaBlockAttributes.Compressed) == 0 && stored != declared)
        {
            return CorruptArchive($"The stored directory of block {index} differs in size from the declared one.");
        }

        state.StoredDirectoryBytes += stored;

        return state.StoredDirectoryBytes > state.Length ? CorruptArchive("The stored directories together exceed the size of the archive.") : null;
    }

    /// <summary>
    /// Reads the directory of a block and adds its entries.
    /// </summary>
    /// <param name="stream">The stream of the archive</param>
    /// <param name="state">The state of the walk</param>
    /// <param name="block">The header of the block</param>
    /// <returns>The error, or null</returns>
    private static RdaError? ReadDirectory(Stream stream, RdaChainState state, RdaBlockHeader block)
    {
        var index = block.Index;
        var flags = block.Flags;
        var count = (int)block.Count;
        var directoryOffset = block.DirectoryOffset;
        var stored = (int)block.Stored;
        var declared = (int)block.Declared;
        byte[] directory;

        if ((flags & (uint)RdaBlockAttributes.Compressed) != 0)
        {
            if (Inflate(stream, directoryOffset, stored, declared, out directory) == false)
            {
                return CorruptArchive($"The compressed directory of block {index} is damaged or does not have its declared size.");
            }
        }
        else
        {
            directory = new byte[declared];
            stream.Position = directoryOffset;
            stream.ReadExactly(directory);
        }

        for (var position = 0; position < count; position++)
        {
            state.Entries.Add(ParseEntry(directory.AsSpan(position * EntrySize, EntrySize), flags, index));
        }

        return null;
    }

    /// <summary>
    /// Parses one directory entry.
    /// </summary>
    /// <param name="bytes">The bytes of the entry</param>
    /// <param name="flags">The flags of the block</param>
    /// <param name="blockIndex">The index of the block</param>
    /// <returns>The entry</returns>
    private static RdaEntry ParseEntry(ReadOnlySpan<byte> bytes, uint flags, int blockIndex)
    {
        var path = bytes[..PathFieldSize];
        var characters = 0;

        while (characters < PathFieldSize / 2 && (path[characters * 2] != 0 || path[(characters * 2) + 1] != 0))
        {
            characters++;
        }

        var offset = BinaryPrimitives.ReadUInt64LittleEndian(bytes[PathFieldSize..]);
        var stored = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(PathFieldSize + 8)..]);
        var size = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(PathFieldSize + 16)..]);

        return new RdaEntry(Encoding.Unicode.GetString(path[..(characters * 2)]),
                            size > long.MaxValue ? -1 : (long)size,
                            unchecked((RdaBlockAttributes)(int)flags),
                            offset,
                            stored,
                            blockIndex);
    }

    /// <summary>
    /// Decompresses a zlib stream from a bounded range of the archive into a buffer of the declared size.
    /// </summary>
    /// <param name="stream">The stream of the archive</param>
    /// <param name="start">The position of the stored data</param>
    /// <param name="stored">The stored size in bytes</param>
    /// <param name="declared">The declared decompressed size in bytes</param>
    /// <param name="result">Receives the decompressed bytes</param>
    /// <returns>True when the stream is valid and yields exactly the declared number of bytes</returns>
    private static bool Inflate(Stream stream, long start, long stored, int declared, out byte[] result)
    {
        result = new byte[declared];

        try
        {
            using var window = new RdaWindowStream(stream, start, stored);
            using var zlib = new ZLibStream(window, CompressionMode.Decompress);

            var filled = 0;
            int read;

            while (filled < declared && (read = zlib.Read(result, filled, declared - filled)) > 0)
            {
                filled += read;
            }

            return filled == declared && zlib.ReadByte() < 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the content of an entry.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <param name="content">Receives the content</param>
    /// <returns>The error, or null</returns>
    private RdaError? ReadContent(RdaEntry entry, out ReadOnlyMemory<byte> content)
    {
        content = default;

        if ((entry.Flags & ~KnownFlags) != 0)
        {
            return new RdaError(RdaErrorKind.UnsupportedEntry, "The entry belongs to a block with an unsupported flag.");
        }

        if (entry.Size < 0 || entry.Size > _limits.MaxEntryBytes || entry.Offset > long.MaxValue || entry.StoredSize > long.MaxValue)
        {
            return CorruptEntry($"The entry has a size or offset above the supported range or above {_limits.MaxEntryBytes} bytes.");
        }

        if ((entry.Flags & RdaBlockAttributes.MemoryResident) != 0)
        {
            return ReadMemoryResident(entry, out content);
        }

        var length = _stream.Length;
        var offset = (long)entry.Offset;
        var stored = (long)entry.StoredSize;
        var compressed = (entry.Flags & RdaBlockAttributes.Compressed) != 0;

        if (stored > _limits.MaxEntryBytes || offset > length || stored > length - offset)
        {
            return CorruptEntry($"The entry lies outside the archive or its stored size exceeds {_limits.MaxEntryBytes} bytes.");
        }

        if (compressed == false && stored != entry.Size)
        {
            return CorruptEntry("The stored size of the entry differs from its size.");
        }

        byte[] data;

        if (compressed)
        {
            if (Inflate(_stream, offset, stored, (int)entry.Size, out data) == false)
            {
                return CorruptEntry("The compressed entry is damaged or does not have its declared size.");
            }
        }
        else
        {
            data = new byte[(int)entry.Size];
            _stream.Position = offset;
            _stream.ReadExactly(data);
        }

        content = data;

        return null;
    }

    /// <summary>
    /// Reads the content of an entry of a memory-resident block, which is a slice of the block's decompressed data.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <param name="content">Receives the content</param>
    /// <returns>The error, or null</returns>
    private RdaError? ReadMemoryResident(RdaEntry entry, out ReadOnlyMemory<byte> content)
    {
        content = default;

        var directoryOffset = _directoryOffsets[entry.BlockIndex];

        if (directoryOffset < HeaderSize + MemoryResidentSizesSize)
        {
            return CorruptEntry($"The data of block {entry.BlockIndex} lies outside the archive.");
        }

        var sizes = new byte[MemoryResidentSizesSize];

        _stream.Position = directoryOffset - MemoryResidentSizesSize;
        _stream.ReadExactly(sizes);

        var stored = BinaryPrimitives.ReadUInt64LittleEndian(sizes);
        var decompressed = BinaryPrimitives.ReadUInt64LittleEndian(sizes.AsSpan(8));
        var compressed = (entry.Flags & RdaBlockAttributes.Compressed) != 0;

        if (stored > (ulong)_limits.MaxEntryBytes || decompressed > (ulong)_limits.MaxEntryBytes)
        {
            return CorruptEntry($"The data of block {entry.BlockIndex} exceeds {_limits.MaxEntryBytes} bytes.");
        }

        var dataStart = directoryOffset - MemoryResidentSizesSize - (long)stored;

        if (dataStart < HeaderSize || (compressed == false && stored != decompressed))
        {
            return CorruptEntry($"The data of block {entry.BlockIndex} lies outside the archive or has inconsistent sizes.");
        }

        var offset = (long)entry.Offset;

        if (offset > (long)decompressed || entry.Size > (long)decompressed - offset)
        {
            return CorruptEntry("The entry lies outside the data of its block.");
        }

        byte[] data;

        if (compressed)
        {
            if (Inflate(_stream, dataStart, (long)stored, (int)decompressed, out data) == false)
            {
                return CorruptEntry($"The compressed data of block {entry.BlockIndex} is damaged or does not have its declared size.");
            }
        }
        else
        {
            data = new byte[(int)decompressed];
            _stream.Position = dataStart;
            _stream.ReadExactly(data);
        }

        content = data.AsMemory((int)offset, (int)entry.Size).ToArray();

        return null;
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }

    #endregion // IDisposable
}