using System.Globalization;

namespace Censio.Pipe;

/// <summary>
/// Describes why a frame or a message could not be read; it never carries payload text.
/// </summary>
public sealed class PipeError
{
    #region Properties

    /// <summary>
    /// Gets the reason of the error.
    /// </summary>
    public required PipeErrorKind Kind { get; init; }

    /// <summary>
    /// Gets the announced frame length, when known.
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// Gets the type byte of the frame, when known.
    /// </summary>
    public byte? TypeValue { get; init; }

    /// <summary>
    /// Gets the position of the field that did not fit, when known.
    /// </summary>
    public int? Offset { get; init; }

    /// <summary>
    /// Gets the protocol version the pipe announced, when known.
    /// </summary>
    public int? ReceivedVersion { get; init; }

    /// <summary>
    /// Gets the protocol version this reader expects, when relevant.
    /// </summary>
    public int? ExpectedVersion { get; init; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Adds a <c>name=value</c> entry when the value is set.
    /// </summary>
    /// <param name="fields">The entries</param>
    /// <param name="name">The field name</param>
    /// <param name="value">The value</param>
    private static void AddField(List<string> fields, string name, int? value)
    {
        if (value.HasValue)
        {
            fields.Add(string.Create(CultureInfo.InvariantCulture, $"{name}={value.Value}"));
        }
    }

    #endregion // Methods

    #region Object

    /// <inheritdoc />
    public override string ToString()
    {
        var fields = new List<string>();

        AddField(fields, "length", Length);
        AddField(fields, "type", TypeValue);
        AddField(fields, "offset", Offset);
        AddField(fields, "received", ReceivedVersion);
        AddField(fields, "expected", ExpectedVersion);

        return fields.Count == 0 ? Kind.ToString() : $"{Kind}: {string.Join(", ", fields)}";
    }

    #endregion // Object
}