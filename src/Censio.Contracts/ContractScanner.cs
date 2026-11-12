using System.Buffers;
using System.Text.Json;
using System.Text.Unicode;

namespace Censio.Contracts;

/// <summary>
/// The syntax pass over a contract message (step 2 of the reader): well-formed JSON of valid UTF-8, string and count bounds
/// by property path, the float range and the contract version and type, before anything is deserialized.
/// </summary>
internal sealed class ContractScanner
{
    #region Constants

    /// <summary>
    /// A container or value of no interest to the pass.
    /// </summary>
    private const int Other = 0;

    /// <summary>
    /// The root object.
    /// </summary>
    private const int Root = 1;

    /// <summary>
    /// The array <c>islands</c> of the root.
    /// </summary>
    private const int Islands = 2;

    /// <summary>
    /// An island: an item of <see cref="Islands" />.
    /// </summary>
    private const int Island = 3;

    /// <summary>
    /// The array <c>entries</c> of an island.
    /// </summary>
    private const int Entries = 4;

    /// <summary>
    /// An entry: an item of <see cref="Entries" />.
    /// </summary>
    private const int Entry = 5;

    /// <summary>
    /// A map of an entry: the array <c>workforceAmounts</c> or <c>buildingAmounts</c>.
    /// </summary>
    private const int Map = 6;

    /// <summary>
    /// A pair: an item of <see cref="Map" />.
    /// </summary>
    private const int Pair = 7;

    /// <summary>
    /// The array <c>supportedVersions</c> of the root.
    /// </summary>
    private const int Versions = 8;

    /// <summary>
    /// The array <c>missingIcons</c> of the root.
    /// </summary>
    private const int Icons = 9;

    /// <summary>
    /// The value of the root property <c>contractVersion</c>.
    /// </summary>
    private const int VersionValue = 10;

    /// <summary>
    /// The value of the root property <c>type</c>.
    /// </summary>
    private const int TypeValue = 11;

    /// <summary>
    /// The value of the root property <c>png</c> or <c>document</c>.
    /// </summary>
    private const int BinaryValue = 12;

    /// <summary>
    /// The value of the property <c>name</c> of an island.
    /// </summary>
    private const int IslandNameValue = 13;

    /// <summary>
    /// The value of one of the eight float properties of an entry.
    /// </summary>
    private const int FloatValue = 14;

    /// <summary>
    /// The number of levels the pass follows: the reader refuses a document deeper than 64 levels.
    /// </summary>
    private const int Levels = 66;

    /// <summary>
    /// The most raw bytes of an escaped string for one unescaped byte: an escape sequence of six bytes stands for one.
    /// </summary>
    private const int RawBytesPerByte = 6;

    /// <summary>
    /// The size of the buffer for escaped strings: copying a string needs room for three UTF-8 bytes per character, and a
    /// string that passes the raw length check has at most as many characters as raw bytes.
    /// </summary>
    private const int BufferSize = ContractReader.MaxStringBytes * RawBytesPerByte * 3;

    /// <summary>
    /// The most characters of a Base64 text: the characters of the largest binary data, padded.
    /// </summary>
    private const int MaxBase64Characters = (ContractReader.MaxBinaryBytes + 2) / 3 * 4;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The UTF-8 names of the message types.
    /// </summary>
    private static readonly byte[][] _typeNames = ContractEnumNames.Create<ContractMessageType>();

    /// <summary>
    /// The message types, in the order of <see cref="_typeNames" />.
    /// </summary>
    private static readonly ContractMessageType[] _types = Enum.GetValues<ContractMessageType>();

    /// <summary>
    /// The UTF-8 names of the eight float properties of an entry.
    /// </summary>
    private static readonly byte[][] _floatNames = [
                                                       "generation"u8.ToArray(),
                                                       "consumption"u8.ToArray(),
                                                       "delta"u8.ToArray(),
                                                       "perfectGeneration"u8.ToArray(),
                                                       "perfectConsumption"u8.ToArray(),
                                                       "income"u8.ToArray(),
                                                       "summedProductivity"u8.ToArray(),
                                                       "averageProductivity"u8.ToArray()
                                                   ];

    /// <summary>
    /// The kind of the container opened at each depth.
    /// </summary>
    private readonly int[] _kinds = new int[Levels];

    /// <summary>
    /// What the value of the last property name is.
    /// </summary>
    private int _next;

    /// <summary>
    /// Whether the first token has been seen.
    /// </summary>
    private bool _started;

    /// <summary>
    /// Whether the root is an object.
    /// </summary>
    private bool _rootIsObject;

    /// <summary>
    /// The number of <c>contractVersion</c> properties of the root.
    /// </summary>
    private int _versionCount;

    /// <summary>
    /// Whether a root <c>contractVersion</c> has a value that is no integer of 32 bits.
    /// </summary>
    private bool _versionBad;

    /// <summary>
    /// The number of <c>type</c> properties of the root.
    /// </summary>
    private int _typeCount;

    /// <summary>
    /// Whether a root <c>type</c> has a value that is no string.
    /// </summary>
    private bool _typeBad;

    /// <summary>
    /// The number of islands so far.
    /// </summary>
    private int _islands;

    /// <summary>
    /// The number of entries and map pairs so far.
    /// </summary>
    private int _entriesAndPairs;

    /// <summary>
    /// The number of pairs of the current map.
    /// </summary>
    private int _pairs;

    /// <summary>
    /// The number of supported versions so far.
    /// </summary>
    private int _versions;

    /// <summary>
    /// The number of missing icons so far.
    /// </summary>
    private int _icons;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the contract version found by <see cref="Scan" />.
    /// </summary>
    internal int Version { get; private set; }

    /// <summary>
    /// Gets the message type found by <see cref="Scan" />, or <c>null</c> when the root <c>type</c> names no message.
    /// </summary>
    internal ContractMessageType? MessageType { get; private set; }

    #endregion // Properties

    #region Static methods

    /// <summary>
    /// Checks the length and the encoding of a string or property name against a bound.
    /// </summary>
    /// <param name="reader">The reader, positioned on the token</param>
    /// <param name="maxBytes">The most UTF-8 bytes after unescaping</param>
    /// <param name="buffer">The buffer for escaped strings</param>
    /// <returns>The error, or <c>null</c></returns>
    private static ContractError? CheckString(ref Utf8JsonReader reader, int maxBytes, byte[] buffer)
    {
        var raw = reader.ValueSpan;

        if (raw.Length > maxBytes * RawBytesPerByte)
        {
            return ContractError.CreateTooLarge();
        }

        if (Utf8.IsValid(raw) == false)
        {
            return ContractError.CreateMalformed();
        }

        if (reader.ValueIsEscaped == false)
        {
            return raw.Length > maxBytes ? ContractError.CreateTooLarge() : null;
        }

        try
        {
            return reader.CopyString(buffer) > maxBytes ? ContractError.CreateTooLarge() : null;
        }
        catch (ArgumentException)
        {
            return ContractError.CreateTooLarge();
        }
        catch (InvalidOperationException)
        {
            return ContractError.CreateMalformed();
        }
    }

    /// <summary>
    /// Checks the Base64 text of binary data against the bound of its characters and of the bytes it decodes to.
    /// </summary>
    /// <param name="reader">The reader, positioned on the string</param>
    /// <returns>The error, or <c>null</c></returns>
    private static ContractError? CheckBinary(ref Utf8JsonReader reader)
    {
        var raw = reader.ValueSpan;

        if (raw.Length > MaxBase64Characters)
        {
            return ContractError.CreateTooLarge();
        }

        if (Utf8.IsValid(raw) == false)
        {
            return ContractError.CreateMalformed();
        }

        if (reader.ValueIsEscaped)
        {
            return null;
        }

        var padding = raw.EndsWith("=="u8) ? 2 : 0;

        padding = raw.EndsWith("="u8) && padding == 0 ? 1 : padding;

        return (raw.Length / 4 * 3) - padding > ContractReader.MaxBinaryBytes ? ContractError.CreateTooLarge() : null;
    }

    /// <summary>
    /// Checks that a number at a float position is a finite float of 32 bits.
    /// </summary>
    /// <param name="reader">The reader, positioned on the number</param>
    /// <returns>The error, or <c>null</c></returns>
    private static ContractError? CheckFloat(ref Utf8JsonReader reader)
    {
        return reader.TryGetSingle(out var value) && float.IsFinite(value) ? null : ContractError.CreateMalformed();
    }

    /// <summary>
    /// Finds what the value of a property of the root is.
    /// </summary>
    /// <param name="reader">The reader, positioned on the property name</param>
    /// <returns>What the value is</returns>
    private static int ClassifyRootProperty(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals("contractVersion"u8))
        {
            return VersionValue;
        }

        if (reader.ValueTextEquals("type"u8))
        {
            return TypeValue;
        }

        if (reader.ValueTextEquals("islands"u8))
        {
            return Islands;
        }

        if (reader.ValueTextEquals("supportedVersions"u8))
        {
            return Versions;
        }

        if (reader.ValueTextEquals("missingIcons"u8))
        {
            return Icons;
        }

        return reader.ValueTextEquals("png"u8) || reader.ValueTextEquals("document"u8) ? BinaryValue : Other;
    }

    /// <summary>
    /// Finds what the value of a property of an island is.
    /// </summary>
    /// <param name="reader">The reader, positioned on the property name</param>
    /// <returns>What the value is</returns>
    private static int ClassifyIslandProperty(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals("name"u8))
        {
            return IslandNameValue;
        }

        return reader.ValueTextEquals("entries"u8) ? Entries : Other;
    }

    /// <summary>
    /// Finds what the value of a property of an entry is.
    /// </summary>
    /// <param name="reader">The reader, positioned on the property name</param>
    /// <returns>What the value is</returns>
    private static int ClassifyEntryProperty(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals("workforceAmounts"u8) || reader.ValueTextEquals("buildingAmounts"u8))
        {
            return Map;
        }

        foreach (var name in _floatNames)
        {
            if (reader.ValueTextEquals(name))
            {
                return FloatValue;
            }
        }

        return Other;
    }

    /// <summary>
    /// Finds the kind of container an item of an array is.
    /// </summary>
    /// <param name="parent">The kind of the array</param>
    /// <param name="type">The token type of the item</param>
    /// <returns>The kind of the item</returns>
    private static int ItemKind(int parent, JsonTokenType type)
    {
        return (parent, type) switch
               {
                   (Islands, JsonTokenType.StartObject) => Island,
                   (Entries, JsonTokenType.StartObject) => Entry,
                   (Map, JsonTokenType.StartArray) => Pair,
                   _ => Other
               };
    }

    #endregion // Static methods

    #region Methods

    /// <summary>
    /// Scans a message.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON bytes</param>
    /// <returns>The error, or <c>null</c> when the message passes and <see cref="Version" /> and <see cref="MessageType" /> are set</returns>
    internal ContractError? Scan(ReadOnlySpan<byte> utf8Json)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            var reader = new Utf8JsonReader(utf8Json);

            while (reader.Read())
            {
                var error = Visit(ref reader, buffer);

                if (error is not null)
                {
                    return error;
                }
            }
        }
        catch (JsonException)
        {
            return ContractError.CreateMalformed();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, true);
        }

        return _rootIsObject && _versionCount == 1 && _versionBad == false && _typeCount == 1 && _typeBad == false ? null : ContractError.CreateMalformed();
    }

    /// <summary>
    /// Checks one token.
    /// </summary>
    /// <param name="reader">The reader, positioned on the token</param>
    /// <param name="buffer">The buffer for escaped strings</param>
    /// <returns>The error, or <c>null</c></returns>
    private ContractError? Visit(ref Utf8JsonReader reader, byte[] buffer)
    {
        var type = reader.TokenType;

        if (type == JsonTokenType.PropertyName)
        {
            return VisitPropertyName(ref reader, buffer);
        }

        if (type is JsonTokenType.EndObject or JsonTokenType.EndArray)
        {
            return null;
        }

        return VisitValue(ref reader, buffer);
    }

    /// <summary>
    /// Checks a property name and finds what its value is.
    /// </summary>
    /// <param name="reader">The reader, positioned on the property name</param>
    /// <param name="buffer">The buffer for escaped strings</param>
    /// <returns>The error, or <c>null</c></returns>
    private ContractError? VisitPropertyName(ref Utf8JsonReader reader, byte[] buffer)
    {
        var error = CheckString(ref reader, ContractReader.MaxStringBytes, buffer);

        if (error is not null)
        {
            return error;
        }

        _next = _kinds[reader.CurrentDepth - 1] switch
                {
                    Root => ClassifyRootProperty(ref reader),
                    Island => ClassifyIslandProperty(ref reader),
                    Entry => ClassifyEntryProperty(ref reader),
                    _ => Other
                };

        _versionCount += _next == VersionValue ? 1 : 0;
        _typeCount += _next == TypeValue ? 1 : 0;

        return null;
    }

    /// <summary>
    /// Checks a value: counts it when it is an item, follows the container it opens and checks the string or number.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value</param>
    /// <param name="buffer">The buffer for escaped strings</param>
    /// <returns>The error, or <c>null</c></returns>
    private ContractError? VisitValue(ref Utf8JsonReader reader, byte[] buffer)
    {
        var type = reader.TokenType;
        int role;
        int kind;

        if (_started)
        {
            var error = Place(_kinds[reader.CurrentDepth - 1], type, out role, out kind);

            if (error is not null)
            {
                return error;
            }
        }
        else
        {
            _started = true;
            _rootIsObject = type == JsonTokenType.StartObject;
            role = Other;
            kind = _rootIsObject ? Root : Other;
        }

        if (type is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            Open(reader.CurrentDepth, kind, role, type);

            return null;
        }

        return CheckScalar(ref reader, role, buffer);
    }

    /// <summary>
    /// Finds what a value is by the container it stands in, and counts it when it is an item of an array.
    /// </summary>
    /// <param name="parent">The kind of the container</param>
    /// <param name="type">The token type of the value</param>
    /// <param name="role">What the value is</param>
    /// <param name="kind">The kind of container the value makes of the position, when it opens one</param>
    /// <returns>The error when a count bound is exceeded, or <c>null</c></returns>
    private ContractError? Place(int parent, JsonTokenType type, out int role, out int kind)
    {
        if (parent is Root or Island or Entry)
        {
            role = _next;
            kind = role is Islands or Entries or Map or Versions or Icons ? role : Other;
            _next = Other;

            return null;
        }

        role = Other;
        kind = ItemKind(parent, type);

        return CountItem(parent);
    }

    /// <summary>
    /// Follows the container a token opens.
    /// </summary>
    /// <param name="depth">The depth of the token</param>
    /// <param name="kind">The kind of container the position makes it</param>
    /// <param name="role">What the value is</param>
    /// <param name="type">The token type: start of an object or of an array</param>
    private void Open(int depth, int kind, int role, JsonTokenType type)
    {
        var isArray = type == JsonTokenType.StartArray;
        var isArrayKind = kind is Islands or Entries or Map or Versions or Icons;
        var isObjectKind = kind is Root or Island or Entry;

        _kinds[depth] = (isArray && isArrayKind) || (isArray == false && isObjectKind) ? kind : Other;

        if (_kinds[depth] == Map)
        {
            _pairs = 0;
        }

        _versionBad |= role == VersionValue;
        _typeBad |= role == TypeValue;
    }

    /// <summary>
    /// Counts an item of an array against the bound of its array.
    /// </summary>
    /// <param name="parent">The kind of the array</param>
    /// <returns>The error when a count bound is exceeded, or <c>null</c></returns>
    private ContractError? CountItem(int parent)
    {
        var exceeded = false;

        switch (parent)
        {
            case Islands:
                exceeded = ++_islands > ContractReader.MaxIslands;
                break;

            case Entries:
                exceeded = ++_entriesAndPairs > ContractReader.MaxEntriesAndPairs;
                break;

            case Map:
                exceeded = ++_entriesAndPairs > ContractReader.MaxEntriesAndPairs || ++_pairs > ContractReader.MaxPairsPerMap;
                break;

            case Versions:
                exceeded = ++_versions > ContractReader.MaxSupportedVersions;
                break;

            case Icons:
                exceeded = ++_icons > ContractReader.MaxMissingIcons;
                break;
        }

        return exceeded ? ContractError.CreateTooLarge() : null;
    }

    /// <summary>
    /// Checks a string or number by what it is.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value</param>
    /// <param name="role">What the value is</param>
    /// <param name="buffer">The buffer for escaped strings</param>
    /// <returns>The error, or <c>null</c></returns>
    private ContractError? CheckScalar(ref Utf8JsonReader reader, int role, byte[] buffer)
    {
        var type = reader.TokenType;

        switch (role)
        {
            case VersionValue:
                NoteVersion(ref reader);
                break;

            case TypeValue:
                NoteType(ref reader);
                break;

            case FloatValue when type == JsonTokenType.Number:
                return CheckFloat(ref reader);

            case BinaryValue when type == JsonTokenType.String:
                return CheckBinary(ref reader);
        }

        if (type != JsonTokenType.String)
        {
            return null;
        }

        return CheckString(ref reader, role == IslandNameValue ? ContractReader.MaxIslandNameBytes : ContractReader.MaxStringBytes, buffer);
    }

    /// <summary>
    /// Notes the value of the root <c>contractVersion</c>: an integer of 32 bits without fraction or exponent.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value</param>
    private void NoteVersion(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.ValueSpan.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0 && reader.TryGetInt32(out var version))
        {
            Version = version;
        }
        else
        {
            _versionBad = true;
        }
    }

    /// <summary>
    /// Notes the value of the root <c>type</c>: a string, which names a message type or none.
    /// </summary>
    /// <param name="reader">The reader, positioned on the value</param>
    private void NoteType(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            _typeBad = true;

            return;
        }

        MessageType = null;

        for (var index = 0; index < _typeNames.Length; index++)
        {
            if (reader.ValueTextEquals(_typeNames[index]))
            {
                MessageType = _types[index];
            }
        }
    }

    #endregion // Methods
}