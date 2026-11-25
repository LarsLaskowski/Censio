using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Starts the Companion's own executable again.
/// </summary>
public sealed class CompanionRestart
{
    #region Fields

#pragma warning disable S4487 // Skeleton: removed with the implementation
    /// <summary>
    /// The delegate that starts a process from a path and an argument
    /// </summary>
    private readonly Action<string, string> _launch;

    /// <summary>
    /// The path of the executable, or <see langword="null" /> when it is unknown
    /// </summary>
    private readonly string? _exePath;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

#pragma warning restore S4487 // Skeleton: removed with the implementation

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionRestart" /> class.
    /// </summary>
    /// <param name="launch">The delegate that starts a process from a path and an argument</param>
    /// <param name="exePath">The path of the Companion's executable, or <see langword="null" /> when it is unknown</param>
    /// <param name="logger">The logger</param>
    public CompanionRestart(Action<string, string> launch, string? exePath, ILogger logger)
    {
        _launch = launch;
        _exePath = exePath;
        _logger = logger;
    }

    #endregion // Constructor

    #region Methods

#pragma warning disable RH2003, S2325 // Skeleton: removed with the implementation
    /// <summary>
    /// Starts the Companion again.
    /// </summary>
    /// <returns><see langword="true" /> when the process was started</returns>
    public bool TryStart()
    {
        throw new NotImplementedException();
    }

#pragma warning restore RH2003, S2325 // Skeleton: removed with the implementation

    #endregion // Methods
}