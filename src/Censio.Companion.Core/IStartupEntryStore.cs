namespace Censio.Companion.Core;

/// <summary>
/// Reads, writes and removes the autostart command of the Companion.
/// </summary>
public interface IStartupEntryStore
{
    #region Methods

    /// <summary>
    /// Reads the command stored under a name.
    /// </summary>
    /// <param name="name">The name of the entry</param>
    /// <returns>The command, or <see langword="null" /> if the entry is absent</returns>
    string? ReadCommand(string name);

    /// <summary>
    /// Writes the command stored under a name.
    /// </summary>
    /// <param name="name">The name of the entry</param>
    /// <param name="command">The command</param>
    void WriteCommand(string name, string command);

    /// <summary>
    /// Removes the entry stored under a name.
    /// </summary>
    /// <param name="name">The name of the entry</param>
    void RemoveCommand(string name);

    #endregion // Methods
}