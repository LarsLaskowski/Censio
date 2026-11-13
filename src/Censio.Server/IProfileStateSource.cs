using Censio.Server.Data;

namespace Censio.Server;

/// <summary>
/// The source of the profile states the data pages read.
/// </summary>
public interface IProfileStateSource
{
    #region Properties

    /// <summary>
    /// Gets what the source knows of the stored catalog.
    /// </summary>
    CatalogHold Catalog { get; }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Tries to get the state of a held profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="state">The held state, or <see langword="null"/> when the profile is not held</param>
    /// <returns><see langword="true"/> when the profile is held</returns>
    bool TryGetHeld(Guid profileId, out StoredProfileState? state);

    /// <summary>
    /// Reads the state of a profile.
    /// </summary>
    /// <param name="profileId">The profile identity</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The read result</returns>
    Task<ProfileStateRead> ReadProfileStateAsync(Guid profileId, CancellationToken cancellationToken);

    #endregion // Methods
}