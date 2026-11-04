using System.IO;

using Censio.Companion.Core;

using Microsoft.Win32;

namespace Censio.Companion;

/// <summary>
/// Stores the autostart command in the per-user Run key of the registry.
/// </summary>
internal sealed class RegistryStartupEntryStore : IStartupEntryStore
{
    #region Constants

    /// <summary>
    /// The path of the Run key below the current user's hive.
    /// </summary>
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Determines whether a name is among the value names of a key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="name">The name of the value</param>
    /// <returns><see langword="true" /> if the value exists</returns>
    private static bool HasValue(RegistryKey key, string name)
    {
        return key.GetValueNames().Any(valueName => string.Equals(valueName, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reads the command of a value without expanding environment variables.
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="name">The name of the value</param>
    /// <returns>The command</returns>
    /// <exception cref="InvalidDataException">The value is neither a string nor an expandable string</exception>
    /// <exception cref="IOException">The value could not be read</exception>
    private static string ReadValue(RegistryKey key, string name)
    {
        var kind = key.GetValueKind(name);

        if (kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
        {
            throw new InvalidDataException("The autostart value is not a string");
        }

        return key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string
                   ?? throw new IOException("The autostart value could not be read");
    }

    #endregion // Methods

    #region IStartupEntryStore

    /// <inheritdoc />
    public string? ReadCommand(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);

        if (key is null || HasValue(key, name) == false)
        {
            return null;
        }

        return ReadValue(key, name);
    }

    /// <inheritdoc />
    public void WriteCommand(string name, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

        key.SetValue(name, command, RegistryValueKind.String);

        if (HasValue(key, name) == false || string.Equals(ReadValue(key, name), command, StringComparison.Ordinal) == false)
        {
            throw new IOException("The autostart value was not stored as written");
        }
    }

    /// <inheritdoc />
    public void RemoveCommand(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);

        if (key is null)
        {
            return;
        }

        key.DeleteValue(name, false);

        if (HasValue(key, name))
        {
            throw new IOException("The autostart value could not be removed");
        }
    }

    #endregion // IStartupEntryStore
}