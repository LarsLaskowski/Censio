namespace Censio.Companion.Core.Tests;

/// <summary>
/// A startup entry store that keeps its commands in memory by name, case-insensitively, can fail each operation with a
/// given exception, and records the name of every read, write and removal.
/// </summary>
public sealed class FakeStartupEntryStore : IStartupEntryStore
{
    #region Fields

    /// <summary>
    /// The stored commands by name.
    /// </summary>
    private readonly Dictionary<string, string> _commands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The names read, in order.
    /// </summary>
    private readonly List<string> _reads = [];

    /// <summary>
    /// The names and commands of the writes, in order.
    /// </summary>
    private readonly List<(string Name, string Command)> _writes = [];

    /// <summary>
    /// The names of the removals, in order.
    /// </summary>
    private readonly List<string> _removals = [];

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the exception a read throws, or <see langword="null" /> to read normally.
    /// </summary>
    public Exception? ReadException { get; set; }

    /// <summary>
    /// Gets or sets the exception a write throws, or <see langword="null" /> to write normally.
    /// </summary>
    public Exception? WriteException { get; set; }

    /// <summary>
    /// Gets or sets the exception a removal throws, or <see langword="null" /> to remove normally.
    /// </summary>
    public Exception? RemoveException { get; set; }

    /// <summary>
    /// Gets the names read, in order, including failed reads.
    /// </summary>
    public IReadOnlyList<string> Reads => _reads;

    /// <summary>
    /// Gets the names and commands of the writes, in order, including failed writes.
    /// </summary>
    public IReadOnlyList<(string Name, string Command)> Writes => _writes;

    /// <summary>
    /// Gets the names of the removals, in order, including failed removals.
    /// </summary>
    public IReadOnlyList<string> Removals => _removals;

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Stores a command under a name, or deletes the entry, without recording a write or removal, as another program or
    /// the player would.
    /// </summary>
    /// <param name="name">The name of the entry</param>
    /// <param name="command">The command, or <see langword="null" /> to delete the entry</param>
    public void SetCommand(string name, string? command)
    {
        if (command is null)
        {
            _commands.Remove(name);
        }
        else
        {
            _commands[name] = command;
        }
    }

    /// <summary>
    /// Gets the command stored under a name without recording a read.
    /// </summary>
    /// <param name="name">The name of the entry</param>
    /// <returns>The command, or <see langword="null" /> if the entry is absent</returns>
    public string? GetCommand(string name)
    {
        return _commands.GetValueOrDefault(name);
    }

    #endregion // Methods

    #region IStartupEntryStore

    /// <inheritdoc />
    public string? ReadCommand(string name)
    {
        _reads.Add(name);

        if (ReadException is not null)
        {
            throw ReadException;
        }

        return _commands.GetValueOrDefault(name);
    }

    /// <inheritdoc />
    public void WriteCommand(string name, string command)
    {
        _writes.Add((name, command));

        if (WriteException is not null)
        {
            throw WriteException;
        }

        _commands[name] = command;
    }

    /// <inheritdoc />
    public void RemoveCommand(string name)
    {
        _removals.Add(name);

        if (RemoveException is not null)
        {
            throw RemoveException;
        }

        _commands.Remove(name);
    }

    #endregion // IStartupEntryStore
}