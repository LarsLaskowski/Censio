using System.Security.Cryptography;

namespace Censio.Server;

/// <summary>
/// Registers the data-protection key ring in the configured data directory and checks it at start.
/// </summary>
public static class DataProtectionSetup
{
    #region Constants

    /// <summary>
    /// The configuration key of the data directory.
    /// </summary>
    public const string DataDirectoryKey = "Censio:DataDirectory";

    /// <summary>
    /// The name of the key directory below the data directory.
    /// </summary>
    public const string KeyDirectoryName = "data-protection-keys";

    /// <summary>
    /// The purpose of the protector the start check uses.
    /// </summary>
    public const string KeyRingCheckPurpose = "Censio.Server.KeyRingCheck";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Registers data protection with the key ring in the configured data directory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCensioDataProtection(this IServiceCollection services)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the key directory and the first key, reporting a failure to the log.
    /// </summary>
    /// <param name="services">The application services.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger for the failure entries.</param>
    /// <returns><c>true</c> when the application may start; otherwise, <c>false</c>.</returns>
    public static bool TryCreateKeyRing(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Determines whether a cryptographic exception wraps a file system failure.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><c>true</c> when the inner exception is an I/O or access failure; otherwise, <c>false</c>.</returns>
    public static bool IsWrappedFileSystemFailure(CryptographicException exception)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the key directory below the data directory.
    /// </summary>
    /// <param name="dataDirectory">The configured data directory.</param>
    /// <returns>The full path of the key directory.</returns>
    private static string KeyDirectory(string dataDirectory)
    {
        throw new NotImplementedException();
    }

    #endregion // Methods
}