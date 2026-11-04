using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// Stores the autostart command in the per-user Run key of the registry.
/// </summary>
internal sealed class RegistryStartupEntryStore : IStartupEntryStore
{
    #region IStartupEntryStore

    /// <inheritdoc />
    public string? ReadCommand(string name)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void WriteCommand(string name, string command)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void RemoveCommand(string name)
    {
        throw new NotImplementedException();
    }

    #endregion // IStartupEntryStore
}