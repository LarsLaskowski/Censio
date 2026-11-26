using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Censio.Companion.Core;

/// <summary>
/// The profiles live data is sent under, and the figures each was used with.
/// </summary>
public sealed class CompanionProfiles
{
    #region Constants

    /// <summary>
    /// The largest size of the profiles file in bytes.
    /// </summary>
    public const int MaxFileLength = 1048576;

    /// <summary>
    /// The largest number of profiles.
    /// </summary>
    public const int MaxProfiles = 1000;

    /// <summary>
    /// The largest number of remembered figures.
    /// </summary>
    public const int MaxFigures = 1000;

    /// <summary>
    /// The largest number of deleted profile identities kept, and of waiting deletions.
    /// </summary>
    public const int MaxDeletions = 1000;

    /// <summary>
    /// The longest profile name in characters.
    /// </summary>
    public const int MaxNameLength = 64;

    /// <summary>
    /// The length of the key of a figure: the hexadecimal SHA-256 of its name.
    /// </summary>
    private const int FigureKeyLength = 64;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The path of the profiles file.
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The lock of the state.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The profiles in file order.
    /// </summary>
    private readonly List<CompanionProfile> _profiles;

    /// <summary>
    /// The figure links, the oldest first.
    /// </summary>
    private readonly List<KeyValuePair<string, Guid>> _figures;

    /// <summary>
    /// The identities of the profiles deleted with their history, the oldest first, each with whether the server confirmed it.
    /// </summary>
    private readonly List<KeyValuePair<Guid, bool>> _deleted;

    /// <summary>
    /// The active profile.
    /// </summary>
    private CompanionProfile _active;

    /// <summary>
    /// Whether the file could not be used and was not replaced yet.
    /// </summary>
    private bool _isFileInvalid;

    /// <summary>
    /// The key of the figure whose session runs, or <see langword="null" /> when none does.
    /// </summary>
    private string? _currentFigure;

    /// <summary>
    /// The number of changes of the state to write.
    /// </summary>
    private int _version;

    /// <summary>
    /// The completion that the next change sets.
    /// </summary>
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    #endregion // Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionProfiles" /> class.
    /// </summary>
    /// <param name="path">The path of the profiles file</param>
    /// <param name="logger">The logger</param>
    /// <param name="profiles">The profiles</param>
    /// <param name="active">The active profile</param>
    /// <param name="figures">The figure links, the oldest first</param>
    /// <param name="deleted">The deleted identities, the oldest first, each with whether it is confirmed</param>
    /// <param name="isFileInvalid">Whether the file could not be used</param>
    private CompanionProfiles(string path, ILogger logger, List<CompanionProfile> profiles, CompanionProfile active, List<KeyValuePair<string, Guid>> figures, List<KeyValuePair<Guid, bool>> deleted, bool isFileInvalid)
    {
        _path = path;
        _logger = logger;
        _profiles = profiles;
        _active = active;
        _figures = figures;
        _deleted = deleted;
        _isFileInvalid = isFileInvalid;
    }

    #endregion // Constructor

    #region Events

    /// <summary>
    /// Occurs when the active profile changes.
    /// </summary>
    public event EventHandler? ActiveChanged;

    /// <summary>
    /// Occurs when the pending notice changes.
    /// </summary>
    public event EventHandler? PendingNoticeChanged;

    /// <summary>
    /// Occurs when the list of deleted profile identities changed.
    /// </summary>
    public event EventHandler? DeletionsChanged;

    #endregion // Events

    #region Properties

    /// <summary>
    /// Gets the profiles, <c>Live</c> first.
    /// </summary>
    public IReadOnlyList<CompanionProfile> Profiles
    {
        get
        {
            lock (_lock)
            {
                return [.. _profiles];
            }
        }
    }

    /// <summary>
    /// Gets the active profile.
    /// </summary>
    public CompanionProfile Active
    {
        get
        {
            lock (_lock)
            {
                return _active;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the file could not be used and was not replaced yet.
    /// </summary>
    public bool IsFileInvalid
    {
        get
        {
            lock (_lock)
            {
                return _isFileInvalid;
            }
        }
    }

    /// <summary>
    /// Gets the pending notice about the active profile, or <see langword="null" /> if none.
    /// </summary>
    public ProfileNotice? PendingNotice
    {
        get
        {
            lock (_lock)
            {
                return field;
            }
        }

        private set
        {
            lock (_lock)
            {
                field = value;
            }
        }
    }

    /// <summary>
    /// Gets the identities of the deleted profiles whose history the server has not confirmed yet, oldest first.
    /// </summary>
    public IReadOnlyList<Guid> WaitingDeletions
    {
        get
        {
            lock (_lock)
            {
                return [.. _deleted.Where(entry => entry.Value == false).Select(entry => entry.Key)];
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether as many deletions wait as <see cref="MaxDeletions" /> allows.
    /// </summary>
    public bool IsDeletionQueueFull
    {
        get
        {
            lock (_lock)
            {
                return CountWaiting() >= MaxDeletions;
            }
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Loads the profiles file; a file that cannot be used gives <c>Live</c> alone.
    /// </summary>
    /// <param name="path">The path of the profiles file</param>
    /// <param name="logger">The logger</param>
    /// <returns>The profiles</returns>
    public static CompanionProfiles Load(string path, ILogger logger)
    {
        byte[] bytes;

        try
        {
            bytes = CompanionSettings.ReadBytes(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return CreateLiveAlone(path, logger, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            return CreateInvalid(path, logger, "cannot be read");
        }

        if (bytes.Length > MaxFileLength)
        {
            return CreateInvalid(path, logger, "larger than 1 MiB");
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);

            return Read(path, logger, document.RootElement) ?? CreateInvalid(path, logger, "not of its format");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return CreateInvalid(path, logger, "not of its format");
        }
    }

    /// <summary>
    /// Checks a profile name.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns>The problem, or <see langword="null" /> when the name can be used</returns>
    public static SettingsProblemKind? CheckName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return SettingsProblemKind.NameBlank;
        }

        if (name.Length > MaxNameLength)
        {
            return SettingsProblemKind.NameTooLong;
        }

        return IsWellFormedWithoutControl(name) ? null : SettingsProblemKind.NameInvalidCharacters;
    }

    /// <summary>
    /// Determines whether figures are observed in a run.
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="setting">The server setting</param>
    /// <returns><see langword="true" /> when the run sends live data</returns>
    public static bool IsObserved(CompanionMode mode, LocalServerSetting setting)
    {
        return LiveSender.IsSending(mode, setting);
    }

    /// <summary>
    /// Determines whether a profile can be deleted.
    /// </summary>
    /// <param name="id">The identity of the profile</param>
    /// <param name="active">The identity of the active profile</param>
    /// <param name="profiles">The profiles</param>
    /// <returns><see langword="true" /> when the profile is in the list, is not <c>Live</c> and is not active</returns>
    public static bool IsDeletable(Guid id, Guid active, IReadOnlyList<CompanionProfile> profiles)
    {
        return id != LiveSender.ProfileId && id != active && profiles.Any(profile => profile.Id == id);
    }

    /// <summary>
    /// Checks the name of a profile to create.
    /// </summary>
    /// <param name="name">The name</param>
    /// <returns>The problem, or <see langword="null" /> when the profile can be created</returns>
    public SettingsProblemKind? CheckNewName(string name)
    {
        if (CheckName(name) is { } problem)
        {
            return problem;
        }

        lock (_lock)
        {
            if (_profiles.Count >= MaxProfiles)
            {
                return SettingsProblemKind.ProfilesFull;
            }

            return _profiles.Any(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                       ? SettingsProblemKind.NameExists
                       : null;
        }
    }

    /// <summary>
    /// Creates a profile and makes it active.
    /// </summary>
    /// <param name="name">The name, which <see cref="CheckNewName" /> accepts</param>
    /// <returns>The profile</returns>
    /// <exception cref="ArgumentException">The name cannot be used</exception>
    public CompanionProfile Create(string name)
    {
        CompanionProfile created;
        bool isNoticeChanged;

        lock (_lock)
        {
            if (CheckNewName(name) is not null)
            {
                throw new ArgumentException("The name cannot be used", nameof(name));
            }

            created = new CompanionProfile
                      {
                          Id = Guid.NewGuid(),
                          Name = name
                      };

            _profiles.Add(created);
            Activate(created);
            isNoticeChanged = ReplaceNotice(null);
            MarkChanged();
        }

        ActiveChanged?.Invoke(this, EventArgs.Empty);
        RaisePendingNoticeChanged(isNoticeChanged);

        return created;
    }

    /// <summary>
    /// Makes a profile active.
    /// </summary>
    /// <param name="id">The identity of the profile</param>
    /// <exception cref="ArgumentException">No profile has the identity</exception>
    public void Select(Guid id)
    {
        bool isChanged;
        bool isNoticeChanged;

        lock (_lock)
        {
            var profile = _profiles.Find(candidate => candidate.Id == id) ?? throw new ArgumentException("No profile has the identity", nameof(id));

            isChanged = ReferenceEquals(profile, _active) == false;

            if (isChanged)
            {
                Activate(profile);
            }

            isNoticeChanged = ReplaceNotice(null);

            if (isChanged || _isFileInvalid)
            {
                MarkChanged();
            }
        }

        if (isChanged)
        {
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        RaisePendingNoticeChanged(isNoticeChanged);
    }

    /// <summary>
    /// Deletes a profile with the figures linked to it, and optionally queues the deletion of its history on the server.
    /// </summary>
    /// <param name="id">The identity of the profile</param>
    /// <param name="isHistoryDeleted">Whether the history on the server is deleted too</param>
    /// <exception cref="ArgumentException">The profile cannot be deleted</exception>
    /// <exception cref="InvalidOperationException">The deletion is ticked and the queue is full</exception>
    public void Delete(Guid id, bool isHistoryDeleted)
    {
        bool isNoticeChanged;

        lock (_lock)
        {
            if (IsDeletable(id, _active.Id, _profiles) == false)
            {
                throw new ArgumentException("The profile cannot be deleted", nameof(id));
            }

            if (isHistoryDeleted && CountWaiting() >= MaxDeletions)
            {
                throw new InvalidOperationException("The queue of deletions is full");
            }

            _profiles.RemoveAll(profile => profile.Id == id);
            _figures.RemoveAll(link => link.Value == id);
            isNoticeChanged = ReplaceNotice(null);

            if (isHistoryDeleted)
            {
                if (_deleted.Count >= MaxDeletions)
                {
                    _deleted.RemoveAt(_deleted.FindIndex(entry => entry.Value));
                }

                _deleted.Add(new KeyValuePair<Guid, bool>(id, false));
            }

            MarkChanged();
        }

        CompanionLog.ProfileDeleted(_logger);
        RaisePendingNoticeChanged(isNoticeChanged);

        if (isHistoryDeleted)
        {
            DeletionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Determines whether the identity of a profile is among the deleted ones, waiting or confirmed.
    /// </summary>
    /// <param name="id">The identity of the profile</param>
    /// <returns><see langword="true" /> when the profile was deleted with its history</returns>
    public bool IsDeleted(Guid id)
    {
        lock (_lock)
        {
            return _deleted.Any(entry => entry.Key == id);
        }
    }

    /// <summary>
    /// Marks a waiting deletion as confirmed by the server.
    /// </summary>
    /// <param name="id">The identity of the profile</param>
    public void ConfirmDeletion(Guid id)
    {
        lock (_lock)
        {
            var index = _deleted.FindIndex(entry => entry.Key == id && entry.Value == false);

            if (index < 0)
            {
                return;
            }

            _deleted[index] = new KeyValuePair<Guid, bool>(id, true);
            MarkChanged();
        }

        DeletionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the name of a profile.
    /// </summary>
    /// <param name="id">The identity of the profile</param>
    /// <returns>The name, or <see langword="null" /> when no profile has the identity</returns>
    public string? NameOf(Guid id)
    {
        lock (_lock)
        {
            return _profiles.Find(candidate => candidate.Id == id)?.Name;
        }
    }

    /// <summary>
    /// Observes the start of a session of a figure and links the figure to the active profile.
    /// </summary>
    /// <param name="figure">The name of the figure</param>
    /// <returns>The notice to show, or <see langword="null" /> when there is none</returns>
    public ProfileNotice? ObserveSessionStart(string figure)
    {
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(figure)));

        ProfileNotice? notice;
        bool isNoticeChanged;

        lock (_lock)
        {
            notice = ObserveSessionStartLocked(key, out isNoticeChanged);
        }

        RaisePendingNoticeChanged(isNoticeChanged);

        return notice;
    }

    /// <summary>
    /// Observes the end of the session of a figure.
    /// </summary>
    public void ObserveSessionEnd()
    {
        bool isNoticeChanged;

        lock (_lock)
        {
            _currentFigure = null;
            isNoticeChanged = ReplaceNotice(null);
        }

        RaisePendingNoticeChanged(isNoticeChanged);
    }

    /// <summary>
    /// Writes the profiles file whenever the state changes, until the token is cancelled; then writes the latest state once more.
    /// </summary>
    /// <param name="cancellationToken">The token that ends the writer</param>
    /// <returns>A task that completes when the writer ended</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var written = 0;

        while (cancellationToken.IsCancellationRequested == false)
        {
            int version;
            Task changed;

            lock (_lock)
            {
                version = _version;
                changed = _changed.Task;
            }

            if (version != written)
            {
                written = await Task.Run(WriteState, CancellationToken.None).ConfigureAwait(false);

                continue;
            }

            try
            {
                await changed.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        bool isChangedSinceWrite;

        lock (_lock)
        {
            isChangedSinceWrite = _version != written;
        }

        if (isChangedSinceWrite)
        {
            await Task.Run(WriteState, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates profiles of <c>Live</c> alone.
    /// </summary>
    /// <param name="path">The path of the profiles file</param>
    /// <param name="logger">The logger</param>
    /// <param name="isFileInvalid">Whether the file could not be used</param>
    /// <returns>The profiles</returns>
    private static CompanionProfiles CreateLiveAlone(string path, ILogger logger, bool isFileInvalid)
    {
        return new CompanionProfiles(path, logger, [CompanionProfile.Live], CompanionProfile.Live, [], [], isFileInvalid);
    }

    /// <summary>
    /// Creates profiles of <c>Live</c> alone for a file that cannot be used and logs the problem.
    /// </summary>
    /// <param name="path">The path of the profiles file</param>
    /// <param name="logger">The logger</param>
    /// <param name="problem">The problem, which quotes no value of the file</param>
    /// <returns>The profiles</returns>
    private static CompanionProfiles CreateInvalid(string path, ILogger logger, string problem)
    {
        CompanionLog.ProfilesFileInvalid(logger, problem);

        return CreateLiveAlone(path, logger, true);
    }

    /// <summary>
    /// Reads the root object of the profiles file.
    /// </summary>
    /// <param name="path">The path of the profiles file</param>
    /// <param name="logger">The logger</param>
    /// <param name="root">The root</param>
    /// <returns>The profiles, or <see langword="null" /> when the file is outside the rules</returns>
    private static CompanionProfiles? Read(string path, ILogger logger, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || TryReadRoot(root, out var version, out var active, out var profilesElement, out var figuresElement, out var deletedElement) == false)
        {
            return null;
        }

        if (version.ValueKind != JsonValueKind.Number || version.TryGetInt32(out var number) == false || number != (deletedElement is null ? 1 : 2))
        {
            return null;
        }

        var profiles = ReadProfiles(profilesElement);

        if (profiles is null || ReadId(active) is not { } activeId || profiles.Find(profile => profile.Id == activeId) is not { } activeProfile)
        {
            return null;
        }

        var figures = ReadFigures(figuresElement, profiles);
        var deleted = deletedElement is { } deletedList ? ReadDeleted(deletedList, profiles) : [];

        if (figures is null || deleted is null)
        {
            return null;
        }

        return new CompanionProfiles(path, logger, profiles, activeProfile, figures, deleted, false);
    }

    /// <summary>
    /// Reads the properties of the root, each exactly once: four for version 1, five with <c>deleted</c> for version 2.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="version">The version</param>
    /// <param name="active">The active identity</param>
    /// <param name="profiles">The profiles</param>
    /// <param name="figures">The figures</param>
    /// <param name="deleted">The deleted identities, or <see langword="null" /> when the root holds four properties</param>
    /// <returns><see langword="true" /> when the root holds exactly one of these sets of properties</returns>
    private static bool TryReadRoot(JsonElement root, out JsonElement version, out JsonElement active, out JsonElement profiles, out JsonElement figures, out JsonElement? deleted)
    {
        var found = ReadProperties(root, "version", "active", "profiles", "figures");

        version = default;
        active = default;
        profiles = default;
        figures = default;
        deleted = null;

        if (found is null)
        {
            found = ReadProperties(root, "version", "active", "profiles", "figures", "deleted");

            if (found is null)
            {
                return false;
            }

            deleted = found[4];
        }

        version = found[0];
        active = found[1];
        profiles = found[2];
        figures = found[3];

        return true;
    }

    /// <summary>
    /// Reads the deleted identities.
    /// </summary>
    /// <param name="element">The array</param>
    /// <param name="profiles">The profiles</param>
    /// <returns>The deleted identities, or <see langword="null" /> when the array is outside the rules</returns>
    private static List<KeyValuePair<Guid, bool>>? ReadDeleted(JsonElement element, List<CompanionProfile> profiles)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > MaxDeletions)
        {
            return null;
        }

        var deleted = new List<KeyValuePair<Guid, bool>>();

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || ReadProperties(item, "id", "confirmed") is not { } values || ReadId(values[0]) is not { } id)
            {
                return null;
            }

            if (values[1].ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || id == LiveSender.ProfileId
                || profiles.Any(profile => profile.Id == id)
                || deleted.Any(entry => entry.Key == id))
            {
                return null;
            }

            deleted.Add(new KeyValuePair<Guid, bool>(id, values[1].GetBoolean()));
        }

        return deleted;
    }

    /// <summary>
    /// Reads the properties of an object, which must be exactly the given names, each once, compared ordinal.
    /// </summary>
    /// <param name="element">The object</param>
    /// <param name="names">The names</param>
    /// <returns>The values in the order of the names, or <see langword="null" /> when the object holds another set of properties</returns>
    private static JsonElement[]? ReadProperties(JsonElement element, params string[] names)
    {
        var values = new JsonElement[names.Length];
        var isFound = new bool[names.Length];

        foreach (var property in element.EnumerateObject())
        {
            var index = Array.IndexOf(names, property.Name);

            if (index < 0 || isFound[index])
            {
                return null;
            }

            isFound[index] = true;
            values[index] = property.Value;
        }

        return isFound.All(found => found) ? values : null;
    }

    /// <summary>
    /// Reads an identity written as a string of the form <c>D</c>.
    /// </summary>
    /// <param name="element">The element</param>
    /// <returns>The identity, or <see langword="null" /> when the element is no such string</returns>
    private static Guid? ReadId(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String && Guid.TryParseExact(element.GetString(), "D", out var id) ? id : null;
    }

    /// <summary>
    /// Reads the profiles.
    /// </summary>
    /// <param name="element">The array</param>
    /// <returns>The profiles, or <see langword="null" /> when the array is outside the rules</returns>
    private static List<CompanionProfile>? ReadProfiles(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() is < 1 or > MaxProfiles)
        {
            return null;
        }

        var profiles = new List<CompanionProfile>();

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || ReadProperties(item, "id", "name") is not { } values || ReadId(values[0]) is not { } id || values[1].ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var name = values[1].GetString();

            if (name is null || CheckName(name) is not null || profiles.Any(profile => profile.Id == id || string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            profiles.Add(new CompanionProfile
                         {
                             Id = id,
                             Name = name
                         });
        }

        return profiles.Any(profile => profile.Id == LiveSender.ProfileId) ? profiles : null;
    }

    /// <summary>
    /// Reads the figure links.
    /// </summary>
    /// <param name="element">The array</param>
    /// <param name="profiles">The profiles</param>
    /// <returns>The links, or <see langword="null" /> when the array is outside the rules</returns>
    private static List<KeyValuePair<string, Guid>>? ReadFigures(JsonElement element, List<CompanionProfile> profiles)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > MaxFigures)
        {
            return null;
        }

        var figures = new List<KeyValuePair<string, Guid>>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || ReadProperties(item, "figure", "profile") is not { } values || values[0].ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var key = values[0].GetString();

            if (key is null || IsFigureKey(key) == false || ReadId(values[1]) is not { } profile || profiles.Any(candidate => candidate.Id == profile) == false || keys.Add(key) == false)
            {
                return null;
            }

            figures.Add(new KeyValuePair<string, Guid>(key, profile));
        }

        return figures;
    }

    /// <summary>
    /// Checks the key of a figure: 64 lowercase hexadecimal characters.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns><see langword="true" /> when the key has that form</returns>
    private static bool IsFigureKey(string key)
    {
        return key.Length == FigureKeyLength && key.All(char.IsAsciiHexDigitLower);
    }

    /// <summary>
    /// Checks that a text is well-formed UTF-16 and holds no control character.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns><see langword="true" /> when it is and does not</returns>
    private static bool IsWellFormedWithoutControl(string text)
    {
        var isHighPending = false;

        foreach (var character in text)
        {
            if (char.IsControl(character))
            {
                return false;
            }

            if (isHighPending != char.IsLowSurrogate(character))
            {
                return false;
            }

            isHighPending = char.IsHighSurrogate(character);
        }

        return isHighPending == false;
    }

    /// <summary>
    /// Creates a notice.
    /// </summary>
    /// <param name="kind">The kind</param>
    /// <param name="profileName">The name of the profile</param>
    /// <returns>The notice</returns>
    private static ProfileNotice CreateNotice(ProfileNoticeKind kind, string profileName)
    {
        return new ProfileNotice
               {
                   Kind = kind,
                   ProfileName = profileName
               };
    }

    /// <summary>
    /// Deletes the temporary file of a write that failed.
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
            // The temporary file stays; the next write replaces it.
        }
    }

    /// <summary>
    /// Observes the start of a session while the lock is held.
    /// </summary>
    /// <param name="key">The key of the figure</param>
    /// <param name="isNoticeChanged">Whether the pending notice changed</param>
    /// <returns>The notice to show, or <see langword="null" /> when there is none</returns>
    private ProfileNotice? ObserveSessionStartLocked(string key, out bool isNoticeChanged)
    {
        if (_isFileInvalid)
        {
            isNoticeChanged = ReplaceNotice(null);

            return null;
        }

        ProfileNotice? notice = null;
        var index = _figures.FindIndex(link => link.Key == key);

        if (index >= 0)
        {
            var linked = _figures[index].Value;

            if (linked != _active.Id && _profiles.Find(candidate => candidate.Id == linked) is { } other)
            {
                notice = CreateNotice(ProfileNoticeKind.LastUsedWithOther, other.Name);
            }

            _figures.RemoveAt(index);
        }
        else if (_figures.Any(link => link.Value == _active.Id))
        {
            notice = CreateNotice(ProfileNoticeKind.NewFigure, _active.Name);
        }

        Link(key, _active.Id);

        _currentFigure = key;
        isNoticeChanged = ReplaceNotice(notice);

        MarkChanged();

        return notice;
    }

    /// <summary>
    /// Replaces the pending notice.
    /// </summary>
    /// <param name="notice">The new pending notice, or <see langword="null" /> if none</param>
    /// <returns>Whether the pending notice is another one than before</returns>
    private bool ReplaceNotice(ProfileNotice? notice)
    {
        var isChanged = ReferenceEquals(PendingNotice, notice) == false;

        PendingNotice = notice;

        return isChanged;
    }

    /// <summary>
    /// Raises <see cref="PendingNoticeChanged" /> outside the lock.
    /// </summary>
    /// <param name="isChanged">Whether the pending notice changed</param>
    private void RaisePendingNoticeChanged(bool isChanged)
    {
        if (isChanged)
        {
            PendingNoticeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Makes a profile active and moves the link of the figure whose session runs; the caller holds the lock.
    /// </summary>
    /// <param name="profile">The profile</param>
    private void Activate(CompanionProfile profile)
    {
        _active = profile;

        if (_currentFigure is { } key && _isFileInvalid == false)
        {
            _figures.RemoveAll(link => link.Key == key);
            Link(key, profile.Id);
        }
    }

    /// <summary>
    /// Links a figure to a profile as the newest link and drops the oldest beyond the bound; the caller holds the lock.
    /// </summary>
    /// <param name="key">The key of the figure</param>
    /// <param name="profile">The identity of the profile</param>
    private void Link(string key, Guid profile)
    {
        _figures.Add(new KeyValuePair<string, Guid>(key, profile));

        if (_figures.Count > MaxFigures)
        {
            _figures.RemoveAt(0);
        }
    }

    /// <summary>
    /// Counts the waiting deletions; the caller holds the lock.
    /// </summary>
    /// <returns>The number of deleted identities the server has not confirmed</returns>
    private int CountWaiting()
    {
        return _deleted.Count(entry => entry.Value == false);
    }

    /// <summary>
    /// Records a change of the state to write and wakes the writer; the caller holds the lock.
    /// </summary>
    private void MarkChanged()
    {
        _version++;
        _isFileInvalid = false;

        var changed = _changed;

        _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        changed.TrySetResult();
    }

    /// <summary>
    /// Writes the latest state to the file.
    /// </summary>
    /// <returns>The version of the state that was written, or attempted</returns>
    private int WriteState()
    {
        byte[] bytes;
        int version;

        lock (_lock)
        {
            version = _version;
            bytes = Serialize();
        }

        var temporaryPath = _path + ".tmp";

        try
        {
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
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException)
        {
            CompanionLog.SettingsNotSaved(_logger, exception.GetType().Name);
            DeleteTemporaryFile(temporaryPath);
        }

        return version;
    }

    /// <summary>
    /// Writes the state as the bytes of the file; the caller holds the lock.
    /// </summary>
    /// <returns>The bytes</returns>
    private byte[] Serialize()
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", _deleted.Count > 0 ? 2 : 1);
            writer.WriteString("active", _active.Id.ToString("D", CultureInfo.InvariantCulture));
            writer.WriteStartArray("profiles");

            foreach (var profile in _profiles)
            {
                writer.WriteStartObject();
                writer.WriteString("id", profile.Id.ToString("D", CultureInfo.InvariantCulture));
                writer.WriteString("name", profile.Name);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("figures");

            foreach (var link in _figures)
            {
                writer.WriteStartObject();
                writer.WriteString("figure", link.Key);
                writer.WriteString("profile", link.Value.ToString("D", CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            if (_deleted.Count > 0)
            {
                writer.WriteStartArray("deleted");

                foreach (var entry in _deleted)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", entry.Key.ToString("D", CultureInfo.InvariantCulture));
                    writer.WriteBoolean("confirmed", entry.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    #endregion // Methods
}