using System.Text.Json;

namespace Censio.Contracts;

/// <summary>
/// Reads contract messages strictly and within bounds.
/// </summary>
public static class ContractReader
{
    #region Constants

    /// <summary>
    /// The only contract version this build reads and writes.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The largest message in bytes.
    /// </summary>
    public const int MaxMessageBytes = 25165824;

    /// <summary>
    /// The largest binary payload in bytes after Base64 decoding.
    /// </summary>
    public const int MaxBinaryBytes = 17825792;

    /// <summary>
    /// The largest string or property name in UTF-8 bytes.
    /// </summary>
    public const int MaxStringBytes = 1024;

    /// <summary>
    /// The largest island name in UTF-8 bytes.
    /// </summary>
    public const int MaxIslandNameBytes = 765;

    /// <summary>
    /// The most islands in a tick.
    /// </summary>
    public const int MaxIslands = 256;

    /// <summary>
    /// The most entries and map pairs in a tick.
    /// </summary>
    public const int MaxEntriesAndPairs = 524288;

    /// <summary>
    /// The most pairs in a map.
    /// </summary>
    public const int MaxPairsPerMap = 1024;

    /// <summary>
    /// The most supported versions in a refusal.
    /// </summary>
    public const int MaxSupportedVersions = 16;

    /// <summary>
    /// The most missing icons in an acknowledgement.
    /// </summary>
    public const int MaxMissingIcons = 16384;

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads a contract message.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON bytes of the message</param>
    /// <returns>The message or the refusal</returns>
    public static ContractReadResult Read(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length > MaxMessageBytes)
        {
            return Refuse(ContractError.CreateTooLarge());
        }

        var scanner = new ContractScanner();
        var scanError = scanner.Scan(utf8Json);

        if (scanError is not null)
        {
            return Refuse(scanError);
        }

        if (scanner.Version != CurrentVersion)
        {
            return ReadOtherVersion(utf8Json, scanner);
        }

        if (scanner.MessageType is null)
        {
            return Refuse(ContractError.CreateMalformed());
        }

        var message = Deserialize(utf8Json, scanner.MessageType.Value);

        if (message is null || ContractRules.ContainsNull(message))
        {
            return Refuse(ContractError.CreateMalformed());
        }

        var ruleKind = new ContractRules(false).Check(message);

        if (ruleKind is not null)
        {
            return new ContractReadResult(null, ContractError.Create(ruleKind.Value), GetRefusedId(message));
        }

        return message is ContractCatalogUpload upload ? CheckCatalog(upload) : new ContractReadResult(message, null);
    }

    /// <summary>
    /// Reads a message of a contract version other than the current one: a refused hello reply of the frozen form is read,
    /// anything else is refused as an unsupported version.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON bytes of the message</param>
    /// <param name="scanner">The scanner that passed the message</param>
    /// <returns>The reply or the refusal</returns>
    private static ContractReadResult ReadOtherVersion(ReadOnlySpan<byte> utf8Json, ContractScanner scanner)
    {
        if (scanner.MessageType == ContractMessageType.HelloRefused)
        {
            var refused = Deserialize(utf8Json, ContractMessageType.HelloRefused);

            if (refused is not null && new ContractRules(false).Check(refused) is null)
            {
                return new ContractReadResult(refused, null);
            }
        }

        return Refuse(ContractError.CreateUnsupportedVersion(scanner.Version));
    }

    /// <summary>
    /// Reads the catalog document of an upload with the catalog reader.
    /// </summary>
    /// <param name="upload">The upload</param>
    /// <returns>The upload or the refusal that carries the catalog's error</returns>
    private static ContractReadResult CheckCatalog(ContractCatalogUpload upload)
    {
        var catalog = CatalogDocumentReader.Read(upload.Document.Span);

        return catalog.Error is null
                   ? new ContractReadResult(upload, null, null, catalog.Document)
                   : new ContractReadResult(null, ContractError.CreateCatalogRefused(catalog.Error), GetRefusedId(upload));
    }

    /// <summary>
    /// Gets the usable <c>id</c> of a built message that carries one.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>The <c>id</c>, or <c>null</c> when the type carries none or the <c>id</c> is all zero</returns>
    private static Guid? GetRefusedId(ContractMessage message)
    {
        var id = message switch
                 {
                     ContractTick tick => tick.Id,
                     ContractSessionStart sessionStart => sessionStart.Id,
                     ContractSessionEnd sessionEnd => sessionEnd.Id,
                     ContractCatalogUpload catalogUpload => catalogUpload.Id,
                     ContractIconImage iconImage => iconImage.Id,
                     _ => Guid.Empty
                 };

        return id == Guid.Empty ? null : id;
    }

    /// <summary>
    /// Deserializes the message of a type with the strict context.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 JSON bytes of the message</param>
    /// <param name="type">The message type the syntax pass found</param>
    /// <returns>The message, or <c>null</c> when it breaks the structure of its type</returns>
    private static ContractMessage? Deserialize(ReadOnlySpan<byte> utf8Json, ContractMessageType type)
    {
        try
        {
            return type switch
                   {
                       ContractMessageType.Hello => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractHello),
                       ContractMessageType.HelloAccepted => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractHelloAccepted),
                       ContractMessageType.HelloRefused => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractHelloRefused),
                       ContractMessageType.CatalogUpload => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractCatalogUpload),
                       ContractMessageType.IconImage => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractIconImage),
                       ContractMessageType.Tick => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractTick),
                       ContractMessageType.SessionStart => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractSessionStart),
                       ContractMessageType.SessionEnd => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractSessionEnd),
                       ContractMessageType.Status => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractStatus),
                       ContractMessageType.Acknowledgement => JsonSerializer.Deserialize(utf8Json, ContractJsonContext.Default.ContractAcknowledgement),
                       _ => null
                   };
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the result of a refused message.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    private static ContractReadResult Refuse(ContractError error)
    {
        return new ContractReadResult(null, error);
    }

    #endregion // Methods
}