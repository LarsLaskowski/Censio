namespace Censio.Server;

/// <summary>
/// Checks the header of PNG data.
/// </summary>
public static class PngHeader
{
    #region Constants

    /// <summary>
    /// The length of the checked header: the signature and the length and type of the first chunk.
    /// </summary>
    private const int HeaderLength = 16;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The signature, then the length 13 and the type IHDR of the first chunk.
    /// </summary>
    private static readonly byte[] _header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Determines whether data starts with the PNG signature.
    /// </summary>
    /// <param name="data">The data</param>
    /// <returns><see langword="true"/> when the data is a PNG header</returns>
    public static bool IsPng(ReadOnlySpan<byte> data)
    {
        return data.Length >= HeaderLength && data[..HeaderLength].SequenceEqual(_header);
    }

    #endregion // Methods
}