using System.Buffers.Binary;
using System.Numerics;

namespace Censio.GameData;

/// <summary>
/// Reads DDS texture files into RGBA images.
/// </summary>
public static class DdsReader
{
    #region Constants

    /// <summary>
    /// The size of the magic, the header and the <c>DX10</c> header in bytes.
    /// </summary>
    private const int HeadersSize = 148;

    /// <summary>
    /// The magic <c>DDS </c> as a little-endian number.
    /// </summary>
    private const uint Magic = 0x20534444;

    /// <summary>
    /// The size of the header.
    /// </summary>
    private const uint HeaderSize = 124;

    /// <summary>
    /// The size of the pixel format.
    /// </summary>
    private const uint PixelFormatSize = 32;

    /// <summary>
    /// The pixel format flag that says the four character code is valid.
    /// </summary>
    private const uint FourCcFlag = 0x4;

    /// <summary>
    /// The four character code <c>DX10</c> as a little-endian number.
    /// </summary>
    private const uint FourCcDx10 = 0x30315844;

    /// <summary>
    /// The header flag that says the level count is valid.
    /// </summary>
    private const uint LevelCountFlag = 0x20000;

    /// <summary>
    /// The DXGI format BC7 UNORM.
    /// </summary>
    private const uint DxgiBc7Unorm = 98;

    /// <summary>
    /// The DXGI format BC7 sRGB.
    /// </summary>
    private const uint DxgiBc7Srgb = 99;

    /// <summary>
    /// The resource dimension of a two-dimensional texture.
    /// </summary>
    private const uint Texture2D = 3;

    /// <summary>
    /// The miscellaneous flag of a cube map.
    /// </summary>
    private const uint CubeFlag = 0x4;

    /// <summary>
    /// The side of a block in pixels.
    /// </summary>
    private const int BlockSide = 4;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads a DDS file.
    /// </summary>
    /// <param name="file">The content of the DDS file</param>
    /// <param name="maxSide">The longest side in pixels the image may have</param>
    /// <returns>The decoded image, or <see langword="null" /> when the file is not a supported texture within the bound</returns>
    public static DdsImage? Read(ReadOnlySpan<byte> file, int maxSide)
    {
        if (file.Length < HeadersSize || HasSupportedFormat(file) == false)
        {
            return null;
        }

        var height = ReadValue(file, 12);
        var width = ReadValue(file, 16);

        if (width < 1 || height < 1 || width > maxSide || height > maxSide)
        {
            return null;
        }

        var levels = LevelCountOf(file, (int)width, (int)height);

        if (levels == 0 || file.Length != ExpectedLength((int)width, (int)height, levels))
        {
            return null;
        }

        return Decode(file[HeadersSize..], (int)width, (int)height);
    }

    /// <summary>
    /// Checks the fields of the headers that select the one supported format.
    /// </summary>
    /// <param name="file">The file, at least <see cref="HeadersSize" /> bytes</param>
    /// <returns><see langword="true" /> when the format is BC7 in a plain two-dimensional texture</returns>
    private static bool HasSupportedFormat(ReadOnlySpan<byte> file)
    {
        var dxgi = ReadValue(file, 128);

        return ReadValue(file, 0) == Magic
               && ReadValue(file, 4) == HeaderSize
               && ReadValue(file, 76) == PixelFormatSize
               && (ReadValue(file, 80) & FourCcFlag) != 0
               && ReadValue(file, 84) == FourCcDx10
               && (dxgi == DxgiBc7Unorm || dxgi == DxgiBc7Srgb)
               && ReadValue(file, 132) == Texture2D
               && (ReadValue(file, 136) & CubeFlag) == 0
               && ReadValue(file, 140) == 1;
    }

    /// <summary>
    /// Gets the number of levels the file holds.
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <returns>The level count, or 0 when it exceeds the full chain</returns>
    private static int LevelCountOf(ReadOnlySpan<byte> file, int width, int height)
    {
        if ((ReadValue(file, 8) & LevelCountFlag) == 0)
        {
            return 1;
        }

        var stored = ReadValue(file, 28);
        var chain = BitOperations.Log2((uint)Math.Max(width, height)) + 1;

        if (stored == 0)
        {
            return 1;
        }

        return stored > chain ? 0 : (int)stored;
    }

    /// <summary>
    /// Gets the length a file of the given levels must have.
    /// </summary>
    /// <param name="width">The width of level 0</param>
    /// <param name="height">The height of level 0</param>
    /// <param name="levels">The number of levels</param>
    /// <returns>The length in bytes</returns>
    private static long ExpectedLength(int width, int height, int levels)
    {
        long length = HeadersSize;

        for (var level = 0; level < levels; level++)
        {
            var levelWidth = Math.Max(1, width >> level);
            var levelHeight = Math.Max(1, height >> level);

            length += (long)BlocksOf(levelWidth) * BlocksOf(levelHeight) * Bc7Decoder.BlockBytes;
        }

        return length;
    }

    /// <summary>
    /// Decodes level 0 and crops it to its size.
    /// </summary>
    /// <param name="data">The bytes after the headers, starting with level 0</param>
    /// <param name="width">The width</param>
    /// <param name="height">The height</param>
    /// <returns>The image</returns>
    private static DdsImage Decode(ReadOnlySpan<byte> data, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        var blocksWide = BlocksOf(width);
        var blocksHigh = BlocksOf(height);
        Span<byte> pixels = stackalloc byte[64];

        for (var blockY = 0; blockY < blocksHigh; blockY++)
        {
            for (var blockX = 0; blockX < blocksWide; blockX++)
            {
                Bc7Decoder.DecodeBlock(data.Slice(((blockY * blocksWide) + blockX) * Bc7Decoder.BlockBytes, Bc7Decoder.BlockBytes), pixels);
                CopyBlock(pixels, rgba, width, height, blockX * BlockSide, blockY * BlockSide);
            }
        }

        return new DdsImage(width, height, rgba);
    }

    /// <summary>
    /// Copies the pixels of a block into the image, leaving out those beyond its edges.
    /// </summary>
    /// <param name="pixels">The 16 pixels of the block</param>
    /// <param name="rgba">The image</param>
    /// <param name="width">The width of the image</param>
    /// <param name="height">The height of the image</param>
    /// <param name="left">The column of the block's first pixel</param>
    /// <param name="top">The row of the block's first pixel</param>
    private static void CopyBlock(ReadOnlySpan<byte> pixels, byte[] rgba, int width, int height, int left, int top)
    {
        var columns = Math.Min(BlockSide, width - left);
        var rows = Math.Min(BlockSide, height - top);

        for (var row = 0; row < rows; row++)
        {
            pixels.Slice(row * BlockSide * 4, columns * 4).CopyTo(rgba.AsSpan((((top + row) * width) + left) * 4));
        }
    }

    /// <summary>
    /// Gets the number of blocks that cover a side.
    /// </summary>
    /// <param name="side">The side in pixels</param>
    /// <returns>The number of blocks</returns>
    private static int BlocksOf(int side)
    {
        return (side + BlockSide - 1) / BlockSide;
    }

    /// <summary>
    /// Reads a little-endian 32 bit value.
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="offset">The offset</param>
    /// <returns>The value</returns>
    private static uint ReadValue(ReadOnlySpan<byte> file, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(file[offset..]);
    }

    #endregion // Methods
}