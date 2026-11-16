namespace Censio.Server.Data;

/// <summary>
/// The outcome of a history step.
/// </summary>
public enum HistoryOutcome
{
    /// <summary>
    /// The step did its work.
    /// </summary>
    Done,

    /// <summary>
    /// There was nothing to do.
    /// </summary>
    NothingToDo,

    /// <summary>
    /// The step could not finish because a reader holds the database.
    /// </summary>
    Blocked,

    /// <summary>
    /// The step failed; nothing was changed.
    /// </summary>
    Failed
}