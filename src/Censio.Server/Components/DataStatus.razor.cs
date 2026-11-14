using System.Globalization;
using System.Text.Json;

using Censio.Contracts;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Censio.Server.Components;

/// <summary>
/// The Companion banner and the as-of line of a data page.
/// </summary>
public sealed partial class DataStatus : IDisposable
{
    #region Constants

    /// <summary>
    /// The greatest UTC offset in minutes the browser may give, in either direction.
    /// </summary>
    private const int MaxOffsetMinutes = 840;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// Cancels the call to the browser when the component is disposed.
    /// </summary>
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>
    /// The UTC offset the browser gave for the shown instant, or <see langword="null" /> for none.
    /// </summary>
    private int? _offsetMinutes;

    /// <summary>
    /// The instant the browser was last asked about, or <see langword="null" /> for none.
    /// </summary>
    private DateTimeOffset? _askedInstant;

    /// <summary>
    /// A value that indicates whether the component is disposed.
    /// </summary>
    private volatile bool _isDisposed;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the receive time of the shown data, or <see langword="null"/> for none.
    /// </summary>
    [Parameter]
    public DateTimeOffset? AsOf { get; set; }

    /// <summary>
    /// Gets or sets the texts of the language shown.
    /// </summary>
    [CascadingParameter]
    public UiTexts Texts { get; set; } = UiTexts.For(UiLanguage.English);

    /// <summary>
    /// Gets or sets the server state.
    /// </summary>
    [Inject]
    private ServerState State { get; set; } = null!;

    /// <summary>
    /// Gets or sets the JavaScript runtime.
    /// </summary>
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    /// <summary>
    /// Gets the banner text for the Companion's state, or <see langword="null" /> for no banner.
    /// </summary>
    private string? Banner
    {
        get
        {
            var companion = State.CompanionState;

            if (companion.IsConnected == false)
            {
                return Texts.CompanionNotConnected;
            }

            return companion.PipeState switch
                   {
                       ContractPipeState.WaitingForGame => Texts.GameNotRunning,
                       ContractPipeState.PipeBusy or ContractPipeState.ProtocolMismatch or ContractPipeState.Error => Texts.StatisticsUnreadable,
                       _ => null
                   };
        }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Re-renders when the Companion's state changes.
    /// </summary>
    /// <param name="sender">The server state</param>
    /// <param name="arguments">The notice</param>
    private void OnStateChanged(object? sender, ServerStateChangedEventArgs arguments)
    {
        if (_isDisposed || arguments.Kind != ServerStateChangeKind.Companion)
        {
            return;
        }

        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Asks the browser for its UTC offset at the shown instant. The answer is untrusted: an offset out of range, a failed
    /// call and an answer of the wrong type leave the time in UTC.
    /// </summary>
    /// <param name="instant">The shown instant</param>
    /// <returns>A task that completes when the browser answered</returns>
    private async Task AskOffsetAsync(DateTimeOffset instant)
    {
        try
        {
            var offset = await JsRuntime.InvokeAsync<int>("censio.utcOffsetMinutes", _cancellation.Token, instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)).ConfigureAwait(true);

            _offsetMinutes = offset is >= -MaxOffsetMinutes and <= MaxOffsetMinutes
                                 ? offset
                                 : null;
        }
        catch (Exception exception) when (exception is JSException or JsonException or JSDisconnectedException or OperationCanceledException)
        {
            _offsetMinutes = null;
        }
    }

    #endregion // Methods

    #region ComponentBase

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        State.Changed += OnStateChanged;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (AsOf is not { } instant || instant == _askedInstant)
        {
            return;
        }

        _askedInstant = instant;

        await AskOffsetAsync(instant).ConfigureAwait(true);

        if (_isDisposed == false)
        {
            StateHasChanged();
        }
    }

    #endregion // ComponentBase

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _isDisposed = true;
        State.Changed -= OnStateChanged;
        _cancellation.Cancel();
        _cancellation.Dispose();
    }

    #endregion // IDisposable
}