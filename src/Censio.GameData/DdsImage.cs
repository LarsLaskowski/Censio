namespace Censio.GameData;

/// <summary>
/// Holds a decoded texture as RGBA pixels.
/// </summary>
public sealed class DdsImage
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsImage" /> class.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <param name="rgba">The pixels, row by row, four bytes per pixel</param>
    public DdsImage(int width, int height, byte[] rgba)
    {
        Width = width;
        Height = height;
        Rgba = rgba;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    /// Gets the width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the pixels, row by row, four bytes per pixel.
    /// </summary>
    public ReadOnlyMemory<byte> Rgba { get; }

    #endregion // Properties
}