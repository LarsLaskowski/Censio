using System.Security;
using System.Text.Json;

namespace Censio.Companion.Core;

/// <summary>
/// Reads the settings file of the Companion.
/// </summary>
public static class CompanionSettings
{
    #region Constants

    /// <summary>
    /// The maximum length of the settings file in bytes.
    /// </summary>
    public const int MaxFileLength = 1048576;

    /// <summary>
    /// The key of the game folder in the settings file.
    /// </summary>
    public const string GameFolderKey = "gameFolder";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the settings file.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <returns>The result</returns>
    public static CompanionSettingsResult Read(string path)
    {
        if (Path.IsPathFullyQualified(path) == false)
        {
            return CompanionSettingsResult.NoGameFolder();
        }

        byte[] bytes;

        try
        {
            bytes = ReadBytes(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return CompanionSettingsResult.NoGameFolder();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            return CompanionSettingsResult.FromProblem("cannot be read", exception);
        }

        if (bytes.Length > MaxFileLength)
        {
            return CompanionSettingsResult.FromProblem("larger than 1 MiB", null);
        }

        return Parse(bytes);
    }

    /// <summary>
    /// Reads at most <see cref="MaxFileLength" /> plus one byte of the file.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <returns>The bytes read</returns>
    private static byte[] ReadBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, false);

        var buffer = new byte[MaxFileLength + 1];
        var length = 0;

        while (length < buffer.Length)
        {
            var read = stream.Read(buffer, length, buffer.Length - length);

            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return buffer.AsSpan(0, length).ToArray();
    }

    /// <summary>
    /// Parses the bytes of a settings file.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <returns>The result</returns>
    private static CompanionSettingsResult Parse(byte[] bytes)
    {
        var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        var options = new JsonDocumentOptions
                      {
                          CommentHandling = JsonCommentHandling.Skip,
                          AllowTrailingCommas = true
                      };

        try
        {
            using var document = JsonDocument.Parse(bytes.AsMemory(start), options);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return CompanionSettingsResult.FromProblem("not a JSON object", null);
            }

            var matches = document.RootElement.EnumerateObject()
                                              .Where(property => string.Equals(property.Name, GameFolderKey, StringComparison.OrdinalIgnoreCase))
                                              .Select(property => property.Value)
                                              .ToList();

            if (matches.Count > 1)
            {
                return CompanionSettingsResult.FromProblem("gameFolder occurs more than once", null);
            }

            if (matches.Count == 0)
            {
                return CompanionSettingsResult.NoGameFolder();
            }

            if (matches[0].ValueKind != JsonValueKind.String)
            {
                return CompanionSettingsResult.FromProblem("gameFolder is not a text", null);
            }

            var folder = matches[0].GetString();

            return string.IsNullOrWhiteSpace(folder) ? CompanionSettingsResult.NoGameFolder() : CompanionSettingsResult.FromGameFolder(folder);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return CompanionSettingsResult.FromProblem("not valid JSON", exception);
        }
    }

    #endregion // Methods
}