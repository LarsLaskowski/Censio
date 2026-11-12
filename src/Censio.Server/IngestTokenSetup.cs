using System.Buffers.Text;
using System.Security.Cryptography;

namespace Censio.Server;

/// <summary>
/// Creates the ingest access of the server from its configuration and the token file in the data directory.
/// </summary>
public static partial class IngestTokenSetup
{
    #region Constants

    /// <summary>
    /// The configuration key that switches the ingest token requirement.
    /// </summary>
    public const string RequireTokenKey = "Censio:Ingest:RequireToken";

    /// <summary>
    /// The name of the token file in the data directory.
    /// </summary>
    public const string TokenFileName = "ingest-token";

    /// <summary>
    /// The maximum length of a token in bytes.
    /// </summary>
    public const int MaxTokenLength = 1024;

    /// <summary>
    /// The name of the temporary file the token is written to before it is moved into place.
    /// </summary>
    private const string TemporaryFileName = "ingest-token.tmp";

    /// <summary>
    /// The maximum size of the token file in bytes.
    /// </summary>
    private const int MaxFileBytes = 65536;

    /// <summary>
    /// The number of random bytes of a generated token.
    /// </summary>
    private const int TokenBytes = 32;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Creates the ingest access, reading or creating the token file, and reports a failure to the log.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The access, or <c>null</c> when the server must stop.</returns>
    public static IngestAccess? TryCreateAccess(IConfiguration configuration, ILogger logger)
    {
        var requireToken = configuration[RequireTokenKey];

        if (requireToken is not null
            && string.Equals(requireToken, "true", StringComparison.OrdinalIgnoreCase) == false
            && string.Equals(requireToken, "false", StringComparison.OrdinalIgnoreCase) == false)
        {
            LogInvalidSwitch(logger);

            return null;
        }

        if (string.Equals(requireToken, "false", StringComparison.OrdinalIgnoreCase))
        {
            LogSwitchedOff(logger);

            return IngestAccess.CreateOpen();
        }

        var dataDirectory = configuration[DataProtectionSetup.DataDirectoryKey];

        if (dataDirectory is null)
        {
            LogNoDataDirectory(logger);

            return IngestAccess.CreateRefusingAll();
        }

        var directory = Path.GetFullPath(dataDirectory);
        var tokenFile = Path.Combine(directory, TokenFileName);

        if (File.Exists(tokenFile))
        {
            return ReadToken(logger, tokenFile);
        }

        return CreateToken(logger, directory, tokenFile);
    }

    /// <summary>
    /// Generates a new token.
    /// </summary>
    /// <returns>The token as Base64Url text without padding.</returns>
    public static string GenerateToken()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
    }

    /// <summary>
    /// Reads the token of the existing token file.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="tokenFile">The full path of the token file.</param>
    /// <returns>The access, or <c>null</c> when the file cannot be read or holds no valid token.</returns>
    private static IngestAccess? ReadToken(ILogger logger, string tokenFile)
    {
        byte[] content;

        try
        {
            using var stream = new FileStream(tokenFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[MaxFileBytes + 1];
            var length = stream.ReadAtLeast(buffer, buffer.Length, false);

            content = buffer[..length];
        }
        catch (IOException exception)
        {
            LogTokenFileNotReadable(logger, exception, tokenFile);

            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            LogTokenFileNotReadable(logger, exception, tokenFile);

            return null;
        }

        var token = ParseToken(content);

        if (token is null)
        {
            LogTokenFileInvalid(logger, tokenFile);

            return null;
        }

        LogTokenRead(logger, tokenFile);

        return IngestAccess.CreateForToken(token);
    }

    /// <summary>
    /// Extracts the token of a file's content.
    /// </summary>
    /// <param name="content">The content of the file.</param>
    /// <returns>The token, or <c>null</c> when the content holds no valid token.</returns>
    private static string? ParseToken(byte[] content)
    {
        if (content.Length > MaxFileBytes)
        {
            return null;
        }

        var token = content.AsSpan().Trim(" \t\r\n"u8);

        if (token.IsEmpty || token.Length > MaxTokenLength)
        {
            return null;
        }

        foreach (var value in token)
        {
            if (value is < 0x21 or > 0x7E)
            {
                return null;
            }
        }

        return System.Text.Encoding.ASCII.GetString(token);
    }

    /// <summary>
    /// Generates a token and stores it in the token file through a temporary file.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="directory">The full path of the data directory.</param>
    /// <param name="tokenFile">The full path of the token file.</param>
    /// <returns>The access, or <c>null</c> when the file cannot be created.</returns>
    private static IngestAccess? CreateToken(ILogger logger, string directory, string tokenFile)
    {
        var token = GenerateToken();
        var temporaryFile = Path.Combine(directory, TemporaryFileName);

        try
        {
            File.Delete(temporaryFile);
            WriteTemporaryFile(temporaryFile, token);
            File.Move(temporaryFile, tokenFile, false);
        }
        catch (IOException exception)
        {
            return FailCreate(logger, exception, temporaryFile, tokenFile);
        }
        catch (UnauthorizedAccessException exception)
        {
            return FailCreate(logger, exception, temporaryFile, tokenFile);
        }

        LogTokenGenerated(logger, tokenFile);

        return IngestAccess.CreateForToken(token);
    }

    /// <summary>
    /// Writes the token into the temporary file, readable by its owner only where the platform allows it.
    /// </summary>
    /// <param name="temporaryFile">The full path of the temporary file.</param>
    /// <param name="token">The token.</param>
    private static void WriteTemporaryFile(string temporaryFile, string token)
    {
        var options = new FileStreamOptions
                      {
                          Mode = FileMode.Create,
                          Access = FileAccess.Write,
                          Share = FileShare.None
                      };

        if (OperatingSystem.IsWindows())
        {
            // The file takes the access rules of the data directory.
        }
        else
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using var stream = new FileStream(temporaryFile, options);

        stream.Write(System.Text.Encoding.ASCII.GetBytes(token));
        stream.Flush(true);
    }

    /// <summary>
    /// Deletes the temporary file and logs that the token file cannot be created.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="temporaryFile">The full path of the temporary file.</param>
    /// <param name="tokenFile">The full path of the token file.</param>
    /// <returns>Always <c>null</c>.</returns>
    private static IngestAccess? FailCreate(ILogger logger, Exception exception, string temporaryFile, string tokenFile)
    {
        TryDelete(temporaryFile);
        LogTokenFileNotCreatable(logger, exception, tokenFile);

        return null;
    }

    /// <summary>
    /// Deletes a file, ignoring a failure.
    /// </summary>
    /// <param name="file">The full path of the file.</param>
    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file stays; the start stops anyway and the next start deletes it before writing.
            _ = exception;
        }
    }

    /// <summary>
    /// Logs that the switch holds neither true nor false.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The setting Censio:Ingest:RequireToken must be true or false; the server stops.")]
    private static partial void LogInvalidSwitch(ILogger logger);

    /// <summary>
    /// Logs that the token is switched off.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The ingest token is switched off: anyone who reaches the server can send data.")]
    private static partial void LogSwitchedOff(ILogger logger);

    /// <summary>
    /// Logs that there is no data directory to hold a token.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "No ingest token without a data directory (Censio:DataDirectory); every Companion is refused.")]
    private static partial void LogNoDataDirectory(ILogger logger);

    /// <summary>
    /// Logs that the token file cannot be read.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="tokenFile">The token file.</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The ingest token file '{TokenFile}' cannot be read; the server stops.")]
    private static partial void LogTokenFileNotReadable(ILogger logger, Exception exception, string tokenFile);

    /// <summary>
    /// Logs that the token file holds no valid token.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="tokenFile">The token file.</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The ingest token file '{TokenFile}' holds no valid token (1 to 1,024 visible ASCII characters); the server stops.")]
    private static partial void LogTokenFileInvalid(ILogger logger, string tokenFile);

    /// <summary>
    /// Logs that the token file cannot be created.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="tokenFile">The token file.</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The ingest token file '{TokenFile}' cannot be created; the server stops. Make the data directory writable for the user the server runs as.")]
    private static partial void LogTokenFileNotCreatable(ILogger logger, Exception exception, string tokenFile);

    /// <summary>
    /// Logs that the token was read from the file.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="tokenFile">The token file.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "The ingest token is read from '{TokenFile}'.")]
    private static partial void LogTokenRead(ILogger logger, string tokenFile);

    /// <summary>
    /// Logs that a token was generated and stored.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="tokenFile">The token file.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "An ingest token was generated and stored in '{TokenFile}'.")]
    private static partial void LogTokenGenerated(ILogger logger, string tokenFile);

    #endregion // Methods
}