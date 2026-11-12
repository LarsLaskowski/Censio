using System.Text.Json;

namespace Censio.Contracts;

/// <summary>
/// Writes contract messages as compact UTF-8 JSON.
/// </summary>
public static class ContractWriter
{
    #region Methods

    /// <summary>
    /// Writes a contract message.
    /// </summary>
    /// <param name="message">The message to write</param>
    /// <returns>The bytes or the refusal</returns>
    public static ContractWriteResult Write(ContractMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var ruleKind = new ContractRules(true).Check(message);

        if (ruleKind is not null)
        {
            return Refuse(ContractError.Create(ruleKind.Value));
        }

        var buffer = new ContractBufferWriter(ContractReader.MaxMessageBytes);

        try
        {
            using var writer = new Utf8JsonWriter(buffer);

            Serialize(writer, message);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return Refuse(buffer.Exceeded ? ContractError.CreateTooLarge() : ContractError.CreateInvalid());
        }

        return new ContractWriteResult(buffer.WrittenMemory, null);
    }

    /// <summary>
    /// Serializes a message with the strict context.
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="message">The message</param>
    private static void Serialize(Utf8JsonWriter writer, ContractMessage message)
    {
        switch (message)
        {
            case ContractHello hello:
                JsonSerializer.Serialize(writer, hello, ContractJsonContext.Default.ContractHello);
                break;

            case ContractHelloAccepted accepted:
                JsonSerializer.Serialize(writer, accepted, ContractJsonContext.Default.ContractHelloAccepted);
                break;

            case ContractHelloRefused refused:
                JsonSerializer.Serialize(writer, refused, ContractJsonContext.Default.ContractHelloRefused);
                break;

            case ContractCatalogUpload upload:
                JsonSerializer.Serialize(writer, upload, ContractJsonContext.Default.ContractCatalogUpload);
                break;

            case ContractIconImage icon:
                JsonSerializer.Serialize(writer, icon, ContractJsonContext.Default.ContractIconImage);
                break;

            case ContractTick tick:
                JsonSerializer.Serialize(writer, tick, ContractJsonContext.Default.ContractTick);
                break;

            case ContractSessionStart start:
                JsonSerializer.Serialize(writer, start, ContractJsonContext.Default.ContractSessionStart);
                break;

            case ContractSessionEnd end:
                JsonSerializer.Serialize(writer, end, ContractJsonContext.Default.ContractSessionEnd);
                break;

            case ContractStatus status:
                JsonSerializer.Serialize(writer, status, ContractJsonContext.Default.ContractStatus);
                break;

            case ContractAcknowledgement acknowledgement:
                JsonSerializer.Serialize(writer, acknowledgement, ContractJsonContext.Default.ContractAcknowledgement);
                break;

            default:
                throw new ArgumentException("The message is of no type of the contract.", nameof(message));
        }
    }

    /// <summary>
    /// Builds the result of a refused message.
    /// </summary>
    /// <param name="error">The error</param>
    /// <returns>The result</returns>
    private static ContractWriteResult Refuse(ContractError error)
    {
        return new ContractWriteResult(ReadOnlyMemory<byte>.Empty, error);
    }

    #endregion // Methods
}