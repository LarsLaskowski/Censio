namespace Censio.Companion.Core;

/// <summary>
/// What the live buffer found in its folder at the start.
/// </summary>
internal sealed class LiveBufferLoad
{
    #region Properties

    /// <summary>
    /// Gets the stored messages found.
    /// </summary>
    public List<LiveBufferItem> Items { get; } = [];

    /// <summary>
    /// Gets or sets the number of files and links that were unreadable and discarded.
    /// </summary>
    public int Unreadable { get; set; }

    /// <summary>
    /// Gets or sets the number of temporary files that were discarded.
    /// </summary>
    public int Temporary { get; set; }

    /// <summary>
    /// Gets or sets the next sequence, one above the largest one seen.
    /// </summary>
    public long NextSequence { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the folder could not be created or read.
    /// </summary>
    public bool Failed { get; set; }

    #endregion // Properties
}