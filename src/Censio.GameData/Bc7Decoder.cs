using System.Numerics;

namespace Censio.GameData;

/// <summary>
/// Decodes BC7 compressed texture blocks into RGBA pixels.
/// </summary>
public static class Bc7Decoder
{
    #region Constants

    /// <summary>
    /// The size of one BC7 block in bytes.
    /// </summary>
    public const int BlockBytes = 16;

    /// <summary>
    /// The number of pixels of one block.
    /// </summary>
    private const int PixelCount = 16;

    /// <summary>
    /// The size of the decoded pixels of one block in bytes.
    /// </summary>
    private const int PixelBytes = PixelCount * 4;

    /// <summary>
    /// The number of channels of a pixel.
    /// </summary>
    private const int Channels = 4;

    /// <summary>
    /// The index of the alpha channel.
    /// </summary>
    private const int AlphaChannel = 3;

    /// <summary>
    /// The subset digit of each pixel for the 64 partitions of two subsets, 16 pixels per partition.
    /// </summary>
    private const string TwoSubsetPartitions = "0011001100110011000100010001000101110111011101110001001100110111"
                                               + "0000000100010011001101110111111100010011011111110000000100110111"
                                               + "0000000000010011001101111111111100000001011111110000000000010111"
                                               + "0001011111111111000000001111111100001111111111110000000000001111"
                                               + "0000100011101111011100010000000000000000100011100111001100010000"
                                               + "0011000100000000000010001100111000000000100011000111001100110001"
                                               + "0011000100010000000010001000110001100110011001100011011001101100"
                                               + "0001011111101000000011111111000001110001100011100011100110011100"
                                               + "0101010101010101000011110000111101011010010110100011001111001100"
                                               + "0011110000111100010101011010101001101001011010010101101010100101"
                                               + "0111001111001110000100111100100000110010010011000011101111011100"
                                               + "0110100110010110001111001100001101100110100110010000011001100000"
                                               + "0100111001000000001001110010000000000010011100100000010011100100"
                                               + "0110110010010011001101101100100101100011100111000011100111000110"
                                               + "0110110011001001011000110011100101111110100000010001100011100111"
                                               + "0000111100110011001100111111000000100010111011100100010001110111";

    /// <summary>
    /// The subset digit of each pixel for the 64 partitions of three subsets, 16 pixels per partition.
    /// </summary>
    private const string ThreeSubsetPartitions = "0011001102212222000100112211222100002001221122110222002200110111"
                                                 + "0000000011221122001100110022002200220022111111110011001122112211"
                                                 + "0000000011112222000011111111222200001111222222220012001200120012"
                                                 + "0112011201120112012201220122012200110112112212220011200122002220"
                                                 + "0001001101121122011100112001220000001122112211220022002200221111"
                                                 + "0111011102220222000100012221222100000011012201220000110022102210"
                                                 + "0122012200110000001200121122222201101221122101100000011012211221"
                                                 + "0022110211020022011001102002222200110122012200110000200022112221"
                                                 + "0000000211221222022200220012001100110012002202220120012001200120"
                                                 + "0000111122220000012012012012012001202012120101200011220011220011"
                                                 + "0011112222000011010101012222222200000000212121210022112200221122"
                                                 + "0022001100220011022012210220122101012222222201010000212121212121"
                                                 + "0101010101012222022201110222011100021112000211120000211221122112"
                                                 + "0222011101110222000211121112000201100110011022220000000021122112"
                                                 + "0110011022222222002200110011002200221122112200220000000000002112"
                                                 + "0002000100020001022212220222122201012222222222220111201122012220";

    /// <summary>
    /// The anchor pixel, as a hexadecimal digit, of the second subset for each partition of two subsets.
    /// </summary>
    private const string TwoSubsetAnchors = "FFFFFFFFFFFFFFFFF282288F28228822"
                                            + "FF6828FF28222FF66268FF22FFFFF22F";

    /// <summary>
    /// The anchor pixel, as a hexadecimal digit, of the second subset for each partition of three subsets.
    /// </summary>
    private const string ThreeSubsetSecondAnchors = "33FF83FF88666533338F336A588685FF"
                                                    + "8F356A8FF3F5FFFF3F55585A5A8DFC33";

    /// <summary>
    /// The anchor pixel, as a hexadecimal digit, of the third subset for each partition of three subsets.
    /// </summary>
    private const string ThreeSubsetThirdAnchors = "F883FF38FFFFFFF8F8F3F8F83F6AFFA8"
                                                   + "F3FAA89A6F8F3668F3FFFFFFFFFF3FF8";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The number of subsets of each mode.
    /// </summary>
    private static readonly int[] _subsetCounts = [3, 2, 3, 2, 1, 1, 1, 2];

    /// <summary>
    /// The number of partition bits of each mode.
    /// </summary>
    private static readonly int[] _partitionBits = [4, 6, 6, 6, 0, 0, 0, 6];

    /// <summary>
    /// The number of rotation bits of each mode.
    /// </summary>
    private static readonly int[] _rotationBits = [0, 0, 0, 0, 2, 2, 0, 0];

    /// <summary>
    /// The number of index selection bits of each mode.
    /// </summary>
    private static readonly int[] _indexSelectionBits = [0, 0, 0, 0, 1, 0, 0, 0];

    /// <summary>
    /// The bits of a colour endpoint channel of each mode, without the shared bit.
    /// </summary>
    private static readonly int[] _colorBits = [4, 6, 5, 7, 5, 7, 7, 5];

    /// <summary>
    /// The bits of an alpha endpoint channel of each mode, without the shared bit.
    /// </summary>
    private static readonly int[] _alphaBits = [0, 0, 0, 0, 6, 8, 7, 5];

    /// <summary>
    /// The kind of shared bits of each mode: 0 none, 1 one per endpoint, 2 one per subset.
    /// </summary>
    private static readonly int[] _sharedBitKinds = [1, 2, 0, 1, 0, 0, 1, 1];

    /// <summary>
    /// The bits of an index of the first index set of each mode.
    /// </summary>
    private static readonly int[] _indexBits = [3, 3, 2, 2, 2, 2, 4, 2];

    /// <summary>
    /// The bits of an index of the second index set of each mode, 0 when there is none.
    /// </summary>
    private static readonly int[] _secondIndexBits = [0, 0, 0, 0, 3, 2, 0, 0];

    /// <summary>
    /// The interpolation weights of a two bit index.
    /// </summary>
    private static readonly int[] _weights2 = [0, 21, 43, 64];

    /// <summary>
    /// The interpolation weights of a three bit index.
    /// </summary>
    private static readonly int[] _weights3 = [0, 9, 18, 27, 37, 46, 55, 64];

    /// <summary>
    /// The interpolation weights of a four bit index.
    /// </summary>
    private static readonly int[] _weights4 = [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Decodes one BC7 block into 4 by 4 RGBA pixels.
    /// </summary>
    /// <param name="block">The <see cref="BlockBytes" /> bytes of the block</param>
    /// <param name="rgba">The 64 bytes that receive the pixels, row by row, four bytes per pixel</param>
    public static void DecodeBlock(ReadOnlySpan<byte> block, Span<byte> rgba)
    {
        if (block[0] == 0)
        {
            rgba[..PixelBytes].Clear();

            return;
        }

        var mode = BitOperations.TrailingZeroCount((uint)block[0]);
        var position = mode + 1;
        var subsets = _subsetCounts[mode];
        var partition = ReadBits(block, ref position, _partitionBits[mode]);
        var rotation = ReadBits(block, ref position, _rotationBits[mode]);
        var indexSelection = ReadBits(block, ref position, _indexSelectionBits[mode]);

        Span<int> endpoints = stackalloc int[6 * Channels];

        ReadEndpoints(block, ref position, mode, endpoints);

        Span<byte> subsetOf = stackalloc byte[PixelCount];
        Span<int> firstIndices = stackalloc int[PixelCount];
        Span<int> secondIndices = stackalloc int[PixelCount];

        FillSubsets(subsets, partition, subsetOf);
        ReadIndices(block, ref position, mode, subsetOf, partition, firstIndices);
        ReadIndices(block, ref position, _secondIndexBits[mode], subsets, subsetOf, partition, secondIndices);

        var colorIndices = firstIndices;
        var alphaIndices = _secondIndexBits[mode] > 0 ? secondIndices : firstIndices;
        var colorWeightBits = _indexBits[mode];
        var alphaWeightBits = _secondIndexBits[mode] > 0 ? _secondIndexBits[mode] : colorWeightBits;

        if (indexSelection == 1)
        {
            var swapped = colorIndices;

            colorIndices = alphaIndices;
            alphaIndices = swapped;
            (colorWeightBits, alphaWeightBits) = (alphaWeightBits, colorWeightBits);
        }

        for (var pixel = 0; pixel < PixelCount; pixel++)
        {
            var low = subsetOf[pixel] * 2 * Channels;
            var high = low + Channels;
            var colorWeight = WeightOf(colorWeightBits, colorIndices[pixel]);
            var alphaWeight = WeightOf(alphaWeightBits, alphaIndices[pixel]);

            for (var channel = 0; channel < Channels; channel++)
            {
                var weight = channel == AlphaChannel ? alphaWeight : colorWeight;

                rgba[(pixel * Channels) + channel] = (byte)(((endpoints[low + channel] * (64 - weight)) + (endpoints[high + channel] * weight) + 32) >> 6);
            }

            Rotate(rgba.Slice(pixel * Channels, Channels), rotation);
        }
    }

    /// <summary>
    /// Reads the endpoints of a block and expands them to eight bits per channel.
    /// </summary>
    /// <param name="block">The block</param>
    /// <param name="position">The bit position, advanced past the endpoints and shared bits</param>
    /// <param name="mode">The mode of the block</param>
    /// <param name="endpoints">Receives the endpoints, four channels each</param>
    private static void ReadEndpoints(ReadOnlySpan<byte> block, ref int position, int mode, Span<int> endpoints)
    {
        var count = _subsetCounts[mode] * 2;

        for (var channel = 0; channel < Channels; channel++)
        {
            var bits = channel == AlphaChannel ? _alphaBits[mode] : _colorBits[mode];

            for (var endpoint = 0; endpoint < count; endpoint++)
            {
                endpoints[(endpoint * Channels) + channel] = ReadBits(block, ref position, bits);
            }
        }

        var kind = _sharedBitKinds[mode];
        Span<int> shared = stackalloc int[count];

        for (var endpoint = 0; endpoint < count; endpoint++)
        {
            if (kind == 1 || (kind == 2 && (endpoint & 1) == 0))
            {
                shared[endpoint] = ReadBits(block, ref position, 1);
            }
            else if (kind == 2)
            {
                shared[endpoint] = shared[endpoint - 1];
            }
        }

        for (var endpoint = 0; endpoint < count; endpoint++)
        {
            for (var channel = 0; channel < Channels; channel++)
            {
                var bits = channel == AlphaChannel ? _alphaBits[mode] : _colorBits[mode];
                var index = (endpoint * Channels) + channel;

                endpoints[index] = bits == 0 ? 255 : Expand(endpoints[index], bits, kind, shared[endpoint]);
            }
        }
    }

    /// <summary>
    /// Expands an endpoint channel to eight bits.
    /// </summary>
    /// <param name="value">The stored bits</param>
    /// <param name="bits">The number of stored bits</param>
    /// <param name="kind">The kind of shared bits of the mode</param>
    /// <param name="sharedBit">The shared bit of the endpoint</param>
    /// <returns>The eight bit value</returns>
    private static int Expand(int value, int bits, int kind, int sharedBit)
    {
        if (kind != 0)
        {
            value = (value << 1) | sharedBit;
            bits++;
        }

        return (value << (8 - bits)) | (value >> ((2 * bits) - 8));
    }

    /// <summary>
    /// Fills the subset of every pixel.
    /// </summary>
    /// <param name="subsets">The number of subsets</param>
    /// <param name="partition">The partition</param>
    /// <param name="subsetOf">Receives the subset of each pixel</param>
    private static void FillSubsets(int subsets, int partition, Span<byte> subsetOf)
    {
        if (subsets == 1)
        {
            subsetOf.Clear();
        }
        else
        {
            var table = subsets == 2 ? TwoSubsetPartitions : ThreeSubsetPartitions;

            for (var pixel = 0; pixel < PixelCount; pixel++)
            {
                subsetOf[pixel] = (byte)(table[(partition * PixelCount) + pixel] - '0');
            }
        }
    }

    /// <summary>
    /// Reads the indices of the first index set of a mode.
    /// </summary>
    /// <param name="block">The block</param>
    /// <param name="position">The bit position</param>
    /// <param name="mode">The mode of the block</param>
    /// <param name="subsetOf">The subset of each pixel</param>
    /// <param name="partition">The partition</param>
    /// <param name="indices">Receives the indices</param>
    private static void ReadIndices(ReadOnlySpan<byte> block, ref int position, int mode, ReadOnlySpan<byte> subsetOf, int partition, Span<int> indices)
    {
        ReadIndices(block, ref position, _indexBits[mode], _subsetCounts[mode], subsetOf, partition, indices);
    }

    /// <summary>
    /// Reads one set of indices; the anchor pixel of each subset has one bit less.
    /// </summary>
    /// <param name="block">The block</param>
    /// <param name="position">The bit position</param>
    /// <param name="bits">The bits of an index, 0 when there is no such set</param>
    /// <param name="subsets">The number of subsets</param>
    /// <param name="subsetOf">The subset of each pixel</param>
    /// <param name="partition">The partition</param>
    /// <param name="indices">Receives the indices</param>
    private static void ReadIndices(ReadOnlySpan<byte> block, ref int position, int bits, int subsets, ReadOnlySpan<byte> subsetOf, int partition, Span<int> indices)
    {
        if (bits == 0)
        {
            return;
        }

        for (var pixel = 0; pixel < PixelCount; pixel++)
        {
            var isAnchor = pixel == AnchorOf(subsetOf[pixel], subsets, partition);

            indices[pixel] = ReadBits(block, ref position, isAnchor ? bits - 1 : bits);
        }
    }

    /// <summary>
    /// Gets the anchor pixel of a subset.
    /// </summary>
    /// <param name="subset">The subset</param>
    /// <param name="subsets">The number of subsets</param>
    /// <param name="partition">The partition</param>
    /// <returns>The pixel whose index lacks its top bit</returns>
    private static int AnchorOf(int subset, int subsets, int partition)
    {
        if (subset == 0)
        {
            return 0;
        }

        if (subset == 1)
        {
            return subsets == 2 ? HexValue(TwoSubsetAnchors[partition]) : HexValue(ThreeSubsetSecondAnchors[partition]);
        }

        return HexValue(ThreeSubsetThirdAnchors[partition]);
    }

    /// <summary>
    /// Gets the value of a hexadecimal digit.
    /// </summary>
    /// <param name="digit">The digit, 0 to 9 or A to F</param>
    /// <returns>The value from 0 to 15</returns>
    private static int HexValue(char digit)
    {
        return digit <= '9' ? digit - '0' : digit - 'A' + 10;
    }

    /// <summary>
    /// Gets the interpolation weight of an index.
    /// </summary>
    /// <param name="bits">The bits of the index</param>
    /// <param name="index">The index</param>
    /// <returns>The weight from 0 to 64</returns>
    private static int WeightOf(int bits, int index)
    {
        return bits switch
               {
                   2 => _weights2[index],
                   3 => _weights3[index],
                   _ => _weights4[index]
               };
    }

    /// <summary>
    /// Swaps the alpha channel of a pixel with the channel the rotation names.
    /// </summary>
    /// <param name="pixel">The four channels of the pixel</param>
    /// <param name="rotation">The rotation, 0 for none, 1 red, 2 green, 3 blue</param>
    private static void Rotate(Span<byte> pixel, int rotation)
    {
        if (rotation != 0)
        {
            (pixel[rotation - 1], pixel[AlphaChannel]) = (pixel[AlphaChannel], pixel[rotation - 1]);
        }
    }

    /// <summary>
    /// Reads up to eight bits, least significant bit first.
    /// </summary>
    /// <param name="block">The block</param>
    /// <param name="position">The bit position, advanced by <paramref name="count" /></param>
    /// <param name="count">The number of bits</param>
    /// <returns>The bits as a number</returns>
    private static int ReadBits(ReadOnlySpan<byte> block, ref int position, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var index = position >> 3;
        var window = block[index] | (index + 1 < BlockBytes ? block[index + 1] << 8 : 0);
        var value = (window >> (position & 7)) & ((1 << count) - 1);

        position += count;

        return value;
    }

    #endregion // Methods
}