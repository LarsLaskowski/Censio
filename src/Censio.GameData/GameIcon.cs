namespace Censio.GameData;

/// <summary>
/// Holds one icon as PNG.
/// </summary>
public sealed class GameIcon
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="GameIcon" /> class.
    /// </summary>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <param name="png">The bytes of the PNG file</param>
    public GameIcon(int width, int height, ReadOnlyMemory<byte> png)
    {
        Width = width;
        Height = height;
        Png = png;
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
    /// Gets the bytes of the PNG file.
    /// </summary>
    public ReadOnlyMemory<byte> Png { get; }

    #endregion // Properties
}