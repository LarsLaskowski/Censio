namespace Censio.GameData;

/// <summary>
/// Encodes RGBA pixels as a PNG file.
/// </summary>
public static class PngWriter
{
    #region Methods

    /// <summary>
    /// Encodes an image as PNG.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <param name="rgba">The pixels, row by row, four bytes per pixel</param>
    /// <returns>The bytes of the PNG file</returns>
    public static byte[] Write(int width, int height, ReadOnlySpan<byte> rgba)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}