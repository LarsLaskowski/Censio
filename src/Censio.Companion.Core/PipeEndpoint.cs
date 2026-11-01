using System.IO.Pipes;
using System.Security.Principal;

namespace Censio.Companion.Core;

/// <summary>
/// Describes the statistics pipe of the game.
/// </summary>
public static class PipeEndpoint
{
    #region Constants

    /// <summary>
    /// The server name of the local machine.
    /// </summary>
    public const string ServerName = ".";

    /// <summary>
    /// The name of the pipe.
    /// </summary>
    public const string PipeName = "anno117";

    /// <summary>
    /// The direction in which the pipe is opened.
    /// </summary>
    public const PipeDirection Direction = PipeDirection.In;

    /// <summary>
    /// The options with which the pipe is opened.
    /// </summary>
    public const PipeOptions Options = PipeOptions.Asynchronous;

    /// <summary>
    /// The impersonation level the server may use.
    /// </summary>
    public const TokenImpersonationLevel ImpersonationLevel = TokenImpersonationLevel.Identification;

    /// <summary>
    /// The full path of the pipe in the pipe directory.
    /// </summary>
    public const string PipePath = @"\\.\pipe\anno117";

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the timeout of one connect attempt.
    /// </summary>
    public static TimeSpan AttemptTimeout => TimeSpan.FromSeconds(2);

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Checks whether the pipe is among the entries of the pipe directory.
    /// </summary>
    /// <param name="entries">The entries of the pipe directory</param>
    /// <returns><c>true</c> if a whole entry equals <see cref="PipePath" />, ignoring case</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries" /> is <c>null</c></exception>
    public static bool IsListed(IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Any(entry => string.Equals(entry, PipePath, StringComparison.OrdinalIgnoreCase));
    }

    #endregion // Methods
}