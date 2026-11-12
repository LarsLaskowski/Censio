using System.Security.Cryptography;
using System.Text;

namespace Censio.Contracts;

/// <summary>
/// The bounds and value rules of the contract messages, shared by reader and writer. A message that breaks a rule is
/// invalid, one above a bound too large; too large wins when both apply.
/// </summary>
internal sealed class ContractRules
{
    #region Constants

    /// <summary>
    /// The length of an icon reference or catalog identity: 64 lowercase hexadecimal digits.
    /// </summary>
    private const int HexLength = 64;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The encoding that refuses a lone surrogate.
    /// </summary>
    private static readonly UTF8Encoding _strictEncoding = new(false, true);

    /// <summary>
    /// Whether the message is checked to be written: maps and missing icons then need not be in order, since the writer sorts them.
    /// </summary>
    private readonly bool _forWrite;

    /// <summary>
    /// Whether a value rule is broken.
    /// </summary>
    private bool _invalid;

    /// <summary>
    /// Whether a bound is exceeded.
    /// </summary>
    private bool _tooLarge;

    /// <summary>
    /// The number of entries and map pairs of the tick so far.
    /// </summary>
    private long _entriesAndPairs;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractRules" /> class.
    /// </summary>
    /// <param name="forWrite">Whether the message is checked to be written</param>
    internal ContractRules(bool forWrite)
    {
        _forWrite = forWrite;
    }

    #endregion // Constructors

    #region Static methods

    /// <summary>
    /// Checks a message of the reader for a <c>null</c> element in a list the deserialization lets through.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns><c>true</c> when the message holds a <c>null</c> island or entry</returns>
    internal static bool ContainsNull(ContractMessage message)
    {
        return message is ContractTick tick && tick.Islands.Any(island => island is null || island.Entries.Any(entry => entry is null));
    }

    /// <summary>
    /// Checks a text of 64 lowercase hexadecimal digits.
    /// </summary>
    /// <param name="text">The text</param>
    /// <returns><c>true</c> when the text is one</returns>
    private static bool IsHex(string? text)
    {
        return text is not null && text.Length == HexLength && text.All(char.IsAsciiHexDigitLower);
    }

    /// <summary>
    /// Checks that a float is a value or no value, but not infinity or NaN.
    /// </summary>
    /// <param name="value">The float</param>
    /// <returns><c>true</c> when the float is allowed</returns>
    private static bool IsAllowed(float? value)
    {
        return value is null || float.IsFinite(value.Value);
    }

    #endregion // Static methods

    #region Methods

    /// <summary>
    /// Checks a message.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The kind of the refusal, or <c>null</c> when the message passes</returns>
    internal ContractErrorKind? Check(ContractMessage message)
    {
        switch (message)
        {
            case ContractHello hello:
                {
                    CheckHello(hello);
                }
                break;

            case ContractHelloAccepted accepted:
                {
                    CheckText(accepted.ServerVersion, ContractReader.MaxStringBytes, true);
                    CheckHexOrNull(accepted.CatalogIdentity);
                }
                break;

            case ContractHelloRefused refused:
                {
                    CheckHelloRefused(refused);
                }
                break;

            case ContractCatalogUpload upload:
                {
                    CheckIdentity(upload.Id);
                    CheckBinarySize(upload.Document);
                }
                break;

            case ContractIconImage icon:
                {
                    CheckIcon(icon);
                }
                break;

            case ContractTick tick:
                {
                    CheckTick(tick);
                }
                break;

            case ContractSessionStart start:
                {
                    CheckSessionEvent(start.Id, start.ProfileId, start.ReceiveTime);
                }
                break;

            case ContractSessionEnd end:
                {
                    CheckSessionEvent(end.Id, end.ProfileId, end.ReceiveTime);
                }
                break;

            case ContractStatus status:
                {
                    Require(Enum.IsDefined(status.PipeState));
                }
                break;

            case ContractAcknowledgement acknowledgement:
                {
                    CheckAcknowledgement(acknowledgement);
                }
                break;

            default:
                {
                    _invalid = true;
                }
                break;
        }

        if (_tooLarge)
        {
            return ContractErrorKind.TooLarge;
        }

        return _invalid ? ContractErrorKind.Invalid : null;
    }

    /// <summary>
    /// Records a broken value rule when a condition does not hold.
    /// </summary>
    /// <param name="condition">The condition</param>
    private void Require(bool condition)
    {
        _invalid |= condition == false;
    }

    /// <summary>
    /// Checks that an identity is not all zero.
    /// </summary>
    /// <param name="identity">The identity</param>
    private void CheckIdentity(Guid identity)
    {
        Require(identity != Guid.Empty);
    }

    /// <summary>
    /// Checks that a receive time is UTC.
    /// </summary>
    /// <param name="time">The receive time</param>
    private void CheckTime(DateTimeOffset time)
    {
        Require(time.Offset == TimeSpan.Zero);
    }

    /// <summary>
    /// Checks a text that is well-formed Unicode and within its bound, and not empty when it is required.
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="maxBytes">The most UTF-8 bytes</param>
    /// <param name="required">Whether the text must not be empty</param>
    private void CheckText(string? text, int maxBytes, bool required)
    {
        if (text is null)
        {
            _invalid = true;

            return;
        }

        int length;

        try
        {
            length = _strictEncoding.GetByteCount(text);
        }
        catch (EncoderFallbackException)
        {
            _invalid = true;
            length = Encoding.UTF8.GetByteCount(text);
        }

        _tooLarge |= length > maxBytes;
        Require(required == false || text.Length > 0);
    }

    /// <summary>
    /// Checks a text that is <c>null</c> or 64 lowercase hexadecimal digits.
    /// </summary>
    /// <param name="text">The text</param>
    private void CheckHexOrNull(string? text)
    {
        Require(text is null || IsHex(text));
    }

    /// <summary>
    /// Checks the size of binary data.
    /// </summary>
    /// <param name="data">The data</param>
    private void CheckBinarySize(ReadOnlyMemory<byte> data)
    {
        _tooLarge |= data.Length > ContractReader.MaxBinaryBytes;
    }

    /// <summary>
    /// Checks a hello.
    /// </summary>
    /// <param name="hello">The hello</param>
    private void CheckHello(ContractHello hello)
    {
        if (hello.Token is not null)
        {
            CheckText(hello.Token, ContractReader.MaxStringBytes, true);
        }

        CheckText(hello.CompanionVersion, ContractReader.MaxStringBytes, true);
        CheckIdentity(hello.ProfileId);
        CheckText(hello.ProfileName, ContractReader.MaxStringBytes, true);
    }

    /// <summary>
    /// Checks a refused hello reply.
    /// </summary>
    /// <param name="refused">The reply</param>
    private void CheckHelloRefused(ContractHelloRefused refused)
    {
        Require(Enum.IsDefined(refused.Reason));

        if (refused.SupportedVersions is null)
        {
            _invalid = true;

            return;
        }

        _tooLarge |= refused.SupportedVersions.Count > ContractReader.MaxSupportedVersions;
        Require(refused.SupportedVersions.Count > 0 && refused.SupportedVersions[0] > 0);

        for (var index = 1; index < refused.SupportedVersions.Count; index++)
        {
            Require(refused.SupportedVersions[index] > refused.SupportedVersions[index - 1]);
        }
    }

    /// <summary>
    /// Checks an icon image.
    /// </summary>
    /// <param name="icon">The icon image</param>
    private void CheckIcon(ContractIconImage icon)
    {
        CheckIdentity(icon.Id);
        CheckBinarySize(icon.Png);
        Require(IsHex(icon.Reference) && icon.Png.IsEmpty == false);

        if (_tooLarge == false && _invalid == false)
        {
            Require(string.Equals(Convert.ToHexStringLower(SHA256.HashData(icon.Png.Span)), icon.Reference, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Checks a session start or end.
    /// </summary>
    /// <param name="id">The identity</param>
    /// <param name="profileId">The profile identity</param>
    /// <param name="receiveTime">The receive time</param>
    private void CheckSessionEvent(Guid id, Guid profileId, DateTimeOffset receiveTime)
    {
        CheckIdentity(id);
        CheckIdentity(profileId);
        CheckTime(receiveTime);
    }

    /// <summary>
    /// Checks an acknowledgement.
    /// </summary>
    /// <param name="acknowledgement">The acknowledgement</param>
    private void CheckAcknowledgement(ContractAcknowledgement acknowledgement)
    {
        CheckIdentity(acknowledgement.Id);

        if (acknowledgement.MissingIcons is null)
        {
            _invalid = true;

            return;
        }

        if (acknowledgement.Refusal is { } refusal)
        {
            var needsVersion = refusal is ContractAcknowledgementRefusal.UnsupportedVersion or ContractAcknowledgementRefusal.CatalogUnsupportedVersion;

            Require(Enum.IsDefined(refusal) && acknowledgement.RefusedVersion.HasValue == needsVersion && acknowledgement.MissingIcons.Count == 0);
        }
        else
        {
            Require(acknowledgement.RefusedVersion.HasValue == false);
        }

        CheckMissingIcons(acknowledgement.MissingIcons);
    }

    /// <summary>
    /// Checks the missing icons: 64 lowercase hexadecimal digits each, strictly ascending, so none is repeated.
    /// </summary>
    /// <param name="icons">The icons; the writer sorts them, so for it only repeated ones count</param>
    private void CheckMissingIcons(IReadOnlyList<string> icons)
    {
        _tooLarge |= icons.Count > ContractReader.MaxMissingIcons;

        var ordered = _forWrite ? icons.Order(StringComparer.Ordinal).ToList() : icons;

        for (var index = 0; index < ordered.Count; index++)
        {
            Require(IsHex(ordered[index]) && (index == 0 || string.CompareOrdinal(ordered[index - 1], ordered[index]) < 0));
        }
    }

    /// <summary>
    /// Checks a tick.
    /// </summary>
    /// <param name="tick">The tick</param>
    private void CheckTick(ContractTick tick)
    {
        CheckIdentity(tick.Id);
        CheckIdentity(tick.ProfileId);
        CheckTime(tick.ReceiveTime);

        if (tick.Islands is null)
        {
            _invalid = true;

            return;
        }

        _tooLarge |= tick.Islands.Count > ContractReader.MaxIslands;
        Require(tick.Islands.Count > 0);

        var keys = new HashSet<(int SessionGuid, byte IslandId, byte AreaIndex)>();

        foreach (var island in tick.Islands)
        {
            if (island is null)
            {
                _invalid = true;

                continue;
            }

            CheckIsland(island, keys);
        }

        _tooLarge |= _entriesAndPairs > ContractReader.MaxEntriesAndPairs;
    }

    /// <summary>
    /// Checks an island of a tick: its name, its key among the keys so far and its entries.
    /// </summary>
    /// <param name="island">The island</param>
    /// <param name="keys">The keys of the islands so far</param>
    private void CheckIsland(ContractTickIsland island, HashSet<(int SessionGuid, byte IslandId, byte AreaIndex)> keys)
    {
        CheckText(island.Name, ContractReader.MaxIslandNameBytes, false);
        Require(keys.Add((island.SessionGuid, island.IslandId, island.AreaIndex)));

        if (island.Entries is null)
        {
            _invalid = true;

            return;
        }

        foreach (var entry in island.Entries)
        {
            if (entry is null)
            {
                _invalid = true;

                continue;
            }

            CheckEntry(entry);
        }
    }

    /// <summary>
    /// Checks an entry of a tick: its floats and its maps.
    /// </summary>
    /// <param name="entry">The entry</param>
    private void CheckEntry(ContractTickEntry entry)
    {
        _entriesAndPairs++;

        Require(IsAllowed(entry.Generation)
                && IsAllowed(entry.Consumption)
                && IsAllowed(entry.Delta)
                && IsAllowed(entry.PerfectGeneration)
                && IsAllowed(entry.PerfectConsumption)
                && IsAllowed(entry.Income)
                && IsAllowed(entry.SummedProductivity)
                && IsAllowed(entry.AverageProductivity));

        CheckMap(entry.WorkforceAmounts);
        CheckMap(entry.BuildingAmounts);
    }

    /// <summary>
    /// Checks a map of a tick entry: not <c>null</c>, within the bound and, as the reader built it, in ascending order.
    /// </summary>
    /// <param name="map">The map</param>
    private void CheckMap(IReadOnlyDictionary<int, int>? map)
    {
        if (map is null)
        {
            _invalid = true;

            return;
        }

        _tooLarge |= map.Count > ContractReader.MaxPairsPerMap;
        _entriesAndPairs += map.Count;
        Require(map is not ContractMap { IsAscending: false });
    }

    #endregion // Methods
}