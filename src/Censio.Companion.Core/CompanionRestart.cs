using System.ComponentModel;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// Starts the Companion's own executable again.
/// </summary>
public sealed class CompanionRestart
{
    #region Fields

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

    /// <summary>
    /// Starts the Companion's own executable with the switch of a restart.
    /// </summary>
    /// <returns><see langword="true" /> when the launch did not fail</returns>
    public bool TryStart()
    {
        if (string.IsNullOrEmpty(_exePath) || Path.IsPathFullyQualified(_exePath) == false)
        {
            CompanionLog.RestartNotStarted(_logger, nameof(ArgumentException));

            return false;
        }

        try
        {
            _launch(_exePath, CompanionCommandLine.RestartedSwitch);

            return true;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            CompanionLog.RestartNotStarted(_logger, exception.GetType().Name);

            return false;
        }
    }

    #endregion // Methods
}