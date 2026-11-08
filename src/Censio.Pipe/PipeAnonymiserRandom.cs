using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Censio.Pipe;

/// <summary>
/// The random source of the anonymiser, based on the system's cryptographic generator.
/// </summary>
public sealed class PipeAnonymiserRandom : IPipeAnonymiserRandom
{
    #region Constants

    /// <summary>
    /// The number of random bits of a draw: the precision of a double.
    /// </summary>
    private const int DrawBits = 53;

    #endregion // Constants

    #region IPipeAnonymiserRandom

    /// <inheritdoc />
    public double NextDouble()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];

        RandomNumberGenerator.Fill(bytes);

        var bits = BinaryPrimitives.ReadUInt64LittleEndian(bytes) >> (64 - DrawBits);

        return bits / (double)(1UL << DrawBits);
    }

    #endregion // IPipeAnonymiserRandom
}