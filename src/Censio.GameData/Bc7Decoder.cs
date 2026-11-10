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

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Decodes one BC7 block into 4 by 4 RGBA pixels.
    /// </summary>
    /// <param name="block">The <see cref="BlockBytes" /> bytes of the block</param>
    /// <param name="rgba">The 64 bytes that receive the pixels, row by row, four bytes per pixel</param>
    public static void DecodeBlock(ReadOnlySpan<byte> block, Span<byte> rgba)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}