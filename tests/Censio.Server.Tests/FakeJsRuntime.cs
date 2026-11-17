using Microsoft.JSInterop;

namespace Censio.Server.Tests;

/// <summary>
/// An <see cref="IJSRuntime" /> set by the test: it answers every call with one value or throws one exception, and records
/// the identifier and the arguments of each call.
/// </summary>
public sealed class FakeJsRuntime : IJSRuntime
{
    #region Fields

    /// <summary>
    /// The recorded calls.
    /// </summary>
    private readonly List<(string Identifier, object?[] Arguments)> _calls = [];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the value every call answers with.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Gets or sets the exception every call throws, or <see langword="null" /> for none.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// Gets the recorded calls, in order.
    /// </summary>
    public IReadOnlyList<(string Identifier, object?[] Arguments)> Calls => _calls;

    #endregion // Properties

    #region IJSRuntime

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        _calls.Add((identifier, args ?? []));

        if (Failure is { } failure)
        {
            throw failure;
        }

        return ValueTask.FromResult((TValue)Result!);
    }

    #endregion // IJSRuntime
}