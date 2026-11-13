namespace Censio.Server;

/// <summary>
/// Checks the header of PNG data.
/// </summary>
public static class PngHeader
{
    #region Methods

    /// <summary>
    /// Determines whether data starts with the PNG signature.
    /// </summary>
    /// <param name="data">The data</param>
    /// <returns><see langword="true"/> when the data is a PNG header</returns>
#pragma warning disable S1172, RH2003 // Skeleton: removed with the implementation
    public static bool IsPng(ReadOnlySpan<byte> data)
    {
        throw new NotImplementedException();
    }
#pragma warning restore S1172, RH2003

    #endregion // Methods
}