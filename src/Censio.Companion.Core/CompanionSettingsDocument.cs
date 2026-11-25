using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Censio.Companion.Core;

/// <summary>
/// The settings file as the settings window reads and writes it.
/// </summary>
public sealed class CompanionSettingsDocument
{
    #region Constants

    /// <summary>
    /// The reason of a file that holds a text that cannot be decoded.
    /// </summary>
    private const string NotValidUtf8 = "holds text that is not valid UTF-8";

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The path of the settings file.
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// The properties of the file that are no known key, as written.
    /// </summary>
    private readonly IReadOnlyList<KeyValuePair<string, JsonElement>> _unknown;

    /// <summary>
    /// The SHA-256 of the bytes read, or <see langword="null" /> for a missing file.
    /// </summary>
    private byte[]? _hash;

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionSettingsDocument" /> class.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <param name="values">The values</param>
    /// <param name="fileProblem">The problem of the file, or <see langword="null" /></param>
    /// <param name="fieldProblems">The problems of the fields</param>
    /// <param name="hash">The SHA-256 of the bytes read, or <see langword="null" /> for a missing file</param>
    /// <param name="unknown">The unknown properties of the file</param>
    private CompanionSettingsDocument(string path, CompanionSettingsValues values, string? fileProblem, IReadOnlyList<SettingsProblem> fieldProblems, byte[]? hash, IReadOnlyList<KeyValuePair<string, JsonElement>> unknown)
    {
        _path = path;
        _hash = hash;
        _unknown = unknown;
        Values = values;
        FileProblem = fileProblem;
        FieldProblems = fieldProblems;
    }

    #endregion // Constructor

    #region Properties

    /// <summary>
    /// Gets the values of the file.
    /// </summary>
    public CompanionSettingsValues Values { get; }

    /// <summary>
    /// Gets the problem of the file, or <see langword="null" /> when it can be read.
    /// </summary>
    public string? FileProblem { get; }

    /// <summary>
    /// Gets the problems of the fields.
    /// </summary>
    public IReadOnlyList<SettingsProblem> FieldProblems { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Loads the settings file.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <returns>The document</returns>
    public static CompanionSettingsDocument Load(string path)
    {
        byte[] bytes;

        try
        {
            bytes = CompanionSettings.ReadBytes(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return CreateUnusable(path, null, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            return CreateUnusable(path, "cannot be read", null);
        }

        var hash = SHA256.HashData(bytes);

        if (bytes.Length > CompanionSettings.MaxFileLength)
        {
            return CreateUnusable(path, "larger than 1 MiB", hash);
        }

        try
        {
            using var document = CompanionSettings.ParseDocument(bytes);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return CreateUnusable(path, "not a JSON object", hash);
            }

            ReadEveryString(root);

            return CreateFromRoot(path, root, hash);
        }
        catch (JsonException)
        {
            return CreateUnusable(path, "not valid JSON", hash);
        }
        catch (InvalidOperationException)
        {
            return CreateUnusable(path, NotValidUtf8, hash);
        }
    }

    /// <summary>
    /// Checks whether saved values need the Companion to restart.
    /// </summary>
    /// <param name="running">The values the running Companion started with</param>
    /// <param name="saved">The saved values</param>
    /// <returns><see langword="true" /> when a connection or game setting differs</returns>
    public static bool NeedsRestart(CompanionSettingsValues running, CompanionSettingsValues saved)
    {
        return running.Mode != saved.Mode
               || Normalize(running.LocalPort, 1, 65535, CompanionSettings.DefaultLocalPort) != Normalize(saved.LocalPort, 1, 65535, CompanionSettings.DefaultLocalPort)
               || string.Equals(running.ServerAddress, saved.ServerAddress, StringComparison.Ordinal) == false
               || string.Equals(TrimToken(running.ServerToken), TrimToken(saved.ServerToken), StringComparison.Ordinal) == false
               || Normalize(running.BufferHours, 1, LiveBufferLimits.MaxHours, LiveBufferLimits.DefaultHours) != Normalize(saved.BufferHours, 1, LiveBufferLimits.MaxHours, LiveBufferLimits.DefaultHours)
               || Normalize(running.BufferMegabytes, 1, LiveBufferLimits.MaxMegabytes, LiveBufferLimits.DefaultMegabytes) != Normalize(saved.BufferMegabytes, 1, LiveBufferLimits.MaxMegabytes, LiveBufferLimits.DefaultMegabytes)
               || string.Equals(running.GameFolder, saved.GameFolder, StringComparison.Ordinal) == false;
    }

    /// <summary>
    /// Saves the values, unless the file changed since it was loaded.
    /// </summary>
    /// <param name="values">The values</param>
    /// <returns>The result of the write</returns>
    public CompanionSettingsWrite Save(CompanionSettingsValues values)
    {
        var temporaryPath = _path + ".tmp";

        try
        {
            var current = ReadHash();

            if (HashesEqual(current, _hash) == false)
            {
                return CreateWrite(CompanionSettingsWriteKind.ChangedOutside, null);
            }

            var bytes = Serialize(values);
            var folder = Path.GetDirectoryName(_path);

            if (string.IsNullOrEmpty(folder) == false)
            {
                Directory.CreateDirectory(folder);
            }

            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }

            File.Move(temporaryPath, _path, true);

            _hash = SHA256.HashData(bytes);

            return CreateWrite(CompanionSettingsWriteKind.Saved, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            DeleteTemporaryFile(temporaryPath);

            return CreateWrite(CompanionSettingsWriteKind.Failed, exception);
        }
    }

    /// <summary>
    /// Creates a document of a file that is missing or cannot be used: the defaults.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <param name="fileProblem">The reason, or <see langword="null" /> for a missing file</param>
    /// <param name="hash">The hash of the bytes read</param>
    /// <returns>The document</returns>
    private static CompanionSettingsDocument CreateUnusable(string path, string? fileProblem, byte[]? hash)
    {
        return new CompanionSettingsDocument(path, CreateDefaults(), fileProblem, [], hash, []);
    }

    /// <summary>
    /// Creates the values of the defaults.
    /// </summary>
    /// <returns>The values</returns>
    private static CompanionSettingsValues CreateDefaults()
    {
        return new CompanionSettingsValues
               {
                   Mode = CompanionSettingsMode.Off,
                   LocalPort = CompanionSettings.DefaultLocalPort.ToString(CultureInfo.InvariantCulture),
                   ServerAddress = string.Empty,
                   ServerToken = string.Empty,
                   BufferHours = LiveBufferLimits.DefaultHours.ToString(CultureInfo.InvariantCulture),
                   BufferMegabytes = LiveBufferLimits.DefaultMegabytes.ToString(CultureInfo.InvariantCulture),
                   GameFolder = string.Empty
               };
    }

    /// <summary>
    /// Creates the document of the root object of a settings file.
    /// </summary>
    /// <param name="path">The path of the settings file</param>
    /// <param name="root">The root object</param>
    /// <param name="hash">The hash of the bytes read</param>
    /// <returns>The document</returns>
    private static CompanionSettingsDocument CreateFromRoot(string path, JsonElement root, byte[] hash)
    {
        var problems = new List<SettingsProblem>();
        var mode = ReadMode(root, problems);
        var port = ReadNumber(root, CompanionSettings.LocalPortKey, SettingsField.LocalPort, 1, 65535, SettingsProblemKind.PortRange, problems);
        var address = ReadText(root, CompanionSettings.ServerAddressKey, SettingsField.ServerAddress, SettingsProblemKind.AddressInvalid, text => CompanionCommandLine.TryParseServerAddress(text, out _), problems);
        var token = ReadText(root, CompanionSettings.ServerTokenKey, SettingsField.ServerToken, SettingsProblemKind.TokenInvalid, text => ReplayTokenFile.ReadToken(text) is not null, problems);
        var hours = ReadNumber(root, CompanionSettings.BufferHoursKey, SettingsField.BufferHours, 1, LiveBufferLimits.MaxHours, SettingsProblemKind.HoursRange, problems);
        var megabytes = ReadNumber(root, CompanionSettings.BufferMegabytesKey, SettingsField.BufferMegabytes, 1, LiveBufferLimits.MaxMegabytes, SettingsProblemKind.MegabytesRange, problems);
        var folder = ReadText(root, CompanionSettings.GameFolderKey, SettingsField.GameFolder, SettingsProblemKind.FolderNotFullPath, _ => true, problems);
        var values = new CompanionSettingsValues
                     {
                         Mode = mode,
                         LocalPort = port ?? CompanionSettings.DefaultLocalPort.ToString(CultureInfo.InvariantCulture),
                         ServerAddress = address ?? string.Empty,
                         ServerToken = token ?? string.Empty,
                         BufferHours = hours ?? LiveBufferLimits.DefaultHours.ToString(CultureInfo.InvariantCulture),
                         BufferMegabytes = megabytes ?? LiveBufferLimits.DefaultMegabytes.ToString(CultureInfo.InvariantCulture),
                         GameFolder = folder ?? string.Empty,
                         Language = CompanionSettings.ReadLanguage(root)
                     };
        var unknown = root.EnumerateObject()
                          .Where(property => IsKnownKey(property.Name) == false)
                          .Select(property => new KeyValuePair<string, JsonElement>(property.Name, property.Value.Clone()))
                          .ToList();

        return new CompanionSettingsDocument(path, values, null, problems.OrderBy(problem => problem.Field).ToList(), hash, unknown);
    }

    /// <summary>
    /// Reads every property name and every text of an element, at any depth, so that a text that cannot be decoded throws.
    /// </summary>
    /// <param name="element">The element</param>
    private static void ReadEveryString(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    foreach (var property in element.EnumerateObject())
                    {
                        _ = property.Name;

                        ReadEveryString(property.Value);
                    }
                }
                break;

            case JsonValueKind.Array:
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        ReadEveryString(item);
                    }
                }
                break;

            case JsonValueKind.String:
                {
                    _ = element.GetString();
                }
                break;
        }
    }

    /// <summary>
    /// Reads the mode of the root object.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="problems">The problems, which get one for a mode written twice or invalid</param>
    /// <returns>The mode, Off when the key is missing, blank, written twice or invalid</returns>
    private static CompanionSettingsMode ReadMode(JsonElement root, List<SettingsProblem> problems)
    {
        var modes = CompanionSettings.FindProperties(root, CompanionSettings.ModeKey);

        if (modes.Count > 1)
        {
            problems.Add(CreateProblem(SettingsField.Mode, SettingsProblemKind.WrittenTwice));

            return CompanionSettingsMode.Off;
        }

        if (modes.Count == 0)
        {
            return CompanionSettingsMode.Off;
        }

        if (modes[0].ValueKind != JsonValueKind.String)
        {
            problems.Add(CreateProblem(SettingsField.Mode, SettingsProblemKind.ModeInvalid));

            return CompanionSettingsMode.Off;
        }

        var mode = modes[0].GetString();

        if (string.IsNullOrWhiteSpace(mode))
        {
            return CompanionSettingsMode.Off;
        }

        if (string.Equals(mode, "forward", StringComparison.OrdinalIgnoreCase))
        {
            return CompanionSettingsMode.Forward;
        }

        if (string.Equals(mode, "local", StringComparison.OrdinalIgnoreCase))
        {
            return CompanionSettingsMode.Local;
        }

        problems.Add(CreateProblem(SettingsField.Mode, SettingsProblemKind.ModeInvalid));

        return CompanionSettingsMode.Off;
    }

    /// <summary>
    /// Reads a key that holds a whole number.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="key">The key</param>
    /// <param name="field">The field of the key</param>
    /// <param name="minimum">The smallest number</param>
    /// <param name="maximum">The largest number</param>
    /// <param name="kind">The problem kind of a wrong value</param>
    /// <param name="problems">The problems, which get one for a wrong or repeated key</param>
    /// <returns>The text of the number, empty for a wrong or repeated key, <see langword="null" /> for a missing one</returns>
    private static string? ReadNumber(JsonElement root, string key, SettingsField field, int minimum, int maximum, SettingsProblemKind kind, List<SettingsProblem> problems)
    {
        var values = CompanionSettings.FindProperties(root, key);

        if (values.Count == 0)
        {
            return null;
        }

        if (values.Count > 1)
        {
            problems.Add(CreateProblem(field, SettingsProblemKind.WrittenTwice));

            return string.Empty;
        }

        if (values[0].ValueKind == JsonValueKind.Number && CompanionSettings.TryParseWholeNumber(values[0].GetRawText(), minimum, maximum, out _))
        {
            return values[0].GetRawText();
        }

        problems.Add(CreateProblem(field, kind));

        return string.Empty;
    }

    /// <summary>
    /// Reads a key that holds a text.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="key">The key</param>
    /// <param name="field">The field of the key</param>
    /// <param name="kind">The problem kind of a wrong value</param>
    /// <param name="isValid">Whether a text that is not empty is valid</param>
    /// <param name="problems">The problems, which get one for a wrong or repeated key</param>
    /// <returns>The text, empty for a wrong or repeated key, <see langword="null" /> for a missing one</returns>
    private static string? ReadText(JsonElement root, string key, SettingsField field, SettingsProblemKind kind, Func<string, bool> isValid, List<SettingsProblem> problems)
    {
        var values = CompanionSettings.FindProperties(root, key);

        if (values.Count == 0)
        {
            return null;
        }

        if (values.Count > 1)
        {
            problems.Add(CreateProblem(field, SettingsProblemKind.WrittenTwice));

            return string.Empty;
        }

        var text = values[0].ValueKind == JsonValueKind.String ? values[0].GetString() : null;

        if (text is not null && (text.Length == 0 || isValid(text)))
        {
            return text;
        }

        problems.Add(CreateProblem(field, kind));

        return string.Empty;
    }

    /// <summary>
    /// Creates a problem.
    /// </summary>
    /// <param name="field">The field</param>
    /// <param name="kind">The kind</param>
    /// <returns>The problem</returns>
    private static SettingsProblem CreateProblem(SettingsField field, SettingsProblemKind kind)
    {
        return new SettingsProblem
               {
                   Field = field,
                   Kind = kind
               };
    }

    /// <summary>
    /// Checks whether a property name is a known key, ignoring case.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns><see langword="true" /> for a known key</returns>
    private static bool IsKnownKey(string name)
    {
        string[] keys = [
                            CompanionSettings.ModeKey,
                            CompanionSettings.LocalPortKey,
                            CompanionSettings.ServerAddressKey,
                            CompanionSettings.ServerTokenKey,
                            CompanionSettings.BufferHoursKey,
                            CompanionSettings.BufferMegabytesKey,
                            CompanionSettings.GameFolderKey,
                            CompanionSettings.LanguageKey
                        ];

        return keys.Any(key => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Normalizes the text of a number so that two spellings of one number compare equal.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="minimum">The smallest number</param>
    /// <param name="maximum">The largest number</param>
    /// <param name="fallback">The number when the text is empty</param>
    /// <returns>The number as text, or the text when it is no valid number</returns>
    private static string Normalize(string text, int minimum, int maximum, int fallback)
    {
        if (text.Length == 0)
        {
            return fallback.ToString(CultureInfo.InvariantCulture);
        }

        return CompanionSettings.TryParseWholeNumber(text, minimum, maximum, out var number)
                   ? number.ToString(CultureInfo.InvariantCulture)
                   : text;
    }

    /// <summary>
    /// Trims a token as the token readers do.
    /// </summary>
    /// <param name="token">The token</param>
    /// <returns>The trimmed token</returns>
    private static string TrimToken(string token)
    {
        return token.Trim(' ', '\t', '\r', '\n');
    }

    /// <summary>
    /// Creates the result of a write.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <param name="exception">The exception, if any</param>
    /// <returns>The result</returns>
    private static CompanionSettingsWrite CreateWrite(CompanionSettingsWriteKind kind, Exception? exception)
    {
        return new CompanionSettingsWrite
               {
                   Kind = kind,
                   Exception = exception
               };
    }

    /// <summary>
    /// Compares two hashes, a missing one included.
    /// </summary>
    /// <param name="left">The first hash</param>
    /// <param name="right">The second hash</param>
    /// <returns><see langword="true" /> when both are missing or equal</returns>
    private static bool HashesEqual(byte[]? left, byte[]? right)
    {
        return left is null || right is null
                   ? left is null && right is null
                   : left.AsSpan().SequenceEqual(right);
    }

    /// <summary>
    /// Deletes the temporary file of a save that failed.
    /// </summary>
    /// <param name="temporaryPath">The path of the temporary file</param>
    private static void DeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException)
        {
            // The temporary file stays; it is replaced by the next save.
        }
    }

    /// <summary>
    /// Writes a key with the parsed number of a text, or nothing when the text is no valid number.
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="key">The key</param>
    /// <param name="text">The text of the number</param>
    /// <param name="maximum">The largest number</param>
    private static void WriteNumber(Utf8JsonWriter writer, string key, string text, int maximum)
    {
        if (CompanionSettings.TryParseWholeNumber(text, 1, maximum, out var number))
        {
            writer.WriteNumber(key, number);
        }
    }

    /// <summary>
    /// Reads the hash of the file as it is now.
    /// </summary>
    /// <returns>The hash, <see langword="null" /> for a missing file</returns>
    private byte[]? ReadHash()
    {
        try
        {
            return SHA256.HashData(CompanionSettings.ReadBytes(_path));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the values and the unknown properties as the bytes of the file.
    /// </summary>
    /// <param name="values">The values</param>
    /// <returns>The bytes, UTF-8 without a byte order mark</returns>
    private byte[] Serialize(CompanionSettingsValues values)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream,
                                               new JsonWriterOptions
                                               {
                                                   Indented = true
                                               }))
        {
            writer.WriteStartObject();

            if (values.Mode != CompanionSettingsMode.Off)
            {
                writer.WriteString(CompanionSettings.ModeKey, values.Mode == CompanionSettingsMode.Forward ? "forward" : "local");
            }

            WriteNumber(writer, CompanionSettings.LocalPortKey, values.LocalPort, 65535);

            if (values.ServerAddress.Length > 0)
            {
                writer.WriteString(CompanionSettings.ServerAddressKey, values.ServerAddress);
            }

            var token = TrimToken(values.ServerToken);

            if (token.Length > 0)
            {
                writer.WriteString(CompanionSettings.ServerTokenKey, token);
            }

            WriteNumber(writer, CompanionSettings.BufferHoursKey, values.BufferHours, LiveBufferLimits.MaxHours);
            WriteNumber(writer, CompanionSettings.BufferMegabytesKey, values.BufferMegabytes, LiveBufferLimits.MaxMegabytes);

            if (values.GameFolder.Length > 0)
            {
                writer.WriteString(CompanionSettings.GameFolderKey, values.GameFolder);
            }

            if (values.Language is { } language)
            {
                writer.WriteString(CompanionSettings.LanguageKey, language == CompanionLanguage.German ? "de" : "en");
            }

            foreach (var property in _unknown)
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    #endregion // Methods
}