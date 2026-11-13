namespace Censio.Server;

/// <summary>
/// Reads an icon through a read function, one read at a time.
/// </summary>
public class IconReader
{
    #region Fields

#pragma warning disable S4487
    // Skeleton: removed with the implementation
    /// <summary>
    /// The function that reads the icon bytes of a reference.
    /// </summary>
    private readonly Func<string, CancellationToken, Task<byte[]?>> _read;
#pragma warning restore S4487

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="IconReader"/> class.
    /// </summary>
    /// <param name="read">The function that reads the icon bytes of a reference</param>
    public IconReader(Func<string, CancellationToken, Task<byte[]?>> read)
    {
        _read = read;
    }

    #endregion // Constructors

    #region Methods

#pragma warning disable RH2003, S2325
    // Skeleton: removed with the implementation
    /// <summary>
    /// Reads the icon of a reference.
    /// </summary>
    /// <param name="reference">The icon reference</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The icon bytes, or <see langword="null"/> when none is stored</returns>
    public Task<byte[]?> ReadAsync(string reference, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#pragma warning restore RH2003, S2325

    #endregion // Methods
}