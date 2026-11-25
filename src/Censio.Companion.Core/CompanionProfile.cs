namespace Censio.Companion.Core;

/// <summary>
/// A profile live data is sent under.
/// </summary>
public sealed class CompanionProfile
{
    #region Properties

    /// <summary>
    /// Gets the profile of the live data before profiles existed.
    /// </summary>
    public static CompanionProfile Live { get; } = new()
                                                   {
                                                       Id = LiveSender.ProfileId,
                                                       Name = LiveSender.ProfileName
                                                   };

    /// <summary>
    /// Gets the identity of the profile.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the name of the profile.
    /// </summary>
    public required string Name { get; init; }

    #endregion // Properties
}