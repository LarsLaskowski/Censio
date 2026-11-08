using System.Text;

namespace Censio.Anonymiser.Tests;

/// <summary>
/// A <see cref="TextWriter" /> whose every write throws an <see cref="IOException" />, like a standard output whose
/// pipe or file is gone.
/// </summary>
public sealed class FailingTextWriter : TextWriter
{
    #region Properties

    /// <summary>
    /// Gets the number of writes tried.
    /// </summary>
    public int WriteCount { get; private set; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Counts a write and creates the exception it throws.
    /// </summary>
    /// <returns>The exception</returns>
    private IOException Fail()
    {
        WriteCount++;

        return new IOException("Simulated broken standard output");
    }

    #endregion // Methods

    #region TextWriter

    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <inheritdoc />
    public override void Write(char value)
    {
        throw Fail();
    }

    /// <inheritdoc />
    public override void Write(string? value)
    {
        throw Fail();
    }

    /// <inheritdoc />
    public override void WriteLine(string? value)
    {
        throw Fail();
    }

    /// <inheritdoc />
    public override Task WriteAsync(char value)
    {
        return Task.FromException(Fail());
    }

    /// <inheritdoc />
    public override Task WriteAsync(string? value)
    {
        return Task.FromException(Fail());
    }

    /// <inheritdoc />
    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        return Task.FromException(Fail());
    }

    /// <inheritdoc />
    public override Task WriteLineAsync(string? value)
    {
        return Task.FromException(Fail());
    }

    /// <inheritdoc />
    public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        return Task.FromException(Fail());
    }

    #endregion // TextWriter
}