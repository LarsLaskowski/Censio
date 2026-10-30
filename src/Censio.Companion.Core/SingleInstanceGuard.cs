#pragma warning disable RH2003 // Skeleton body until the implementation task

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
        throw new NotImplementedException(_name);
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    #endregion // IDisposable
}