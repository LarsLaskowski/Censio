using System.Globalization;
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

    /// <summary>
    /// The key of the mode in the settings file.
    /// </summary>
    public const string ModeKey = "mode";

    /// <summary>
    /// The key of the local server's port in the settings file.
    /// </summary>
    public const string LocalPortKey = "localPort";

    /// <summary>
    /// The key of the server address in the settings file.
    /// </summary>
    public const string ServerAddressKey = "serverAddress";

    /// <summary>
    /// The key of the server token in the settings file.
    /// </summary>
    public const string ServerTokenKey = "serverToken";

    /// <summary>
    /// The key of the age limit of the live buffer in hours in the settings file.
    /// </summary>
    public const string BufferHoursKey = "bufferHours";

    /// <summary>
    /// The key of the size limit of the live buffer in megabytes in the settings file.
    /// </summary>
    public const string BufferMegabytesKey = "bufferMegabytes";

    /// <summary>
    /// The port of the local server when the settings file names none.
    /// </summary>
    public const int DefaultLocalPort = 5000;

    /// <summary>
    /// The key of the language of the Companion's texts.
    /// </summary>
    public const string LanguageKey = "language";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the language of the settings file.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <returns>The language, or <see langword="null" /> when the file names none or an invalid one</returns>
    public static CompanionLanguage? ReadLanguage(string path)
    {
        if (Path.IsPathFullyQualified(path) == false)
        {
            return null;
        }

        byte[] bytes;

        try
        {
            bytes = ReadBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            return null;
        }

        if (bytes.Length > MaxFileLength)
        {
            return null;
        }

        try
        {
            using var document = ParseDocument(bytes);

            return document.RootElement.ValueKind == JsonValueKind.Object
                       ? ReadLanguage(document.RootElement)
                       : null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the local server setting of the settings file.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <returns>The setting</returns>
    public static LocalServerSetting ReadLocalServer(string path)
    {
        var off = new LocalServerSetting
                  {
                      Kind = LocalServerSettingKind.Off
                  };

        if (Path.IsPathFullyQualified(path) == false)
        {
            return off;
        }

        byte[] bytes;

        try
        {
            bytes = ReadBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            return off;
        }

        if (bytes.Length > MaxFileLength)
        {
            return off;
        }

        try
        {
            using var document = ParseDocument(bytes);

            return document.RootElement.ValueKind == JsonValueKind.Object
                       ? ReadLocalServer(document.RootElement)
                       : off;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return off;
        }
    }

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
    /// Parses the bytes of a settings file into a JSON document, past a byte order mark.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <returns>The document</returns>
    private static JsonDocument ParseDocument(byte[] bytes)
    {
        var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        var options = new JsonDocumentOptions
                      {
                          CommentHandling = JsonCommentHandling.Skip,
                          AllowTrailingCommas = true
                      };

        return JsonDocument.Parse(bytes.AsMemory(start), options);
    }

    /// <summary>
    /// Reads the language of the root object of a settings file.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <returns>The language, or <see langword="null" /> when the key is missing, written twice or not <c>en</c> or <c>de</c></returns>
    private static CompanionLanguage? ReadLanguage(JsonElement root)
    {
        var languages = FindProperties(root, LanguageKey);

        if (languages.Count != 1 || languages[0].ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var language = languages[0].GetString();

        if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
        {
            return CompanionLanguage.English;
        }

        return string.Equals(language, "de", StringComparison.OrdinalIgnoreCase)
                   ? CompanionLanguage.German
                   : null;
    }

    /// <summary>
    /// Reads the local server setting of the root object of a settings file.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <returns>The setting</returns>
    private static LocalServerSetting ReadLocalServer(JsonElement root)
    {
        var modes = FindProperties(root, ModeKey);

        if (modes.Count == 0)
        {
            return new LocalServerSetting
                   {
                       Kind = LocalServerSettingKind.Off
                   };
        }

        if (modes.Count > 1)
        {
            return Invalid("mode occurs more than once");
        }

        if (modes[0].ValueKind != JsonValueKind.String)
        {
            return Invalid("mode is not local or forward");
        }

        var mode = modes[0].GetString();

        if (string.IsNullOrWhiteSpace(mode))
        {
            return new LocalServerSetting
                   {
                       Kind = LocalServerSettingKind.Off
                   };
        }

        if (string.Equals(mode, "forward", StringComparison.OrdinalIgnoreCase))
        {
            return ReadForward(root);
        }

        if (string.Equals(mode, "local", StringComparison.OrdinalIgnoreCase) == false)
        {
            return Invalid("mode is not local or forward");
        }

        var ports = FindProperties(root, LocalPortKey);
        var port = DefaultLocalPort;

        if (ports.Count > 1)
        {
            return Invalid("localPort occurs more than once");
        }

        if (ports.Count == 1 && TryReadWholeNumber(ports[0], 1, 65535, out port) == false)
        {
            return Invalid("localPort is not a port from 1 to 65535");
        }

        var limits = ReadBufferLimits(root, out var problem);

        return limits is null
                   ? Invalid(problem ?? string.Empty)
                   : new LocalServerSetting
                     {
                         Kind = LocalServerSettingKind.Local,
                         Port = port,
                         BufferLimits = limits
                     };
    }

    /// <summary>
    /// Reads the forward setting of the root object of a settings file.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <returns>The setting</returns>
    private static LocalServerSetting ReadForward(JsonElement root)
    {
        var addresses = FindProperties(root, ServerAddressKey);

        if (addresses.Count == 0)
        {
            return Invalid("serverAddress is missing");
        }

        if (addresses.Count > 1)
        {
            return Invalid("serverAddress occurs more than once");
        }

        if (addresses[0].ValueKind != JsonValueKind.String
            || CompanionCommandLine.TryParseServerAddress(addresses[0].GetString() ?? string.Empty, out var address) == false)
        {
            return Invalid("serverAddress is not a ws:// or wss:// address");
        }

        var tokens = FindProperties(root, ServerTokenKey);

        if (tokens.Count > 1)
        {
            return Invalid("serverToken occurs more than once");
        }

        string? token = null;

        if (tokens.Count == 1)
        {
            token = tokens[0].ValueKind == JsonValueKind.String ? ReplayTokenFile.ReadToken(tokens[0].GetString() ?? string.Empty) : null;

            if (token is null)
            {
                return Invalid("serverToken is not a valid token");
            }
        }

        var limits = ReadBufferLimits(root, out var problem);

        return limits is null
                   ? Invalid(problem ?? string.Empty)
                   : new LocalServerSetting
                     {
                         Kind = LocalServerSettingKind.Forward,
                         Forward = new ForwardServerSetting(address, token),
                         BufferLimits = limits
                     };
    }

    /// <summary>
    /// Reads the limits of the live buffer from the root object of a settings file.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="problem">The problem when a key is invalid, otherwise <see langword="null" /></param>
    /// <returns>The limits, or <see langword="null" /> when a key is invalid</returns>
    private static LiveBufferLimits? ReadBufferLimits(JsonElement root, out string? problem)
    {
        problem = null;

        var hours = LiveBufferLimits.DefaultHours;
        var megabytes = LiveBufferLimits.DefaultMegabytes;

        if (TryReadBufferKey(root, BufferHoursKey, LiveBufferLimits.MaxHours, ref hours, out problem) == false
            || TryReadBufferKey(root, BufferMegabytesKey, LiveBufferLimits.MaxMegabytes, ref megabytes, out problem) == false)
        {
            return null;
        }

        return new LiveBufferLimits
               {
                   Hours = hours,
                   Megabytes = megabytes
               };
    }

    /// <summary>
    /// Reads one key of the live buffer limits, which stays as it is when the key is missing.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="key">The key</param>
    /// <param name="maximum">The largest value</param>
    /// <param name="number">The value</param>
    /// <param name="problem">The problem when the key is invalid, otherwise <see langword="null" /></param>
    /// <returns><see langword="true" /> when the key is missing or valid</returns>
    private static bool TryReadBufferKey(JsonElement root, string key, int maximum, ref int number, out string? problem)
    {
        var values = FindProperties(root, key);

        problem = null;

        if (values.Count > 1)
        {
            problem = $"{key} occurs more than once";

            return false;
        }

        if (values.Count == 1 && TryReadWholeNumber(values[0], 1, maximum, out number) == false)
        {
            problem = $"{key} is not a whole number from 1 to {maximum.ToString(CultureInfo.InvariantCulture)}";

            return false;
        }

        return true;
    }

    /// <summary>
    /// Finds the properties of an object whose name is a key, ignoring case.
    /// </summary>
    /// <param name="root">The object</param>
    /// <param name="key">The key</param>
    /// <returns>The values of the properties</returns>
    private static List<JsonElement> FindProperties(JsonElement root, string key)
    {
        return root.EnumerateObject()
                   .Where(property => string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                   .Select(property => property.Value)
                   .ToList();
    }

    /// <summary>
    /// Reads a whole number from a JSON number written as decimal digits only, within a range.
    /// </summary>
    /// <param name="value">The value</param>
    /// <param name="minimum">The smallest number</param>
    /// <param name="maximum">The largest number</param>
    /// <param name="number">The number</param>
    /// <returns><see langword="true" /> when the value is such a number</returns>
    private static bool TryReadWholeNumber(JsonElement value, int minimum, int maximum, out int number)
    {
        number = 0;

        if (value.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        var text = value.GetRawText().AsSpan();

        return text.IndexOfAnyExceptInRange('0', '9') < 0
               && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number)
               && number >= minimum
               && number <= maximum;
    }

    /// <summary>
    /// Creates an invalid local server setting.
    /// </summary>
    /// <param name="problem">The problem</param>
    /// <returns>The setting</returns>
    private static LocalServerSetting Invalid(string problem)
    {
        return new LocalServerSetting
               {
                   Kind = LocalServerSettingKind.Invalid,
                   Problem = problem
               };
    }

    /// <summary>
    /// Parses the bytes of a settings file.
    /// </summary>
    /// <param name="bytes">The bytes</param>
    /// <returns>The result</returns>
    private static CompanionSettingsResult Parse(byte[] bytes)
    {
        try
        {
            using var document = ParseDocument(bytes);

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