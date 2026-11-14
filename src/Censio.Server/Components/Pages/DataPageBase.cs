using Censio.Server.Data;

using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The shared behavior of the data pages: reads the view of its address through the <see cref="PageReader" /> and follows
/// the server state, keeping nothing but the view.
/// </summary>
public abstract class DataPageBase : ComponentBase, IDisposable
{
    #region Fields

    /// <summary>
    /// The cancellation source of the page's reads.
    /// </summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>
    /// The token of <see cref="_cancellation" />, read once so that no notice reads a disposed source.
    /// </summary>
    private CancellationToken _cancellationToken;

    /// <summary>
    /// A value that indicates whether the page is disposed.
    /// </summary>
    private volatile bool _isDisposed;

    /// <summary>
    /// A value that indicates whether a read is in flight.
    /// </summary>
    private bool _isReading;

    /// <summary>
    /// A value that indicates whether a notice or a new address arrived during a read.
    /// </summary>
    private bool _isPending;

    /// <summary>
    /// The address the page last read, or <see langword="null" /> before the first.
    /// </summary>
    private object? _addressKey;

    /// <summary>
    /// The profile the page last read, <see cref="Guid.Empty" /> for none.
    /// </summary>
    private Guid _readProfile;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the language shown.
    /// </summary>
    [CascadingParameter]
    public UiLanguage Language { get; set; }

    /// <summary>
    /// Gets or sets the texts of the language shown.
    /// </summary>
    [CascadingParameter]
    public UiTexts Texts { get; set; } = UiTexts.For(UiLanguage.English);

    /// <summary>
    /// Gets or sets the request the page is rendered for; its abort cancels the page's reads during prerendering.
    /// </summary>
    [CascadingParameter]
    public HttpContext? HttpContext { get; set; }

    /// <summary>
    /// Gets the shown view, or <see langword="null" /> when the page shows none.
    /// </summary>
    protected ProfileView? View { get; private set; }

    /// <summary>
    /// Gets what the page shows.
    /// </summary>
    protected PageMode Mode { get; private set; } = PageMode.Loading;

    /// <summary>
    /// Gets the text that replaces the figures, or <see langword="null" /> while the page shows them.
    /// </summary>
    protected string? StatusText => Mode switch
                                    {
                                        PageMode.Loading => Texts.Loading,
                                        PageMode.NoData => Texts.NoData,
                                        PageMode.NotFound => Texts.NotFound,
                                        PageMode.Failed => Texts.ReadFailed,
                                        _ => null
                                    };

    /// <summary>
    /// Gets the key of the page's address: two keys of the same address are equal.
    /// </summary>
    protected abstract object AddressKey { get; }

    /// <summary>
    /// Gets a value that indicates whether the page shows the newest profile and follows it.
    /// </summary>
    protected virtual bool FollowsNewestProfile => false;

    /// <summary>
    /// Gets or sets the server state.
    /// </summary>
    [Inject]
    protected ServerState State { get; set; } = null!;

    /// <summary>
    /// Gets or sets the page reader.
    /// </summary>
    [Inject]
    protected PageReader Reader { get; set; } = null!;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Resolves the address into what to read.
    /// </summary>
    /// <param name="profileId">The profile to read</param>
    /// <param name="island">The island whose goods to read, or <see langword="null" /> for none</param>
    /// <returns><see cref="PageMode.Loading" /> when there is a profile to read, otherwise the mode to show instead</returns>
    protected abstract PageMode ResolveTarget(out Guid profileId, out IslandKey? island);

    /// <summary>
    /// Checks that the profile's view holds what the address names.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns><see langword="true" /> when the view holds it</returns>
    protected abstract bool IsKnown(ProfileView view);

    /// <summary>
    /// Gives the shown name of the profile.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns>The name</returns>
    protected string ProfileLabel(ProfileView view)
    {
        var profiles = State.Profiles.Where(profile => profile.Id != view.ProfileId)
                                     .Append(new StoredProfile
                                             {
                                                 Id = view.ProfileId,
                                                 Name = view.ProfileName
                                             })
                                     .ToList();

        return PageNames.Profiles(profiles, Language, profiles.Count).First(profile => profile.Key == view.ProfileId).Value;
    }

    /// <summary>
    /// Reads, then reads again while notices arrived during the read.
    /// </summary>
    /// <returns>A task that completes when the page shows the newest state</returns>
    private async Task LoadAsync()
    {
        if (_isReading)
        {
            _isPending = true;

            return;
        }

        _isReading = true;

        try
        {
            do
            {
                _isPending = false;

                await ReadOnceAsync().ConfigureAwait(true);
            }
            while (_isPending && _cancellationToken.IsCancellationRequested == false);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            // The page is disposed: nothing to show
        }
        finally
        {
            _isReading = false;
        }
    }

    /// <summary>
    /// Reads the profile of the address once and shows the result.
    /// </summary>
    /// <returns>A task that completes when the result is shown</returns>
    private async Task ReadOnceAsync()
    {
        var target = ResolveTarget(out var profileId, out var island);

        _readProfile = target == PageMode.Loading ? profileId : Guid.Empty;

        if (target != PageMode.Loading)
        {
            Show(target, null);

            return;
        }

        if (View is not null && (View.ProfileId != profileId || View.Island != island))
        {
            View = null;
        }

        if (View is null)
        {
            Show(PageMode.Loading, null);
        }

        var read = await Reader.ReadAsync(profileId, island, _cancellationToken).ConfigureAwait(true);

        if (_isDisposed)
        {
            return;
        }

        if (ResolveTarget(out var profileAgain, out var islandAgain) != target || profileAgain != profileId || islandAgain != island)
        {
            _isPending = true;

            return;
        }

        if (read.IsFailed)
        {
            Show(PageMode.Failed, null);
        }
        else if (read.View is not { } view)
        {
            Show(PageMode.NotFound, null);
        }
        else if (view.AsOf is null || view.Regions.Count == 0)
        {
            Show(PageMode.NoData, null);
        }
        else if (IsKnown(view))
        {
            Show(PageMode.Ready, view);
        }
        else
        {
            Show(PageMode.NotFound, null);
        }
    }

    /// <summary>
    /// Shows a mode with a view.
    /// </summary>
    /// <param name="mode">The mode</param>
    /// <param name="view">The view, or <see langword="null" /> for none</param>
    private void Show(PageMode mode, ProfileView? view)
    {
        Mode = mode;
        View = view;

        StateHasChanged();
    }

    /// <summary>
    /// Answers a notice of the server state: it only hands the work to the page's context and never blocks the follower.
    /// </summary>
    /// <param name="sender">The server state</param>
    /// <param name="arguments">The notice</param>
    private void OnStateChanged(object? sender, ServerStateChangedEventArgs arguments)
    {
        if (_isDisposed || arguments.Kind == ServerStateChangeKind.Companion)
        {
            return;
        }

        _ = InvokeAsync(() => ReadAfterNoticeAsync(arguments));
    }

    /// <summary>
    /// Reads again when a notice concerns the page's profile.
    /// </summary>
    /// <param name="arguments">The notice</param>
    /// <returns>A task that completes when the page shows the newest state</returns>
    private async Task ReadAfterNoticeAsync(ServerStateChangedEventArgs arguments)
    {
        if (_isDisposed || Mode == PageMode.Failed)
        {
            return;
        }

        var concerns = arguments.Kind == ServerStateChangeKind.Catalog
                       || arguments.ProfileId is null
                       || arguments.ProfileId == _readProfile
                       || (FollowsNewestProfile && State.NewestProfile != _readProfile);

        if (concerns)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    #endregion // Methods

    #region ComponentBase

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(HttpContext?.RequestAborted ?? CancellationToken.None);
        _cancellationToken = _cancellation.Token;
        State.Changed += OnStateChanged;
    }

    /// <inheritdoc />
    protected override Task OnParametersSetAsync()
    {
        var key = AddressKey;

        if (key.Equals(_addressKey))
        {
            return Task.CompletedTask;
        }

        _addressKey = key;

        return LoadAsync();
    }

    #endregion // ComponentBase

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the page's subscription and cancels its reads.
    /// </summary>
    /// <param name="disposing"><see langword="true" /> when called from <see cref="Dispose()" /></param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing == false || _isDisposed)
        {
            return;
        }

        _isDisposed = true;
        State.Changed -= OnStateChanged;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
    }

    #endregion // IDisposable
}