namespace Censio.Server;

/// <summary>
/// Reads an icon through a read function, one read at a time.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The semaphore is never waited on through its handle, so it holds nothing to dispose")]
public sealed class IconReader
{
    #region Fields

    /// <summary>
    /// The function that reads the icon bytes of a reference.
    /// </summary>
    private readonly Func<string, CancellationToken, Task<byte[]?>> _read;

    /// <summary>
    /// The gate that lets one read at a time call the read function.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

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

    /// <summary>
    /// Reads the icon of a reference.
    /// </summary>
    /// <param name="reference">The icon reference</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The icon bytes, or <see langword="null"/> when none is stored</returns>
    public async Task<byte[]?> ReadAsync(string reference, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await _read(reference, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    #endregion // Methods
}