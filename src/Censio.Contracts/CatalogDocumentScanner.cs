using System.Buffers;
using System.Text.Json;
using System.Text.Unicode;

namespace Censio.Contracts;

/// <summary>
/// The structural pass over a catalog document (step 2 of the reader): well-formed JSON, string bounds, count bounds
/// and the schema version, before anything is deserialized.
/// </summary>
internal sealed class CatalogDocumentScanner
{
    #region Constants

    /// <summary>
    /// The depth of the root's own properties.
    /// </summary>
    private const int RootPropertyDepth = 1;

    /// <summary>
    /// The depth of the items of the root's entries array.
    /// </summary>
    private const int EntryDepth = 2;

    /// <summary>
    /// The depth of the properties of an entry.
    /// </summary>
    private const int EntryPropertyDepth = 3;

    /// <summary>
    /// The depth of the names of an entry and of the items of its region types.
    /// </summary>
    private const int EntryChildDepth = 4;

    /// <summary>
    /// The marker of the names property of an entry.
    /// </summary>
    private const int NamesMarker = 1;

    /// <summary>
    /// The marker of the region types property of an entry.
    /// </summary>
    private const int RegionTypesMarker = 2;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// Whether the first token has been seen.
    /// </summary>
    private bool _started;

    /// <summary>
    /// Whether the root is an object.
    /// </summary>
    private bool _rootIsObject;

    /// <summary>
    /// The number of <c>schemaVersion</c> properties of the root.
    /// </summary>
    private int _versionCount;

    /// <summary>
    /// Whether the next token is the value of a root <c>schemaVersion</c>.
    /// </summary>
    private bool _expectVersion;

    /// <summary>
    /// Whether a root <c>schemaVersion</c> has a value that is no 32-bit integer.
    /// </summary>
    private bool _versionBad;

    /// <summary>
    /// The schema version found.
    /// </summary>
    private int _version;

    /// <summary>
    /// Whether the last property of the root is <c>entries</c> and its value has not begun.
    /// </summary>
    private bool _entriesPending;

    /// <summary>
    /// Whether the reader is inside the root's entries array.
    /// </summary>
    private bool _inEntries;

    /// <summary>
    /// The marker of the last property of the current entry, or 0.
    /// </summary>
    private int _entryProperty;

    /// <summary>
    /// Whether the reader is inside the names object of an entry.
    /// </summary>
    private bool _inNames;

    /// <summary>
    /// Whether the reader is inside the region types array of an entry.
    /// </summary>
    private bool _inRegionTypes;

    /// <summary>
    /// The number of entries so far.
    /// </summary>
    private int _entries;

    /// <summary>
    /// The number of names of the current entry.
    /// </summary>
    private int _names;

    /// <summary>
    /// The number of region types of the current entry.
    /// </summary>
    private int _regionTypes;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets the schema version found by <see cref="Scan" />.
    /// </summary>
    internal int Version => _version;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Checks the length and the encoding of a string or property name.
    /// </summary>
    /// <param name="reader">The reader, positioned on the token</param>
    /// <param name="buffer">The buffer for escaped strings</param>
    /// <returns>The error, or <c>null</c></returns>
    internal static CatalogDocumentError? CheckString(ref Utf8JsonReader reader, byte[] buffer)
    {
        if (reader.ValueIsEscaped == false)
        {
            var span = reader.ValueSpan;

            if (span.Length > CatalogDocumentReader.MaxStringBytes)
            {
                return CatalogDocumentError.CreateTooLarge();
            }

            return Utf8.IsValid(span) ? null : CatalogDocumentError.CreateMalformed();
        }

        try
        {
            var length = reader.CopyString(buffer.AsSpan(0, CatalogDocumentReader.MaxStringBytes));

            return Utf8.IsValid(buffer.AsSpan(0, length)) ? null : CatalogDocumentError.CreateMalformed();
        }
        catch (ArgumentException)
        {
            return CatalogDocumentError.CreateTooLarge();
        }
        catch (InvalidOperationException)
        {
            return CatalogDocumentError.CreateMalformed();
        }
    }

    /// <summary>
    /// Scans a document.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON bytes</param>
    /// <returns>The error, or <c>null</c> when the document passes the pass and <see cref="Version" /> holds its version</returns>
    internal CatalogDocumentError? Scan(ReadOnlySpan<byte> utf8Json)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(CatalogDocumentReader.MaxStringBytes);

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
            return CatalogDocumentError.CreateMalformed();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, true);
        }

        if (_rootIsObject == false || _versionCount != 1 || _versionBad)
        {
            return CatalogDocumentError.CreateMalformed();
        }

        return null;
    }

    /// <summary>
    /// Checks one token.
    /// </summary>
    /// <param name="reader">The reader, positioned on the token</param>
    /// <param name="buffer">The buffer for escaped strings</param>
    /// <returns>The error, or <c>null</c></returns>
    private CatalogDocumentError? Visit(ref Utf8JsonReader reader, byte[] buffer)
    {
        var type = reader.TokenType;

        if (type is JsonTokenType.String or JsonTokenType.PropertyName)
        {
            var stringError = CheckString(ref reader, buffer);

            if (stringError is not null)
            {
                return stringError;
            }
        }

        if (_started == false)
        {
            _started = true;
            _rootIsObject = type == JsonTokenType.StartObject;
        }

        if (_expectVersion)
        {
            _expectVersion = false;

            if (type == JsonTokenType.Number && reader.TryGetInt32(out var version))
            {
                _version = version;
            }
            else
            {
                _versionBad = true;
            }
        }

        return Track(ref reader, type);
    }

    /// <summary>
    /// Follows the position of the token in the document and counts entries, names and region types.
    /// </summary>
    /// <param name="reader">The reader, positioned on the token</param>
    /// <param name="type">The token type</param>
    /// <returns>The error when a count bound is exceeded, or <c>null</c></returns>
    private CatalogDocumentError? Track(ref Utf8JsonReader reader, JsonTokenType type)
    {
        var depth = reader.CurrentDepth;
        var isEnd = type is JsonTokenType.EndObject or JsonTokenType.EndArray;
        var exceeded = false;

        switch (depth)
        {
            case RootPropertyDepth:
                TrackRoot(ref reader, type);
                break;

            case EntryDepth when _inEntries && isEnd == false:
                exceeded = CountEntry();
                break;

            case EntryPropertyDepth when _inEntries:
                TrackEntryProperty(ref reader, type);
                break;

            case EntryChildDepth:
                exceeded = CountChild(type);
                break;
        }

        return exceeded ? CatalogDocumentError.CreateTooLarge() : null;
    }

    /// <summary>
    /// Follows the properties of the root.
    /// </summary>
    /// <param name="reader">The reader, positioned on the token</param>
    /// <param name="type">The token type</param>
    private void TrackRoot(ref Utf8JsonReader reader, JsonTokenType type)
    {
        switch (type)
        {
            case JsonTokenType.PropertyName:
                {
                    _entriesPending = reader.ValueTextEquals("entries"u8);

                    if (reader.ValueTextEquals("schemaVersion"u8))
                    {
                        _versionCount++;
                        _expectVersion = true;
                    }
                }
                break;

            case JsonTokenType.StartArray:
                {
                    _inEntries = _entriesPending;
                    _entriesPending = false;
                }
                break;

            case JsonTokenType.EndArray:
                {
                    _inEntries = false;
                }
                break;

            default:
                {
                    _entriesPending = false;
                }
                break;
        }
    }

    /// <summary>
    /// Counts an item of the entries array and resets the counts of the entry.
    /// </summary>
    /// <returns><c>true</c> when the bound for entries is exceeded</returns>
    private bool CountEntry()
    {
        _entries++;
        _names = 0;
        _regionTypes = 0;
        _entryProperty = 0;
        _inNames = false;
        _inRegionTypes = false;

        return _entries > CatalogDocumentReader.MaxEntries;
    }

    /// <summary>
    /// Follows the properties of an entry.
    /// </summary>
    /// <param name="reader">The reader, positioned on the token</param>
    /// <param name="type">The token type</param>
    private void TrackEntryProperty(ref Utf8JsonReader reader, JsonTokenType type)
    {
        switch (type)
        {
            case JsonTokenType.PropertyName:
                {
                    if (reader.ValueTextEquals("names"u8))
                    {
                        _entryProperty = NamesMarker;
                    }
                    else
                    {
                        _entryProperty = reader.ValueTextEquals("regionTypes"u8) ? RegionTypesMarker : 0;
                    }
                }
                break;

            case JsonTokenType.StartObject:
                {
                    _inNames = _entryProperty == NamesMarker;
                    _entryProperty = 0;
                }
                break;

            case JsonTokenType.StartArray:
                {
                    _inRegionTypes = _entryProperty == RegionTypesMarker;
                    _entryProperty = 0;
                }
                break;

            case JsonTokenType.EndObject:
                {
                    _inNames = false;
                }
                break;

            case JsonTokenType.EndArray:
                {
                    _inRegionTypes = false;
                }
                break;

            default:
                {
                    _entryProperty = 0;
                }
                break;
        }
    }

    /// <summary>
    /// Counts a name or a region type of the current entry.
    /// </summary>
    /// <param name="type">The token type</param>
    /// <returns><c>true</c> when a bound is exceeded</returns>
    private bool CountChild(JsonTokenType type)
    {
        if (_inNames && type == JsonTokenType.PropertyName)
        {
            _names++;

            return _names > CatalogDocumentReader.MaxNamesPerEntry;
        }

        if (_inRegionTypes && type is not (JsonTokenType.EndObject or JsonTokenType.EndArray))
        {
            _regionTypes++;

            return _regionTypes > CatalogDocumentReader.MaxRegionTypesPerEntry;
        }

        return false;
    }

    #endregion // Methods
}