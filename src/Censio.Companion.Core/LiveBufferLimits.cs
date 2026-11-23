namespace Censio.Companion.Core;

/// <summary>
/// The age and size limits of the live buffer.
/// </summary>
public sealed record LiveBufferLimits
{
    #region Constants

    /// <summary>
    /// The age limit in hours when the settings file names none.
    /// </summary>
    public const int DefaultHours = 24;

    /// <summary>
    /// The largest age limit in hours.
    /// </summary>
    public const int MaxHours = 168;

    /// <summary>
    /// The size limit in megabytes when the settings file names none.
    /// </summary>
    public const int DefaultMegabytes = 100;

    /// <summary>
    /// The largest size limit in megabytes.
    /// </summary>
    public const int MaxMegabytes = 10240;

    /// <summary>
    /// The number of bytes in a megabyte.
    /// </summary>
    public const int BytesPerMegabyte = 1048576;

    #endregion // Constants

    #region Properties

    /// <summary>
    /// Gets the limits when the settings file names none.
    /// </summary>
    public static LiveBufferLimits Defaults { get; } = new()
                                                       {
                                                           Hours = DefaultHours,
                                                           Megabytes = DefaultMegabytes
                                                       };

    /// <summary>
    /// Gets the age limit in hours.
    /// </summary>
    public required int Hours { get; init; }

    /// <summary>
    /// Gets the size limit in megabytes.
    /// </summary>
    public required int Megabytes { get; init; }

    /// <summary>
    /// Gets the age limit.
    /// </summary>
    public TimeSpan MaxAge => TimeSpan.FromHours(Hours);

    /// <summary>
    /// Gets the size limit in bytes.
    /// </summary>
    public long MaxBytes => (long)Megabytes * BytesPerMegabyte;

    #endregion // Properties
}