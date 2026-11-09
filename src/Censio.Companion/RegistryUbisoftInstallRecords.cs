using System.IO;
using System.Security;

using Censio.Companion.Core;

using Microsoft.Win32;

namespace Censio.Companion;

/// <summary>
/// Reads the install records of Ubisoft Connect from the registry.
/// </summary>
internal sealed class RegistryUbisoftInstallRecords : IUbisoftInstallRecords
{
    #region Constants

    /// <summary>
    /// The key of the install records below the 32-bit view of the local machine.
    /// </summary>
    private const string InstallsKeyPath = @"SOFTWARE\Ubisoft\Launcher\Installs";

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Reads the <c>InstallDir</c> value of an install record.
    /// </summary>
    /// <param name="installs">The key of the install records</param>
    /// <param name="name">The name of the record</param>
    /// <returns>The value as stored, or <c>null</c> if it is missing, not a text or cannot be read</returns>
    private static string? ReadInstallDir(RegistryKey installs, string name)
    {
        try
        {
            using var record = installs.OpenSubKey(name, false);

            if (record is null || record.GetValueKind("InstallDir") != RegistryValueKind.String)
            {
                return null;
            }

            return record.GetValue("InstallDir", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    #endregion // Methods

    #region IUbisoftInstallRecords

    /// <inheritdoc />
    public IReadOnlyList<UbisoftInstallRecord> Read()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var installs = baseKey.OpenSubKey(InstallsKeyPath, false);

            if (installs is null)
            {
                return [];
            }

            return installs.GetSubKeyNames()
                           .Select(name => new UbisoftInstallRecord
                                           {
                                               Name = name,
                                               InstallDir = ReadInstallDir(installs, name)
                                           })
                           .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return [];
        }
    }

    #endregion // IUbisoftInstallRecords
}