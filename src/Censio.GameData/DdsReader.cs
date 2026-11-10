namespace Censio.GameData;

/// <summary>
/// Reads DDS texture files into RGBA images.
/// </summary>
public static class DdsReader
{
    #region Methods

    /// <summary>
    /// Reads a DDS file.
    /// </summary>
    /// <param name="file">The content of the DDS file</param>
    /// <param name="maxSide">The longest side in pixels the image may have</param>
    /// <returns>The decoded image, or <see langword="null" /> when the file is not a supported texture within the bound</returns>
    public static DdsImage? Read(ReadOnlySpan<byte> file, int maxSide)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}