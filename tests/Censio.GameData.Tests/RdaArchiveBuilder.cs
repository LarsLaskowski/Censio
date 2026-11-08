using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Censio.GameData.Tests;

/// <summary>
/// Builds synthetic RDA archives of format version 2.2 with invented content, and the single pieces of one.
/// </summary>
public sealed class RdaArchiveBuilder
{
    #region Constants

    /// <summary>
    /// The size of the header region in bytes.
    /// </summary>
    public const int HeaderSize = 792;

    /// <summary>
    /// The size of a directory entry in bytes.
    /// </summary>
    public const int EntrySize = 560;

    /// <summary>
    /// The size of a block header in bytes.
    /// </summary>
    public const int BlockHeaderSize = 32;

    /// <summary>
    /// The position of the first block offset in the header region.
    /// </summary>
    public const int FirstBlockOffsetPosition = 784;

    /// <summary>
    /// The position of the offset in a directory entry.
    /// </summary>
    public const int EntryOffsetPosition = 520;

    /// <summary>
    /// The position of the stored size in a directory entry.
    /// </summary>
    public const int EntryStoredSizePosition = 528;

    /// <summary>
    /// The position of the size in a directory entry.
    /// </summary>
    public const int EntrySizePosition = 536;

    /// <summary>
    /// The position of the file count in a block header.
    /// </summary>
    public const int BlockFileCountPosition = 4;

    /// <summary>
    /// The position of the stored directory size in a block header.
    /// </summary>
    public const int BlockStoredSizePosition = 8;

    /// <summary>
    /// The position of the decompressed directory size in a block header.
    /// </summary>
    public const int BlockDirectorySizePosition = 16;

    /// <summary>
    /// The position of the next block offset in a block header.
    /// </summary>
    public const int BlockNextPosition = 24;

    /// <summary>
    /// The magic of the supported version.
    /// </summary>
    public const string Magic = "Resource File V2.2";

    /// <summary>
    /// The flag of a compressed block.
    /// </summary>
    public const uint Compressed = 1;

    /// <summary>
    /// The flag of an encrypted block.
    /// </summary>
    public const uint Encrypted = 2;

    /// <summary>
    /// The flag of a memory-resident block.
    /// </summary>
    public const uint MemoryResident = 4;

    /// <summary>
    /// The flag of a deleted block.
    /// </summary>
    public const uint Deleted = 8;

    /// <summary>
    /// A flag the reader does not know.
    /// </summary>
    public const uint Unknown = 16;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The blocks in chain order.
    /// </summary>
    private readonly List<(uint Flags, (string Path, byte[] Content)[] Files, byte[]? StoredDirectory, Action<byte[]>? EditEntries)> _blocks = [];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Creates the header region.
    /// </summary>
    /// <param name="firstBlockOffset">The offset of the first block header</param>
    /// <param name="magic">The magic text</param>
    /// <param name="encoding">The encoding of the magic, ASCII when omitted</param>
    /// <returns>The header region</returns>
    public static byte[] Header(ulong firstBlockOffset, string magic = Magic, Encoding? encoding = null)
    {
        var header = new byte[HeaderSize];

        (encoding ?? Encoding.ASCII).GetBytes(magic).CopyTo(header, 0);
        WriteUInt64(header, FirstBlockOffsetPosition, firstBlockOffset);

        return header;
    }

    /// <summary>
    /// Creates a block header.
    /// </summary>
    /// <param name="flags">The flags</param>
    /// <param name="fileCount">The file count</param>
    /// <param name="storedDirectorySize">The stored size of the directory</param>
    /// <param name="directorySize">The decompressed size of the directory</param>
    /// <param name="nextBlockOffset">The offset of the next block header</param>
    /// <returns>The block header</returns>
    public static byte[] BlockHeader(uint flags, uint fileCount, ulong storedDirectorySize, ulong directorySize, ulong nextBlockOffset)
    {
        var header = new byte[BlockHeaderSize];

        BinaryPrimitives.WriteUInt32LittleEndian(header, flags);
        WriteUInt32(header, BlockFileCountPosition, fileCount);
        WriteUInt64(header, BlockStoredSizePosition, storedDirectorySize);
        WriteUInt64(header, BlockDirectorySizePosition, directorySize);
        WriteUInt64(header, BlockNextPosition, nextBlockOffset);

        return header;
    }

    /// <summary>
    /// Creates one directory entry.
    /// </summary>
    /// <param name="path">The path inside the archive</param>
    /// <param name="offset">The offset of the data</param>
    /// <param name="storedSize">The stored size</param>
    /// <param name="size">The decompressed size</param>
    /// <returns>The entry</returns>
    public static byte[] DirectoryEntry(string path, ulong offset, ulong storedSize, ulong size)
    {
        var entry = new byte[EntrySize];

        Encoding.Unicode.GetBytes(path).CopyTo(entry, 0);
        WriteUInt64(entry, EntryOffsetPosition, offset);
        WriteUInt64(entry, EntryStoredSizePosition, storedSize);
        WriteUInt64(entry, EntrySizePosition, size);

        return entry;
    }

    /// <summary>
    /// Creates a plain directory of entries without data.
    /// </summary>
    /// <param name="paths">The paths</param>
    /// <returns>The directory</returns>
    public static byte[] DirectoryOf(params string[] paths)
    {
        return paths.SelectMany(path => DirectoryEntry(path, 0, 0, 0)).ToArray();
    }

    /// <summary>
    /// Compresses bytes to a zlib stream.
    /// </summary>
    /// <param name="data">The bytes</param>
    /// <returns>The zlib stream</returns>
    public static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Creates invented content.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="repeat">How often the text is repeated</param>
    /// <returns>The bytes</returns>
    public static byte[] Content(string text, int repeat = 1)
    {
        return Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(text, repeat)));
    }

    /// <summary>
    /// Writes a 32-bit value.
    /// </summary>
    /// <param name="bytes">The bytes to patch</param>
    /// <param name="position">The position</param>
    /// <param name="value">The value</param>
    public static void WriteUInt32(byte[] bytes, long position, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan((int)position), value);
    }

    /// <summary>
    /// Writes a 64-bit value.
    /// </summary>
    /// <param name="bytes">The bytes to patch</param>
    /// <param name="position">The position</param>
    /// <param name="value">The value</param>
    public static void WriteUInt64(byte[] bytes, long position, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan((int)position), value);
    }

    /// <summary>
    /// Adds a block holding files.
    /// </summary>
    /// <param name="flags">The flags of the block</param>
    /// <param name="files">The paths and contents of the files; none for an empty block</param>
    /// <returns>This builder</returns>
    public RdaArchiveBuilder AddBlock(uint flags, params (string Path, byte[] Content)[] files)
    {
        _blocks.Add((flags, files, null, null));

        return this;
    }

    /// <summary>
    /// Adds a block holding files whose stored directory is given instead of derived.
    /// </summary>
    /// <param name="flags">The flags of the block</param>
    /// <param name="storedDirectory">The stored bytes of the directory</param>
    /// <param name="files">The paths and contents of the files</param>
    /// <returns>This builder</returns>
    public RdaArchiveBuilder AddBlockWithDirectory(uint flags, byte[] storedDirectory, params (string Path, byte[] Content)[] files)
    {
        _blocks.Add((flags, files, storedDirectory, null));

        return this;
    }

    /// <summary>
    /// Adds a block whose directory entries are edited before the directory is compressed, so that a malformed field
    /// can be written into a compressed directory.
    /// </summary>
    /// <param name="flags">The flags of the block</param>
    /// <param name="editEntry">Edits the bytes of each directory entry</param>
    /// <param name="files">The paths and contents of the files</param>
    /// <returns>This builder</returns>
    public RdaArchiveBuilder AddBlockEditingEntries(uint flags, Action<byte[]> editEntry, params (string Path, byte[] Content)[] files)
    {
        _blocks.Add((flags, files, null, editEntry));

        return this;
    }

    /// <summary>
    /// Builds the archive.
    /// </summary>
    /// <returns>The bytes and where its parts lie</returns>
    public RdaBuiltArchive Build()
    {
        using var stream = new MemoryStream();

        stream.Write(new byte[HeaderSize]);

        var placements = _blocks.Select(block => WriteBlock(stream, block)).ToList();
        var bytes = stream.ToArray();

        Header((ulong)placements[0].HeaderOffset).CopyTo(bytes, 0);

        for (var index = 0; index < placements.Count; index++)
        {
            var next = index + 1 < placements.Count ? placements[index + 1].HeaderOffset : bytes.Length;

            WriteUInt64(bytes, placements[index].HeaderOffset + BlockNextPosition, (ulong)next);
        }

        return new RdaBuiltArchive(bytes, placements);
    }

    /// <summary>
    /// Writes one block: the data, the directory and the header, in this order.
    /// </summary>
    /// <param name="stream">The stream to write to</param>
    /// <param name="block">The block</param>
    /// <returns>Where the parts of the block lie</returns>
    private static RdaBuiltBlock WriteBlock(MemoryStream stream, (uint Flags, (string Path, byte[] Content)[] Files, byte[]? StoredDirectory, Action<byte[]>? EditEntries) block)
    {
        var compressed = (block.Flags & Compressed) != 0;
        var entries = new List<byte[]>();
        var dataOffsets = new List<long>();

        if ((block.Flags & Deleted) != 0)
        {
            foreach (var file in block.Files)
            {
                entries.Add(DirectoryEntry(file.Path, 0, 0, 0));
                dataOffsets.Add(0);
            }
        }
        else if ((block.Flags & MemoryResident) != 0)
        {
            WriteMemoryResidentData(stream, block, compressed, entries, dataOffsets);
        }
        else
        {
            foreach (var file in block.Files)
            {
                var stored = compressed ? Deflate(file.Content) : file.Content;

                entries.Add(DirectoryEntry(file.Path, (ulong)stream.Position, (ulong)stored.Length, (ulong)file.Content.Length));
                dataOffsets.Add(stream.Position);
                stream.Write(stored);
            }
        }

        if (block.EditEntries is not null)
        {
            entries.ForEach(block.EditEntries);
        }

        var plainDirectory = entries.SelectMany(entry => entry).ToArray();
        var directory = block.StoredDirectory ?? (compressed && plainDirectory.Length > 0 ? Deflate(plainDirectory) : plainDirectory);
        var directoryOffset = stream.Position;

        stream.Write(directory);

        var headerOffset = stream.Position;

        stream.Write(BlockHeader(block.Flags, (uint)block.Files.Length, (ulong)directory.Length, (ulong)plainDirectory.Length, 0));

        return new RdaBuiltBlock(headerOffset, directoryOffset, dataOffsets);
    }

    /// <summary>
    /// Writes the data of a memory-resident block and the two sizes that follow it.
    /// </summary>
    /// <param name="stream">The stream to write to</param>
    /// <param name="block">The block</param>
    /// <param name="compressed">Whether the data is compressed</param>
    /// <param name="entries">Receives the directory entries</param>
    /// <param name="dataOffsets">Receives the absolute position of each entry's data</param>
    private static void WriteMemoryResidentData(MemoryStream stream, (uint Flags, (string Path, byte[] Content)[] Files, byte[]? StoredDirectory, Action<byte[]>? EditEntries) block, bool compressed, List<byte[]> entries, List<long> dataOffsets)
    {
        var data = block.Files.SelectMany(file => file.Content).ToArray();
        var stored = compressed ? Deflate(data) : data;
        var dataStart = stream.Position;
        var relative = 0UL;

        foreach (var file in block.Files)
        {
            entries.Add(DirectoryEntry(file.Path, relative, (ulong)file.Content.Length, (ulong)file.Content.Length));
            dataOffsets.Add(dataStart + (long)relative);
            relative += (ulong)file.Content.Length;
        }

        var sizes = new byte[16];

        WriteUInt64(sizes, 0, (ulong)stored.Length);
        WriteUInt64(sizes, 8, (ulong)data.Length);
        stream.Write(stored);
        stream.Write(sizes);
    }

    #endregion // Methods
}