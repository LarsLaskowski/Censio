using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Censio.Server;

/// <summary>
/// Registers the data-protection key ring in the configured data directory and checks it at start.
/// </summary>
public static partial class DataProtectionSetup
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
        services.AddLogging();
        services.AddDataProtection();
        services.AddOptions<KeyManagementOptions>()
                .Configure<IConfiguration, ILoggerFactory>((options, configuration, loggerFactory) =>
                                                           {
                                                               var dataDirectory = configuration[DataDirectoryKey];

                                                               if (dataDirectory is not null && Path.IsPathFullyQualified(dataDirectory))
                                                               {
                                                                   options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(KeyDirectory(dataDirectory)), loggerFactory);
                                                               }
                                                           });

        return services;
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
        var dataDirectory = configuration[DataDirectoryKey];

        if (dataDirectory is null)
        {
            return true;
        }

        if (Path.IsPathFullyQualified(dataDirectory))
        {
            return CreateKeyRing(services, logger, KeyDirectory(dataDirectory));
        }

        LogRelativeDataDirectory(logger, dataDirectory);

        return false;
    }

    /// <summary>
    /// Determines whether a cryptographic exception wraps a file system failure.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><c>true</c> when the inner exception is an I/O or access failure; otherwise, <c>false</c>.</returns>
    public static bool IsWrappedFileSystemFailure(CryptographicException exception)
    {
        return exception.InnerException is IOException or UnauthorizedAccessException;
    }

    /// <summary>
    /// Creates the key directory and the first key.
    /// </summary>
    /// <param name="services">The application services.</param>
    /// <param name="logger">The logger for the failure entries.</param>
    /// <param name="keyDirectory">The full path of the key directory.</param>
    /// <returns><c>true</c> when the key ring is usable; otherwise, <c>false</c>.</returns>
    private static bool CreateKeyRing(IServiceProvider services, ILogger logger, string keyDirectory)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(keyDirectory);
            }
            else
            {
                Directory.CreateDirectory(keyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            services.GetRequiredService<IDataProtectionProvider>().CreateProtector(KeyRingCheckPurpose).Protect([0]);
        }
        catch (IOException exception)
        {
            LogKeyRingNotWritable(logger, exception, keyDirectory);

            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            LogKeyRingNotWritable(logger, exception, keyDirectory);

            return false;
        }
        catch (CryptographicException exception) when (IsWrappedFileSystemFailure(exception))
        {
            LogKeyRingNotWritable(logger, exception, keyDirectory);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the key directory below the data directory.
    /// </summary>
    /// <param name="dataDirectory">The configured data directory.</param>
    /// <returns>The full path of the key directory.</returns>
    private static string KeyDirectory(string dataDirectory)
    {
        return Path.Combine(Path.GetFullPath(dataDirectory), KeyDirectoryName);
    }

    /// <summary>
    /// Logs that the data directory is not an absolute path.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="dataDirectory">The configured value.</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The data directory (Censio:DataDirectory) must be an absolute path, not '{DataDirectory}'; the server stops.")]
    private static partial void LogRelativeDataDirectory(ILogger logger, string dataDirectory);

    /// <summary>
    /// Logs that the key ring cannot be stored.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="keyDirectory">The key directory.</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The data-protection key ring cannot be stored in '{KeyDirectory}'; the server stops. Make the data directory writable for the user the server runs as.")]
    private static partial void LogKeyRingNotWritable(ILogger logger, Exception exception, string keyDirectory);

    #endregion // Methods
}