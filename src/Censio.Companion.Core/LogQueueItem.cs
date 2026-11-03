namespace Censio.Companion.Core;

/// <summary>
/// An item of the queue of the log writer: a line, or a marker for a flush.
/// </summary>
/// <param name="Date">The local date of the line</param>
/// <param name="Line">The line</param>
/// <param name="Marker">The marker of a flush, or <see langword="null" /> for a line</param>
internal readonly record struct LogQueueItem(DateOnly Date, string Line, TaskCompletionSource? Marker);