using System.Buffers.Binary;

namespace Censio.GameData.Tests;

/// <summary>
/// Builds synthetic DDS files with a <c>DX10</c> header whose BC7 blocks are generated in code, and the expected pixels
/// of those blocks; no game icon is used.
/// </summary>
public static class DdsFileBuilder
{
    #region Constants

    /// <summary>
    /// The size of the magic, the header and the <c>DX10</c> header in bytes.
    /// </summary>
    public const int HeadersSize = 148;

    /// <summary>
    /// The position of the magic.
    /// </summary>
    public const int MagicPosition = 0;

    /// <summary>
    /// The position of the header size.
    /// </summary>
    public const int HeaderSizePosition = 4;

    /// <summary>
    /// The position of the header flags.
    /// </summary>
    public const int FlagsPosition = 8;

    /// <summary>
    /// The position of the height.
    /// </summary>
    public const int HeightPosition = 12;

    /// <summary>
    /// The position of the width.
    /// </summary>
    public const int WidthPosition = 16;

    /// <summary>
    /// The position of the level count.
    /// </summary>
    public const int LevelCountPosition = 28;

    /// <summary>
    /// The position of the pixel format size.
    /// </summary>
    public const int PixelFormatSizePosition = 76;

    /// <summary>
    /// The position of the pixel format flags.
    /// </summary>
    public const int PixelFormatFlagsPosition = 80;

    /// <summary>
    /// The position of the four character code.
    /// </summary>
    public const int FourCcPosition = 84;

    /// <summary>
    /// The position of the DXGI format.
    /// </summary>
    public const int DxgiFormatPosition = 128;

    /// <summary>
    /// The position of the resource dimension.
    /// </summary>
    public const int DimensionPosition = 132;

    /// <summary>
    /// The position of the miscellaneous flags.
    /// </summary>
    public const int MiscFlagsPosition = 136;

    /// <summary>
    /// The position of the array size.
    /// </summary>
    public const int ArraySizePosition = 140;

    /// <summary>
    /// The header flags with the level count flag.
    /// </summary>
    public const int FlagsWithLevels = 0xA1007;

    /// <summary>
    /// The DXGI format BC7 sRGB.
    /// </summary>
    public const int DxgiBc7Srgb = 99;

    /// <summary>
    /// The DXGI format BC7 UNORM.
    /// </summary>
    public const int DxgiBc7Unorm = 98;

    /// <summary>
    /// The bytes of one block.
    /// </summary>
    private const int BlockBytes = 16;

    /// <summary>
    /// The size of one block in pixels per side.
    /// </summary>
    private const int BlockSide = 4;

    /// <summary>
    /// The flags of a header without the level count flag.
    /// </summary>
    private const int FlagsWithoutLevels = 0x81007;

    /// <summary>
    /// The four character code <c>DX10</c>.
    /// </summary>
    private const int FourCcDx10 = 0x30315844;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Builds a block of mode 6 that decodes to one colour in all 16 pixels: both endpoints and all indices are the same.
    /// </summary>
    /// <param name="red">The red value; the lowest bit of all four values must be the same</param>
    /// <param name="green">The green value</param>
    /// <param name="blue">The blue value</param>
    /// <param name="alpha">The alpha value</param>
    /// <returns>The 16 bytes of the block</returns>
    public static byte[] SolidBlock(byte red, byte green, byte blue, byte alpha)
    {
        var pBit = red & 1;

        if ((green & 1) != pBit || (blue & 1) != pBit || (alpha & 1) != pBit)
        {
            throw new ArgumentException("The values must share their lowest bit", nameof(red));
        }

        var block = new byte[BlockBytes];
        var position = 0;

        Put(block, ref position, 1 << 6, 7);

        foreach (var value in new[] { red, green, blue, alpha })
        {
            Put(block, ref position, value >> 1, 7);
            Put(block, ref position, value >> 1, 7);
        }

        Put(block, ref position, pBit, 1);
        Put(block, ref position, pBit, 1);

        return block;
    }

    /// <summary>
    /// Gets the colour of the block with an index, all four values odd and different from white.
    /// </summary>
    /// <param name="index">The index of the block in level 0, row by row</param>
    /// <returns>The red, green, blue and alpha values</returns>
    public static byte[] BlockColor(int index)
    {
        return [(byte)(index * 20 + 1), (byte)(index * 20 + 3), (byte)(index * 20 + 5), (byte)(255 - index * 2)];
    }

    /// <summary>
    /// Builds a DDS file whose level 0 holds a solid block of <see cref="BlockColor(int)" /> per block and whose further
    /// levels hold white blocks.
    /// </summary>
    /// <param name="width">The width of level 0 in pixels</param>
    /// <param name="height">The height of level 0 in pixels</param>
    /// <param name="dxgiFormat">The DXGI format</param>
    /// <param name="levelCount">The number of levels the file holds; the header flags name them when it is above 1</param>
    /// <returns>The file</returns>
    public static byte[] Build(int width, int height, int dxgiFormat, int levelCount)
    {
        var file = new List<byte>(HeadersSize);
        var header = new byte[HeadersSize];

        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(MagicPosition), 0x20534444);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(HeaderSizePosition), 124);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(FlagsPosition), levelCount > 1 ? FlagsWithLevels : FlagsWithoutLevels);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(HeightPosition), height);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(WidthPosition), width);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(LevelCountPosition), levelCount);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(PixelFormatSizePosition), 32);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(PixelFormatFlagsPosition), 4);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(FourCcPosition), FourCcDx10);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(DxgiFormatPosition), dxgiFormat);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(DimensionPosition), 3);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(ArraySizePosition), 1);

        file.AddRange(header);

        var white = SolidBlock(255, 255, 255, 255);
        var levelWidth = width;
        var levelHeight = height;

        for (var level = 0; level < levelCount; level++)
        {
            var blocks = BlocksOf(levelWidth) * BlocksOf(levelHeight);

            for (var block = 0; block < blocks; block++)
            {
                var color = BlockColor(block);

                file.AddRange(level == 0 ? SolidBlock(color[0], color[1], color[2], color[3]) : white);
            }

            levelWidth = Math.Max(1, levelWidth >> 1);
            levelHeight = Math.Max(1, levelHeight >> 1);
        }

        return [.. file];
    }

    /// <summary>
    /// Builds a square DDS file of one level whose blocks are random blocks of mode 6, so that the pixels hardly compress.
    /// </summary>
    /// <param name="side">The side in pixels, a multiple of 4</param>
    /// <returns>The file</returns>
    public static byte[] BuildNoise(int side)
    {
        var file = Build(side, side, DxgiBc7Srgb, 1);
        var random = new Random(side);

        random.NextBytes(file.AsSpan(HeadersSize));

        for (var position = HeadersSize; position < file.Length; position += BlockBytes)
        {
            file[position] = (byte)(0x40 | (file[position] & 0x80));
        }

        return file;
    }

    /// <summary>
    /// Gets the pixels a reader must return for a file of <see cref="Build(int, int, int, int)" />, cropped to the size.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <returns>The RGBA bytes, row by row</returns>
    public static byte[] ExpectedRgba(int width, int height)
    {
        var rgba = new byte[width * height * 4];
        var blocksPerRow = BlocksOf(width);

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var color = BlockColor(row / BlockSide * blocksPerRow + column / BlockSide);

                color.CopyTo(rgba, (row * width + column) * 4);
            }
        }

        return rgba;
    }

    /// <summary>
    /// Gets a copy of a file with a 32-bit value replaced.
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="position">The position of the value</param>
    /// <param name="value">The new value</param>
    /// <returns>The changed copy</returns>
    public static byte[] Patch(byte[] file, int position, int value)
    {
        var copy = (byte[])file.Clone();

        BinaryPrimitives.WriteInt32LittleEndian(copy.AsSpan(position), value);

        return copy;
    }

    /// <summary>
    /// Gets a copy of a file that is shorter or longer.
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="delta">The change of the length in bytes</param>
    /// <returns>The resized copy; added bytes are zero</returns>
    public static byte[] Resize(byte[] file, int delta)
    {
        var copy = (byte[])file.Clone();

        Array.Resize(ref copy, file.Length + delta);

        return copy;
    }

    /// <summary>
    /// Gets the number of blocks that cover a side.
    /// </summary>
    /// <param name="pixels">The side in pixels</param>
    /// <returns>The block count</returns>
    private static int BlocksOf(int pixels)
    {
        return (pixels + BlockSide - 1) / BlockSide;
    }

    /// <summary>
    /// Writes the low bits of a value, least significant bit first.
    /// </summary>
    /// <param name="buffer">The buffer</param>
    /// <param name="position">The bit position, advanced by the count</param>
    /// <param name="value">The value</param>
    /// <param name="count">The number of bits</param>
    private static void Put(byte[] buffer, ref int position, int value, int count)
    {
        for (var bit = 0; bit < count; bit++)
        {
            buffer[position >> 3] |= (byte)(((value >> bit) & 1) << (position & 7));
            position++;
        }
    }

    #endregion // Methods
}