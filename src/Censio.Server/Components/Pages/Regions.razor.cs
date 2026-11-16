using Censio.Server.Data;

using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components.Pages;

/// <summary>
/// The region overview of a profile.
/// </summary>
public partial class Regions
{
    #region Fields

    /// <summary>
    /// Whether the deletion dialog is open.
    /// </summary>
    private bool _dialogOpen;

    /// <summary>
    /// Whether a deletion is running.
    /// </summary>
    private bool _deleting;

    /// <summary>
    /// Whether the last deletion failed.
    /// </summary>
    private bool _deleteFailed;

    /// <summary>
    /// The identity of the profile the dialog was opened for.
    /// </summary>
    private Guid _dialogProfile;

    /// <summary>
    /// The shown name of the profile the dialog was opened for.
    /// </summary>
    private string _dialogName = string.Empty;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the profile identity from the address, or <see langword="null"/> for the newest profile.
    /// </summary>
    [Parameter]
    public string? Profile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the page may delete a profile's history.
    /// </summary>
    [CascadingParameter(Name = "DeletionAllowed")]
    public bool DeletionAllowed { get; set; }

    /// <summary>
    /// Gets or sets the history retention.
    /// </summary>
    [Inject]
    public HistoryRetention Retention { get; set; } = null!;

    /// <summary>
    /// Gets or sets the navigation manager.
    /// </summary>
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>
    /// Gets the heading of the page.
    /// </summary>
    private string Heading => Texts.RegionsEntry;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Opens the deletion dialog for the profile of the page's address.
    /// </summary>
    private void OpenDeleteDialog()
    {
        if (DeletionAllowed == false || PageAddress.TryParseProfile(Profile, out var profileId) == false)
        {
            return;
        }

        var profiles = State.Profiles.Where(profile => profile.Id != profileId)
                                     .Append(new StoredProfile
                                             {
                                                 Id = profileId,
                                                 Name = View?.ProfileId == profileId
                                                            ? View.ProfileName
                                                            : State.Profiles.FirstOrDefault(profile => profile.Id == profileId)?.Name
                                             })
                                     .ToList();

        _dialogProfile = profileId;
        _dialogName = PageNames.Profiles(profiles, Language, profiles.Count).First(profile => profile.Key == profileId).Value;
        _deleteFailed = false;
        _dialogOpen = true;
    }

    /// <summary>
    /// Closes the deletion dialog without deleting.
    /// </summary>
    private void CancelDelete()
    {
        _dialogOpen = false;
    }

    /// <summary>
    /// Deletes the profile the dialog was opened for and goes to the newest profile's overview.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    private async Task ConfirmDeleteAsync()
    {
        if (DeletionAllowed == false || _deleting)
        {
            return;
        }

        _deleting = true;
        _deleteFailed = false;

        try
        {
            var outcome = await Retention.DeleteProfileAsync(_dialogProfile, CancellationToken.None).ConfigureAwait(true);

            if (outcome == HistoryOutcome.Failed)
            {
                _deleteFailed = true;
            }
            else
            {
                _dialogOpen = false;
                Navigation.NavigateTo("/regions");
            }
        }
        finally
        {
            _deleting = false;
        }
    }

    #endregion // Methods

    #region DataPageBase

    /// <inheritdoc />
    protected override object AddressKey => ValueTuple.Create(Profile);

    /// <inheritdoc />
    protected override bool FollowsNewestProfile => Profile is null;

    /// <inheritdoc />
    protected override PageMode ResolveTarget(out Guid profileId, out IslandKey? island)
    {
        island = null;

        if (Profile is not null)
        {
            return PageAddress.TryParseProfile(Profile, out profileId)
                       ? PageMode.Loading
                       : PageMode.NotFound;
        }

        if (State.NewestProfile is { } newest)
        {
            profileId = newest;

            return PageMode.Loading;
        }

        profileId = Guid.Empty;

        return PageMode.NoData;
    }

    /// <inheritdoc />
    protected override bool IsKnown(ProfileView view)
    {
        return true;
    }

    #endregion // DataPageBase
}