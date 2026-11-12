namespace Censio.Contracts;

/// <summary>
/// The map of a tick entry as the reader builds it: it also remembers whether the pairs came in strictly ascending
/// order of their GUID.
/// </summary>
internal sealed class ContractMap : Dictionary<int, int>
{
    #region Fields

    /// <summary>
    /// The GUID of the last pair, when <see cref="_hasPair" /> is set.
    /// </summary>
    private int _last;

    /// <summary>
    /// Whether a pair has been appended.
    /// </summary>
    private bool _hasPair;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets a value indicating whether every pair had a GUID above the one before it, so none was repeated.
    /// </summary>
    internal bool IsAscending { get; private set; } = true;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Appends a pair as it stands in the message.
    /// </summary>
    /// <param name="guid">The GUID</param>
    /// <param name="amount">The amount</param>
    internal void Append(int guid, int amount)
    {
        if (_hasPair && guid <= _last)
        {
            IsAscending = false;
        }

        _hasPair = true;
        _last = guid;
        TryAdd(guid, amount);
    }

    #endregion // Methods
}