namespace Censio.Companion.Core;

/// <summary>
/// Allows only one running instance per named mutex.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    #region Constants

    /// <summary>
    /// The default name of the mutex.
    /// </summary>
    public const string DefaultName = "Local\\Censio.Companion.SingleInstance";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The name of the mutex
    /// </summary>
    private readonly string _name;

    /// <summary>
    /// The mutex, once <see cref="TryAcquire" /> has run
    /// </summary>
    private Mutex? _mutex;

    /// <summary>
    /// Whether this guard created the mutex and therefore owns it
    /// </summary>
    private bool _isOwner;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleInstanceGuard" /> class.
    /// </summary>
    /// <param name="name">The name of the mutex</param>
    public SingleInstanceGuard(string name)
    {
        _name = name;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Tries to become the only running instance.
    /// </summary>
    /// <returns><see langword="true" /> when this guard created the mutex; otherwise <see langword="false" /></returns>
    public bool TryAcquire()
    {
        _mutex = new Mutex(true, _name, out var createdNew);
        _isOwner = createdNew;

        return createdNew;
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isOwner)
        {
            _mutex!.ReleaseMutex();
            _isOwner = false;
        }

        _mutex?.Dispose();
        _mutex = null;
    }

    #endregion // IDisposable
}