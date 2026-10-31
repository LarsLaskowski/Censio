using System.Runtime.Versioning;
using System.Security.Cryptography;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Censio.Server.Tests;

/// <summary>
/// Tests for <see cref="DataProtectionSetup" />.
/// </summary>
[TestClass]
public sealed class DataProtectionSetupTests
{
    #region Fields

    /// <summary>
    /// The temporary paths of the running test, deleted afterwards.
    /// </summary>
    private readonly List<string> _temporaryPaths = [];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Deletes the temporary paths of the test.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var path in _temporaryPaths)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// An absolute data directory binds the key repository to its key subdirectory.
    /// </summary>
    [TestMethod]
    public void DataProtectionSetupAddWithAbsoluteDataDirectoryRegistersFileSystemRepository()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var services = new ServiceCollection();

        services.AddSingleton(CreateConfiguration(dataDirectory));
        services.AddCensioDataProtection();

        using var provider = services.BuildServiceProvider();

        // Act
        var repository = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;

        // Assert
        var fileSystemRepository = Assert.IsInstanceOfType<FileSystemXmlRepository>(repository, "The key repository should be a file system repository");

        Assert.AreEqual(Path.Combine(dataDirectory, DataProtectionSetup.KeyDirectoryName), fileSystemRepository.Directory.FullName, "The repository should lie in the key subdirectory of the data directory");
    }

    /// <summary>
    /// Without a data directory no key repository is registered.
    /// </summary>
    [TestMethod]
    public void DataProtectionSetupAddWithoutDataDirectoryRegistersNoRepository()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddSingleton(CreateConfiguration(null));
        services.AddCensioDataProtection();

        using var provider = services.BuildServiceProvider();

        // Act
        var repository = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;

        // Assert
        Assert.IsNull(repository, "ASP.NET Core's default should stay when no data directory is set");
    }

    /// <summary>
    /// The configuration registered after the setup call is the one the repository is bound from.
    /// </summary>
    [TestMethod]
    public void DataProtectionSetupAddReadsDataDirectoryWhenOptionsResolve()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var services = new ServiceCollection();

        services.AddCensioDataProtection();
        services.AddSingleton(CreateConfiguration(dataDirectory));

        using var provider = services.BuildServiceProvider();

        // Act
        var repository = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;

        // Assert
        var fileSystemRepository = Assert.IsInstanceOfType<FileSystemXmlRepository>(repository, "A configuration registered late should still bind the repository");

        Assert.AreEqual(Path.Combine(dataDirectory, DataProtectionSetup.KeyDirectoryName), fileSystemRepository.Directory.FullName, "The repository should point to the late configured data directory");
    }

    /// <summary>
    /// An absolute data directory gets its key directory and a first key file.
    /// </summary>
    [TestMethod]
    public void DataProtectionSetupTryCreateKeyRingWithAbsoluteDataDirectoryWritesKeyFile()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var configuration = CreateConfiguration(dataDirectory);
        using var provider = BuildProvider(configuration);
        var logger = new RecordingLogger();

        // Act
        var result = DataProtectionSetup.TryCreateKeyRing(provider, configuration, logger);

        // Assert
        var keyFiles = Directory.GetFiles(Path.Combine(dataDirectory, DataProtectionSetup.KeyDirectoryName), "key-*.xml");

        Assert.IsTrue(result, "The check should pass");
        Assert.IsNotEmpty(keyFiles, "The check should have created a key file");
        Assert.IsEmpty(logger.Entries, "The check should log nothing on success");
    }

    /// <summary>
    /// A key directory the check creates is owner-only on Linux.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [SupportedOSPlatform("linux")]
    public void DataProtectionSetupTryCreateKeyRingNewKeyDirectoryIsOwnerOnly()
    {
        // Arrange
        var dataDirectory = CreateTemporaryDirectory();
        var configuration = CreateConfiguration(dataDirectory);
        using var provider = BuildProvider(configuration);

        // Act
        DataProtectionSetup.TryCreateKeyRing(provider, configuration, new RecordingLogger());

        // Assert
        var mode = File.GetUnixFileMode(Path.Combine(dataDirectory, DataProtectionSetup.KeyDirectoryName));

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, mode, "The key directory should be readable and writable by its owner only");
    }

    /// <summary>
    /// Without a data directory the check passes silently.
    /// </summary>
    [TestMethod]
    public void DataProtectionSetupTryCreateKeyRingWithoutDataDirectoryReturnsTrueAndLogsNothing()
    {
        // Arrange
        var configuration = CreateConfiguration(null);
        using var provider = BuildProvider(configuration);
        var logger = new RecordingLogger();

        // Act
        var result = DataProtectionSetup.TryCreateKeyRing(provider, configuration, logger);

        // Assert
        Assert.IsTrue(result, "The check should pass without a data directory");
        Assert.IsEmpty(logger.Entries, "The check should log nothing without a data directory");
    }

    /// <summary>
    /// A data directory that is not an absolute path stops the start with one critical entry.
    /// </summary>
    /// <param name="value">The configured value</param>
    [TestMethod]
    [DataRow("relative/dir")]
    [DataRow("")]
    public void DataProtectionSetupTryCreateKeyRingWithRelativeDataDirectoryLogsCriticalAndReturnsFalse(string value)
    {
        // Arrange
        var configuration = CreateConfiguration(value);
        using var provider = BuildProvider(configuration);
        var logger = new RecordingLogger();
        var before = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory()).Order().ToList();

        // Act
        var result = DataProtectionSetup.TryCreateKeyRing(provider, configuration, logger);

        // Assert
        var after = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory()).Order().ToList();

        Assert.IsFalse(result, "The check should fail");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, "The entry should be critical");
        Assert.AreEqual($"The data directory (Censio:DataDirectory) must be an absolute path, not '{value}'; the server stops.", logger.Entries[0].Message, "The entry should carry the exact text");
        Assert.AreSequenceEqual(before, after, "The check should create nothing in the working directory");
    }

    /// <summary>
    /// A data directory below a regular file stops the start with one critical entry carrying the exception.
    /// </summary>
    [TestMethod]
    public void DataProtectionSetupTryCreateKeyRingWithUnwritableDataDirectoryLogsCriticalAndReturnsFalse()
    {
        // Arrange
        var blocker = Path.Combine(Path.GetTempPath(), "censio-test-" + Guid.NewGuid().ToString("N"));

        File.WriteAllText(blocker, "not a directory");
        _temporaryPaths.Add(blocker);

        var dataDirectory = Path.Combine(blocker, "data");
        var keyDirectory = Path.Combine(dataDirectory, DataProtectionSetup.KeyDirectoryName);
        var configuration = CreateConfiguration(dataDirectory);
        using var provider = BuildProvider(configuration);
        var logger = new RecordingLogger();

        // Act
        var result = DataProtectionSetup.TryCreateKeyRing(provider, configuration, logger);

        // Assert
        Assert.IsFalse(result, "The check should fail");
        Assert.HasCount(1, logger.Entries, "Exactly one entry should be logged");
        Assert.AreEqual(LogLevel.Critical, logger.Entries[0].Level, "The entry should be critical");
        Assert.AreEqual($"The data-protection key ring cannot be stored in '{keyDirectory}'; the server stops. Make the data directory writable for the user the server runs as.", logger.Entries[0].Message, "The entry should carry the exact text");
        Assert.IsNotNull(logger.Entries[0].Exception, "The entry should carry the exception");
    }

    /// <summary>
    /// A cryptographic exception wrapping an I/O or access failure is a file system failure.
    /// </summary>
    /// <param name="innerType">The type of the inner exception</param>
    [TestMethod]
    [DataRow(typeof(UnauthorizedAccessException))]
    [DataRow(typeof(IOException))]
    public void DataProtectionSetupIsWrappedFileSystemFailureWithFileSystemInnerReturnsTrue(Type innerType)
    {
        // Arrange
        var inner = (Exception)Activator.CreateInstance(innerType)!;
        var exception = new CryptographicException("wrapped", inner);

        // Act
        var result = DataProtectionSetup.IsWrappedFileSystemFailure(exception);

        // Assert
        Assert.IsTrue(result, "A wrapped file system failure should be recognized");
    }

    /// <summary>
    /// A cryptographic exception wrapping any other exception is not a file system failure.
    /// </summary>
    /// <param name="innerType">The type of the inner exception</param>
    [TestMethod]
    [DataRow(typeof(FormatException))]
    [DataRow(typeof(InvalidOperationException))]
    public void DataProtectionSetupIsWrappedFileSystemFailureWithOtherInnerReturnsFalse(Type innerType)
    {
        // Arrange
        var inner = (Exception)Activator.CreateInstance(innerType)!;
        var exception = new CryptographicException("wrapped", inner);

        // Act
        var result = DataProtectionSetup.IsWrappedFileSystemFailure(exception);

        // Assert
        Assert.IsFalse(result, "Any other inner exception should not be recognized");
    }

    /// <summary>
    /// A cryptographic exception without an inner exception is not a file system failure.
    /// </summary>
    [TestMethod]
    public void DataProtectionSetupIsWrappedFileSystemFailureWithoutInnerReturnsFalse()
    {
        // Arrange
        var exception = new CryptographicException("plain");

        // Act
        var result = DataProtectionSetup.IsWrappedFileSystemFailure(exception);

        // Assert
        Assert.IsFalse(result, "A cryptographic exception without inner exception should not be recognized");
    }

    /// <summary>
    /// Creates a configuration holding the data directory setting.
    /// </summary>
    /// <param name="dataDirectory">The value of the setting, or <c>null</c> for no setting</param>
    /// <returns>The configuration</returns>
    private static IConfiguration CreateConfiguration(string? dataDirectory)
    {
        var settings = new Dictionary<string, string?>();

        if (dataDirectory is not null)
        {
            settings[DataProtectionSetup.DataDirectoryKey] = dataDirectory;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    /// <summary>
    /// Builds a service provider with the setup registered and the given configuration.
    /// </summary>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service provider</returns>
    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddCensioDataProtection();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates an empty temporary directory that is deleted after the test.
    /// </summary>
    /// <returns>The full path of the directory</returns>
    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "censio-test-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        _temporaryPaths.Add(path);

        return path;
    }

    #endregion // Methods
}